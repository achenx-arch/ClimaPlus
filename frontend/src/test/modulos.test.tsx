import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import Navbar from "../components/Navbar";
import AdminAlerts from "../pages/AdminAlerts";
import AlertSettings from "../pages/AlertSettings";
import Alerts from "../pages/Alerts";
import Favorites from "../pages/Favorites";
import History from "../pages/History";
import UserProfile from "../pages/UserProfile";
import { CLAVE_SESION } from "../context/AuthProvider";
import {
  administrador,
  alerta,
  alertaAdmin,
  climaActual,
  consultaActual,
  consultaPronostico,
  coban,
  detalleActual,
  favorito,
  guatemala,
  pagina,
  regla,
  usuario,
} from "./datos";
import { conSesion, renderizar } from "./renderizar";
import { crearServidor, problema } from "./servidorFalso";

function servidorConSesion(u = usuario, nuevas = 0) {
  const servidor = crearServidor();
  conSesion(servidor, u, nuevas);
  return servidor;
}

describe("M07 – favoritos", () => {
  it("lista los favoritos reales con su última consulta", async () => {
    servidorConSesion().en("GET", "/api/favoritos", { cuerpo: [favorito()] });
    renderizar(<Favorites />, { ruta: "/favoritos" });

    expect(await screen.findByRole("heading", { name: "Cobán" })).toBeInTheDocument();
    expect(screen.getByText("Nubes dispersas")).toBeInTheDocument();
    expect(screen.getByText(/Última consulta almacenada:/)).toBeInTheDocument();
    expect(screen.getByText("1 favorito")).toBeInTheDocument();
  });

  it("estado vacío sin favoritos", async () => {
    servidorConSesion().en("GET", "/api/favoritos", { cuerpo: [] });
    renderizar(<Favorites />, { ruta: "/favoritos" });

    expect(await screen.findByText("No tienes ubicaciones favoritas")).toBeInTheDocument();
  });

  it("agrega un favorito: buscar → elegir → POST con idUbicacion", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [guatemala] })
      .en("POST", "/api/favoritos", { status: 201, cuerpo: favorito({ idFavorito: 9, ubicacion: guatemala, ultimaConsulta: null }) });
    renderizar(<Favorites />, { ruta: "/favoritos" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText("No tienes ubicaciones favoritas");

    await usuarioEvento.type(screen.getByLabelText("Buscador de ubicaciones"), "Guatemala");
    await usuarioEvento.click(screen.getByRole("button", { name: "Buscar" }));
    await usuarioEvento.click(await screen.findByRole("button", { name: /Ciudad de Guatemala.*Agregar a favoritos/ }));

    expect(await screen.findByRole("heading", { name: "Ciudad de Guatemala" })).toBeInTheDocument();
    expect(servidor.a("/api/favoritos", "POST")[0].cuerpo).toEqual({ idUbicacion: 7 });
    expect(screen.getByText("Aún no hay consultas de esta ubicación")).toBeInTheDocument();
  });

  it("quita un favorito con DELETE y actualiza la lista", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [favorito()] })
      .en("DELETE", "/api/favoritos/7", { status: 204 });
    renderizar(<Favorites />, { ruta: "/favoritos" });

    await userEvent.setup().click(await screen.findByRole("button", { name: /Quitar de favoritos/ }));

    expect(await screen.findByText("No tienes ubicaciones favoritas")).toBeInTheDocument();
    expect(servidor.a("/api/favoritos/7", "DELETE")).toHaveLength(1);
  });

  it("Ver clima navega con idUbicacion", async () => {
    servidorConSesion().en("GET", "/api/favoritos", { cuerpo: [favorito()] });
    renderizar(<Favorites />, { ruta: "/favoritos" });

    await userEvent.setup().click(await screen.findByRole("button", { name: /Ver clima/ }));

    expect(screen.getByTestId("ruta")).toHaveTextContent("/clima?idUbicacion=12");
  });

  it("Actualizar clima consulta cada favorito con sesión y notifica alertas", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [favorito()] })
      .en("GET", "/api/clima/actual", {
        cuerpo: climaActual({
          humedad: 81,
          alertasGeneradas: [{ idAlerta: 5, tipo: "LLUVIA", mensaje: "Lluvia detectada en Cobán", fechaGeneracion: "2026-10-28T10:00:00Z" }],
        }),
      });
    renderizar(<Favorites />, { ruta: "/favoritos" });

    await userEvent.setup().click(await screen.findByRole("button", { name: /Actualizar clima/ }));

    expect(await screen.findByText("81% humedad")).toBeInTheDocument();
    expect(screen.getByText("Nueva alerta: Lluvia detectada en Cobán")).toBeInTheDocument();
    expect(servidor.a("/api/clima/actual")[0].encabezados.authorization).toBeDefined();
  });
});

describe("M08 – historial", () => {
  it("lista la página real, con '—' en las mediciones del pronóstico", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/historial", { cuerpo: pagina([consultaPronostico, consultaActual]) });
    renderizar(<History />, { ruta: "/historial" });

    const tabla = await screen.findByRole("table");
    const filas = within(tabla).getAllByRole("row");
    expect(filas).toHaveLength(3);
    expect(within(filas[1]).getByText("PRONÓSTICO")).toBeInTheDocument();
    expect(within(filas[1]).getByText("—")).toBeInTheDocument();
    expect(within(filas[2]).getByText("18°")).toBeInTheDocument();
    expect(screen.getByText("Página 1 de 1 · 2 consultas")).toBeInTheDocument();
    expect(servidor.a("/api/historial")[0].url.searchParams.get("pagina")).toBe("1");
  });

  it("estado vacío del mockup sin consultas", async () => {
    servidorConSesion().en("GET", "/api/favoritos", { cuerpo: [] }).en("GET", "/api/historial", { cuerpo: pagina([]) });
    renderizar(<History />, { ruta: "/historial" });

    expect(await screen.findByText("Todavía no has consultado el clima")).toBeInTheDocument();
  });

  it("envía los filtros aprobados: tipo, ubicación y rango de fecha", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [favorito()] })
      .en("GET", "/api/historial", { cuerpo: pagina([consultaActual]) });
    renderizar(<History />, { ruta: "/historial" });
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("table");

    await usuarioEvento.selectOptions(screen.getByLabelText("Tipo de consulta"), "PRONOSTICO");
    await usuarioEvento.selectOptions(screen.getByLabelText("Ubicación"), "12");
    await usuarioEvento.type(screen.getByLabelText("Fecha de consulta"), "2026-10-28");

    await waitFor(() => {
      const ultima = servidor.a("/api/historial").at(-1)!.url.searchParams;
      expect(ultima.get("tipo")).toBe("PRONOSTICO");
      expect(ultima.get("idUbicacion")).toBe("12");
      expect(ultima.get("desde")).toBe(new Date(2026, 9, 28).toISOString());
      expect(ultima.get("hasta")).toBe(new Date(2026, 9, 29).toISOString());
      expect(ultima.get("pagina")).toBe("1");
    });
  });

  it("pagina con Siguiente", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/historial", (peticion) => ({
        cuerpo: pagina([consultaActual], { pagina: Number(peticion.url.searchParams.get("pagina")), totalElementos: 25, totalPaginas: 2 }),
      }));
    renderizar(<History />, { ruta: "/historial" });

    await userEvent.setup().click(await screen.findByRole("button", { name: "Siguiente" }));

    expect(await screen.findByText(/Página 2 de 2/)).toBeInTheDocument();
    expect(servidor.a("/api/historial").at(-1)!.url.searchParams.get("pagina")).toBe("2");
  });

  it("abre el detalle de una consulta ACTUAL y permite consultar de nuevo", async () => {
    servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/historial", { cuerpo: pagina([consultaActual]) })
      .en("GET", "/api/historial/57", { cuerpo: detalleActual });
    renderizar(<History />, { ruta: "/historial" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Ver" }));
    const dialogo = await screen.findByRole("dialog");
    expect(await within(dialogo).findByText("18.6 °C")).toBeInTheDocument();
    expect(within(dialogo).getByText("2.1 m/s")).toBeInTheDocument();

    await usuarioEvento.click(within(dialogo).getByRole("button", { name: "Consultar de nuevo" }));
    expect(screen.getByTestId("ruta")).toHaveTextContent("/clima?idUbicacion=12");
  });

  it("detalle inexistente o ajeno (404)", async () => {
    servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/historial", { cuerpo: pagina([consultaActual]) })
      .en("GET", "/api/historial/57", problema(404, "La consulta no existe."));
    renderizar(<History />, { ruta: "/historial" });

    await userEvent.setup().click(await screen.findByRole("button", { name: "Ver" }));

    expect(await within(await screen.findByRole("dialog")).findByText("La consulta no existe.")).toBeInTheDocument();
  });
});

describe("M10 – configuración de alertas", () => {
  it("lista las reglas reales (global y específica)", async () => {
    servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/configuraciones-alerta", {
        cuerpo: [regla(), regla({ idConfiguracion: 6, tipo: "LLUVIA", ubicacion: null, valorUmbral: null, activa: false })],
      });
    renderizar(<AlertSettings />, { ruta: "/configuracion-alertas" });

    expect(await screen.findByText("Todas mis ubicaciones")).toBeInTheDocument();
    expect(screen.getByText("32 °C")).toBeInTheDocument();
    expect(screen.getByText("No aplica")).toBeInTheDocument();
    expect(screen.getByText("1 activas · 1 inactivas")).toBeInTheDocument();
  });

  it("crea una regla LLUVIA global sin umbral", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [favorito()] })
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [] })
      .en("POST", "/api/configuraciones-alerta", {
        status: 201,
        cuerpo: regla({ idConfiguracion: 8, tipo: "LLUVIA", ubicacion: null, valorUmbral: null }),
      });
    renderizar(<AlertSettings />, { ruta: "/configuracion-alertas" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText(/Aún no tienes reglas/);

    await usuarioEvento.click(screen.getByRole("button", { name: /Nueva configuración/ }));
    await usuarioEvento.selectOptions(screen.getByLabelText("Tipo de alerta"), "LLUVIA");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar configuración" }));

    expect(await screen.findByText("Configuración guardada correctamente.")).toBeInTheDocument();
    expect(servidor.a("/api/configuraciones-alerta", "POST")[0].cuerpo).toEqual({
      tipo: "LLUVIA",
      idUbicacion: null,
      valorUmbral: null,
      activa: true,
    });
  });

  it("crea una regla de temperatura para una ubicación y muestra el 409 del backend", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [favorito()] })
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [] })
      .en("POST", "/api/configuraciones-alerta", problema(409, "Ya tienes una regla de ese tipo para esta ubicación.", { tipo: ["Ya tienes una regla de ese tipo para esta ubicación."] }));
    renderizar(<AlertSettings />, { ruta: "/configuracion-alertas" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText(/Aún no tienes reglas/);

    await usuarioEvento.click(screen.getByRole("button", { name: /Nueva configuración/ }));
    await usuarioEvento.selectOptions(screen.getByLabelText("Ubicación"), "12");
    await usuarioEvento.selectOptions(screen.getByLabelText("Tipo de alerta"), "TEMPERATURA_BAJA");
    await usuarioEvento.clear(screen.getByLabelText("Valor umbral"));
    await usuarioEvento.type(screen.getByLabelText("Valor umbral"), "12.75");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar configuración" }));

    expect(await within(screen.getByRole("dialog")).findByRole("alert")).toHaveTextContent("Ya tienes una regla de ese tipo");
    expect(servidor.a("/api/configuraciones-alerta", "POST")[0].cuerpo).toEqual({
      tipo: "TEMPERATURA_BAJA",
      idUbicacion: 12,
      valorUmbral: 12.75,
      activa: true,
    });
  });

  it("valida el umbral antes de enviar", async () => {
    const servidor = servidorConSesion().en("GET", "/api/favoritos", { cuerpo: [] }).en("GET", "/api/configuraciones-alerta", { cuerpo: [] });
    renderizar(<AlertSettings />, { ruta: "/configuracion-alertas" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText(/Aún no tienes reglas/);

    await usuarioEvento.click(screen.getByRole("button", { name: /Nueva configuración/ }));
    await usuarioEvento.clear(screen.getByLabelText("Valor umbral"));
    await usuarioEvento.type(screen.getByLabelText("Valor umbral"), "61");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar configuración" }));

    expect(await screen.findByText("Ingresa una temperatura válida entre -50 y 60 °C.")).toBeInTheDocument();
    expect(servidor.a("/api/configuraciones-alerta", "POST")).toHaveLength(0);
  });

  it("edita con PUT (todos los campos), pausa con PATCH y elimina con DELETE", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const servidor = servidorConSesion()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [regla()] })
      .en("PUT", "/api/configuraciones-alerta/5", { cuerpo: regla({ valorUmbral: 33 }) })
      .en("PATCH", "/api/configuraciones-alerta/5/estado", { cuerpo: regla({ valorUmbral: 33, activa: false }) })
      .en("DELETE", "/api/configuraciones-alerta/5", { status: 204 });
    renderizar(<AlertSettings />, { ruta: "/configuracion-alertas" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Editar" }));
    await usuarioEvento.clear(screen.getByLabelText("Valor umbral"));
    await usuarioEvento.type(screen.getByLabelText("Valor umbral"), "33");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar configuración" }));
    expect(await screen.findByText("33 °C")).toBeInTheDocument();
    expect(servidor.a("/api/configuraciones-alerta/5", "PUT")[0].cuerpo).toEqual({
      tipo: "TEMPERATURA_ALTA",
      idUbicacion: 12,
      valorUmbral: 33,
      activa: true,
    });

    await usuarioEvento.click(screen.getByRole("button", { name: "Desactivar" }));
    expect(await screen.findByText("Inactiva")).toBeInTheDocument();
    expect(servidor.a("/api/configuraciones-alerta/5/estado", "PATCH")[0].cuerpo).toEqual({ activa: false });

    await usuarioEvento.click(screen.getByRole("button", { name: "Eliminar" }));
    expect(await screen.findByText(/Aún no tienes reglas/)).toBeInTheDocument();
    expect(servidor.a("/api/configuraciones-alerta/5", "DELETE")).toHaveLength(1);
  });
});

describe("M09 – alertas y campana", () => {
  it("lista las alertas reales y los resúmenes", async () => {
    servidorConSesion(usuario, 1)
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [regla()] })
      .en("GET", "/api/alertas", (p) =>
        p.url.searchParams.get("estado") === "LEIDA"
          ? { cuerpo: pagina([], { totalElementos: 4 }) }
          : { cuerpo: pagina([alerta(), alerta({ idAlerta: 92, tipo: "LLUVIA", mensaje: "Lluvia detectada en Cobán", valorDetectado: null, valorUmbral: null, estado: "LEIDA" })]) }
      );
    renderizar(<Alerts />, { ruta: "/alertas" });

    expect(await screen.findByText("Cobán: 33.4 C supera tu umbral de 32 C")).toBeInTheDocument();
    expect(screen.getByText("Detectada: 33.4 °C · Umbral: 32 °C")).toBeInTheDocument();
    expect(screen.getByText("Detección: lluvia presente · Sin umbral numérico")).toBeInTheDocument();
    expect(await screen.findByText("4")).toBeInTheDocument();
  });

  it("filtra por estado en el servidor", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [] })
      .en("GET", "/api/alertas", { cuerpo: pagina([]) });
    renderizar(<Alerts />, { ruta: "/alertas" });
    await screen.findByText("Sin alertas por ahora");

    await userEvent.setup().click(screen.getByRole("button", { name: "Nuevas" }));

    await waitFor(() => expect(servidor.a("/api/alertas").some((p) => p.url.searchParams.get("estado") === "NUEVA")).toBe(true));
    expect(await screen.findByText("No hay alertas en esta categoría")).toBeInTheDocument();
  });

  it("marca como LEIDA con PATCH y actualiza la campana", async () => {
    let nuevas = 1;
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    servidor
      .en("GET", "/api/alertas/contador", () => ({ cuerpo: { nuevas } }))
      .en("GET", "/api/configuraciones-alerta", { cuerpo: [] })
      .en("GET", "/api/alertas", { cuerpo: pagina([alerta()]) })
      .en("PATCH", "/api/alertas/91/estado", () => {
        nuevas = 0;
        return { cuerpo: alerta({ estado: "LEIDA" }) };
      });
    renderizar(<><Navbar /><Alerts /></>, { ruta: "/alertas" });
    expect(await screen.findByTestId("contador-alertas")).toHaveTextContent("1");

    await userEvent.setup().click(await screen.findByRole("button", { name: /Marcar como leída/ }));

    expect(await screen.findByText("LEÍDA")).toBeInTheDocument();
    expect(servidor.a("/api/alertas/91/estado", "PATCH")[0].cuerpo).toEqual({ estado: "LEIDA" });
    await waitFor(() => expect(screen.queryByTestId("contador-alertas")).not.toBeInTheDocument());
  });
});

describe("M15 – alertas del sistema (administrador)", () => {
  it("lista con el usuario destinatario, filtra en el servidor y no permite marcar alertas ajenas", async () => {
    const servidor = servidorConSesion(administrador)
      .en("GET", "/api/admin/alertas", { cuerpo: pagina([alertaAdmin()]) })
      .en("GET", "/api/admin/alertas/91", { cuerpo: alertaAdmin() });
    renderizar(<AdminAlerts />, { ruta: "/admin/alertas" });
    const usuarioEvento = userEvent.setup();

    expect(await screen.findByText("Cobán: 33.4 C supera tu umbral de 32 C")).toBeInTheDocument();
    await usuarioEvento.selectOptions(screen.getByLabelText("Tipo"), "LLUVIA");
    await waitFor(() =>
      expect(servidor.a("/api/admin/alertas").some((p) => p.url.searchParams.get("tipo") === "LLUVIA")).toBe(true)
    );

    await usuarioEvento.click((await screen.findAllByRole("button", { name: "Ver detalle" }))[0]);
    const dialogo = await screen.findByRole("dialog");
    expect(await within(dialogo).findByText(/ana.lopez@ejemplo.com/)).toBeInTheDocument();
    expect(within(dialogo).queryByRole("button", { name: /Marcar como leída/ })).not.toBeInTheDocument();
  });
});

describe("M16 – perfil", () => {
  it("muestra el perfil real y guarda nombre y correo con PUT", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/usuarios/me", { cuerpo: usuario })
      .en("PUT", "/api/usuarios/me", { cuerpo: { ...usuario, nombre: "Ana María López" } });
    renderizar(<><Navbar /><UserProfile /></>, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    const nombre = await screen.findByDisplayValue("Ana López");
    await usuarioEvento.clear(nombre);
    await usuarioEvento.type(nombre, "Ana María López");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar cambios" }));

    expect(await screen.findByText("Cambios guardados")).toBeInTheDocument();
    expect(servidor.a("/api/usuarios/me", "PUT")[0].cuerpo).toEqual({ nombre: "Ana María López", correo: "ana.lopez@ejemplo.com" });
    expect(screen.getAllByText("Ana María López").length).toBeGreaterThan(0);
  });

  it("cambia la contraseña con PUT /api/usuarios/me/password y muestra el error del campo", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/usuarios/me", { cuerpo: usuario })
      .en("PUT", "/api/usuarios/me/password", problema(400, "x", { passwordActual: ["La contraseña actual no es correcta."] }));
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Cambiar contraseña" }));
    await usuarioEvento.type(screen.getByLabelText("Contraseña actual"), "Mala2026");
    await usuarioEvento.type(screen.getByLabelText("Nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.type(screen.getByLabelText("Confirmar nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Cambiar contraseña" }));

    expect(await screen.findByText("La contraseña actual no es correcta.")).toBeInTheDocument();
    expect(servidor.a("/api/usuarios/me/password", "PUT")[0].cuerpo).toEqual({
      passwordActual: "Mala2026",
      passwordNueva: "NuevaClave2027",
    });
  });
});

describe("seguridad – Fase 8", () => {
  it("al cambiar la contraseña cierra la sesión porque el backend revoca los tokens", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/usuarios/me", { cuerpo: usuario })
      .en("PUT", "/api/usuarios/me/password", { status: 204 });
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Cambiar contraseña" }));
    await usuarioEvento.type(screen.getByLabelText("Contraseña actual"), "Actual2026");
    await usuarioEvento.type(screen.getByLabelText("Nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.type(screen.getByLabelText("Confirmar nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Cambiar contraseña" }));

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(screen.getByText("Pantalla de inicio de sesión")).toBeInTheDocument();
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
    expect(servidor.a("/api/usuarios/me/password", "PUT")).toHaveLength(1);
  });
});

describe("utilidades", () => {
  it("coban del contrato tiene los campos de UbicacionResponse", () => {
    expect(Object.keys(coban).sort()).toEqual(["idUbicacion", "latitud", "longitud", "nombre", "pais", "region"]);
  });
});
