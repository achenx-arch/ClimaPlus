import { useEffect, useRef, useState } from "react";
import "./VerificacionCodigo.css";
import { ApiError, esCancelacion } from "../services/apiClient";
import { iniciarGoogle } from "../services/authService";
import { cargarGoogle } from "../services/googleIdentity";
import type { UsoGoogle } from "../types/api";

interface Props {
  /** "login": entrar o crear cuenta. "vincular": exige sesión; el nonce queda atado a esta cuenta. */
  uso: UsoGoogle;
  /**
   * Recibe el ID token que entregó Google. Quien lo usa lo envía a la API y muestra los errores; este componente
   * solo garantiza un envío a la vez y un nonce nuevo para cada intento.
   */
  alRecibirCredencial: (credencial: string) => Promise<void>;
  texto?: "continue_with" | "signin_with";
}

type Estado = "preparando" | "listo" | "procesando" | "no-disponible" | "error";

/** Antes de que venza el nonce (10 min) se pide otro, para que el botón nunca use uno vencido. */
const MARGEN_RENOVACION_MS = 30_000;

/**
 * Botón oficial "Continuar con Google" (Google Identity Services, contrato-auth-perfil.md 4.9).
 * 1. Pide a la API el Client ID y un nonce de un solo uso (GET /api/auth/google/iniciar).
 * 2. Inicializa GIS con ese nonce y dibuja el botón oficial (popup).
 * 3. Google entrega el ID token; se pasa a alRecibirCredencial una sola vez.
 * 4. Después de cada intento (y antes de que venza) se pide un nonce nuevo: nunca se reutiliza uno.
 * Si Google no está configurado en el servidor (503), el botón no se muestra.
 */
export default function BotonGoogle({ uso, alRecibirCredencial, texto = "continue_with" }: Props) {
  const contenedor = useRef<HTMLDivElement>(null);
  const [estado, setEstado] = useState<Estado>("preparando");
  const [mensaje, setMensaje] = useState("");
  const [ronda, setRonda] = useState(0);
  const procesando = useRef(false);
  const manejador = useRef(alRecibirCredencial);

  useEffect(() => {
    manejador.current = alRecibirCredencial;
  }, [alRecibirCredencial]);

  useEffect(() => {
    const control = new AbortController();
    let renovar: number | undefined;

    (async () => {
      try {
        const inicio = await iniciarGoogle(uso, control.signal);
        const gis = await cargarGoogle();
        if (control.signal.aborted || !contenedor.current) return;

        gis.initialize({
          client_id: inicio.clientId,
          nonce: inicio.nonce,
          auto_select: false,
          cancel_on_tap_outside: true,
          ux_mode: "popup",
          callback: async ({ credential }) => {
            if (procesando.current || control.signal.aborted) return;   // un solo envío por credencial
            procesando.current = true;
            setEstado("procesando");
            try {
              await manejador.current(credential);
            } finally {
              procesando.current = false;
              // El nonce ya se usó (o se intentó usar): siempre se pide otro.
              if (!control.signal.aborted) setRonda((r) => r + 1);
            }
          },
        });
        contenedor.current.replaceChildren();
        gis.renderButton(contenedor.current, {
          type: "standard",
          theme: "outline",
          size: "large",
          text: texto,
          shape: "rectangular",
          width: Math.min(400, Math.max(200, contenedor.current.offsetWidth || 320)),
          locale: "es",
        });
        setEstado("listo");
        setMensaje("");

        const restante = new Date(inicio.expiraEn).getTime() - Date.now() - MARGEN_RENOVACION_MS;
        renovar = window.setTimeout(() => setRonda((r) => r + 1), Math.max(5_000, restante));
      } catch (error) {
        if (esCancelacion(error) || control.signal.aborted) return;
        if (error instanceof ApiError && error.status === 503) {
          setEstado("no-disponible");
          return;
        }
        setEstado("error");
        setMensaje(
          error instanceof ApiError
            ? "No se pudo preparar el inicio con Google. Inténtalo de nuevo."
            : "No se pudo cargar Google. Revisa tu conexión o desactiva el bloqueador de contenido."
        );
      }
    })();

    return () => {
      control.abort();
      if (renovar !== undefined) window.clearTimeout(renovar);
    };
  }, [uso, texto, ronda]);

  if (estado === "no-disponible") {
    return null;
  }

  return (
    <div className="boton-google-envoltura">
      <div
        ref={contenedor}
        className="boton-google"
        data-testid="boton-google"
        aria-busy={estado === "preparando" || estado === "procesando"}
        hidden={estado === "error"}
      />
      {estado === "procesando" && (
        <p className="boton-google-estado" role="status">
          Verificando tu cuenta de Google...
        </p>
      )}
      {/* role="status" y no "alert": este aviso no responde a algo que la persona hizo y no debe tapar el error del
          formulario de la misma pantalla para los lectores de pantalla. */}
      {estado === "error" && (
        <div className="boton-google-estado" role="status">
          {mensaje}{" "}
          <button type="button" className="codigo-verificacion-enlace" onClick={() => setRonda((r) => r + 1)}>
            Reintentar
          </button>
        </div>
      )}
    </div>
  );
}
