import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import "./Login.css";
import { iniciarSesion } from "../services/authService";

export default function Login() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
  e.preventDefault();
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
    const respuesta = await iniciarSesion({
      correo: email.trim(),
      password: password,
    });

    console.log("Inicio de sesión correcto:", {
      usuario: respuesta.usuario.nombre,
      rol: respuesta.usuario.rol,
    });

    alert(`Bienvenido a ClimaPlus, ${respuesta.usuario.nombre}`);

  } catch (error) {
    if (error instanceof TypeError) {
      setError(
        "No se pudo conectar con el servidor. Verifica que el backend esté disponible."
      );
    } else {
      setError(
        error instanceof Error
          ? error.message
          : "No se pudo iniciar sesión."
      );
    }
  } finally {
    setLoading(false);
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

            {error && (
              <div className="login-error" role="alert">
                {error}
              </div>
            )}

<button
  type="submit"
  className="login-submit"
  disabled={loading}
>
  {loading ? "Iniciando sesión..." : "Iniciar sesión"}

  {!loading && <i className="bi bi-chevron-right"></i>}
</button>
          </form>

          <p className="login-terms">
            Al continuar confirmas que eres mayor de edad y aceptas
            nuestras condiciones de uso.
          </p>
        </div>
      </section>
    </main>
  );
}