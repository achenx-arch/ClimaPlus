import { createContext, useContext } from "react";
import type { Usuario } from "../types/api";

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
  iniciarSesion: (correo: string, password: string, recordar: boolean) => Promise<Usuario>;
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
