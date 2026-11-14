import { act, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Route } from "react-router-dom";
import { CLAVE_SESION } from "../context/AuthProvider";
import Login from "../pages/Login";
import RecoverPassword from "../pages/RecoverPassword";
import Register from "../pages/Register";
import UserProfile from "../pages/UserProfile";
import { hayToken } from "../services/apiClient";
import { reiniciarCargaGoogle, type ConfiguracionGoogle } from "../services/googleIdentity";
import type { Usuario } from "../types/api";
import { usuario } from "./datos";
import { TOKEN_PRUEBA, conSesion, renderizar } from "./renderizar";
import { crearServidor, problema } from "./servidorFalso";

// Fase 6: registro con verificación, recuperación en tres pasos, "Continuar con Google" y los métodos de acceso del
// perfil (vincular, desvincular y establecer contraseña). El backend es el servidor falso; Google es un
// window.google falso que se comporta como GIS: initialize guarda el callback y renderButton dibuja un botón que,
// al pulsarlo, entrega un ID token.

const ID_TOKEN = "id.token.de.google";

const desafio = (proposito: string, id = "desafio.1", reenviarEn = 60) => ({
  status: 202,
  cuerpo: {
    desafio: id,
    proposito,
    destino: "a***@ejemplo.com",
    expiraEn: new Date(Date.now() + 300_000).toISOString(),
    reenviarEn,
  },
});

const sesion = (u: Usuario = usuario, token = "jwt.nuevo") => ({
  cuerpo: { token, tipoToken: "Bearer", expiraEn: new Date(Date.now() + 3_600_000).toISOString(), usuario: u },
});

const rutasPrivadas = <Route path="/dashboard" element={<p>Dashboard privado</p>} />;

/** GIS falso. Devuelve los espías y la lista de configuraciones recibidas (para revisar los nonces). */
function instalarGoogleFalso() {
  const configuraciones: ConfiguracionGoogle[] = [];
  const initialize = vi.fn((configuracion: ConfiguracionGoogle) => {
    configuraciones.push(configuracion);
  });
  const renderButton = vi.fn((contenedor: HTMLElement) => {
    const boton = document.createElement("button");
    boton.type = "button";
    boton.textContent = "Google falso";
    // Como GIS: el callback que vale es el del último initialize.
    boton.addEventListener("click", () => configuraciones.at(-1)?.callback({ credential: ID_TOKEN }));
    contenedor.appendChild(boton);
  });
  window.google = { accounts: { id: { initialize, renderButton } } };
  return { initialize, renderButton, configuraciones };
}

/** /api/auth/google/iniciar con un nonce distinto en cada llamada. */
function conInicioGoogle(servidor: ReturnType<typeof crearServidor>) {
  let n = 0;
  return servidor.en("GET", "/api/auth/google/iniciar", () => ({
    cuerpo: { clientId: "123.apps.googleusercontent.com", nonce: `nonce-${++n}`, expiraEn: new Date(Date.now() + 600_000).toISOString() },
  }));
}

async function escribirCodigo(boton: RegExp | string, codigo = "048213") {
  const usuarioEvento = userEvent.setup();
  await usuarioEvento.type(await screen.findByLabelText("Código de verificación"), codigo);
  await usuarioEvento.click(screen.getByRole("button", { name: boton }));
}

afterEach(() => {
  delete window.google;
  reiniciarCargaGoogle();
});

describe("registro con verificación de correo", () => {
  async function llenarRegistro() {
    const usuarioEvento = userEvent.setup();
    await usuarioEvento.type(screen.getByLabelText("Nombre completo"), "Ana López");
    await usuarioEvento.type(screen.getByLabelText("Correo electrónico"), "ana.lopez@ejemplo.com");
    await usuarioEvento.type(screen.getByLabelText("Contraseña"), "Ejemplo2026");
    await usuarioEvento.type(screen.getByLabelText("Confirmar contraseña"), "Ejemplo2026");
    await usuarioEvento.click(screen.getByRole("checkbox"));
    await usuarioEvento.click(screen.getByRole("button", { name: /Crear cuenta/ }));
  }

  it("crea la cuenta, pide el código y, al validarlo, manda a iniciar sesión sin abrir sesión", async () => {
    const servidor = crearServidor()
      .en("POST", "/api/auth/register", desafio("VERIFICAR_CORREO", "desafio.registro"))
      .en("POST", "/api/auth/verificar-correo", { status: 204 });
    renderizar(<Register />, { ruta: "/registro", patron: "/registro" });

    await llenarRegistro();

    expect(await screen.findByRole("heading", { name: "Verifica tu correo" })).toBeInTheDocument();
    expect(servidor.a("/api/auth/register")[0].cuerpo).toEqual({
      nombre: "Ana López",
      correo: "ana.lopez@ejemplo.com",
      password: "Ejemplo2026",
    });

    await escribirCodigo("Verificar correo");

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(servidor.a("/api/auth/verificar-correo")[0].cuerpo).toEqual({ desafio: "desafio.registro", codigo: "048213" });
    expect(hayToken()).toBe(false);
  });

  it("un código vencido muestra el error del campo y deja reintentar", async () => {
    crearServidor()
      .en("POST", "/api/auth/register", desafio("VERIFICAR_CORREO"))
      .en("POST", "/api/auth/verificar-correo", problema(400, "x", { codigo: ["El código no es válido o ya venció."] }));
    renderizar(<Register />, { ruta: "/registro", patron: "/registro" });

    await llenarRegistro();
    await escribirCodigo("Verificar correo", "999999");

    expect(await screen.findByRole("alert")).toHaveTextContent("El código no es válido o ya venció.");
    expect(screen.getByRole("button", { name: "Verificar correo" })).toBeEnabled();
  });

  it("un correo ya registrado (409) se queda en el formulario con el mensaje del backend", async () => {
    crearServidor().en("POST", "/api/auth/register", problema(409, "Ya existe una cuenta con ese correo."));
    renderizar(<Register />, { ruta: "/registro", patron: "/registro" });

    await llenarRegistro();

    expect(await screen.findByText(/Ya existe una cuenta con ese correo/)).toBeInTheDocument();
    expect(screen.queryByLabelText("Código de verificación")).not.toBeInTheDocument();
  });
});

describe("recuperación de contraseña en tres pasos", () => {
  async function pedirCodigo(correo = "ana.lopez@ejemplo.com") {
    const usuarioEvento = userEvent.setup();
    await usuarioEvento.type(screen.getByLabelText("Correo electrónico"), correo);
    await usuarioEvento.click(screen.getByRole("button", { name: /Enviar código/ }));
  }

  async function nuevaPassword(password = "NuevaClave2027", confirmacion = password) {
    const usuarioEvento = userEvent.setup();
    await usuarioEvento.type(await screen.findByLabelText("Nueva contraseña"), password);
    await usuarioEvento.type(screen.getByLabelText("Confirmar nueva contraseña"), confirmacion);
    await usuarioEvento.click(screen.getByRole("button", { name: /Guardar contraseña/ }));
  }

  it("correo → código → contraseña nueva, y vuelve al login con aviso", async () => {
    const servidor = crearServidor()
      .en("POST", "/api/auth/password/solicitar", desafio("RECUPERAR_PASSWORD", "desafio.recuperar"))
      .en("POST", "/api/auth/password/verificar", { cuerpo: { permiso: "permiso.1", expiraEn: new Date(Date.now() + 600_000).toISOString() } })
      .en("POST", "/api/auth/password/restablecer", { status: 204 });
    renderizar(<RecoverPassword />, { ruta: "/recuperar", patron: "/recuperar" });

    await pedirCodigo();
    await escribirCodigo("Continuar");
    await nuevaPassword();

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(servidor.a("/api/auth/password/solicitar")[0].cuerpo).toEqual({ correo: "ana.lopez@ejemplo.com" });
    expect(servidor.a("/api/auth/password/verificar")[0].cuerpo).toEqual({ desafio: "desafio.recuperar", codigo: "048213" });
    expect(servidor.a("/api/auth/password/restablecer")[0].cuerpo).toEqual({
      permiso: "permiso.1",
      passwordNueva: "NuevaClave2027",
      confirmacion: "NuevaClave2027",
    });
    expect(hayToken()).toBe(false);
  });

  it("valida la política y la confirmación antes de llamar al backend", async () => {
    const servidor = crearServidor()
      .en("POST", "/api/auth/password/solicitar", desafio("RECUPERAR_PASSWORD"))
      .en("POST", "/api/auth/password/verificar", { cuerpo: { permiso: "permiso.1", expiraEn: new Date(Date.now() + 600_000).toISOString() } });
    renderizar(<RecoverPassword />, { ruta: "/recuperar", patron: "/recuperar" });

    await pedirCodigo();
    await escribirCodigo("Continuar");
    await nuevaPassword("corta");
    expect(await screen.findByRole("alert")).toHaveTextContent("entre 8 y 128 caracteres");

    const usuarioEvento = userEvent.setup();
    await usuarioEvento.clear(screen.getByLabelText("Nueva contraseña"));
    await usuarioEvento.clear(screen.getByLabelText("Confirmar nueva contraseña"));
    await nuevaPassword("NuevaClave2027", "OtraClave2027");
    expect(await screen.findByRole("alert")).toHaveTextContent("Las contraseñas no coinciden.");
    expect(servidor.a("/api/auth/password/restablecer")).toHaveLength(0);
  });

  it("un permiso vencido o ya usado obliga a empezar de nuevo", async () => {
    crearServidor()
      .en("POST", "/api/auth/password/solicitar", desafio("RECUPERAR_PASSWORD"))
      .en("POST", "/api/auth/password/verificar", { cuerpo: { permiso: "permiso.1", expiraEn: new Date(Date.now() + 600_000).toISOString() } })
      .en("POST", "/api/auth/password/restablecer", problema(400, "x", { permiso: ["El permiso para cambiar la contraseña venció. Solicita un código nuevo."] }));
    renderizar(<RecoverPassword />, { ruta: "/recuperar", patron: "/recuperar" });

    await pedirCodigo();
    await escribirCodigo("Continuar");
    await nuevaPassword();

    expect(await screen.findByRole("alert")).toHaveTextContent("Solicita un código nuevo");
    expect(screen.getByLabelText("Correo electrónico")).toBeInTheDocument();
    expect(screen.queryByLabelText("Nueva contraseña")).not.toBeInTheDocument();
  });

  it("429 al pedir el código deshabilita el envío", async () => {
    crearServidor().en(
      "POST",
      "/api/auth/password/solicitar",
      problema(429, "Demasiadas solicitudes. Espera un momento.", undefined, { "Retry-After": "60" })
    );
    renderizar(<RecoverPassword />, { ruta: "/recuperar", patron: "/recuperar" });

    await pedirCodigo();

    expect(await screen.findByRole("alert")).toHaveTextContent("Demasiadas solicitudes");
    expect(screen.getByRole("button", { name: /Enviar código/ })).toBeDisabled();
  });
});

describe("Continuar con Google en el login", () => {
  it("una cuenta nueva o vinculada entra con el JWT de ClimaPlus, sin código por correo", async () => {
    const google = instalarGoogleFalso();
    const servidor = conInicioGoogle(crearServidor())
      .en("POST", "/api/auth/google", sesion({ ...usuario, tienePassword: false, tieneGoogle: true }))
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });

    await userEvent.setup().click(await screen.findByRole("button", { name: "Google falso" }));

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(servidor.a("/api/auth/google")[0].cuerpo).toEqual({ credencial: ID_TOKEN });
    expect(servidor.a("/api/auth/google")[0].encabezados.authorization).toBeUndefined();
    expect(servidor.a("/api/auth/login")).toHaveLength(0);
    expect(hayToken()).toBe(true);
    // El nonce que la API emitió es el que recibe GIS; el ID token nunca se guarda.
    expect(google.configuraciones.at(-1)).toMatchObject({ client_id: "123.apps.googleusercontent.com", ux_mode: "popup", auto_select: false });
    expect(google.configuraciones.at(-1)?.nonce).toMatch(/^nonce-\d+$/);
    expect(JSON.stringify(sessionStorage)).not.toContain(ID_TOKEN);
  });

  it("un Gmail que ya tiene cuenta local (409) muestra el mensaje y no abre sesión", async () => {
    instalarGoogleFalso();
    conInicioGoogle(crearServidor()).en(
      "POST",
      "/api/auth/google",
      problema(409, "Ya existe una cuenta con ese correo. Inicia sesión con tu contraseña y vincula Google desde tu perfil.")
    );
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await userEvent.setup().click(await screen.findByRole("button", { name: "Google falso" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("vincula Google desde tu perfil");
    expect(hayToken()).toBe(false);
  });

  it("401 (token o nonce inválido) muestra un mensaje genérico y prepara un nonce nuevo para reintentar", async () => {
    const google = instalarGoogleFalso();
    const servidor = conInicioGoogle(crearServidor()).en("POST", "/api/auth/google", problema(401, "detalle interno"));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });
    const usuarioEvento = userEvent.setup();

    const boton = await screen.findByRole("button", { name: "Google falso" });
    const nonceUsado = google.configuraciones.at(-1)?.nonce;
    await usuarioEvento.click(boton);

    expect(await screen.findByRole("alert")).toHaveTextContent("No se pudo validar el inicio con Google. Vuelve a intentarlo.");
    expect(screen.getByRole("alert")).not.toHaveTextContent("detalle interno");
    await waitFor(() => expect(google.configuraciones.at(-1)?.nonce).not.toBe(nonceUsado));
    const llamadasIniciar = servidor.a("/api/auth/google/iniciar").length;
    expect(llamadasIniciar).toBeGreaterThan(1);
    expect(hayToken()).toBe(false);
  });

  it("no envía la misma credencial dos veces aunque Google llame dos veces al callback", async () => {
    const google = instalarGoogleFalso();
    let liberar: () => void = () => {};
    const servidor = conInicioGoogle(crearServidor())
      .en("POST", "/api/auth/google", () => new Promise((resolver) => {
        liberar = () => resolver(sesion());
      }))
      .en("GET", "/api/alertas/contador", { cuerpo: { nuevas: 0 } });
    renderizar(<Login />, { ruta: "/login", patron: "/login", rutasExtra: rutasPrivadas });
    await screen.findByRole("button", { name: "Google falso" });

    const callback = google.configuraciones.at(-1)!.callback;
    act(() => {
      callback({ credential: ID_TOKEN });
      callback({ credential: ID_TOKEN });
    });
    expect(await screen.findByText("Verificando tu cuenta de Google...")).toBeInTheDocument();
    await act(async () => liberar());

    expect(await screen.findByText("Dashboard privado")).toBeInTheDocument();
    expect(servidor.a("/api/auth/google")).toHaveLength(1);
  });

  it("sin Google configurado en el servidor (503) no muestra el botón y el login normal sigue", async () => {
    instalarGoogleFalso();
    crearServidor().en("GET", "/api/auth/google/iniciar", problema(503, "Google no está configurado."));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    await waitFor(() => expect(screen.queryByTestId("boton-google")).not.toBeInTheDocument());
    expect(screen.getByRole("button", { name: /^Iniciar sesión/ })).toBeEnabled();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("si la API falla al preparar Google, ofrece reintentar sin tapar el formulario", async () => {
    instalarGoogleFalso();
    const servidor = crearServidor().en("GET", "/api/auth/google/iniciar", problema(500, "x"));
    renderizar(<Login />, { ruta: "/login", patron: "/login" });

    expect(await screen.findByText(/No se pudo preparar el inicio con Google/)).toBeInTheDocument();
    conInicioGoogle(servidor);
    await userEvent.setup().click(screen.getByRole("button", { name: "Reintentar" }));
    expect(await screen.findByRole("button", { name: "Google falso" })).toBeInTheDocument();
  });
});

describe("métodos de acceso en el perfil", () => {
  function servidorConSesion(u: Usuario = usuario) {
    const servidor = crearServidor();
    conSesion(servidor, u);
    servidor.en("GET", "/api/usuarios/me", { cuerpo: u });
    return servidor;
  }

  it("vincula Google: contraseña + cuenta de Google + código, y reemplaza el JWT", async () => {
    instalarGoogleFalso();
    const vinculado = { ...usuario, tieneGoogle: true };
    const servidor = conInicioGoogle(servidorConSesion())
      .en("POST", "/api/usuarios/me/google/vincular", desafio("VINCULAR_GOOGLE", "desafio.vincular"))
      .en("POST", "/api/usuarios/me/google/vincular/verificar", sesion(vinculado, "jwt.tras.vincular"));
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Vincular Google" }));
    const dialogo = screen.getByRole("dialog");
    await usuarioEvento.type(within(dialogo).getByLabelText("Contraseña actual"), "Ejemplo2026");
    await usuarioEvento.click(await within(dialogo).findByRole("button", { name: "Google falso" }));
    await escribirCodigo("Confirmar vinculación");

    expect(await screen.findByText("Cuenta de Google vinculada. Se cerraron tus otras sesiones.")).toBeInTheDocument();
    expect(servidor.a("/api/auth/google/iniciar").at(-1)?.url.searchParams.get("uso")).toBe("vincular");
    expect(servidor.a("/api/auth/google/iniciar").at(-1)?.encabezados.authorization).toBe(`Bearer ${TOKEN_PRUEBA}`);
    expect(servidor.a("/api/usuarios/me/google/vincular")[0].cuerpo).toEqual({ credencial: ID_TOKEN, passwordActual: "Ejemplo2026" });
    expect(servidor.a("/api/usuarios/me/google/vincular/verificar")[0].cuerpo).toEqual({ desafio: "desafio.vincular", codigo: "048213" });
    expect(screen.getByText("Vinculada")).toBeInTheDocument();
    // La sesión recordada se conserva, ahora con el JWT nuevo.
    expect(sessionStorage.getItem(CLAVE_SESION)).toContain("jwt.tras.vincular");
    expect(sessionStorage.getItem(CLAVE_SESION)).not.toContain(TOKEN_PRUEBA);
  });

  it("vincular sin escribir la contraseña no envía nada", async () => {
    instalarGoogleFalso();
    const servidor = conInicioGoogle(servidorConSesion());
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Vincular Google" }));
    await usuarioEvento.click(await within(screen.getByRole("dialog")).findByRole("button", { name: "Google falso" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Escribe tu contraseña actual");
    expect(servidor.a("/api/usuarios/me/google/vincular")).toHaveLength(0);
  });

  it("vincular con la contraseña incorrecta muestra el error del campo", async () => {
    instalarGoogleFalso();
    conInicioGoogle(servidorConSesion()).en(
      "POST",
      "/api/usuarios/me/google/vincular",
      problema(400, "x", { passwordActual: ["La contraseña actual no es correcta."] })
    );
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Vincular Google" }));
    const dialogo = screen.getByRole("dialog");
    await usuarioEvento.type(within(dialogo).getByLabelText("Contraseña actual"), "Mala2026");
    await usuarioEvento.click(await within(dialogo).findByRole("button", { name: "Google falso" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("La contraseña actual no es correcta.");
    expect(screen.queryByLabelText("Código de verificación")).not.toBeInTheDocument();
  });

  it("desvincula Google con la contraseña y reemplaza el JWT", async () => {
    const conGoogle = { ...usuario, tieneGoogle: true };
    const servidor = servidorConSesion(conGoogle).en(
      "POST",
      "/api/usuarios/me/google/desvincular",
      sesion({ ...conGoogle, tieneGoogle: false }, "jwt.tras.desvincular")
    );
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Desvincular" }));
    const dialogo = screen.getByRole("dialog");
    await usuarioEvento.type(within(dialogo).getByLabelText("Contraseña actual"), "Ejemplo2026");
    await usuarioEvento.click(within(dialogo).getByRole("button", { name: "Desvincular Google" }));

    expect(await screen.findByText("Cuenta de Google desvinculada. Se cerraron tus otras sesiones.")).toBeInTheDocument();
    expect(servidor.a("/api/usuarios/me/google/desvincular")[0].cuerpo).toEqual({ passwordActual: "Ejemplo2026" });
    expect(screen.getByText("No vinculada")).toBeInTheDocument();
    expect(sessionStorage.getItem(CLAVE_SESION)).toContain("jwt.tras.desvincular");
  });

  it("una cuenta creada con Google no puede desvincular ni vincular hasta tener contraseña", async () => {
    servidorConSesion({ ...usuario, tienePassword: false, tieneGoogle: true });
    renderizar(<UserProfile />, { ruta: "/perfil" });

    expect(await screen.findByRole("button", { name: "Desvincular" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Establecer contraseña" })).toBeEnabled();
    expect(screen.queryByRole("button", { name: "Cambiar contraseña" })).not.toBeInTheDocument();
  });

  it("establece contraseña: código al correo → permiso → contraseña nueva, y cierra la sesión", async () => {
    const servidor = servidorConSesion({ ...usuario, tienePassword: false, tieneGoogle: true })
      .en("POST", "/api/usuarios/me/password/establecer", desafio("RECUPERAR_PASSWORD", "desafio.establecer"))
      .en("POST", "/api/auth/password/verificar", { cuerpo: { permiso: "permiso.establecer", expiraEn: new Date(Date.now() + 600_000).toISOString() } })
      .en("POST", "/api/auth/password/restablecer", { status: 204 });
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Establecer contraseña" }));
    await usuarioEvento.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Enviarme el código" }));
    await escribirCodigo("Continuar");
    await usuarioEvento.type(await screen.findByLabelText("Nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.type(screen.getByLabelText("Confirmar nueva contraseña"), "NuevaClave2027");
    await usuarioEvento.click(screen.getByRole("button", { name: "Guardar contraseña" }));

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(servidor.a("/api/auth/password/verificar")[0].cuerpo).toEqual({ desafio: "desafio.establecer", codigo: "048213" });
    expect(servidor.a("/api/auth/password/restablecer")[0].cuerpo).toEqual({
      permiso: "permiso.establecer",
      passwordNueva: "NuevaClave2027",
      confirmacion: "NuevaClave2027",
    });
    expect(hayToken()).toBe(false);
    expect(sessionStorage.getItem(CLAVE_SESION)).toBeNull();
  });

  it("si la sesión vence (401) mientras se desvincula, vuelve al inicio de sesión", async () => {
    servidorConSesion({ ...usuario, tieneGoogle: true }).en("POST", "/api/usuarios/me/google/desvincular", problema(401, "Expiró"));
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Desvincular" }));
    await usuarioEvento.type(within(screen.getByRole("dialog")).getByLabelText("Contraseña actual"), "Ejemplo2026");
    await usuarioEvento.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Desvincular Google" }));

    await waitFor(() => expect(screen.getByTestId("ruta")).toHaveTextContent("/login"));
    expect(hayToken()).toBe(false);
  });

  it("un 500 al desvincular muestra un mensaje genérico y la sesión sigue", async () => {
    servidorConSesion({ ...usuario, tieneGoogle: true }).en("POST", "/api/usuarios/me/google/desvincular", problema(500, "SqlException"));
    renderizar(<UserProfile />, { ruta: "/perfil" });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.click(await screen.findByRole("button", { name: "Desvincular" }));
    await usuarioEvento.type(within(screen.getByRole("dialog")).getByLabelText("Contraseña actual"), "Ejemplo2026");
    await usuarioEvento.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Desvincular Google" }));

    const alerta = await screen.findByRole("alert");
    expect(alerta).toHaveTextContent("Ocurrió un error inesperado en el servidor.");
    expect(alerta).not.toHaveTextContent("SqlException");
    expect(hayToken()).toBe(true);
  });
});
