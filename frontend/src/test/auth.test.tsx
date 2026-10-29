import { act, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { Route } from "react-router-dom";
import RutaProtegida from "../components/RutaProtegida";
import Navbar from "../components/Navbar";
import { CLAVE_SESION } from "../context/AuthProvider";
import Login from "../pages/Login";
import { hayToken, solicitar } from "../services/apiClient";
import { administrador, usuario } from "./datos";
import { TOKEN_PRUEBA, conSesion, renderizar } from "./renderizar";
import { crearServidor, problema } from "./servidorFalso";

const respuestaLogin = (u = usuario) => ({
  cuerpo: {
    token: "jwt.nuevo.token",
    tipoToken: "Bearer",
    expiraEn: new Date(Date.now() + 3_600_000).toISOString(),
    usuario: u,
  },
});

async function iniciarSesion(correo = "ana.lopez@ejemplo.com", password = "Ejemplo2026", recordar = false) {
  const usuarioEvento = userEvent.setup();
  await usuarioEvento.type(screen.getByLabelText("Correo electrónico"), correo);
  await usuarioEvento.type(screen.getByLabelText("Contraseña"), password);
  if (recordar) await usuarioEvento.click(screen.getByLabelText("Recordarme"));
  await usuarioEvento.click(screen.getByRole("button", { name: /Iniciar sesión/ }));
}

const rutasPrivadas = (
  <>
    <Route path="/dashboard" element={<RutaProtegida><p>Dashboard privado</p></RutaProtegida>} />
    <Route path="/admin" element={<RutaProtegida rol="ADMINISTRADOR"><p>Panel admin</p></RutaProtegida>} />
  </>
);

describe("autenticación", () => {
  it("inicia sesión, guarda el token solo en memoria y lleva a M06", async () => {
    const servidor = crearServidor().en("POST", "/api/auth/login", respuestaLogin()).en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion();

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(servidor.a("/api/auth/login")[0].cuerpo).toEqual({ correo: "ana.lopez@ejemplo.com", password: "Ejemplo2026" });
    expect(hayToken()).toBe(true);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
    expect(JSON.stringify({ ...localStorage })).not.toContain("jwt.nuevo.token");
  });

  it("con Recordarme guarda solo token y expiración en sessionStorage (nunca la contraseña)", async () => {
    crearServidor().en("POST", "/api/auth/login", respuestaLogin()).en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion(undefined, undefined, true);
    await screen.findByText("Dashboard privado");

    const guardado = JSON.parse(sessionStorage.getItem(CLAVE_SESION)!);
    expect(Object.keys(guardado).sort()).toEqual(["expiraEn", "token"]);
    expect(JSON.stringify(guardado)).not.toContain("Ejemplo2026");
    expect(localStorage.length).toBe(0);
  });

  it("un ADMINISTRADOR va a M13", async () => {
    crearServidor().en("POST", "/api/auth/login", respuestaLogin(administrador)).en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion("admin@ejemplo.com");

    expect(await screen.findByText("Panel admin")).toBeInTheDocument();
  });

  it.each([
    [401, "Correo o contraseña incorrectos."],
    [403, "Tu cuenta está desactivada. Contacta al administrador."],
  ])("login %i muestra el mensaje del contrato", async (status, mensaje) => {
    crearServidor().en("POST", "/api/auth/login", problema(status, "x"));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await iniciarSesion();

    expect(await screen.findByRole("alert")).toHaveTextContent(mensaje);
    expect(hayToken()).toBe(false);
  });

  it("login 429 deshabilita el botón", async () => {
    crearServidor().en("POST", "/api/auth/login", problema(429, "Demasiados intentos. Espera un minuto.", undefined, { "Retry-After": "60" }));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await iniciarSesion();

    expect(await screen.findByRole("alert")).toHaveTextContent("Demasiados intentos");
    expect(screen.getByRole("button", { name: /Iniciar sesión/ })).toBeDisabled();
  });

  it("restaura una sesión recordada validándola con GET /api/auth/me", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario, 3);
    renderizar(<><Navbar /><RutaProtegida><p>Dashboard privado</p></RutaProtegida></>, { ruta: "/dashboard" });

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(screen.getByText("Ana López")).toBeInTheDocument();
    expect(await screen.findByTestId("contador-alertas")).toHaveTextContent("3");
    expect(servidor.a("/api/auth/me")[0].encabezados.authorization).toBe(`Bearer ${TOKEN_PRUEBA}`);
  });

  it("descarta una sesión guardada vencida sin llamar al backend", async () => {
    sessionStorage.setItem(CLAVE_SESION, JSON.stringify({ token: "viejo", expiraEn: "2020-01-01T00:00:00Z" }));
    const servidor = crearServidor();
    renderizar(<RutaProtegida><p>Dashboard privado</p></RutaProtegida>, { ruta: "/dashboard" });

    expect(await screen.findByText("Pantalla de inicio de sesión")).toBeInTheDocument();
    expect(servidor.peticiones).toHaveLength(0);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
  });

  it("descarta una sesión guardada si /api/auth/me responde 401 (token inválido)", async () => {
    sessionStorage.setItem(CLAVE_SESION, JSON.stringify({ token: "invalido", expiraEn: new Date(Date.now() + 60_000).toISOString() }));
    crearServidor().en("GET", "/api/auth/me", problema(401, "Token inválido"));
    renderizar(<RutaProtegida><p>Dashboard privado</p></RutaProtegida>, { ruta: "/dashboard" });

    expect(await screen.findByText("Pantalla de inicio de sesión")).toBeInTheDocument();
    expect(hayToken()).toBe(false);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
  });

  it("un 401 durante la sesión (token expirado) la cierra y vuelve a M02 con aviso", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    servidor.en("GET", "/api/favoritos", problema(401, "Expiró"));
    renderizar(<RutaProtegida><p>Dashboard privado</p></RutaProtegida>, { ruta: "/dashboard", rutasExtra: <Route path="/login" element={<Login />} /> });
    await screen.findByText("Dashboard privado");

    await act(async () => {
      await solicitar("/api/favoritos").catch(() => {});
    });

    expect(await screen.findByText("Tu sesión expiró. Inicia sesión de nuevo.")).toBeInTheDocument();
    expect(hayToken()).toBe(false);
  });

  it("cerrar sesión borra el token y la ruta privada vuelve a pedir inicio de sesión", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    renderizar(<><Navbar /><RutaProtegida><p>Dashboard privado</p></RutaProtegida></>, { ruta: "/dashboard" });
    await screen.findByText("Dashboard privado");

    await userEvent.setup().click(screen.getByRole("button", { name: /Cerrar sesión/ }));

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(hayToken()).toBe(false);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
  });
});

describe("rutas protegidas", () => {
  it("sin sesión redirige a M02", async () => {
    crearServidor();
    renderizar(<RutaProtegida><p>Dashboard privado</p></RutaProtegida>, { ruta: "/historial" });

    expect(await screen.findByText("Pantalla de inicio de sesión")).toBeInTheDocument();
    expect(screen.queryByText("Dashboard privado")).not.toBeInTheDocument();
  });

  it("un USUARIO no ve pantallas de ADMINISTRADOR", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    renderizar(<RutaProtegida rol="ADMINISTRADOR"><p>Panel admin</p></RutaProtegida>, { ruta: "/admin" });

    expect(await screen.findByText("Acceso denegado")).toBeInTheDocument();
    expect(screen.queryByText("Panel admin")).not.toBeInTheDocument();
  });

  it("un ADMINISTRADOR sí entra", async () => {
    const servidor = crearServidor();
    conSesion(servidor, administrador);
    renderizar(<RutaProtegida rol="ADMINISTRADOR"><p>Panel admin</p></RutaProtegida>, { ruta: "/admin" });

    expect(await screen.findByText("Panel admin")).toBeInTheDocument();
  });

  it("la navbar sin sesión muestra Iniciar sesión y Crear cuenta, sin campana", async () => {
    crearServidor();
    renderizar(<Navbar />);

    expect(screen.getByRole("link", { name: "Iniciar sesión" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Crear cuenta" })).toBeInTheDocument();
    expect(screen.queryByLabelText(/notificaciones/)).not.toBeInTheDocument();
  });
});
