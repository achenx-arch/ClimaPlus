import { NavLink } from "react-router-dom";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import "./UserSidebar.css";

interface Enlace {
  ruta: string;
  icono: string;
  texto: string;
  pendiente?: boolean;
}

/** Opciones de "Mi espacio" (M06–M12, M16). Iguales para USUARIO y ADMINISTRADOR en su propia cuenta. */
const ENLACES_USUARIO: Enlace[] = [
  { ruta: "/dashboard", icono: "bi-grid", texto: "Dashboard" },
  { ruta: "/clima", icono: "bi-cloud-sun", texto: "Clima" },
  { ruta: "/pronostico", icono: "bi-calendar3", texto: "Pronóstico" },
  { ruta: "/favoritos", icono: "bi-star", texto: "Favoritos" },
  { ruta: "/historial", icono: "bi-clock-history", texto: "Historial" },
  { ruta: "/alertas", icono: "bi-bell", texto: "Alertas" },
  { ruta: "/configuracion-alertas", icono: "bi-sliders", texto: "Configuración de alertas" },
  { ruta: "/dispositivos", icono: "bi-pc-display", texto: "Mis dispositivos" },
];

const clase = ({ isActive }: { isActive: boolean }) => `user-sidebar-link ${isActive ? "active" : ""}`;

/** Menú lateral persistente de las pantallas privadas. alNavegar cierra el menú móvil. */
export default function UserSidebar({ alNavegar }: { alNavegar?: () => void }) {
  const { cerrarSesion, esAdministrador } = useAuth();
  const { nuevas } = useNotificaciones();

  return (
    <aside className="user-sidebar" aria-label="Menú de mi espacio">
      <div className="user-sidebar-main">
        <span className="user-sidebar-label">MI ESPACIO</span>

        <nav className="user-sidebar-nav" aria-label="Menú del usuario">
          {ENLACES_USUARIO.map((enlace) => (
            <NavLink key={enlace.ruta} to={enlace.ruta} className={clase} onClick={alNavegar}>
              <i className={`bi ${enlace.icono}`} aria-hidden="true"></i>
              <span>{enlace.texto}</span>
              {enlace.ruta === "/alertas" && nuevas > 0 && (
                <span className="badge rounded-pill bg-danger ms-auto" aria-label={`${nuevas} alertas nuevas`}>
                  {nuevas > 99 ? "99+" : nuevas}
                </span>
              )}
              {enlace.pendiente && (
                <span className="badge rounded-pill text-bg-light border ms-auto" title="Fase de dispositivos ESP32">
                  Pendiente
                </span>
              )}
            </NavLink>
          ))}
        </nav>
      </div>

      <div className="user-sidebar-bottom">
        {esAdministrador && (
          <NavLink to="/admin" end className={clase} onClick={alNavegar}>
            <i className="bi bi-speedometer2" aria-hidden="true"></i>
            <span>Panel administrativo</span>
          </NavLink>
        )}

        <NavLink to="/perfil" className={clase} onClick={alNavegar}>
          <i className="bi bi-person" aria-hidden="true"></i>
          <span>Perfil</span>
        </NavLink>

        <NavLink to="/" end className={clase} onClick={alNavegar}>
          <i className="bi bi-house" aria-hidden="true"></i>
          <span>Inicio</span>
        </NavLink>

        <button
          type="button"
          className="user-sidebar-link border-0 bg-transparent w-100 text-start"
          onClick={() => {
            alNavegar?.();
            cerrarSesion();
          }}
        >
          <i className="bi bi-box-arrow-right" aria-hidden="true"></i>
          <span>Cerrar sesión</span>
        </button>
      </div>
    </aside>
  );
}
