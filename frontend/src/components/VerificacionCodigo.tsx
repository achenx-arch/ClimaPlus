import { useEffect, useRef, useState, type FormEvent, type ReactNode } from "react";
import "../pages/Login.css";
import "./VerificacionCodigo.css";
import { ApiError, esCancelacion, mensajeDeError } from "../services/apiClient";
import { reenviarCodigo } from "../services/authService";
import type { RespuestaDesafio } from "../types/api";

interface Props {
  /** Desafío recibido del backend (registro, login, recuperación, establecer contraseña o vincular Google). */
  desafio: RespuestaDesafio;
  /** Envía el código. Debe lanzar ApiError si el backend lo rechaza. */
  alVerificar: (desafio: string, codigo: string) => Promise<void>;
  textoBoton: string;
  textoCargando: string;
  /** Texto bajo el título; por defecto "Te enviamos un código de 6 dígitos a …". */
  descripcion?: ReactNode;
  alVolver?: () => void;
  textoVolver?: string;
  idCampo?: string;
}

const CODIGO_VALIDO = /^[0-9]{6}$/;

/**
 * Pantalla del código de 6 dígitos (contrato-auth-perfil.md, 4.1.1, 4.1.2, 4.2.1 y 4.8.2).
 * - Solo dígitos; se envía una vez (sin envíos duplicados mientras espera la respuesta).
 * - "Reenviar código" respeta reenviarEn y Retry-After; usa el desafío nuevo que devuelve el backend.
 * - El token del desafío vive solo en memoria de este componente.
 */
export default function VerificacionCodigo({
  desafio: inicial,
  alVerificar,
  textoBoton,
  textoCargando,
  descripcion,
  alVolver,
  textoVolver = "Volver",
  idCampo = "codigo-verificacion",
}: Props) {
  const [desafio, setDesafio] = useState(inicial.desafio);
  const [expiraEn, setExpiraEn] = useState(inicial.expiraEn);
  const [reenviarDesde, setReenviarDesde] = useState(() => Date.now() + inicial.reenviarEn * 1000);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);
  const [ahora, setAhora] = useState(() => Date.now());
  const [codigo, setCodigo] = useState("");
  const [error, setError] = useState("");
  const [aviso, setAviso] = useState("");
  const [enviando, setEnviando] = useState(false);
  const [reenviando, setReenviando] = useState(false);
  const enCurso = useRef(false);

  // Reloj de la pantalla: cuenta regresiva del reenvío y del vencimiento.
  useEffect(() => {
    const intervalo = window.setInterval(() => setAhora(Date.now()), 1000);
    return () => window.clearInterval(intervalo);
  }, []);

  const segundosReenvio = Math.max(0, Math.ceil((reenviarDesde - ahora) / 1000));
  const bloqueado = bloqueadoHasta !== null && bloqueadoHasta > ahora;
  const vencido = new Date(expiraEn).getTime() <= ahora;

  const verificar = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (enCurso.current) return;
    setError("");
    setAviso("");

    const limpio = codigo.trim();
    if (!CODIGO_VALIDO.test(limpio)) {
      setError("Escribe los 6 dígitos del código.");
      return;
    }

    enCurso.current = true;
    setEnviando(true);
    try {
      await alVerificar(desafio, limpio);
    } catch (err) {
      if (esCancelacion(err)) return;
      setCodigo("");
      if (err instanceof ApiError && err.status === 429) {
        setBloqueadoHasta(Date.now() + (err.retryAfterSegundos ?? 60) * 1000);
        setError(err.message);
      } else if (err instanceof ApiError) {
        setError(err.errorDe("codigo") ?? err.errorDe("desafio") ?? err.message);
      } else {
        setError(mensajeDeError(err, "No se pudo verificar el código."));
      }
    } finally {
      enCurso.current = false;
      setEnviando(false);
    }
  };

  const reenviar = async () => {
    if (reenviando || segundosReenvio > 0) return;
    setError("");
    setAviso("");
    setReenviando(true);
    try {
      const nuevo = await reenviarCodigo(desafio);
      setDesafio(nuevo.desafio);
      setExpiraEn(nuevo.expiraEn);
      setReenviarDesde(Date.now() + nuevo.reenviarEn * 1000);
      setCodigo("");
      setAviso("Te enviamos un código nuevo. El anterior ya no sirve.");
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        setReenviarDesde(Date.now() + (err.retryAfterSegundos ?? 60) * 1000);
        setError(err.message);
      } else if (err instanceof ApiError) {
        setError(err.errorDe("desafio") ?? err.message);
      } else {
        setError(mensajeDeError(err, "No se pudo reenviar el código."));
      }
    } finally {
      setReenviando(false);
    }
  };

  return (
    <form className="codigo-verificacion" onSubmit={verificar} noValidate>
      <p className="login-subtitle">
        {descripcion ?? (
          <>
            Te enviamos un código de 6 dígitos a <strong>{inicial.destino}</strong>. Revisa tu bandeja de entrada.
          </>
        )}
      </p>

      <div className="login-field">
        <label htmlFor={idCampo}>Código de verificación</label>
        <div className="login-input-wrapper">
          <i className="bi bi-shield-lock"></i>
          <input
            id={idCampo}
            className="codigo-verificacion-input"
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]{6}"
            maxLength={6}
            placeholder="000000"
            value={codigo}
            onChange={(e) => setCodigo(e.target.value.replace(/\D/g, "").slice(0, 6))}
            aria-describedby={`${idCampo}-ayuda`}
            aria-invalid={error ? true : undefined}
            autoFocus
          />
        </div>
        <small id={`${idCampo}-ayuda`} className="codigo-verificacion-ayuda">
          {vencido ? "El código venció. Pide uno nuevo." : "Sirve una sola vez y vence en unos minutos."}
        </small>
      </div>

      {aviso && !error && (
        <div className="alert alert-info py-2" role="status">
          {aviso}
        </div>
      )}

      {error && (
        <div className="login-error" role="alert">
          {error}
        </div>
      )}

      <button type="submit" className="login-submit" disabled={enviando || bloqueado}>
        {enviando ? textoCargando : textoBoton}
        {!enviando && <i className="bi bi-chevron-right"></i>}
      </button>

      <div className="codigo-verificacion-acciones">
        <button type="button" className="codigo-verificacion-enlace" onClick={reenviar} disabled={reenviando || segundosReenvio > 0}>
          {segundosReenvio > 0 ? `Reenviar código en ${segundosReenvio} s` : reenviando ? "Reenviando..." : "Reenviar código"}
        </button>
        {alVolver && (
          <button type="button" className="codigo-verificacion-enlace" onClick={alVolver}>
            {textoVolver}
          </button>
        )}
      </div>
    </form>
  );
}
