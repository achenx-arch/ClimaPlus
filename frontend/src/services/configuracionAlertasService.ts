import { solicitar } from "./apiClient";
import type { ConfiguracionAlerta, DatosConfiguracionAlerta } from "../types/api";

// contrato-configuracion-alertas.md (requiere sesión).
const RUTA = "/api/configuraciones-alerta";

/** LLUVIA no admite umbral (RN-BD-17): se envía null. */
function cuerpo(datos: DatosConfiguracionAlerta) {
  return {
    tipo: datos.tipo,
    idUbicacion: datos.idUbicacion,
    valorUmbral: datos.tipo === "LLUVIA" ? null : datos.valorUmbral,
    activa: datos.activa,
  };
}

export function listarConfiguraciones(signal?: AbortSignal): Promise<ConfiguracionAlerta[]> {
  return solicitar<ConfiguracionAlerta[]>(RUTA, { signal });
}

export function crearConfiguracion(datos: DatosConfiguracionAlerta): Promise<ConfiguracionAlerta> {
  return solicitar<ConfiguracionAlerta>(RUTA, { metodo: "POST", cuerpo: cuerpo(datos) });
}

/** PUT reemplaza todos los campos editables (activa obligatorio). */
export function actualizarConfiguracion(id: number, datos: DatosConfiguracionAlerta): Promise<ConfiguracionAlerta> {
  return solicitar<ConfiguracionAlerta>(`${RUTA}/${id}`, { metodo: "PUT", cuerpo: cuerpo(datos) });
}

export function cambiarEstadoConfiguracion(id: number, activa: boolean): Promise<ConfiguracionAlerta> {
  return solicitar<ConfiguracionAlerta>(`${RUTA}/${id}/estado`, { metodo: "PATCH", cuerpo: { activa } });
}

export function eliminarConfiguracion(id: number): Promise<void> {
  return solicitar<void>(`${RUTA}/${id}`, { metodo: "DELETE" });
}
