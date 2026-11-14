import { createContext, useContext } from "react";
import type { LoginResponse, RespuestaDesafio, Usuario } from "../types/api";

/**
 * "cerrando": el usuario pulsó "Cerrar sesión" y la navegación a Inicio todavía no termina.
 * Los datos privados ya se borraron; las rutas protegidas envían a "/" (no a /login).
 */
export type EstadoSesion = "cargando" | "autenticado" | "cerrando" | "anonimo";

export interface ValorAuth {
  estado: EstadoSesion;
  usuario: Usuario | null;
  esAdministrador: boolean;
  /** Mensaje para M02 (por ejemplo "Tu sesión expiró"). */
  mensajeSesion: string | null;
  /**
   * Paso 1 del inicio de sesión: comprueba la contraseña y devuelve el desafío del código enviado al correo.
   * NO abre sesión: no hay token hasta completar el paso 2.
   */
  iniciarSesion: (correo: string, password: string) => Promise<RespuestaDesafio>;
  /** Paso 2: valida el código y solo entonces abre la sesión. */
  completarInicioSesion: (desafio: string, codigo: string, recordar: boolean) => Promise<Usuario>;
  /** "Continuar con Google": la API valida el ID token y devuelve la sesión. */
  iniciarSesionConGoogle: (credencial: string, recordar: boolean) => Promise<Usuario>;
  /**
   * El backend emitió un JWT nuevo para esta sesión (vincular o desvincular Google cierran las demás sesiones).
   * Conserva la elección de "Recordarme".
   */
  reemplazarSesion: (respuesta: LoginResponse) => void;
  /** Cierra la sesión (solo en el navegador; no hay logout en el backend) y vuelve a Inicio. */
  cerrarSesion: () => void;
  /**
   * Termina la sesión y lleva a M02 mostrando un mensaje. Se usa cuando el backend ya revocó la sesión
   * (por ejemplo, al cambiar la contraseña – Fase 8, OWASP A07).
   */
  cerrarSesionConMensaje: (mensaje: string) => void;
  actualizarUsuario: (usuario: Usuario) => void;
  limpiarMensajeSesion: () => void;
}

export const AuthContext = createContext<ValorAuth | null>(null);

export function useAuth(): ValorAuth {
  const valor = useContext(AuthContext);
  if (!valor) {
    throw new Error("useAuth debe usarse dentro de <AuthProvider>.");
  }
  return valor;
}

/** Clave para compartir peticiones de clima por sesión (no contiene el token). */
export function claveSesion(usuario: Usuario | null): string {
  return usuario ? `u${usuario.idUsuario}` : "anonimo";
}
