import type { TipoAlerta, Ubicacion } from "../types/api";

// Presentación de los datos meteorológicos según 08-mockups.md.

/** Icono de Bootstrap Icons por código de condición de OpenWeather (tabla de 08-mockups.md). */
export function iconoCondicion(codigo: number | null | undefined, esDeDia = true): string {
  if (codigo === null || codigo === undefined) return "bi-cloud";
  if (codigo >= 200 && codigo < 300) return "bi-cloud-lightning-rain";
  if (codigo >= 300 && codigo < 400) return "bi-cloud-drizzle";
  if (codigo >= 500 && codigo < 600) return "bi-cloud-rain-heavy";
  if (codigo >= 600 && codigo < 700) return "bi-cloud-snow";
  if (codigo >= 700 && codigo < 800) return "bi-cloud-fog";
  if (codigo === 800) return esDeDia ? "bi-sun" : "bi-moon";
  if (codigo === 801 || codigo === 802) return esDeDia ? "bi-cloud-sun" : "bi-cloud-moon";
  return "bi-clouds";
}

/** Primera letra en mayúscula ("lluvia ligera" → "Lluvia ligera"). */
export function capitalizar(texto: string | null | undefined): string {
  if (!texto) return "";
  return texto.charAt(0).toLocaleUpperCase("es") + texto.slice(1);
}

/** Temperatura redondeada para las tarjetas: 22.46 → "22°". */
export function grados(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? "—" : `${Math.round(valor)}°`;
}

/** Valor con un decimal como máximo, con coma decimal (es-GT usa punto: se respeta la configuración local). */
export function numero(valor: number | null | undefined, decimales = 1): string {
  if (valor === null || valor === undefined) return "—";
  return valor.toLocaleString("es-GT", { maximumFractionDigits: decimales });
}

/** Viento en m/s, la unidad de la API y del mockup M04. */
export function viento(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? "—" : `${numero(valor)} m/s`;
}

export function humedad(valor: number | null | undefined): string {
  return valor === null || valor === undefined ? "—" : `${valor}%`;
}

export function nivelHumedad(valor: number): string {
  if (valor < 30) return "Baja";
  if (valor <= 70) return "Moderada";
  return "Alta";
}

const PUNTOS_CARDINALES = ["Norte", "Noreste", "Este", "Sureste", "Sur", "Suroeste", "Oeste", "Noroeste"];

/** Grados (0–360) → punto cardinal en español. */
export function direccionViento(grados: number | null | undefined): string {
  if (grados === null || grados === undefined) return "Sin dirección";
  return PUNTOS_CARDINALES[Math.round((((grados % 360) + 360) % 360) / 45) % 8];
}

/** "Cobán, Alta Verapaz, GT" sin partes vacías. */
export function lugar(ubicacion: Pick<Ubicacion, "region" | "pais">): string {
  return [ubicacion.region, ubicacion.pais].filter(Boolean).join(", ");
}

export const NOMBRES_TIPO_ALERTA: Record<TipoAlerta, string> = {
  LLUVIA: "Lluvia",
  TEMPERATURA_ALTA: "Temperatura alta",
  TEMPERATURA_BAJA: "Temperatura baja",
};

export const ICONOS_TIPO_ALERTA: Record<TipoAlerta, string> = {
  LLUVIA: "bi-cloud-rain",
  TEMPERATURA_ALTA: "bi-thermometer-high",
  TEMPERATURA_BAJA: "bi-thermometer-low",
};

/** Clases CSS de las tarjetas de alerta existentes (Alerts.css). */
export const CLASE_TIPO_ALERTA: Record<TipoAlerta, "lluvia" | "calor" | "frio"> = {
  LLUVIA: "lluvia",
  TEMPERATURA_ALTA: "calor",
  TEMPERATURA_BAJA: "frio",
};
