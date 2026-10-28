import { useState, type FormEvent } from "react";
import UserSidebar from "../components/UserSidebar";
import "./UserProfile.css";
import AdminSidebar from "../components/AdminSidebar";
import { useLocation } from "react-router-dom";

interface PerfilUsuario {
  nombre: string;
  correo: string;
  rol: string;
  estado: string;
  fechaRegistro: string;
}

// Datos temporales hasta conectar el backend.
const perfilInicial: PerfilUsuario = {
  nombre: "Amilcar Chen",
  correo: "chx.amilcar@example.com",
  rol: "Usuario",
  estado: "Activa",
  fechaRegistro: "1 oct. 2026",
};

export default function UserProfile() {
  const location = useLocation();
  const esAdministrador = location.pathname.startsWith("/admin");
const perfilAdministrador: PerfilUsuario = {
  nombre: "Amilcar Chen",
  correo: "chen.amilcar@example.com",
  rol: "Administrador",
  estado: "Activa",
  fechaRegistro: "15 sep. 2026",
};

const datosIniciales = esAdministrador
  ? perfilAdministrador
  : perfilInicial;

const clavePerfil = esAdministrador
  ? "climaplus_perfil_admin"
  : "climaplus_perfil_usuario";

const [perfil, setPerfil] = useState<PerfilUsuario>(() => {
  try {
    const guardado = localStorage.getItem(clavePerfil);

    if (guardado) {
      const datos = JSON.parse(guardado) as Partial<PerfilUsuario>;

      return {
        ...datosIniciales,
        nombre: datos.nombre || datosIniciales.nombre,
        correo: datos.correo || datosIniciales.correo,
      };
    }
  } catch (error) {
    console.error("Error al recuperar el perfil:", error);
  }

  return datosIniciales;
});
const [nombre, setNombre] = useState(perfil.nombre);
const [correo, setCorreo] = useState(perfil.correo);
  const [mensaje, setMensaje] = useState("");
  const [mostrarCambioPassword, setMostrarCambioPassword] = useState(false);

  const [passwordActual, setPasswordActual] = useState("");
  const [passwordNueva, setPasswordNueva] = useState("");
  const [confirmarPassword, setConfirmarPassword] = useState("");

  const [mensajePassword, setMensajePassword] = useState("");

  const iniciales = perfil.nombre
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((palabra) => palabra.charAt(0).toUpperCase())
    .join("");

  const guardarPerfil = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setMensaje("");

    const nombreLimpio = nombre.trim();
    const correoLimpio = correo.trim();

    if (!nombreLimpio || !correoLimpio) {
      setMensaje("Debes completar el nombre y el correo electrónico.");
      return;
    }

const perfilActualizado: PerfilUsuario = {
  ...perfil,
  nombre: nombreLimpio,
  correo: correoLimpio,
};

try {
  localStorage.setItem(
    clavePerfil,
    JSON.stringify({
      nombre: nombreLimpio,
      correo: correoLimpio,
    })
  );

  setPerfil(perfilActualizado);
} catch (error) {
  console.error("Error al guardar el perfil:", error);
  setMensaje("No se pudieron guardar los cambios.");
  return;
}

    setNombre(nombreLimpio);
    setCorreo(correoLimpio);
    setMensaje("Cambios guardados correctamente en esta demostración.");
  };

  const solicitarCambioPassword = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setMensajePassword("");

    if (!passwordActual || !passwordNueva || !confirmarPassword) {
      setMensajePassword("Completa todos los campos.");
      return;
    }

    if (passwordNueva.length < 8) {
      setMensajePassword(
        "La contraseña nueva debe tener al menos 8 caracteres."
      );
      return;
    }

    if (passwordNueva !== confirmarPassword) {
      setMensajePassword("Las contraseñas nuevas no coinciden.");
      return;
    }

    // La contraseña se actualizará mediante el backend.
    // No guardamos contraseñas en el navegador.
    setMensajePassword(
      "Formulario validado. El cambio real estará disponible al conectar el backend."
    );

    setPasswordActual("");
    setPasswordNueva("");
    setConfirmarPassword("");
  };

  return (
    <div className="user-profile-layout">
      {esAdministrador ? <AdminSidebar /> : <UserSidebar />}

      <main className="user-profile-page">
        <div className="user-profile-container">
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
                        setNombre(e.target.value);
                        setMensaje("");
                      }}
                      maxLength={100}
                      required
                    />
                  </label>

                  <label>
                    Correo electrónico
                    <input
                      type="email"
                      value={correo}
                      onChange={(e) => {
                        setCorreo(e.target.value);
                        setMensaje("");
                      }}
                      maxLength={150}
                      required
                    />
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
                  <button type="submit">
                    Guardar cambios
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
                  Datos de ejemplo para la demostración.
                  <br />
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
              setMostrarCambioPassword(false);
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
                onClick={() => setMostrarCambioPassword(false)}
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
                <button type="submit">
                  Validar cambio
                </button>

                <button
                  type="button"
                  className="secondary"
                  onClick={() => setMostrarCambioPassword(false)}
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