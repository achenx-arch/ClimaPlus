import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import App from "../App";
import RutaProtegida from "../components/RutaProtegida";
import { AuthContext, type ValorAuth } from "../context/auth";
import { AuthProvider, CLAVE_SESION } from "../context/AuthProvider";
import { NotificacionesProvider } from "../context/NotificacionesProvider";
import { hayToken } from "../services/apiClient";
import type { Usuario } from "../types/api";
import { administrador, alertaAdmin, climaActual, pagina, pronostico, regla, usuario } from "./datos";
import { conSesion } from "./renderizar";
import { crearServidor } from "./servidorFalso";

/** Rutas por las que pasó la aplicación (para comprobar que el logout nunca visita /login). */
let rutasVisitadas: string[] = [];

function RegistroDeRutas() {
  const { pathname } = useLocation();
  if (rutasVisitadas.at(-1) !== pathname) rutasVisitadas.push(pathname);
  return null;
}

/** La aplicación completa (App con Navbar, layouts y rutas reales) en una ruta inicial. */
function renderizarApp(ruta: string) {
  rutasVisitadas = [];
  return render(
    <StrictMode>
      <MemoryRouter initialEntries={[ruta]}>
        <AuthProvider>
          <NotificacionesProvider>
            <App />
            <RegistroDeRutas />
          </NotificacionesProvider>
        </AuthProvider>
      </MemoryRouter>
    </StrictMode>
  );
}

/** Rutas privadas que cargan datos al abrirse (respuestas vacías). */
function datosPrivados(servidor: ReturnType<typeof crearServidor>) {
  servidor
    .en("GET", "/api/favoritos", { cuerpo: [] })
    .en("GET", "/api/alertas", { cuerpo: pagina([]) })
    .en("GET", "/api/configuraciones-alerta", { cuerpo: [regla()] })
    .en("GET", "/api/historial", { cuerpo: pagina([]) })
    .en("GET", "/api/usuarios/me", { cuerpo: usuario });
  return servidor;
}

function servidorConSesion(u: Usuario = usuario, nuevas = 0) {
  const servidor = crearServidor();
  conSesion(servidor, u, nuevas);
  return datosPrivados(servidor);
}

const menuLateral = () => screen.getAllByRole("complementary", { name: "Menú de mi espacio" })[0];

describe("navegación pública", () => {
  it("Inicio (M01) está disponible sin sesión, con acceso a Login, Registro, Clima y Pronóstico", async () => {
    const servidor = crearServidor();
    renderizarApp("/");

    expect(screen.getByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
    for (const [nombre, destino] of [
      ["Inicio", "/"],
      ["Clima actual", "/clima"],
      ["Pronóstico", "/pronostico"],
      ["Iniciar sesión", "/login"],
      ["Crear cuenta", "/registro"],
    ]) {
      expect(screen.getByRole("link", { name: nombre })).toHaveAttribute("href", destino);
    }
    expect(screen.queryByRole("complementary", { name: "Menú de mi espacio" })).not.toBeInTheDocument();
    expect(servidor.peticiones).toHaveLength(0);
  });

  it("un visitante consulta el clima sin iniciar sesión y puede volver al Inicio", async () => {
    crearServidor().en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizarApp("/clima?idUbicacion=12");

    expect(await screen.findByRole("heading", { name: "Cobán" })).toBeInTheDocument();
    expect(screen.queryByText("Pantalla de inicio de sesión")).not.toBeInTheDocument();
    expect(screen.queryByRole("complementary", { name: "Menú de mi espacio" })).not.toBeInTheDocument();

    await userEvent.setup().click(screen.getByRole("link", { name: "Volver al Inicio" }));
    expect(screen.getByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
  });

  it("Login y Registro tienen enlace para volver al Inicio", async () => {
    crearServidor();
    renderizarApp("/registro");

    await userEvent.setup().click(screen.getByRole("link", { name: "Volver al Inicio" }));

    expect(screen.getByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
  });

  it("una ruta inexistente ofrece volver al Inicio", async () => {
    crearServidor();
    renderizarApp("/no-existe");

    expect(screen.getByRole("heading", { name: "Página no encontrada" })).toBeInTheDocument();
    await userEvent.setup().click(screen.getByRole("link", { name: "Ir al Inicio" }));
    expect(screen.getByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
  });
});

describe("menú lateral persistente", () => {
  it("con sesión, Clima muestra el menú de Mi espacio y permite ir directo a Alertas", async () => {
    const servidor = servidorConSesion(usuario, 2).en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizarApp("/clima?idUbicacion=12");

    await screen.findByRole("heading", { name: "Cobán" });
    const menu = menuLateral();
    for (const opcion of ["Dashboard", "Clima", "Pronóstico", "Favoritos", "Historial", "Alertas", "Configuración de alertas", "Perfil"]) {
      expect(within(menu).getByRole("link", { name: new RegExp(`^${opcion}`) })).toBeInTheDocument();
    }
    expect(within(menu).getByRole("link", { name: /^Clima/ })).toHaveClass("active");
    expect(await within(menu).findByLabelText("2 alertas nuevas")).toBeInTheDocument();

    await userEvent.setup().click(within(menu).getByRole("link", { name: /^Alertas/ }));

    expect(await screen.findByRole("heading", { name: "Mis alertas" })).toBeInTheDocument();
    expect(within(menuLateral()).getByRole("link", { name: /^Alertas/ })).toHaveClass("active");
    expect(hayToken()).toBe(true);
    expect(servidor.a("/api/alertas")[0].encabezados.authorization).toBeDefined();
  });

  it("desde Pronóstico se llega a Configuración de alertas sin perder la sesión", async () => {
    servidorConSesion().en("GET", "/api/clima/pronostico", { cuerpo: pronostico });
    renderizarApp("/pronostico?idUbicacion=12");

    await screen.findAllByText("Lluvia ligera");
    await userEvent.setup().click(within(menuLateral()).getByRole("link", { name: "Configuración de alertas" }));

    expect(await screen.findByRole("heading", { name: "Configuración de alertas" })).toBeInTheDocument();
    expect(hayToken()).toBe(true);
  });

  it("las pantallas privadas no duplican el menú ni la navbar", async () => {
    servidorConSesion();
    renderizarApp("/historial");

    await screen.findByRole("heading", { name: "Historial de consultas" });

    expect(screen.getAllByRole("complementary", { name: "Menú de mi espacio" })).toHaveLength(1);
    expect(screen.getAllByRole("navigation", { name: "Menú del usuario" })).toHaveLength(1);
    expect(document.querySelectorAll("nav.navbar")).toHaveLength(1);
  });

  it("los botones de regreso llevan al dashboard", async () => {
    servidorConSesion();
    renderizarApp("/favoritos");

    await userEvent.setup().click(await screen.findByRole("link", { name: "Volver a mi dashboard" }));

    expect(await screen.findByRole("heading", { name: /Hola, Ana López/ })).toBeInTheDocument();
  });

  it("en móvil el menú se abre como panel y se cierra al navegar", async () => {
    servidorConSesion();
    renderizarApp("/dashboard");
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: /Hola, Ana López/ });

    const boton = screen.getByRole("button", { name: "Menú de mi espacio" });
    expect(boton).toHaveAttribute("aria-expanded", "false");
    await usuarioEvento.click(boton);

    const panel = screen.getByRole("dialog", { name: "Menú de mi espacio" });
    expect(boton).toHaveAttribute("aria-expanded", "true");
    await usuarioEvento.click(within(panel).getByRole("link", { name: /^Historial/ }));

    expect(await screen.findByRole("heading", { name: "Historial de consultas" })).toBeInTheDocument();
    expect(screen.queryByRole("dialog", { name: "Menú de mi espacio" })).not.toBeInTheDocument();
  });

  it("el panel móvil se cierra con el botón Cerrar menú", async () => {
    servidorConSesion();
    renderizarApp("/dashboard");
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: /Hola, Ana López/ });

    await usuarioEvento.click(screen.getByRole("button", { name: "Menú de mi espacio" }));
    await usuarioEvento.click(screen.getByRole("button", { name: "Cerrar menú" }));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});

describe("cierre de sesión y cambio de cuenta", () => {
  it("cerrar sesión limpia la sesión, va a Inicio y las rutas privadas siguen protegidas", async () => {
    servidorConSesion(usuario, 3);
    renderizarApp("/dashboard");
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: /Hola, Ana López/ });

    await usuarioEvento.click(within(menuLateral()).getByRole("button", { name: /Cerrar sesión/ }));

    expect(await screen.findByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
    await waitFor(() => expect(hayToken()).toBe(false));
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
    expect(screen.getByRole("link", { name: "Iniciar sesión" })).toBeInTheDocument();
    expect(screen.queryByText("Ana López")).not.toBeInTheDocument();
    expect(screen.queryByTestId("contador-alertas")).not.toBeInTheDocument();

    // Volver a una ruta privada pide iniciar sesión.
    await usuarioEvento.click(screen.getByRole("link", { name: "Clima actual" }));
    expect(screen.queryByRole("complementary", { name: "Menú de mi espacio" })).not.toBeInTheDocument();
  });

  it("tras cerrar sesión, otra cuenta inicia con su propio token, nombre y contador", async () => {
    const otra: Usuario = { ...usuario, idUsuario: 30, nombre: "Luis Gómez", correo: "luis@ejemplo.com" };
    const servidor = servidorConSesion(usuario, 3);
    renderizarApp("/dashboard");
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: /Hola, Ana López/ });
    expect(await screen.findByTestId("contador-alertas")).toHaveTextContent("3");

    await usuarioEvento.click(within(menuLateral()).getByRole("button", { name: /Cerrar sesión/ }));
    await screen.findByRole("heading", { name: /El clima de tu ciudad/ });
    await waitFor(() => expect(hayToken()).toBe(false));

    servidor
      .en("POST", "/api/auth/login", {
        cuerpo: { token: "token.de.luis", tipoToken: "Bearer", expiraEn: new Date(Date.now() + 3_600_000).toISOString(), usuario: otra },
      })
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    await usuarioEvento.click(screen.getByRole("link", { name: "Iniciar sesión" }));
    await usuarioEvento.type(screen.getByLabelText("Correo electrónico"), "luis@ejemplo.com");
    await usuarioEvento.type(screen.getByLabelText("Contraseña"), "Ejemplo2026");
    await usuarioEvento.click(screen.getByRole("button", { name: /Iniciar sesión/ }));

    expect(await screen.findByRole("heading", { name: /Hola, Luis Gómez/ })).toBeInTheDocument();
    expect(screen.queryByText("Ana López")).not.toBeInTheDocument();
    await waitFor(() => expect(screen.queryByTestId("contador-alertas")).not.toBeInTheDocument());
    const ultimas = servidor.a("/api/favoritos").at(-1)!;
    expect(ultimas.encabezados.authorization).toBe("Bearer token.de.luis");
  });

  it("con la sesión iniciada, /login lleva al dashboard", async () => {
    servidorConSesion();
    renderizarApp("/login");

    expect(await screen.findByRole("heading", { name: /Hola, Ana López/ })).toBeInTheDocument();
  });
});

describe("cierre de sesión: destino final", () => {
  it("el logout manual termina en / sin pasar nunca por /login ni mostrar datos privados", async () => {
    servidorConSesion(usuario, 3);
    renderizarApp("/historial");
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: "Historial de consultas" });
    expect(screen.getAllByText("Ana López").length).toBeGreaterThan(0);

    await usuarioEvento.click(within(menuLateral()).getByRole("button", { name: /Cerrar sesión/ }));

    // Inmediatamente después del clic ya no queda nada privado en pantalla.
    expect(screen.queryByText("Ana López")).not.toBeInTheDocument();
    expect(screen.queryByRole("complementary", { name: "Menú de mi espacio" })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Historial de consultas" })).not.toBeInTheDocument();
    expect(hayToken()).toBe(false);

    expect(await screen.findByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
    await waitFor(() => expect(rutasVisitadas.at(-1)).toBe("/"));
    expect(rutasVisitadas).toEqual(["/historial", "/"]);
    expect(rutasVisitadas).not.toContain("/login");
    expect(screen.queryByText("Pantalla de inicio de sesión")).not.toBeInTheDocument();
    expect(screen.queryByText(/Tu sesión expiró/)).not.toBeInTheDocument();
  });

  it("también desde el menú del avatar (navbar) en una pantalla privada", async () => {
    servidorConSesion();
    renderizarApp("/alertas");
    await screen.findByRole("heading", { name: "Mis alertas" });

    const navbar = document.querySelector("nav.navbar") as HTMLElement;
    await userEvent.setup().click(within(navbar).getByRole("button", { name: /Cerrar sesión/ }));

    expect(await screen.findByRole("heading", { name: /El clima de tu ciudad/ })).toBeInTheDocument();
    expect(rutasVisitadas).not.toContain("/login");
  });

  it("si la sesión se borra antes de que el router cambie de ruta, la ruta protegida va a / y no a /login", () => {
    // Reproduce el orden del navegador real: React Router actualiza la URL dentro de una transición,
    // así que la pantalla privada puede volver a pintarse con la sesión ya cerrada.
    const valor: ValorAuth = {
      estado: "cerrando",
      usuario: null,
      esAdministrador: false,
      mensajeSesion: null,
      iniciarSesion: async () => usuario,
      cerrarSesion: () => {},
      actualizarUsuario: () => {},
      limpiarMensajeSesion: () => {},
    };
    rutasVisitadas = [];
    render(
      <AuthContext.Provider value={valor}>
        <MemoryRouter initialEntries={["/historial"]}>
          <Routes>
            <Route path="/historial" element={<RutaProtegida><p>Datos privados</p></RutaProtegida>} />
            <Route path="/" element={<p>Inicio público</p>} />
            <Route path="/login" element={<p>Pantalla de inicio de sesión</p>} />
          </Routes>
          <RegistroDeRutas />
        </MemoryRouter>
      </AuthContext.Provider>
    );

    expect(screen.getByText("Inicio público")).toBeInTheDocument();
    expect(screen.queryByText("Datos privados")).not.toBeInTheDocument();
    expect(rutasVisitadas).not.toContain("/login");
  });

  it("la expiración del JWT sigue enviando a /login con aviso", async () => {
    const servidor = crearServidor();
    sessionStorage.setItem(
      CLAVE_SESION,
      JSON.stringify({ token: "por.expirar", expiraEn: new Date(Date.now() + 600).toISOString() })
    );
    servidor.en("GET", "/api/auth/me", { cuerpo: usuario }).en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    datosPrivados(servidor);
    renderizarApp("/dashboard");
    await screen.findByRole("heading", { name: /Hola, Ana López/ });

    expect(await screen.findByText("Tu sesión expiró. Inicia sesión de nuevo.", {}, { timeout: 3000 })).toBeInTheDocument();
    expect(rutasVisitadas.at(-1)).toBe("/login");
    expect(hayToken()).toBe(false);
  });
});

describe("roles", () => {
  it("un USUARIO no entra a administración: Acceso denegado con regreso, sin llamar a /api/admin", async () => {
    const servidor = servidorConSesion();
    renderizarApp("/admin/alertas");
    const usuarioEvento = userEvent.setup();

    expect(await screen.findByRole("heading", { name: "Acceso denegado" })).toBeInTheDocument();
    expect(screen.getByText(/no tiene permisos para ver esta pantalla/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ir al Inicio" })).toHaveAttribute("href", "/");
    expect(screen.queryByRole("complementary", { name: "Menú de administración" })).not.toBeInTheDocument();
    expect(servidor.peticiones.some((p) => p.url.pathname.startsWith("/api/admin"))).toBe(false);

    await usuarioEvento.click(screen.getByRole("link", { name: "Volver a mi dashboard" }));
    expect(await screen.findByRole("heading", { name: /Hola, Ana López/ })).toBeInTheDocument();
  });

  it("el menú de un USUARIO no ofrece el panel administrativo", async () => {
    servidorConSesion();
    renderizarApp("/dashboard");

    await screen.findByRole("heading", { name: /Hola, Ana López/ });

    expect(within(menuLateral()).queryByRole("link", { name: "Panel administrativo" })).not.toBeInTheDocument();
  });

  it("un ADMINISTRADOR ve el panel con datos reales y navega a Gestión de alertas con su menú", async () => {
    const servidor = servidorConSesion(administrador).en("GET", "/api/admin/alertas", { cuerpo: pagina([alertaAdmin()]) });
    renderizarApp("/admin");
    const usuarioEvento = userEvent.setup();

    expect(await screen.findByRole("heading", { name: "Panel de administración" })).toBeInTheDocument();
    const menu = screen.getAllByRole("complementary", { name: "Menú de administración" })[0];
    expect(within(menu).getByRole("link", { name: "Panel administrativo" })).toHaveClass("active");
    expect(await screen.findByText(/Cobán · Ana López/)).toBeInTheDocument();
    expect(servidor.a("/api/admin/alertas")[0].encabezados.authorization).toBeDefined();

    await usuarioEvento.click(within(menu).getByRole("link", { name: "Gestión de alertas" }));
    expect(await screen.findByRole("heading", { name: "Alertas del sistema" })).toBeInTheDocument();
    expect(within(screen.getAllByRole("complementary", { name: "Menú de administración" })[0]).getByRole("link", { name: "Gestión de alertas" })).toHaveClass("active");

    await usuarioEvento.click(screen.getByRole("link", { name: "Volver al panel administrativo" }));
    expect(await screen.findByRole("heading", { name: "Panel de administración" })).toBeInTheDocument();
  });

  it("Gestión de usuarios se identifica como módulo pendiente sin datos simulados", async () => {
    const servidor = servidorConSesion(administrador);
    renderizarApp("/admin/usuarios");

    expect(await screen.findByRole("heading", { name: "Módulo pendiente" })).toBeInTheDocument();
    expect(screen.getByText(/RF-26, RF-27 y RF-28/)).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(servidor.peticiones.some((p) => p.url.pathname.includes("/usuarios") && p.url.pathname.startsWith("/api/admin"))).toBe(false);
  });

  it("el ADMINISTRADOR puede usar Mi espacio y volver al panel desde su menú", async () => {
    servidorConSesion(administrador).en("GET", "/api/admin/alertas", { cuerpo: pagina([]) });
    renderizarApp("/dashboard");
    const usuarioEvento = userEvent.setup();

    await screen.findByRole("heading", { name: /Hola, Admin ClimaPlus/ });
    await usuarioEvento.click(within(menuLateral()).getByRole("link", { name: "Panel administrativo" }));

    expect(await screen.findByRole("heading", { name: "Panel de administración" })).toBeInTheDocument();
  });
});
