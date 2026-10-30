import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import "./Register.css";
import { registrarUsuario } from "../services/authService";
import { ApiError, mensajeDeError } from "../services/apiClient";
import EnlaceVolver from "../components/EnlaceVolver";

export default function Register() {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [acceptTerms, setAcceptTerms] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [success, setSuccess] = useState("");
  const [erroresCampo, setErroresCampo] = useState<Record<string, string>>({});
  const navigate = useNavigate();

const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
  e.preventDefault();

  if (loading) return;

  setError("");
  setSuccess("");
  setErroresCampo({});

  const nombreLimpio = name.trim();
  const correoLimpio = email.trim();

  if (!nombreLimpio || !correoLimpio || !password || !confirmPassword) {
    setError("Por favor, completa todos los campos.");
    return;
  }

  if (
    nombreLimpio.length > 100 ||
    !/^[\p{L}\p{M}][\p{L}\p{M} .'-]*$/u.test(nombreLimpio)
  ) {
    setError(
      "El nombre solo puede contener letras, espacios, puntos, apóstrofos y guiones. Máximo 100 caracteres."
    );
    return;
  }

  if (
    correoLimpio.length > 150 ||
    !/^[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}$/.test(correoLimpio)
  ) {
    setError(
      "Ingresa un correo electrónico válido de máximo 150 caracteres."
    );
    return;
  }

  if (
    password.length < 8 ||
    password.length > 128 ||
    !/^(?=.*\p{L})(?=.*\d).+$/u.test(password)
  ) {
    setError(
      "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número."
    );
    return;
  }

  if (password !== confirmPassword) {
    setError("Las contraseñas no coinciden.");
    return;
  }

  if (!acceptTerms) {
    setError("Debes aceptar los términos y condiciones.");
    return;
  }

  setLoading(true);

  try {
    await registrarUsuario({
      nombre: nombreLimpio,
      correo: correoLimpio,
      password,
    });

    setSuccess(
      "¡Cuenta creada correctamente! Ya puedes iniciar sesión."
    );

    setPassword("");
    setConfirmPassword("");

    // M03 → M02 con el mensaje "Cuenta creada, inicia sesión" (contrato-auth-perfil.md).
    navigate("/login", { state: { mensaje: "Cuenta creada, inicia sesión." } });

  } catch (error) {
    if (error instanceof ApiError && Object.keys(error.errores).length > 0) {
      const porCampo: Record<string, string> = {};
      for (const [campo, mensajes] of Object.entries(error.errores)) {
        porCampo[campo] = mensajes[0];
      }
      setErroresCampo(porCampo);
      setError(error.message);
    } else {
      setError(mensajeDeError(error, "No se pudo completar el registro."));
    }
  } finally {
    setLoading(false);
  }
};

  return (
    <main className="register-page">
      <section className="register-left">
        <Link to="/" className="register-brand">
          <span className="register-brand-icon">
            <i className="bi bi-cloud"></i>
          </span>
          <span>
            Clima<span className="register-brand-plus">Plus</span>
          </span>
        </Link>

        <div className="register-left-content">
          <div className="register-weather-icon">
            <i className="bi bi-cloud-sun"></i>
          </div>

          <h1>El clima a tu alcance.</h1>

          <p>
            Crea tu cuenta y comienza a consultar el clima,
            guardar tus ciudades favoritas y recibir alertas
            personalizadas.
          </p>
        </div>

        <footer className="register-footer">
          <span>Privacidad</span>
          <span>·</span>
          <span>Términos</span>
          <span>·</span>
          <span>Ayuda</span>
        </footer>
      </section>

      <section className="register-right">
        <div className="register-top-link">
          ¿Ya tienes una cuenta?{" "}
          <Link to="/login">Inicia sesión</Link>
        </div>

        <div className="register-form-container">
          <EnlaceVolver to="/">Volver al Inicio</EnlaceVolver>

          <h2>Crea tu cuenta</h2>

          <p className="register-subtitle">
            Regístrate para disfrutar de todas las funciones de ClimaPlus.
          </p>

          <form onSubmit={handleSubmit} noValidate>
            <div className="register-field">
              <label htmlFor="register-name">Nombre completo</label>

              <div className="register-input-wrapper">
                <i className="bi bi-person"></i>

                <input
                  id="register-name"
                  type="text"
                  placeholder="Ingresa tu nombre completo"
                  autoComplete="name"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                />
              </div>
              {erroresCampo.nombre && (
                <small className="text-danger d-block mt-1">{erroresCampo.nombre}</small>
              )}
            </div>

            <div className="register-field">
              <label htmlFor="register-email">Correo electrónico</label>

              <div className="register-input-wrapper">
                <i className="bi bi-envelope"></i>

                <input
                  id="register-email"
                  type="email"
                  placeholder="nombre@correo.com"
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>
              {erroresCampo.correo && (
                <small className="text-danger d-block mt-1">{erroresCampo.correo}</small>
              )}
            </div>

            <div className="register-field">
              <label htmlFor="register-password">Contraseña</label>

              <div className="register-input-wrapper">
                <i className="bi bi-lock"></i>

                <input
                  id="register-password"
                  type={showPassword ? "text" : "password"}
                  placeholder="Mínimo 8 caracteres"
                  autoComplete="new-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />

                <button
                  type="button"
                  className="register-toggle-password"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={
                    showPassword
                      ? "Ocultar contraseña"
                      : "Mostrar contraseña"
                  }
                >
                  <i
                    className={
                      showPassword ? "bi bi-eye-slash" : "bi bi-eye"
                    }
                  ></i>
                </button>
              </div>
              {erroresCampo.password && (
                <small className="text-danger d-block mt-1">{erroresCampo.password}</small>
              )}
            </div>

            <div className="register-field">
              <label htmlFor="register-confirm">
                Confirmar contraseña
              </label>

              <div className="register-input-wrapper">
                <i className="bi bi-shield-lock"></i>

                <input
                  id="register-confirm"
                  type={showPassword ? "text" : "password"}
                  placeholder="Repite tu contraseña"
                  autoComplete="new-password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                />
              </div>
            </div>

            <label className="register-terms-checkbox">
              <input
                type="checkbox"
                checked={acceptTerms}
                onChange={(e) => setAcceptTerms(e.target.checked)}
              />

              <span>
                Acepto los términos y condiciones y la política de privacidad.
              </span>
            </label>

{error && (
  <div className="register-error" role="alert">
    {error}
  </div>
)}

{success && (
  <div className="alert alert-success" role="status">
    {success}
    <div className="mt-2">
      <Link to="/login" className="alert-link">
        Ir a iniciar sesión
      </Link>
    </div>
  </div>
)}

            <button
            type="submit"
            className="register-submit"
            disabled={loading}
>
                {loading ? "Creando cuenta..." : "Crear cuenta"}

                {!loading && <i className="bi bi-chevron-right"></i>}
            </button>
          </form>

          <p className="register-bottom-text">
            ¿Ya tienes una cuenta?{" "}
            <Link to="/login">Iniciar sesión</Link>
          </p>
        </div>
      </section>
    </main>
  );
}