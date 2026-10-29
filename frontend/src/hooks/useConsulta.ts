import { useCallback, useEffect, useEffectEvent, useState } from "react";
import { esCancelacion } from "../services/apiClient";

interface Estado<T> {
  clave: string | null;
  datos?: T;
  error?: unknown;
}

export interface Consulta<T> {
  datos: T | undefined;
  error: unknown;
  cargando: boolean;
  /** Vuelve a pedir los datos con la misma clave. */
  recargar: () => void;
  /** Reemplaza los datos en memoria tras una operación (alta, baja, cambio de estado). */
  actualizar: (cambio: (actuales: T | undefined) => T | undefined) => void;
}

/**
 * Carga datos de la API cuando cambia la clave (null = no cargar).
 * - Cancela la petición anterior con AbortController al cambiar de clave o desmontar.
 * - Ignora respuestas que llegan tarde, así nunca se muestran datos de otra búsqueda.
 * - El estado de carga se deriva de la clave: no hace falta setState dentro del efecto.
 */
export function useConsulta<T>(clave: string | null, cargar: (signal: AbortSignal) => Promise<T>): Consulta<T> {
  const [version, setVersion] = useState(0);
  const claveCompleta = clave === null ? null : `${clave}#${version}`;
  const [estado, setEstado] = useState<Estado<T>>({ clave: null });

  const ejecutar = useEffectEvent((signal: AbortSignal) => cargar(signal));

  useEffect(() => {
    if (claveCompleta === null) return;
    const control = new AbortController();
    ejecutar(control.signal)
      .then((datos) => {
        if (!control.signal.aborted) setEstado({ clave: claveCompleta, datos });
      })
      .catch((error: unknown) => {
        if (control.signal.aborted || esCancelacion(error)) return;
        setEstado({ clave: claveCompleta, error });
      });
    return () => control.abort();
  }, [claveCompleta]);

  const recargar = useCallback(() => setVersion((v) => v + 1), []);

  const actualizar = useCallback(
    (cambio: (actuales: T | undefined) => T | undefined) =>
      setEstado((actual) => ({ ...actual, datos: cambio(actual.datos) })),
    []
  );

  const vigente = claveCompleta !== null && estado.clave === claveCompleta;
  // Al recargar la misma clave se siguen mostrando los datos anteriores hasta que llegan los nuevos.
  const mismaClave = clave !== null && estado.clave !== null && estado.clave.startsWith(`${clave}#`);

  return {
    datos: vigente || mismaClave ? estado.datos : undefined,
    error: vigente ? estado.error : undefined,
    cargando: claveCompleta !== null && !vigente,
    recargar,
    actualizar,
  };
}
