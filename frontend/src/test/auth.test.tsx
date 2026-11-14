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

/** 202 del paso 1 del login (RespuestaDesafio). */
const desafioLogin = (proposito = "INICIO_SESION", reenviarEn = 60) => ({
  status: 202,
  cuerpo: {
    desafio: "desafio.login.1",
    proposito,
    destino: "a***@ejemplo.com",
    expiraEn: new Date(Date.now() + 300_000).toISOString(),
    reenviarEn,
  },
});

const respuestaLogin = (u = usuario) => ({
  cuerpo: {
    token: "jwt.nuevo.token",
    tipoToken: "Bearer",
    expiraEn: new Date(Date.now() + 3_600_000).toISOString(),
    usuario: u,
  },
});

/** Paso 1: correo y contraseña. */
async function iniciarSesion(correo = "ana.lopez@ejemplo.com", password = "Ejemplo2026", recordar = false) {
  const usuarioEvento = userEvent.setup();
  await usuarioEvento.type(screen.getByLabelText("Correo electrónico"), correo);
  await usuarioEvento.type(screen.getByLabelText("Contraseña"), password);
  if (recordar) await usuarioEvento.click(screen.getByLabelText("Recordarme"));
  await usuarioEvento.click(screen.getByRole("button", { name: /^Iniciar sesión/ }));
}

/** Paso 2: el código que llegó al correo. */
async function escribirCodigo(codigo = "048213") {
  const usuarioEvento = userEvent.setup();
  await usuarioEvento.type(await screen.findByLabelText("Código de verificación"), codigo);
  await usuarioEvento.click(screen.getByRole("button", { name: /Verificar e iniciar sesión/ }));
}

const rutasPrivadas = (
  <>
    <Route path="/dashboard" element={<RutaProtegida><p>Dashboard privado</p></RutaProtegida>} />
    <Route path="/admin" element={<RutaProtegida rol="ADMINISTRADOR"><p>Panel admin</p></RutaProtegida>} />
  </>
);

describe("autenticación", () => {
  it("inicia sesión en dos pasos: la contraseña no abre sesión; el código sí, y guarda el token solo en memoria", async () => {
    const servidor = crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", respuestaLogin())
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion();

    // Paso 1: sin token y en la pantalla del código.
    expect(await screen.findByRole("heading", { name: "Verifica que eres tú" })).toBeInTheDocument();
    expect(screen.getByText("a***@ejemplo.com")).toBeInTheDocument();
    expect(hayToken()).toBe(false);
    expect(servidor.a("/api/auth/login")[0].cuerpo).toEqual({ correo: "ana.lopez@ejemplo.com", password: "Ejemplo2026" });

    await escribirCodigo();

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(servidor.a("/api/auth/login/verificar")[0].cuerpo).toEqual({ desafio: "desafio.login.1", codigo: "048213" });
    expect(hayToken()).toBe(true);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
    expect(JSON.stringify({ ...localStorage })).not.toContain("jwt.nuevo.token");
  });

  it("con Recordarme guarda solo token y expiración en sessionStorage (nunca la contraseña)", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", respuestaLogin())
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion(undefined, undefined, true);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();   // nada se guarda antes del código
    await escribirCodigo();
    await screen.findByText("Dashboard privado");

    const guardado = JSON.parse(sessionStorage.getItem(CLAVE_SESION)!);
    expect(Object.keys(guardado).sort()).toEqual(["expiraEn", "token"]);
    expect(JSON.stringify(guardado)).not.toContain("Ejemplo2026");
    expect(localStorage.length).toBe(0);
  });

  it("un ADMINISTRADOR va a M13", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", respuestaLogin(administrador))
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion("admin@ejemplo.com");
    await escribirCodigo();

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
    expect(screen.getByRole("button", { name: /^Iniciar sesión/ })).toBeDisabled();
  });

  it("código incorrecto: muestra el error del campo y no abre sesión", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", problema(400, "x", { codigo: ["El código no es válido o ya venció."] }));
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion();
    await escribirCodigo("111111");

    expect(await screen.findByRole("alert")).toHaveTextContent("El código no es válido o ya venció.");
    expect(hayToken()).toBe(false);
    expect(screen.queryByText("Dashboard privado")).not.toBeInTheDocument();
  });

  it("código: 429 por bloqueo de la cuenta deshabilita la verificación", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", problema(429, "Demasiados intentos fallidos para esta cuenta.", undefined, { "Retry-After": "900" }));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await iniciarSesion();
    await escribirCodigo();

    expect(await screen.findByRole("alert")).toHaveTextContent("Demasiados intentos fallidos");
    expect(screen.getByRole("button", { name: /Verificar e iniciar sesión/ })).toBeDisabled();
    expect(hayToken()).toBe(false);
  });

  it("un código con menos de 6 dígitos no llega al backend", async () => {
    const servidor = crearServidor().en("POST", "/api/auth/login", desafioLogin());
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await iniciarSesion();
    await escribirCodigo("12a4");

    expect(await screen.findByRole("alert")).toHaveTextContent("Escribe los 6 dígitos del código.");
    expect(servidor.a("/api/auth/login/verificar")).toHaveLength(0);
  });

  it("no envía el código dos veces aunque se pulse dos veces", async () => {
    let liberar: () => void = () => {};
    const servidor = crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", () => new Promise((resolver) => {
        liberar = () => resolver(respuestaLogin());
      }))
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });
    const usuarioEvento = userEvent.setup();

    await iniciarSesion();
    await usuarioEvento.type(await screen.findByLabelText("Código de verificación"), "048213");
    const formulario = screen.getByRole("button", { name: /Verificar e iniciar sesión/ }).closest("form")!;
    act(() => {
      formulario.requestSubmit();
      formulario.requestSubmit();
    });
    await act(async () => liberar());

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(servidor.a("/api/auth/login/verificar")).toHaveLength(1);
  });

  it("reenviar código usa el desafío nuevo que devuelve el backend", async () => {
    const servidor = crearServidor()
      .en("POST", "/api/auth/login", desafioLogin("INICIO_SESION", 0))
      .en("POST", "/api/auth/reenviar-codigo", {
        status: 202,
        cuerpo: { desafio: "desafio.login.2", proposito: "INICIO_SESION", expiraEn: new Date(Date.now() + 300_000).toISOString(), reenviarEn: 60 },
      })
      .en("POST", "/api/auth/login/verificar", respuestaLogin())
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });
    const usuarioEvento = userEvent.setup();

    await iniciarSesion();
    await usuarioEvento.click(await screen.findByRole("button", { name: "Reenviar código" }));

    expect(await screen.findByText(/Te enviamos un código nuevo/)).toBeInTheDocument();
    expect(servidor.a("/api/auth/reenviar-codigo")[0].cuerpo).toEqual({ desafio: "desafio.login.1" });
    expect(screen.getByRole("button", { name: /Reenviar código en \d+ s/ })).toBeDisabled();
    await escribirCodigo();
    await screen.findByText("Dashboard privado");
    expect(servidor.a("/api/auth/login/verificar")[0].cuerpo).toEqual({ desafio: "desafio.login.2", codigo: "048213" });
  });

  it("una cuenta sin verificar recibe el código de verificación de correo y entra al validarlo", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin("VERIFICAR_CORREO"))
      .en("POST", "/api/auth/login/verificar", respuestaLogin())
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await iniciarSesion();

    expect(await screen.findByRole("heading", { name: "Verifica tu correo" })).toBeInTheDocument();
    await escribirCodigo();
    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
  });

  it("un 500 en el código muestra un mensaje genérico, sin detalles internos, y no abre sesión", async () => {
    crearServidor()
      .en("POST", "/api/auth/login", desafioLogin())
      .en("POST", "/api/auth/login/verificar", problema(500, "NullReferenceException en AuthService"));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await iniciarSesion();
    await escribirCodigo();

    const alerta = await screen.findByRole("alert");
    expect(alerta).toHaveTextContent("Ocurrió un error inesperado en el servidor.");
    expect(alerta).not.toHaveTextContent("NullReference");
    expect(hayToken()).toBe(false);
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
