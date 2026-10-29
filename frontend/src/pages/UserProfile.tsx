import { useState, type FormEvent } from "react";
import "./UserProfile.css";
import { useLocation } from "react-router-dom";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { actualizarPerfil, cambiarPassword, obtenerPerfil } from "../services/authService";
import type { Usuario } from "../types/api";
import { fecha } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

const POLITICA_PASSWORD = /^(?=.*\p{L})(?=.*\d).{8,128}$/u;

// M16 / M17 – Perfil: GET y PUT /api/usuarios/me, PUT /api/usuarios/me/password
// (contrato-auth-perfil.md). Rol y estado son de solo lectura.
export default function UserProfile() {
  const location = useLocation();
  const esAdministrador = location.pathname.startsWith("/admin");
  const { usuario, actualizarUsuario } = useAuth();
  const { avisar } = useNotificaciones();

  const consultaPerfil = useConsulta(`perfil:${usuario?.idUsuario}`, (signal) => obtenerPerfil(signal));
  const perfilServidor: Usuario | null = consultaPerfil.datos ?? usuario;

  // null = el campo muestra el valor del servidor; texto = lo que el usuario está editando.
  const [nombreEditado, setNombreEditado] = useState<string | null>(null);
  const [correoEditado, setCorreoEditado] = useState<string | null>(null);
  const nombre = nombreEditado ?? perfilServidor?.nombre ?? "";
  const correo = correoEditado ?? perfilServidor?.correo ?? "";

  const [mensaje, setMensaje] = useState("");
  const [errores, setErrores] = useState<Record<string, string>>({});
  const [guardando, setGuardando] = useState(false);
  const [mostrarCambioPassword, setMostrarCambioPassword] = useState(false);

  const [passwordActual, setPasswordActual] = useState("");
  const [passwordNueva, setPasswordNueva] = useState("");
  const [confirmarPassword, setConfirmarPassword] = useState("");

  const [mensajePassword, setMensajePassword] = useState("");
  const [cambiandoPassword, setCambiandoPassword] = useState(false);

  const perfil = {
    nombre: perfilServidor?.nombre ?? "",
    correo: perfilServidor?.correo ?? "",
    rol: perfilServidor?.rol === "ADMINISTRADOR" ? "Administrador" : "Usuario",
    estado: perfilServidor ? (perfilServidor.activo ? "Activa" : "Inactiva") : "—",
    fechaRegistro: perfilServidor ? fecha(perfilServidor.fechaCreacion) : "—",
  };

  const iniciales = perfil.nombre
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((palabra) => palabra.charAt(0).toUpperCase())
    .join("");

  const primerError = (error: ApiError) =>
    Object.fromEntries(Object.entries(error.errores).map(([campo, mensajes]) => [campo, mensajes[0]]));

  const guardarPerfil = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setMensaje("");
    setErrores({});

    const nombreLimpio = nombre.trim();
    const correoLimpio = correo.trim();

    if (!nombreLimpio || !correoLimpio) {
      setMensaje("Debes completar el nombre y el correo electrónico.");
      return;
    }

    setGuardando(true);
    try {
      const actualizado = await actualizarPerfil({ nombre: nombreLimpio, correo: correoLimpio });
      actualizarUsuario(actualizado);
      consultaPerfil.actualizar(() => actualizado);
      setNombreEditado(null);
      setCorreoEditado(null);
      avisar("Cambios guardados");
    } catch (error) {
      if (error instanceof ApiError) setErrores(primerError(error));
      setMensaje(mensajeDeError(error, "No se pudieron guardar los cambios."));
    } finally {
      setGuardando(false);
    }
  };

  const cerrarModalPassword = () => {
    setMostrarCambioPassword(false);
    setPasswordActual("");
    setPasswordNueva("");
    setConfirmarPassword("");
    setMensajePassword("");
  };

  const solicitarCambioPassword = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setMensajePassword("");

    if (!passwordActual || !passwordNueva || !confirmarPassword) {
      setMensajePassword("Completa todos los campos.");
      return;
    }

    if (!POLITICA_PASSWORD.test(passwordNueva)) {
      setMensajePassword(
        "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número."
      );
      return;
    }

    if (passwordNueva !== confirmarPassword) {
      setMensajePassword("Las contraseñas nuevas no coinciden.");
      return;
    }

    setCambiandoPassword(true);
    try {
      // No se guarda ninguna contraseña en el navegador.
      await cambiarPassword({ passwordActual, passwordNueva });
      cerrarModalPassword();
      avisar("Contraseña actualizada");
    } catch (error) {
      if (error instanceof ApiError) {
        setMensajePassword(error.errorDe("passwordActual") ?? error.errorDe("passwordNueva") ?? error.message);
      } else {
        setMensajePassword(mensajeDeError(error, "No se pudo cambiar la contraseña."));
      }
    } finally {
      setCambiandoPassword(false);
    }
  };

  return (
    <div className="user-profile-layout">

      <main className="user-profile-page">
        <div className="user-profile-container">
          <EnlaceVolver to={esAdministrador ? "/admin" : "/dashboard"}>
            {esAdministrador ? "Volver al panel administrativo" : "Volver a mi dashboard"}
          </EnlaceVolver>

          <header className="user-profile-heading">
            <span>PERFIL</span>
            <h1>Mi perfil</h1>
            <p>
  {esAdministrador
    ? "Administra la información de tu cuenta de administrador."
    : "Administra la información de tu cuenta."}
</p>
          </header>

          <section className="user-profile-summary">
            <div className="user-profile-avatar">{iniciales}</div>

            <div className="user-profile-summary-info">
              <h2>{perfil.nombre}</h2>
              <p>{perfil.correo}</p>
            </div>

            <span className="user-profile-role-badge">
              {perfil.rol}
            </span>
          </section>

          <div className="user-profile-columns">
            <section className="user-profile-personal">
              <h2>Información personal</h2>

              <form
                className="user-profile-card"
                onSubmit={guardarPerfil}
              >
                <div className="user-profile-fields">
                  <label>
                    Nombre
                    <input
                      type="text"
                      value={nombre}
                      onChange={(e) => {
                        setNombreEditado(e.target.value);
                        setMensaje("");
                      }}
                      maxLength={100}
                      required
                    />
                    {errores.nombre && <small className="text-danger d-block">{errores.nombre}</small>}
                  </label>

                  <label>
                    Correo electrónico
                    <input
                      type="email"
                      value={correo}
                      onChange={(e) => {
                        setCorreoEditado(e.target.value);
                        setMensaje("");
                      }}
                      maxLength={150}
                      required
                    />
                    {errores.correo && <small className="text-danger d-block">{errores.correo}</small>}
                  </label>
                </div>

                <div className="user-profile-role-row">
                  <strong>Rol</strong>
                  <span className="user-profile-role-badge">
                    {perfil.rol}
                  </span>
                  <small>Solo lectura</small>
                </div>

                <div className="user-profile-form-footer">
                  <button type="submit" disabled={guardando}>
                    {guardando ? "Guardando..." : "Guardar cambios"}
                  </button>
                </div>

                {mensaje && (
                  <p className="user-profile-message" role="status">
                    {mensaje}
                  </p>
                )}
              </form>
            </section>

            <section className="user-profile-account">
              <h2>Información de la cuenta</h2>

              <div className="user-profile-card">
                <div className="user-profile-account-item">
                  <span>Estado</span>
                  <strong>{perfil.estado}</strong>
                </div>

                <div className="user-profile-account-item">
                  <span>Fecha de registro</span>
                  <strong>{perfil.fechaRegistro}</strong>
                </div>

                <div className="user-profile-account-note">
                  El rol y el estado son de solo lectura.
                </div>
              </div>
            </section>
          </div>

          <section className="user-profile-security">
            <h2>Seguridad</h2>

            <div className="user-profile-security-card">
              <div className="user-profile-security-icon">
                <i className="bi bi-lock"></i>
              </div>

              <div className="user-profile-security-info">
                <strong>Contraseña</strong>
                <span>••••••••••••</span>
                <p>
                  Protege tu cuenta utilizando una contraseña segura.
                </p>
              </div>

              <button
                type="button"
                onClick={() => {
                  setMostrarCambioPassword(true);
                  setMensajePassword("");
                }}
              >
                Cambiar contraseña
              </button>
            </div>
          </section>
        </div>
      </main>

      {mostrarCambioPassword && (
        <div
          className="user-profile-modal-overlay"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) {
              cerrarModalPassword();
            }
          }}
        >
          <div
            className="user-profile-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="user-profile-password-title"
          >
            <div className="user-profile-modal-header">
              <h2 id="user-profile-password-title">
                Cambiar contraseña
              </h2>

              <button
                type="button"
                onClick={cerrarModalPassword}
                aria-label="Cerrar ventana"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={solicitarCambioPassword}>
              <label>
                Contraseña actual
                <input
                  type="password"
                  value={passwordActual}
                  onChange={(e) =>
                    setPasswordActual(e.target.value)
                  }
                  autoComplete="current-password"
                  required
                />
              </label>

              <label>
                Nueva contraseña
                <input
                  type="password"
                  value={passwordNueva}
                  onChange={(e) =>
                    setPasswordNueva(e.target.value)
                  }
                  autoComplete="new-password"
                  minLength={8}
                  required
                />
              </label>

              <label>
                Confirmar nueva contraseña
                <input
                  type="password"
                  value={confirmarPassword}
                  onChange={(e) =>
                    setConfirmarPassword(e.target.value)
                  }
                  autoComplete="new-password"
                  minLength={8}
                  required
                />
              </label>

              {mensajePassword && (
                <p className="user-profile-message" role="status">
                  {mensajePassword}
                </p>
              )}

              <div className="user-profile-modal-actions">
                <button type="submit" disabled={cambiandoPassword}>
                  {cambiandoPassword ? "Guardando..." : "Cambiar contraseña"}
                </button>

                <button
                  type="button"
                  className="secondary"
                  onClick={cerrarModalPassword}
                >
                  Cancelar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}