import { solicitar } from "./apiClient";
import type {
  ActualizarPerfilRequest,
  CambiarPasswordRequest,
  LoginRequest,
  LoginResponse,
  RegistroRequest,
  Usuario,
} from "../types/api";

// contrato-auth-perfil.md

/** POST /api/auth/login (público). */
export function iniciarSesion(datos: LoginRequest): Promise<LoginResponse> {
  return solicitar<LoginResponse>("/api/auth/login", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

/** POST /api/auth/register (público). 201 con el Usuario; no devuelve token. */
export function registrarUsuario(datos: RegistroRequest): Promise<Usuario> {
  return solicitar<Usuario>("/api/auth/register", { metodo: "POST", cuerpo: datos, autenticacion: "ninguna" });
}

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

/** PUT /api/usuarios/me/password: 204 sin cuerpo. */
export function cambiarPassword(datos: CambiarPasswordRequest): Promise<void> {
  return solicitar<void>("/api/usuarios/me/password", { metodo: "PUT", cuerpo: datos });
}
