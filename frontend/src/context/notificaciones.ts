import { createContext, useContext } from "react";
import type { AlertaGenerada } from "../types/api";

export type TipoAviso = "alerta" | "exito" | "error" | "info";

export interface Aviso {
  id: string;
  tipo: TipoAviso;
  texto: string;
}

export interface ValorNotificaciones {
  /** Alertas en estado NUEVA del usuario (GET /api/alertas/contador). */
  nuevas: number;
  refrescarContador: () => Promise<void>;
  /** Muestra un toast "Nueva alerta: …" por cada alerta recibida en alertasGeneradas, una sola vez por idAlerta. */
  notificarAlertasGeneradas: (alertas: AlertaGenerada[] | undefined) => void;
  /** Toast de confirmación o error (08-mockups.md: abajo a la derecha). */
  avisar: (texto: string, tipo?: TipoAviso) => void;
  avisos: Aviso[];
  cerrarAviso: (id: string) => void;
}

export const NotificacionesContext = createContext<ValorNotificaciones | null>(null);

export function useNotificaciones(): ValorNotificaciones {
  const valor = useContext(NotificacionesContext);
  if (!valor) {
    throw new Error("useNotificaciones debe usarse dentro de <NotificacionesProvider>.");
  }
  return valor;
}
