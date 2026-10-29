import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import "./Login.css";
import EnlaceVolver from "../components/EnlaceVolver";

export default function RecoverPassword() {
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  const handleSubmit = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError("");
    setMensaje("");

    const correo = email.trim();

    if (!correo) {
      setError("Por favor, ingresa tu correo electrónico.");
      return;
    }

    if (
      correo.length > 150 ||
      !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo)
    ) {
      setError("Ingresa un correo electrónico válido.");
      return;
    }

    setMensaje(
      "El formulario se validó correctamente. El envío del correo estará disponible cuando se integre el backend."
    );
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

          <p className="login-subtitle">
            Ingresa tu correo electrónico para solicitar
            la recuperación de tu cuenta.
          </p>

          <form onSubmit={handleSubmit} noValidate>
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

            {mensaje && (
              <div className="alert alert-info mt-3" role="status">
                {mensaje}
              </div>
            )}

            <button type="submit" className="login-submit">
              Validar correo
              <i className="bi bi-chevron-right"></i>
            </button>
          </form>

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