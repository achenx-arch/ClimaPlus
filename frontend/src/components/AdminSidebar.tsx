import { Link, useLocation } from "react-router-dom";
import "./AdminSidebar.css";

function AdminSidebar() {
  const location = useLocation();

  const opciones = [
    {
      nombre: "Panel administrativo",
      ruta: "/admin",
      icono: "bi-speedometer2",
    },
    {
      nombre: "Gestión de usuarios",
      ruta: "/admin/usuarios",
      icono: "bi-people",
    },
    {
      nombre: "Gestión de alertas",
      ruta: "/admin/alertas",
      icono: "bi-bell",
    },
  ];

  return (
    <aside className="admin-sidebar">
      <div className="admin-sidebar-title">
        <i className="bi bi-cloud-sun"></i>
        <span>ClimaPlus</span>
      </div>

      <p className="admin-sidebar-label">ADMINISTRACIÓN</p>

      <nav className="admin-sidebar-nav">
        {opciones.map((opcion) => (
          <Link
            key={opcion.ruta}
            to={opcion.ruta}
            className={
              location.pathname === opcion.ruta
                ? "admin-sidebar-link active"
                : "admin-sidebar-link"
            }
          >
            <i className={`bi ${opcion.icono}`}></i>
            <span>{opcion.nombre}</span>
          </Link>
        ))}
      </nav>

      {/* Opciones inferiores */}
      <div className="admin-sidebar-bottom">
        <Link
          to="/admin/perfil"
          className={
            location.pathname === "/admin/perfil"
              ? "admin-sidebar-link active"
              : "admin-sidebar-link"
          }
        >
          <i className="bi bi-person"></i>
          <span>Perfil</span>
        </Link>

        <Link
          to="/login"
          className="admin-sidebar-link"
        >
          <i className="bi bi-box-arrow-right"></i>
          <span>Cerrar sesión</span>
        </Link>
      </div>
    </aside>
  );
}

export default AdminSidebar;