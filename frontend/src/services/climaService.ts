import { solicitar } from "./apiClient";
import type { ClimaActual, Pronostico, Ubicacion } from "../types/api";

// contrato-clima-pronostico.md. Rutas públicas: con sesión se envía el token
// (la consulta entra al historial y puede generar alertas); sin sesión, no.

/** GET /api/clima/ubicaciones?q=&limite= (máx. 5 resultados). */
export function buscarUbicaciones(q: string, signal?: AbortSignal, limite = 5): Promise<Ubicacion[]> {
  return solicitar<Ubicacion[]>("/api/clima/ubicaciones", {
    query: { q: q.trim(), limite },
    autenticacion: "opcional",
    signal,
  });
}

// Cada GET de clima registra una consulta en el servidor. Se comparte la petición
// en curso para la misma ubicación y sesión: así React StrictMode o un doble clic
// no generan dos consultas (ni dos alertas) por una sola acción del usuario.
const enCurso = new Map<string, Promise<unknown>>();

function compartir<T>(clave: string, crear: () => Promise<T>): Promise<T> {
  const existente = enCurso.get(clave) as Promise<T> | undefined;
  if (existente) {
    return existente;
  }

  const promesa = crear().finally(() => enCurso.delete(clave));
  enCurso.set(clave, promesa);
  return promesa;
}

/** Olvida las peticiones en curso (al cerrar sesión no se reutiliza nada de la cuenta anterior). */
export function limpiarPeticionesClima(): void {
  enCurso.clear();
}

/** GET /api/clima/actual?idUbicacion= */
export function obtenerClimaActual(idUbicacion: number, claveSesion = "anonimo"): Promise<ClimaActual> {
  return compartir(`actual:${idUbicacion}:${claveSesion}`, () =>
    solicitar<ClimaActual>("/api/clima/actual", { query: { idUbicacion }, autenticacion: "opcional" })
  );
}

/** GET /api/clima/pronostico?idUbicacion= */
export function obtenerPronostico(idUbicacion: number, claveSesion = "anonimo"): Promise<Pronostico> {
  return compartir(`pronostico:${idUbicacion}:${claveSesion}`, () =>
    solicitar<Pronostico>("/api/clima/pronostico", { query: { idUbicacion }, autenticacion: "opcional" })
  );
}
