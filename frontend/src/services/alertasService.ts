import { solicitar } from "./apiClient";
import type { Alerta, AlertaAdmin, FiltroAlertas, FiltroAlertasAdmin, Pagina } from "../types/api";

// contrato-alertas-generadas.md

export function listarAlertas(filtro: FiltroAlertas = {}, signal?: AbortSignal): Promise<Pagina<Alerta>> {
  return solicitar<Pagina<Alerta>>("/api/alertas", {
    query: { estado: filtro.estado, tipo: filtro.tipo, pagina: filtro.pagina ?? 1 },
    signal,
  });
}

export async function contarAlertasNuevas(signal?: AbortSignal): Promise<number> {
  const respuesta = await solicitar<{ nuevas: number }>("/api/alertas/contador", { signal });
  return respuesta.nuevas;
}

/** Solo se puede marcar como LEIDA (RN-BD-26). Es idempotente. */
export function marcarAlertaLeida(idAlerta: number): Promise<Alerta> {
  return solicitar<Alerta>(`/api/alertas/${idAlerta}/estado`, { metodo: "PATCH", cuerpo: { estado: "LEIDA" } });
}

/** M15, solo ADMINISTRADOR (403 para USUARIO). */
export function listarAlertasAdmin(filtro: FiltroAlertasAdmin = {}, signal?: AbortSignal): Promise<Pagina<AlertaAdmin>> {
  return solicitar<Pagina<AlertaAdmin>>("/api/admin/alertas", {
    query: {
      estado: filtro.estado,
      tipo: filtro.tipo,
      origen: filtro.origen,
      desde: filtro.desde,
      hasta: filtro.hasta,
      idUsuario: filtro.idUsuario,
      pagina: filtro.pagina ?? 1,
    },
    signal,
  });
}

export function obtenerAlertaAdmin(idAlerta: number, signal?: AbortSignal): Promise<AlertaAdmin> {
  return solicitar<AlertaAdmin>(`/api/admin/alertas/${idAlerta}`, { signal });
}
