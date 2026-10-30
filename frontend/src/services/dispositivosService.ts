import { solicitar } from "./apiClient";
import type {
  Dispositivo,
  DispositivoRegistrado,
  LecturaDispositivo,
  Alerta,
  Pagina,
} from "../types/api";

// contrato-dispositivos-lecturas.md (requiere sesión JWT, excepto POST /api/lecturas que usa X-Device-Key).

/** GET /api/dispositivos → lista de dispositivos del usuario autenticado. */
export function listarDispositivos(signal?: AbortSignal): Promise<Dispositivo[]> {
  return solicitar<Dispositivo[]>("/api/dispositivos", { signal });
}

/** GET /api/dispositivos/{id} → dispositivo por id (404 si no existe o es ajeno). */
export function obtenerDispositivo(id: number, signal?: AbortSignal): Promise<Dispositivo> {
  return solicitar<Dispositivo>(`/api/dispositivos/${id}`, { signal });
}

/** POST /api/dispositivos → 201 con { dispositivo, apiKey, aviso }. La clave se muestra una sola vez. */
export function registrarDispositivo(
  nombre: string,
  idUbicacion: number
): Promise<DispositivoRegistrado> {
  return solicitar<DispositivoRegistrado>("/api/dispositivos", {
    metodo: "POST",
    cuerpo: { nombre, idUbicacion },
  });
}

/** PUT /api/dispositivos/{id} → 200 con el Dispositivo actualizado. */
export function actualizarDispositivo(
  id: number,
  nombre: string,
  idUbicacion: number
): Promise<Dispositivo> {
  return solicitar<Dispositivo>(`/api/dispositivos/${id}`, {
    metodo: "PUT",
    cuerpo: { nombre, idUbicacion },
  });
}

/** PATCH /api/dispositivos/{id}/estado → 200 con el Dispositivo. activo=false revoca la API Key. */
export function cambiarEstadoDispositivo(id: number, activo: boolean): Promise<Dispositivo> {
  return solicitar<Dispositivo>(`/api/dispositivos/${id}/estado`, {
    metodo: "PATCH",
    cuerpo: { activo },
  });
}

/** GET /api/dispositivos/{id}/lecturas?pagina= → PaginaResponse<LecturaDispositivo> (20 por página). */
export function listarLecturasDispositivo(
  id: number,
  pagina = 1,
  signal?: AbortSignal
): Promise<Pagina<LecturaDispositivo>> {
  return solicitar<Pagina<LecturaDispositivo>>(`/api/dispositivos/${id}/lecturas`, {
    query: { pagina },
    signal,
  });
}

/** GET /api/dispositivos/{id}/alertas?pagina= → PaginaResponse<Alerta> (mismo objeto que GET /api/alertas). */
export function listarAlertasDispositivo(
  id: number,
  pagina = 1,
  signal?: AbortSignal
): Promise<Pagina<Alerta>> {
  return solicitar<Pagina<Alerta>>(`/api/dispositivos/${id}/alertas`, {
    query: { pagina },
    signal,
  });
}
