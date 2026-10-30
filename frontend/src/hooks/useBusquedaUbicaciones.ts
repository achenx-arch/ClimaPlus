import { useCallback, useRef, useState } from "react";
import { esCancelacion, mensajeDeError } from "../services/apiClient";
import { buscarUbicaciones } from "../services/climaService";
import type { Ubicacion } from "../types/api";

/** Misma regla que el backend (BusquedaUbicacionRequest): 2 a 100 caracteres permitidos. */
const PATRON_BUSQUEDA = /^\s*[\p{L}\p{M}\d][\p{L}\p{M}\d .,'-]*\s*$/u;

export function validarBusqueda(texto: string): string | null {
  const limpio = texto.trim();
  if (limpio.length < 2 || limpio.length > 100) {
    return "La búsqueda debe tener entre 2 y 100 caracteres.";
  }
  if (!PATRON_BUSQUEDA.test(limpio)) {
    return "La búsqueda solo puede contener letras, números, espacios, puntos, comas, apóstrofos y guiones.";
  }
  return null;
}

/** Búsqueda de ciudades con GET /api/clima/ubicaciones (OpenWeather Geocoding vía backend). */
export function useBusquedaUbicaciones() {
  const [resultados, setResultados] = useState<Ubicacion[] | null>(null);
  const [buscando, setBuscando] = useState(false);
  const [error, setError] = useState("");
  const control = useRef<AbortController | null>(null);

  const buscar = useCallback(async (texto: string): Promise<Ubicacion[] | null> => {
    const invalido = validarBusqueda(texto);
    if (invalido) {
      setError(invalido);
      setResultados(null);
      return null;
    }

    control.current?.abort();
    const actual = new AbortController();
    control.current = actual;
    setBuscando(true);
    setError("");

    try {
      const encontrados = await buscarUbicaciones(texto, actual.signal);
      setResultados(encontrados);
      if (encontrados.length === 0) {
        setError("No encontramos esa ubicación. Revisa el nombre e inténtalo de nuevo.");
      }
      return encontrados;
    } catch (e) {
      if (esCancelacion(e)) return null;
      setResultados(null);
      setError(mensajeDeError(e, "No se pudo buscar la ubicación."));
      return null;
    } finally {
      if (control.current === actual) setBuscando(false);
    }
  }, []);

  const limpiar = useCallback(() => {
    control.current?.abort();
    setResultados(null);
    setError("");
    setBuscando(false);
  }, []);

  return { resultados, buscando, error, buscar, limpiar };
}
