import { NavLink } from "react-router-dom";
import { useAuth } from "../context/auth";
import "./AdminSidebar.css";

/** Opciones administrativas (M13–M15, M17). "pendiente" = sin endpoints en el backend todavía. */
const ENLACES_ADMIN = [
  { ruta: "/admin", nombre: "Panel administrativo", icono: "bi-speedometer2", pendiente: false },
  { ruta: "/admin/alertas", nombre: "Gestión de alertas", icono: "bi-bell", pendiente: false },
  { ruta: "/admin/usuarios", nombre: "Gestión de usuarios", icono: "bi-people", pendiente: true },
];

const clase = ({ isActive }: { isActive: boolean }) => (isActive ? "admin-sidebar-link active" : "admin-sidebar-link");

function AdminSidebar({ alNavegar }: { alNavegar?: () => void }) {
  const { cerrarSesion } = useAuth();

  return (
    <aside className="admin-sidebar" aria-label="Menú de administración">
      <div className="admin-sidebar-title">
        <i className="bi bi-cloud-sun"></i>
        <span>ClimaPlus</span>
      </div>

      <p className="admin-sidebar-label">ADMINISTRACIÓN</p>

      <nav className="admin-sidebar-nav" aria-label="Menú administrativo">
        {ENLACES_ADMIN.map((opcion) => (
          <NavLink key={opcion.ruta} to={opcion.ruta} end className={clase} onClick={alNavegar}>
            <i className={`bi ${opcion.icono}`} aria-hidden="true"></i>
            <span>{opcion.nombre}</span>
            {opcion.pendiente && (
              <span className="badge rounded-pill text-bg-light border ms-auto">Pendiente</span>
            )}
          </NavLink>
        ))}
      </nav>

      {/* Opciones inferiores */}
      <div className="admin-sidebar-bottom">
        <NavLink to="/dashboard" className={clase} onClick={alNavegar}>
          <i className="bi bi-grid" aria-hidden="true"></i>
          <span>Mi espacio</span>
        </NavLink>

        <NavLink to="/admin/perfil" end className={clase} onClick={alNavegar}>
          <i className="bi bi-person" aria-hidden="true"></i>
          <span>Perfil</span>
        </NavLink>

        <NavLink to="/" end className={clase} onClick={alNavegar}>
          <i className="bi bi-house" aria-hidden="true"></i>
          <span>Inicio</span>
        </NavLink>

        <button
          type="button"
          className="admin-sidebar-link border-0 bg-transparent w-100 text-start"
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

export default AdminSidebar;
