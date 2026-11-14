import { useState, type FormEvent, type ReactNode } from "react";
import "./VerificacionCodigo.css";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { ApiError, mensajeDeError } from "../services/apiClient";
import {
  desvincularGoogle,
  establecerPassword,
  restablecerPassword,
  verificarRecuperacion,
  verificarVinculacionGoogle,
  vincularGoogle,
} from "../services/authService";
import type { RespuestaDesafio, Usuario } from "../types/api";
import BotonGoogle from "./BotonGoogle";
import VerificacionCodigo from "./VerificacionCodigo";

const POLITICA_PASSWORD = /^(?=.*\p{L})(?=.*\d).{8,128}$/u;

type Modal = "establecer" | "vincular" | "desvincular" | null;

interface Props {
  perfil: Usuario;
  /** Abre el modal existente de "Cambiar contraseña" (cuentas con contraseña local). */
  alCambiarPassword: () => void;
  /** El backend devolvió el usuario actualizado (con tieneGoogle / tienePassword nuevos). */
  alActualizarPerfil: (usuario: Usuario) => void;
}

/**
 * Seguridad del perfil (contrato-auth-perfil.md 4.6, 4.7 y 4.9): método de acceso, cambiar o establecer contraseña,
 * vincular y desvincular Google. Vincular y desvincular devuelven un JWT nuevo (las demás sesiones se cierran) que
 * reemplaza al actual; establecer contraseña cierra la sesión (el backend renueva el sello).
 */
export default function MetodosAcceso({ perfil, alCambiarPassword, alActualizarPerfil }: Props) {
  const { reemplazarSesion, cerrarSesionConMensaje } = useAuth();
  const { avisar } = useNotificaciones();
  const [modal, setModal] = useState<Modal>(null);

  const [passwordActual, setPasswordActual] = useState("");
  const [desafio, setDesafio] = useState<RespuestaDesafio | null>(null);
  const [permiso, setPermiso] = useState<string | null>(null);
  const [passwordNueva, setPasswordNueva] = useState("");
  const [confirmacion, setConfirmacion] = useState("");
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);

  const abrir = (cual: Modal) => {
    setModal(cual);
    setPasswordActual("");
    setDesafio(null);
    setPermiso(null);
    setPasswordNueva("");
    setConfirmacion("");
    setError("");
  };
  const cerrar = () => abrir(null);

  const errorDe = (err: unknown, porDefecto: string) =>
    err instanceof ApiError
      ? err.errorDe("passwordActual") ?? err.errorDe("credencial") ?? err.errorDe("passwordNueva") ?? err.errorDe("confirmacion") ?? err.message
      : mensajeDeError(err, porDefecto);

  // ── Establecer contraseña (cuentas creadas con Google) ──────────────────────

  const pedirCodigoEstablecer = async () => {
    if (cargando) return;
    setError("");
    setCargando(true);
    try {
      setDesafio(await establecerPassword());
    } catch (err) {
      setError(errorDe(err, "No se pudo enviar el código."));
    } finally {
      setCargando(false);
    }
  };

  const guardarPasswordNueva = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (cargando || !permiso) return;
    setError("");
    if (!POLITICA_PASSWORD.test(passwordNueva)) {
      setError("La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número.");
      return;
    }
    if (passwordNueva !== confirmacion) {
      setError("Las contraseñas no coinciden.");
      return;
    }

    setCargando(true);
    try {
      await restablecerPassword({ permiso, passwordNueva, confirmacion });
      cerrar();
      // El backend renovó el sello: todas las sesiones (también esta) quedaron cerradas.
      cerrarSesionConMensaje("Contraseña creada. Por seguridad, inicia sesión de nuevo.");
    } catch (err) {
      if (err instanceof ApiError && err.errorDe("permiso")) {
        setPermiso(null);
        setDesafio(null);
      }
      setError(errorDe(err, "No se pudo guardar la contraseña."));
    } finally {
      setCargando(false);
    }
  };

  // ── Vincular Google ─────────────────────────────────────────────────────────

  const vincularConCredencial = async (credencial: string) => {
    setError("");
    if (!passwordActual) {
      setError("Escribe tu contraseña actual antes de elegir tu cuenta de Google.");
      return;
    }
    try {
      setDesafio(await vincularGoogle(credencial, passwordActual));
      setPasswordActual("");
    } catch (err) {
      setError(errorDe(err, "No se pudo vincular tu cuenta de Google."));
    }
  };

  const confirmarVinculacion = async (token: string, codigo: string) => {
    const respuesta = await verificarVinculacionGoogle({ desafio: token, codigo });
    reemplazarSesion(respuesta);
    alActualizarPerfil(respuesta.usuario);
    cerrar();
    avisar("Cuenta de Google vinculada. Se cerraron tus otras sesiones.");
  };

  // ── Desvincular Google ──────────────────────────────────────────────────────

  const desvincular = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (cargando) return;
    setError("");
    if (!passwordActual) {
      setError("Escribe tu contraseña actual.");
      return;
    }
    setCargando(true);
    try {
      const respuesta = await desvincularGoogle(passwordActual);
      reemplazarSesion(respuesta);
      alActualizarPerfil(respuesta.usuario);
      cerrar();
      avisar("Cuenta de Google desvinculada. Se cerraron tus otras sesiones.");
    } catch (err) {
      setError(errorDe(err, "No se pudo desvincular tu cuenta de Google."));
    } finally {
      setCargando(false);
    }
  };

  const campoPasswordActual = (id: string) => (
    <label htmlFor={id}>
      Contraseña actual
      <input
        id={id}
        type="password"
        value={passwordActual}
        onChange={(e) => setPasswordActual(e.target.value)}
        autoComplete="current-password"
        required
      />
    </label>
  );

  const mensajeError = error && (
    <p className="user-profile-message" role="alert">
      {error}
    </p>
  );

  return (
    <>
      <div className="user-profile-security-card">
        <div className="user-profile-security-icon">
          <i className="bi bi-lock"></i>
        </div>
        <div className="user-profile-security-info">
          <strong>Contraseña</strong>
          <span>{perfil.tienePassword ? "••••••••••••" : "Sin contraseña (entras con Google)"}</span>
          <p>
            {perfil.correoVerificado ? "Correo verificado." : "Correo sin verificar."}{" "}
            {perfil.tienePassword
              ? "Protege tu cuenta utilizando una contraseña segura."
              : "Crea una contraseña para poder entrar también con tu correo."}
          </p>
        </div>
        {perfil.tienePassword ? (
          <button type="button" onClick={alCambiarPassword}>
            Cambiar contraseña
          </button>
        ) : (
          <button type="button" onClick={() => abrir("establecer")}>
            Establecer contraseña
          </button>
        )}
      </div>

      <div className="user-profile-security-card mt-3">
        <div className="user-profile-security-icon">
          <i className="bi bi-google"></i>
        </div>
        <div className="user-profile-security-info">
          <strong>Google</strong>
          <span>{perfil.tieneGoogle ? "Vinculada" : "No vinculada"}</span>
          <p>
            {perfil.tieneGoogle
              ? perfil.tienePassword
                ? "Puedes entrar con \"Continuar con Google\" o con tu contraseña."
                : "Tu cuenta solo entra con Google. Para desvincularla, primero establece una contraseña."
              : "Vincula tu cuenta de Google para entrar con \"Continuar con Google\"."}
          </p>
        </div>
        {perfil.tieneGoogle ? (
          <button type="button" onClick={() => abrir("desvincular")} disabled={!perfil.tienePassword}>
            Desvincular
          </button>
        ) : (
          <button type="button" onClick={() => abrir("vincular")} disabled={!perfil.tienePassword}>
            Vincular Google
          </button>
        )}
      </div>

      {modal && (
        <div
          className="user-profile-modal-overlay"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) cerrar();
          }}
        >
          <div className="user-profile-modal" role="dialog" aria-modal="true" aria-labelledby="metodos-acceso-titulo">
            <div className="user-profile-modal-header">
              <h2 id="metodos-acceso-titulo">
                {modal === "establecer" ? "Establecer contraseña" : modal === "vincular" ? "Vincular Google" : "Desvincular Google"}
              </h2>
              <button type="button" onClick={cerrar} aria-label="Cerrar ventana">
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            {modal === "establecer" && (
              <PasoEstablecer
                desafio={desafio}
                permiso={permiso}
                cargando={cargando}
                pedirCodigo={pedirCodigoEstablecer}
                verificar={async (token, codigo) => {
                  setPermiso((await verificarRecuperacion({ desafio: token, codigo })).permiso);
                  setError("");
                }}
                formulario={
                  <form onSubmit={guardarPasswordNueva}>
                    <label htmlFor="establecer-nueva">
                      Nueva contraseña
                      <input
                        id="establecer-nueva"
                        type="password"
                        value={passwordNueva}
                        onChange={(e) => setPasswordNueva(e.target.value)}
                        autoComplete="new-password"
                        required
                      />
                    </label>
                    <label htmlFor="establecer-confirmacion">
                      Confirmar nueva contraseña
                      <input
                        id="establecer-confirmacion"
                        type="password"
                        value={confirmacion}
                        onChange={(e) => setConfirmacion(e.target.value)}
                        autoComplete="new-password"
                        required
                      />
                    </label>
                    {mensajeError}
                    <div className="user-profile-modal-actions">
                      <button type="submit" disabled={cargando}>
                        {cargando ? "Guardando..." : "Guardar contraseña"}
                      </button>
                      <button type="button" className="secondary" onClick={cerrar}>
                        Cancelar
                      </button>
                    </div>
                  </form>
                }
                error={mensajeError}
              />
            )}

            {modal === "vincular" &&
              (desafio ? (
                <VerificacionCodigo
                  desafio={desafio}
                  alVerificar={confirmarVinculacion}
                  textoBoton="Confirmar vinculación"
                  textoCargando="Verificando..."
                  idCampo="vincular-codigo"
                  descripcion={
                    <>
                      Para confirmar que eres tú, te enviamos un código de 6 dígitos a <strong>{desafio.destino}</strong>.
                    </>
                  }
                />
              ) : (
                <div>
                  <p>Escribe tu contraseña actual y elige la cuenta de Google que quieres vincular.</p>
                  <form onSubmit={(e) => e.preventDefault()}>{campoPasswordActual("vincular-password")}</form>
                  {mensajeError}
                  <BotonGoogle uso="vincular" texto="signin_with" alRecibirCredencial={vincularConCredencial} />
                </div>
              ))}

            {modal === "desvincular" && (
              <form onSubmit={desvincular}>
                <p>
                  Ya no podrás entrar con "Continuar con Google". Seguirás entrando con tu correo, tu contraseña y el
                  código por correo.
                </p>
                {campoPasswordActual("desvincular-password")}
                {mensajeError}
                <div className="user-profile-modal-actions">
                  <button type="submit" disabled={cargando}>
                    {cargando ? "Desvinculando..." : "Desvincular Google"}
                  </button>
                  <button type="button" className="secondary" onClick={cerrar}>
                    Cancelar
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </>
  );
}

interface PasoEstablecerProps {
  desafio: RespuestaDesafio | null;
  permiso: string | null;
  cargando: boolean;
  pedirCodigo: () => void;
  verificar: (desafio: string, codigo: string) => Promise<void>;
  formulario: ReactNode;
  error: ReactNode;
}

/** Establecer contraseña: 1) código al correo, 2) validar el código (permiso), 3) contraseña nueva. */
function PasoEstablecer({ desafio, permiso, cargando, pedirCodigo, verificar, formulario, error }: PasoEstablecerProps) {
  if (permiso) {
    return <>{formulario}</>;
  }

  if (desafio) {
    return (
      <VerificacionCodigo
        desafio={desafio}
        alVerificar={verificar}
        textoBoton="Continuar"
        textoCargando="Verificando..."
        idCampo="establecer-codigo"
      />
    );
  }

  return (
    <div>
      <p>
        Por seguridad, primero te enviaremos un código a tu correo. Una sesión abierta no basta para crear la
        contraseña.
      </p>
      {error}
      <div className="user-profile-modal-actions">
        <button type="button" onClick={pedirCodigo} disabled={cargando}>
          {cargando ? "Enviando..." : "Enviarme el código"}
        </button>
      </div>
    </div>
  );
}
