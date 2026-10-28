import { NavLink } from "react-router-dom";
import "./UserSidebar.css";

export default function UserSidebar() {
  const enlaces = [
    { ruta: "/dashboard", icono: "bi-grid", texto: "Dashboard" },
    { ruta: "/clima", icono: "bi-cloud-sun", texto: "Clima" },
    { ruta: "/pronostico", icono: "bi-calendar3", texto: "Pronóstico" },
    { ruta: "/favoritos", icono: "bi-star", texto: "Favoritos" },
    { ruta: "/historial", icono: "bi-clock-history", texto: "Historial" },
    { ruta: "/alertas", icono: "bi-bell", texto: "Alertas" },
    { ruta: "/dispositivos", icono: "bi-pc-display", texto: "Mis dispositivos" },
  ];

  return (
    <aside className="user-sidebar">
      <div className="user-sidebar-main">
        <span className="user-sidebar-label">MI ESPACIO</span>

        <nav className="user-sidebar-nav" aria-label="Menú del usuario">
          {enlaces.map((enlace) => (
            <NavLink
              key={enlace.ruta}
              to={enlace.ruta}
              className={({ isActive }) =>
                `user-sidebar-link ${isActive ? "active" : ""}`
              }
            >
              <i className={`bi ${enlace.icono}`}></i>
              <span>{enlace.texto}</span>
            </NavLink>
          ))}
        </nav>
      </div>

      <div className="user-sidebar-bottom">
        <NavLink
          to="/perfil"
          className={({ isActive }) =>
            `user-sidebar-link ${isActive ? "active" : ""}`
          }
        >
          <i className="bi bi-person"></i>
          <span>Perfil</span>
        </NavLink>

        <NavLink to="/login" className="user-sidebar-link">
          <i className="bi bi-box-arrow-right"></i>
          <span>Cerrar sesión</span>
        </NavLink>
      </div>
    </aside>
  );
}