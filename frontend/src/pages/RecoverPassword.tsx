import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import "./Login.css";
import EnlaceVolver from "../components/EnlaceVolver";
import VerificacionCodigo from "../components/VerificacionCodigo";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { restablecerPassword, solicitarRecuperacion, verificarRecuperacion } from "../services/authService";
import type { RespuestaDesafio } from "../types/api";

const POLITICA_PASSWORD = /^(?=.*\p{L})(?=.*\d).{8,128}$/u;

type Paso = "correo" | "codigo" | "nueva";

// Recuperar contraseña (contrato-auth-perfil.md 4.8): correo → código → contraseña nueva. No abre sesión: al terminar,
// el usuario inicia sesión en dos pasos con la contraseña nueva. El permiso de restablecer vive solo en memoria.
export default function RecoverPassword() {
  const navigate = useNavigate();
  const [paso, setPaso] = useState<Paso>("correo");
  const [email, setEmail] = useState("");
  const [desafio, setDesafio] = useState<RespuestaDesafio | null>(null);
  const [permiso, setPermiso] = useState<string | null>(null);
  const [passwordNueva, setPasswordNueva] = useState("");
  const [confirmacion, setConfirmacion] = useState("");
  const [mostrar, setMostrar] = useState(false);
  const [error, setError] = useState("");
  const [cargando, setCargando] = useState(false);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);

  const reiniciar = (mensaje = "") => {
    setPaso("correo");
    setDesafio(null);
    setPermiso(null);
    setPasswordNueva("");
    setConfirmacion("");
    setError(mensaje);
  };

  const solicitar = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (cargando) return;
    setError("");

    const correo = email.trim();
    if (!correo) {
      setError("Por favor, ingresa tu correo electrónico.");
      return;
    }

    if (correo.length > 150 || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo)) {
      setError("Ingresa un correo electrónico válido.");
      return;
    }

    setCargando(true);
    try {
      // Siempre 202 con la misma forma, exista o no la cuenta.
      setDesafio(await solicitarRecuperacion(correo));
      setPaso("codigo");
    } catch (err) {
      if (err instanceof ApiError && err.status === 429) {
        setBloqueadoHasta(Date.now() + (err.retryAfterSegundos ?? 60) * 1000);
        window.setTimeout(() => setBloqueadoHasta(null), (err.retryAfterSegundos ?? 60) * 1000);
      }
      setError(err instanceof ApiError ? err.errorDe("correo") ?? err.message : mensajeDeError(err, "No se pudo solicitar el código."));
    } finally {
      setCargando(false);
    }
  };

  const verificar = async (token: string, codigo: string) => {
    const respuesta = await verificarRecuperacion({ desafio: token, codigo });
    setPermiso(respuesta.permiso);
    setPaso("nueva");
    setError("");
  };

  const restablecer = async (e: FormEvent<HTMLFormElement>) => {
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
      setPermiso(null);
      setPasswordNueva("");
      setConfirmacion("");
      navigate("/login", {
        replace: true,
        state: { mensaje: "Contraseña actualizada. Inicia sesión con tu nueva contraseña." },
      });
    } catch (err) {
      if (err instanceof ApiError && err.errorDe("permiso")) {
        // Permiso usado, vencido o anulado: hay que empezar de nuevo.
        reiniciar(err.errorDe("permiso"));
      } else if (err instanceof ApiError) {
        setError(err.errorDe("passwordNueva") ?? err.errorDe("confirmacion") ?? err.message);
      } else {
        setError(mensajeDeError(err, "No se pudo cambiar la contraseña."));
      }
    } finally {
      setCargando(false);
    }
  };

  return (
    <main className="login-page">
      <section className="login-left">
        <Link to="/" className="login-brand">
          <span className="login-brand-icon">
            <i className="bi bi-cloud"></i>
          </span>
          <span>
            Clima<span className="login-brand-plus">Plus</span>
          </span>
        </Link>

        <div className="login-left-content">
          <div className="login-weather-icon">
            <i className="bi bi-shield-lock"></i>
          </div>

          <h1>Recupera tu acceso.</h1>

          <p>
            Mantén tu información meteorológica siempre a tu alcance.
          </p>
        </div>
      </section>

      <section className="login-right">
        <div className="login-form-container">
          <EnlaceVolver to="/">Volver al Inicio</EnlaceVolver>

          <h2>Recuperar contraseña</h2>

          {paso === "correo" && (
            <>
              <p className="login-subtitle">
                Ingresa tu correo electrónico. Si tiene una cuenta en ClimaPlus, te enviaremos un código para crear una
                contraseña nueva.
              </p>

              <form onSubmit={solicitar} noValidate>
                <div className="login-field">
                  <label htmlFor="recover-email">
                    Correo electrónico
                  </label>

                  <div className="login-input-wrapper">
                    <i className="bi bi-envelope"></i>

                    <input
                      id="recover-email"
                      type="email"
                      placeholder="nombre@correo.com"
                      autoComplete="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                    />
                  </div>
                </div>

                {error && (
                  <div className="login-error" role="alert">
                    {error}
                  </div>
                )}

                <button type="submit" className="login-submit" disabled={cargando || bloqueadoHasta !== null}>
                  {cargando ? "Enviando..." : "Enviar código"}
                  {!cargando && <i className="bi bi-chevron-right"></i>}
                </button>
              </form>
            </>
          )}

          {paso === "codigo" && desafio && (
            <VerificacionCodigo
              desafio={desafio}
              alVerificar={verificar}
              textoBoton="Continuar"
              textoCargando="Verificando..."
              alVolver={() => reiniciar()}
              textoVolver="Usar otro correo"
              idCampo="recover-codigo"
              descripcion={
                <>
                  Si <strong>{desafio.destino}</strong> tiene una cuenta, te enviamos un código de 6 dígitos. Revisa tu
                  bandeja de entrada.
                </>
              }
            />
          )}

          {paso === "nueva" && (
            <>
              <p className="login-subtitle">
                Código correcto. Escribe tu contraseña nueva. Al guardarla se cerrarán todas tus sesiones abiertas.
              </p>

              <form onSubmit={restablecer} noValidate>
                <div className="login-field">
                  <label htmlFor="recover-nueva">Nueva contraseña</label>
                  <div className="login-input-wrapper">
                    <i className="bi bi-lock"></i>
                    <input
                      id="recover-nueva"
                      type={mostrar ? "text" : "password"}
                      autoComplete="new-password"
                      value={passwordNueva}
                      onChange={(e) => setPasswordNueva(e.target.value)}
                      aria-describedby="recover-politica"
                    />
                    <button
                      type="button"
                      className="login-toggle-password"
                      onClick={() => setMostrar(!mostrar)}
                      aria-label={mostrar ? "Ocultar contraseña" : "Mostrar contraseña"}
                    >
                      <i className={mostrar ? "bi bi-eye-slash" : "bi bi-eye"}></i>
                    </button>
                  </div>
                  <small id="recover-politica" className="codigo-verificacion-ayuda">
                    Entre 8 y 128 caracteres, con al menos una letra y un número.
                  </small>
                </div>

                <div className="login-field">
                  <label htmlFor="recover-confirmacion">Confirmar nueva contraseña</label>
                  <div className="login-input-wrapper">
                    <i className="bi bi-lock"></i>
                    <input
                      id="recover-confirmacion"
                      type={mostrar ? "text" : "password"}
                      autoComplete="new-password"
                      value={confirmacion}
                      onChange={(e) => setConfirmacion(e.target.value)}
                    />
                  </div>
                </div>

                {error && (
                  <div className="login-error" role="alert">
                    {error}
                  </div>
                )}

                <button type="submit" className="login-submit" disabled={cargando}>
                  {cargando ? "Guardando..." : "Guardar contraseña"}
                  {!cargando && <i className="bi bi-chevron-right"></i>}
                </button>
              </form>
            </>
          )}

          <p className="text-center mt-4">
            <Link to="/login">
              <i className="bi bi-arrow-left me-2"></i>
              Volver al inicio de sesión
            </Link>
          </p>
        </div>
      </section>
    </main>
  );
}
