// Las fechas de la API llegan en UTC (ISO 8601 con Z) y se muestran en hora local.

const LOCALE = "es-GT";

export function fecha(iso: string): string {
  return new Date(iso).toLocaleDateString(LOCALE, { day: "numeric", month: "short", year: "numeric" });
}

export function hora(iso: string): string {
  return new Date(iso).toLocaleTimeString(LOCALE, { hour: "2-digit", minute: "2-digit" });
}

export function fechaHora(iso: string): string {
  return `${fecha(iso)} - ${hora(iso)}`;
}

/** Hora local de la ubicación: UTC + zonaHorariaSegundos (contrato-clima-pronostico.md). */
export function horaEnUbicacion(isoUtc: string | null | undefined, zonaHorariaSegundos: number): string {
  if (!isoUtc) return "—";
  const local = new Date(new Date(isoUtc).getTime() + zonaHorariaSegundos * 1000);
  return local.toLocaleTimeString(LOCALE, { hour: "2-digit", minute: "2-digit", timeZone: "UTC" });
}

/** "AAAA-MM-DD" (fecha local de la ubicación) → Date a mediodía para no cambiar de día. */
function desdeFechaLocal(texto: string): Date {
  const [anio, mes, dia] = texto.split("-").map(Number);
  return new Date(anio, mes - 1, dia, 12);
}

export function nombreDia(texto: string): string {
  const valor = desdeFechaLocal(texto).toLocaleDateString(LOCALE, { weekday: "long" });
  return valor.charAt(0).toLocaleUpperCase(LOCALE) + valor.slice(1);
}

export function diaCorto(texto: string): string {
  return desdeFechaLocal(texto).toLocaleDateString(LOCALE, { day: "numeric", month: "short" });
}

/**
 * Rango [desde, hasta) en UTC para un día local elegido en un input type="date".
 * desde incluido y hasta excluido, como espera la API.
 */
export function rangoDelDia(texto: string): { desde: string; hasta: string } {
  const [anio, mes, dia] = texto.split("-").map(Number);
  const inicio = new Date(anio, mes - 1, dia, 0, 0, 0);
  const fin = new Date(anio, mes - 1, dia + 1, 0, 0, 0);
  return { desde: inicio.toISOString(), hasta: fin.toISOString() };
}
