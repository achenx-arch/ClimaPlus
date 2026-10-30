import { solicitar } from "./apiClient";
import type { ConsultaHistorial, ConsultaHistorialDetalle, FiltroHistorial, Pagina } from "../types/api";

// contrato-historial.md (requiere sesión; el usuario sale del token).

export function listarHistorial(filtro: FiltroHistorial, signal?: AbortSignal): Promise<Pagina<ConsultaHistorial>> {
  return solicitar<Pagina<ConsultaHistorial>>("/api/historial", {
    query: {
      tipo: filtro.tipo,
      idUbicacion: filtro.idUbicacion,
      desde: filtro.desde,
      hasta: filtro.hasta,
      pagina: filtro.pagina ?? 1,
    },
    signal,
  });
}

export function obtenerConsultaHistorial(idConsulta: number, signal?: AbortSignal): Promise<ConsultaHistorialDetalle> {
  return solicitar<ConsultaHistorialDetalle>(`/api/historial/${idConsulta}`, { signal });
}
