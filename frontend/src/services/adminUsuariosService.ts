import { solicitar } from "./apiClient";
import type { FiltroUsuariosAdmin, Pagina, Rol, UsuarioAdmin } from "../types/api";

// M14 – Gestión de usuarios (contrato-admin-usuarios.md). Solo ADMINISTRADOR: 403 para USUARIO.

/** RF-26: lista paginada (20 por página), más recientes primero. */
export function listarUsuariosAdmin(filtro: FiltroUsuariosAdmin = {}, signal?: AbortSignal): Promise<Pagina<UsuarioAdmin>> {
  const busqueda = filtro.busqueda?.trim();
  return solicitar<Pagina<UsuarioAdmin>>("/api/admin/usuarios", {
    query: {
      busqueda: busqueda ? busqueda : undefined,
      rol: filtro.rol,
      estado: filtro.estado,
      pagina: filtro.pagina ?? 1,
    },
    signal,
  });
}

export function obtenerUsuarioAdmin(idUsuario: number, signal?: AbortSignal): Promise<UsuarioAdmin> {
  return solicitar<UsuarioAdmin>(`/api/admin/usuarios/${idUsuario}`, { signal });
}

/** RF-27. 409 si es el propio rol o el último administrador activo. Cierra las sesiones del usuario afectado. */
export function cambiarRolUsuario(idUsuario: number, rol: Rol): Promise<UsuarioAdmin> {
  return solicitar<UsuarioAdmin>(`/api/admin/usuarios/${idUsuario}/rol`, { metodo: "PATCH", cuerpo: { rol } });
}

/** RF-28. 409 si es la propia cuenta o el último administrador activo. Cierra las sesiones del usuario afectado. */
export function cambiarEstadoUsuario(idUsuario: number, activo: boolean): Promise<UsuarioAdmin> {
  return solicitar<UsuarioAdmin>(`/api/admin/usuarios/${idUsuario}/estado`, { metodo: "PATCH", cuerpo: { activo } });
}
