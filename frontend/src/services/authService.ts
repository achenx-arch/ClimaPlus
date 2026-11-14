import { solicitar } from "./apiClient";
import type {
  ActualizarPerfilRequest,
  CambiarPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegistroRequest,
  RespuestaDesafio,
  RespuestaInicioGoogle,
  RespuestaPermiso,
  RespuestaReenvio,
  RestablecerPasswordRequest,
  Usuario,
  UsoGoogle,
  VerificarCambioPasswordRequest,
  VerificarCodigoRequest,
} from "../types/api";

// contrato-auth-perfil.md (autenticación segura, fases 2 a 5). Ningún token se registra en la consola.

// ─── Registro y verificación del correo (4.1) ────────────────────────────────

/** POST /api/auth/register (público). 202 con el desafío; el código llega al correo. Nunca devuelve sesión. */
export function registrarUsuario(datos: RegistroRequest): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/auth/register", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

/** POST /api/auth/verificar-correo: 204. */
export function verificarCorreo(datos: VerificarCodigoRequest): Promise<void> {
  return solicitar<void>("/api/auth/verificar-correo", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

/** POST /api/auth/reenviar-codigo: 202 con un desafío nuevo (sirve para cualquier propósito). */
export function reenviarCodigo(desafio: string): Promise<RespuestaReenvio> {
  return solicitar<RespuestaReenvio>("/api/auth/reenviar-codigo", { metodo: "POST", cuerpo: { desafio }, autenticacion: "ninguna" });
}

// ─── Inicio de sesión en dos pasos (4.2) ─────────────────────────────────────

/** POST /api/auth/login, paso 1: 202 con el desafío (INICIO_SESION o VERIFICAR_CORREO). Nunca devuelve el JWT. */
export function iniciarSesion(datos: LoginRequest): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/auth/login", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

/** POST /api/auth/login/verificar, paso 2: 200 con el JWT. */
export function verificarInicioSesion(datos: VerificarCodigoRequest): Promise<LoginResponse> {
  return solicitar<LoginResponse>("/api/auth/login/verificar", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

// ─── Recuperación de contraseña (4.8) ────────────────────────────────────────

/** POST /api/auth/password/solicitar: siempre 202 con la misma forma, exista o no la cuenta. */
export function solicitarRecuperacion(correo: string): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/auth/password/solicitar", { metodo: "POST", cuerpo: { correo }, autenticacion: "ninguna" });
}

/** POST /api/auth/password/verificar: 200 con el permiso de restablecer (10 minutos, un solo uso). */
export function verificarRecuperacion(datos: VerificarCodigoRequest): Promise<RespuestaPermiso> {
  return solicitar<RespuestaPermiso>("/api/auth/password/verificar", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

/** POST /api/auth/password/restablecer: 204. Cierra todas las sesiones; no inicia sesión. */
export function restablecerPassword(datos: RestablecerPasswordRequest): Promise<void> {
  return solicitar<void>("/api/auth/password/restablecer", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

// ─── Google (4.9) ────────────────────────────────────────────────────────────

/** GET /api/auth/google/iniciar: Client ID y nonce de un solo uso. "vincular" exige sesión. */
export function iniciarGoogle(uso: UsoGoogle, signal?: AbortSignal): Promise<RespuestaInicioGoogle> {
  return solicitar<RespuestaInicioGoogle>("/api/auth/google/iniciar", {
    query: { uso },
    autenticacion: uso === "vincular" ? "requerida" : "ninguna",
    signal,
  });
}

/** POST /api/auth/google: 200 con el JWT de ClimaPlus. */
export function iniciarSesionGoogle(credencial: string): Promise<LoginResponse> {
  return solicitar<LoginResponse>("/api/auth/google", { metodo: "POST", cuerpo: { credencial }, autenticacion: "ninguna" });
}

/** POST /api/usuarios/me/google/vincular: 202 con el desafío VINCULAR_GOOGLE. */
export function vincularGoogle(credencial: string, passwordActual: string): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/usuarios/me/google/vincular", { metodo: "POST", cuerpo: { credencial, passwordActual } });
}

/** POST /api/usuarios/me/google/vincular/verificar: 200 con un JWT nuevo (se cerraron las demás sesiones). */
export function verificarVinculacionGoogle(datos: VerificarCodigoRequest): Promise<LoginResponse> {
  return solicitar<LoginResponse>("/api/usuarios/me/google/vincular/verificar", { metodo: "POST", cuerpo: datos });
}

/** POST /api/usuarios/me/google/desvincular: 200 con un JWT nuevo. */
export function desvincularGoogle(passwordActual: string): Promise<LoginResponse> {
  return solicitar<LoginResponse>("/api/usuarios/me/google/desvincular", { metodo: "POST", cuerpo: { passwordActual } });
}

// ─── Sesión y perfil ─────────────────────────────────────────────────────────

/** GET /api/auth/me con un token concreto (restauración de sesión). */
export function obtenerUsuarioActual(signal?: AbortSignal): Promise<Usuario> {
  return solicitar<Usuario>("/api/auth/me", { signal });
}

/** GET /api/usuarios/me (M16, M17). */
export function obtenerPerfil(signal?: AbortSignal): Promise<Usuario> {
  return solicitar<Usuario>("/api/usuarios/me", { signal });
}

/** PUT /api/usuarios/me: siempre nombre y correo. */
export function actualizarPerfil(datos: ActualizarPerfilRequest): Promise<Usuario> {
  return solicitar<Usuario>("/api/usuarios/me", { metodo: "PUT", cuerpo: datos });
}

/**
 * POST /api/usuarios/me/password/cambio (paso 1): comprueba la contraseña actual y la nueva y envía un código
 * CAMBIAR_PASSWORD al correo (202). Todavía no cambia nada.
 */
export function solicitarCambioPassword(datos: CambiarPasswordRequest): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/usuarios/me/password/cambio", { metodo: "POST", cuerpo: datos });
}

/**
 * POST /api/usuarios/me/password/cambio/verificar (paso 2): con el código válido cambia la contraseña (204) y revoca
 * todas las sesiones, también la actual.
 */
export function verificarCambioPassword(datos: VerificarCambioPasswordRequest): Promise<void> {
  return solicitar<void>("/api/usuarios/me/password/cambio/verificar", { metodo: "POST", cuerpo: datos });
}

/** POST /api/usuarios/me/password/establecer (cuentas de Google): 202 con un desafío RECUPERAR_PASSWORD. */
export function establecerPassword(): Promise<RespuestaDesafio> {
  return solicitar<RespuestaDesafio>("/api/usuarios/me/password/establecer", { metodo: "POST" });
}
