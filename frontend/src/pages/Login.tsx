import { useCallback, useEffect, useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import "./Login.css";
import { useAuth } from "../context/auth";
import { ApiError, mensajeDeError } from "../services/apiClient";
import EnlaceVolver from "../components/EnlaceVolver";
import VerificacionCodigo from "../components/VerificacionCodigo";
import BotonGoogle from "../components/BotonGoogle";
import type { RespuestaDesafio, Usuario } from "../types/api";

// M02 – Inicio de sesión en dos pasos (contrato-auth-perfil.md 4.2 y 4.2.1) y "Continuar con Google" (4.9).
// La sesión solo se abre al validar el código (o el ID token de Google en la API): el paso 1 nunca da un token.
export default function Login() {
  const { estado, usuario, iniciarSesion, completarInicioSesion, iniciarSesionConGoogle, mensajeSesion, limpiarMensajeSesion } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const estadoNavegacion = location.state as { desde?: string; mensaje?: string } | null;

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [bloqueadoHasta, setBloqueadoHasta] = useState<number | null>(null);
  const [desafio, setDesafio] = useState<RespuestaDesafio | null>(null);

  // "Cuenta creada, inicia sesión" (M03) o "Tu sesión expiró" (401 / expiraEn).
  const aviso = estadoNavegacion?.mensaje ?? mensajeSesion;

  // 429: el botón se deshabilita los segundos de Retry-After (contrato-auth-perfil.md).
  useEffect(() => {
    if (bloqueadoHasta === null) return;
    const restante = bloqueadoHasta - Date.now();
    const temporizador = window.setTimeout(() => setBloqueadoHasta(null), Math.max(0, restante));
    return () => window.clearTimeout(temporizador);
  }, [bloqueadoHasta]);

  /** USUARIO → M06, ADMINISTRADOR → M13, o la ruta privada que se pidió antes del login. */
  const irAlDestino = useCallback((u: Usuario) => {
    limpiarMensajeSesion();
    navigate(estadoNavegacion?.desde ?? (u.rol === "ADMINISTRADOR" ? "/admin" : "/dashboard"), { replace: true });
  }, [estadoNavegacion?.desde, limpiarMensajeSesion, navigate]);

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (loading) return;
    setError("");

    if (!email.trim() || !password) {
      setError("Por favor, completa todos los campos.");
      return;
    }

    if (
      email.trim().length > 150 ||
      !/^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$/.test(email.trim())
    ) {
      setError("Ingresa un correo electrónico válido.");
      return;
    }

    if (password.length > 128) {
      setError("La contraseña no puede superar los 128 caracteres.");
      return;
    }

    setLoading(true);
    try {
      // Paso 1: la API comprueba la contraseña y envía el código. Todavía no hay sesión.
      const respuesta = await iniciarSesion(email.trim(), password);
      setPassword("");
      setDesafio(respuesta);
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setError("Correo o contraseña incorrectos.");
      } else if (error instanceof ApiError && error.status === 403) {
        setError("Tu cuenta está desactivada. Contacta al administrador.");
      } else if (error instanceof ApiError && error.status === 429) {
        setError(error.message);
        setBloqueadoHasta(Date.now() + (error.retryAfterSegundos ?? 60) * 1000);
      } else {
        setError(mensajeDeError(error, "No se pudo iniciar sesión."));
      }
    } finally {
      setLoading(false);
    }
  };

  // Paso 2: el código del correo abre la sesión. Los errores los muestra VerificacionCodigo.
  const verificarCodigo = async (token: string, codigo: string) => {
    try {
      const u = await completarInicioSesion(token, codigo, remember);
      irAlDestino(u);
    } catch (error) {
      if (error instanceof ApiError && error.status === 403) {
        throw new ApiError(403, "Tu cuenta está desactivada. Contacta al administrador.");
      }
      throw error;
    }
  };

  const entrarConGoogle = useCallback(async (credencial: string) => {
    setError("");
    try {
      const u = await iniciarSesionConGoogle(credencial, remember);
      irAlDestino(u);
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setError("No se pudo validar el inicio con Google. Vuelve a intentarlo.");
      } else if (error instanceof ApiError && error.status === 403) {
        setError("Tu cuenta está desactivada. Contacta al administrador.");
      } else if (error instanceof ApiError && (error.status === 409 || error.status === 400 || error.status === 429)) {
        setError(error.message);
      } else {
        setError(mensajeDeError(error, "No se pudo iniciar sesión con Google."));
      }
    }
  }, [iniciarSesionConGoogle, irAlDestino, remember]);

  const volverAlPaso1 = () => {
    setDesafio(null);
    setError("");
  };

  // Con la sesión ya iniciada no se muestra el formulario: para entrar con otra cuenta,
  // primero se cierra la sesión actual.
  if (estado === "autenticado" && usuario) {
    return (
      <Navigate
        to={estadoNavegacion?.desde ?? (usuario.rol === "ADMINISTRADOR" ? "/admin" : "/dashboard")}
        replace
      />
    );
  }

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
            <i className="bi bi-sun"></i>
          </div>

          <h1>Tu clima, siempre cerca.</h1>

          <p>
            Accede a tus ciudades favoritas y revisa tus últimas
            consultas en segundos.
          </p>
        </div>

        <footer className="login-footer">
          <span>Privacidad</span>
          <span>·</span>
          <span>Términos</span>
          <span>·</span>
          <span>Ayuda</span>
        </footer>
      </section>

      <section className="login-right">
        <div className="login-top-link">
          ¿Aún no tienes cuenta?{" "}
          <Link to="/registro">Regístrate</Link>
        </div>

        <div className="login-form-container">
          <EnlaceVolver to="/">Volver al Inicio</EnlaceVolver>

          {desafio ? (
            <>
              <h2>{desafio.proposito === "VERIFICAR_CORREO" ? "Verifica tu correo" : "Verifica que eres tú"}</h2>
              <VerificacionCodigo
                desafio={desafio}
                alVerificar={verificarCodigo}
                textoBoton="Verificar e iniciar sesión"
                textoCargando="Verificando..."
                alVolver={volverAlPaso1}
                textoVolver="Usar otra cuenta"
                idCampo="login-codigo"
                descripcion={
                  desafio.proposito === "VERIFICAR_CORREO" ? (
                    <>
                      Antes de entrar necesitamos confirmar tu correo. Te enviamos un código de 6 dígitos a{" "}
                      <strong>{desafio.destino}</strong>.
                    </>
                  ) : undefined
                }
              />
            </>
          ) : (
          <>
          <h2>Bienvenido de nuevo</h2>
          <p className="login-subtitle">
            Ingresa tus datos para continuar.
          </p>

          <form onSubmit={handleSubmit} noValidate>
            <div className="login-field">
              <label htmlFor="login-email">
                Correo electrónico
              </label>

              <div className="login-input-wrapper">
                <i className="bi bi-person"></i>

                <input
                  id="login-email"
                  type="email"
                  placeholder="nombre@correo.com"
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>
            </div>

            <div className="login-field">
              <label htmlFor="login-password">
                Contraseña
              </label>

              <div className="login-input-wrapper">
                <i className="bi bi-lock"></i>

                <input
                  id="login-password"
                  type={showPassword ? "text" : "password"}
                  placeholder="Ingresa tu contraseña"
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />

                <button
                  type="button"
                  className="login-toggle-password"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={
                    showPassword
                      ? "Ocultar contraseña"
                      : "Mostrar contraseña"
                  }
                >
                  <i
                    className={
                      showPassword
                        ? "bi bi-eye-slash"
                        : "bi bi-eye"
                    }
                  ></i>
                </button>
              </div>
            </div>

            <div className="login-options">
              <label className="login-remember">
                <input
                  type="checkbox"
                  checked={remember}
                  onChange={(e) => setRemember(e.target.checked)}
                />
                Recordarme
              </label>

              <Link to="/recuperar-contrasena">
                ¿Olvidaste tu contraseña?
              </Link>
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

<button
  type="submit"
  className="login-submit"
  disabled={loading || bloqueadoHasta !== null}
>
  {loading ? "Iniciando sesión..." : "Iniciar sesión"}

  {!loading && <i className="bi bi-chevron-right"></i>}
</button>
          </form>

          <div className="acceso-separador">o</div>
          <BotonGoogle uso="login" alRecibirCredencial={entrarConGoogle} />
          </>
          )}

          <p className="login-terms">
            Al continuar confirmas que eres mayor de edad y aceptas
            nuestras condiciones de uso.
          </p>
        </div>
      </section>
    </main>
  );
}
