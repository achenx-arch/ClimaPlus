import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { contarAlertasNuevas } from "../services/alertasService";
import type { AlertaGenerada } from "../types/api";
import { useAuth } from "./auth";
import { NotificacionesContext, type Aviso, type TipoAviso, type ValorNotificaciones } from "./notificaciones";

/** Intervalo de actualización de la campana mientras hay sesión. */
const INTERVALO_CONTADOR_MS = 60_000;
const DURACION_AVISO_MS = 6_000;

let secuencia = 0;

export function NotificacionesProvider({ children }: { children: ReactNode }) {
  const { estado, usuario } = useAuth();
  const autenticado = estado === "autenticado";

  const [nuevas, setNuevas] = useState(0);
  const [avisos, setAvisos] = useState<Aviso[]>([]);

  // Al cerrar sesión o cambiar de cuenta se descartan los avisos y el contador anteriores.
  const cuenta = autenticado ? usuario?.idUsuario ?? null : null;
  const [cuentaPrevia, setCuentaPrevia] = useState(cuenta);
  if (cuentaPrevia !== cuenta) {
    setCuentaPrevia(cuenta);
    setAvisos([]);
    setNuevas(0);
  }

  // idAlerta ya notificados: evita toasts repetidos por renderizados o StrictMode.
  const notificadas = useRef(new Set<number>());
  const temporizadores = useRef(new Map<string, number>());

  const cerrarAviso = useCallback((id: string) => {
    setAvisos((actuales) => actuales.filter((aviso) => aviso.id !== id));
    const temporizador = temporizadores.current.get(id);
    if (temporizador !== undefined) {
      window.clearTimeout(temporizador);
      temporizadores.current.delete(id);
    }
  }, []);

  const avisar = useCallback(
    (texto: string, tipo: TipoAviso = "exito") => {
      const id = `aviso-${++secuencia}`;
      setAvisos((actuales) => [...actuales, { id, tipo, texto }]);
      temporizadores.current.set(id, window.setTimeout(() => cerrarAviso(id), DURACION_AVISO_MS));
    },
    [cerrarAviso]
  );

  const refrescarContador = useCallback(async () => {
    if (!autenticado) {
      setNuevas(0);
      return;
    }
    try {
      setNuevas(await contarAlertasNuevas());
    } catch {
      // La campana conserva el último valor; un 401 ya lo gestiona la sesión.
    }
  }, [autenticado]);

  const notificarAlertasGeneradas = useCallback(
    (alertas: AlertaGenerada[] | undefined) => {
      if (!alertas || alertas.length === 0) return;
      let hayNuevas = false;
      for (const alerta of alertas) {
        if (notificadas.current.has(alerta.idAlerta)) continue;
        notificadas.current.add(alerta.idAlerta);
        hayNuevas = true;
        avisar(`Nueva alerta: ${alerta.mensaje}`, "alerta");
      }
      if (hayNuevas) {
        void refrescarContador();
      }
    },
    [avisar, refrescarContador]
  );

  // Contador al iniciar sesión, cada minuto y al volver a la pestaña. Sin sesión: 0.
  useEffect(() => {
    if (!autenticado) {
      notificadas.current.clear();
      return;
    }
    const control = new AbortController();
    contarAlertasNuevas(control.signal)
      .then(setNuevas)
      .catch(() => {});
    const intervalo = window.setInterval(() => void refrescarContador(), INTERVALO_CONTADOR_MS);
    const alEnfocar = () => void refrescarContador();
    window.addEventListener("focus", alEnfocar);
    return () => {
      control.abort();
      window.clearInterval(intervalo);
      window.removeEventListener("focus", alEnfocar);
    };
  }, [autenticado, usuario?.idUsuario, refrescarContador]);

  useEffect(() => {
    const mapa = temporizadores.current;
    return () => {
      mapa.forEach((temporizador) => window.clearTimeout(temporizador));
      mapa.clear();
    };
  }, []);

  const valor = useMemo<ValorNotificaciones>(
    () => ({
      nuevas: autenticado ? nuevas : 0,
      refrescarContador,
      notificarAlertasGeneradas,
      avisar,
      avisos,
      cerrarAviso,
    }),
    [autenticado, nuevas, refrescarContador, notificarAlertasGeneradas, avisar, avisos, cerrarAviso]
  );

  return <NotificacionesContext.Provider value={valor}>{children}</NotificacionesContext.Provider>;
}

