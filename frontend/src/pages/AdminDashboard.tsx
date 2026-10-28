import { Link } from "react-router-dom";
import "./AdminDashboard.css";

const estadisticas = [
  {
    titulo: "Usuarios registrados",
    valor: "128",
    icono: "bi-people",
    color: "blue",
  },
  {
    titulo: "Dispositivos ESP32",
    valor: "3",
    icono: "bi-cpu",
    color: "cyan",
  },
  {
    titulo: "Alertas activas",
    valor: "3",
    icono: "bi-bell",
    color: "orange",
  },
  {
    titulo: "Consultas realizadas",
    valor: "1,245",
    icono: "bi-cloud-sun",
    color: "green",
  },
];

const actividades = [
  {
    icono: "bi-person-plus",
    titulo: "Nuevo usuario registrado",
    descripcion: "Se ha registrado un nuevo usuario en ClimaPlus.",
    tiempo: "Hace 5 minutos",
  },
  {
    icono: "bi-cpu",
    titulo: "Dispositivo conectado",
    descripcion: "ESP32 - Estación Antigua se encuentra conectado.",
    tiempo: "Hace 15 minutos",
  },
  {
    icono: "bi-exclamation-triangle",
    titulo: "Nueva alerta meteorológica",
    descripcion: "Se registró una alerta por lluvias intensas.",
    tiempo: "Hace 30 minutos",
  },
];

export default function AdminDashboard() {
  return (
    <main className="admin-dashboard-page">
      <div className="admin-dashboard-container">

        <div className="admin-dashboard-heading">
          <span className="admin-dashboard-label">
            ADMINISTRACIÓN
          </span>

          <h1>Panel de administración</h1>

          <p>
            Supervisa los usuarios, dispositivos y alertas
            meteorológicas de ClimaPlus.
          </p>
        </div>

        <section className="admin-dashboard-section">
          <div className="admin-dashboard-section-heading">
            <h2>Resumen general</h2>
            <span>Datos de demostración</span>
          </div>

          <div className="admin-dashboard-stats">
            {estadisticas.map((estadistica) => (
              <article
                className="admin-dashboard-stat"
                key={estadistica.titulo}
              >
                <div
                  className={`admin-dashboard-stat-icon ${estadistica.color}`}
                >
                  <i className={`bi ${estadistica.icono}`}></i>
                </div>

                <div>
                  <h3>{estadistica.valor}</h3>
                  <p>{estadistica.titulo}</p>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="admin-dashboard-section">
          <h2>Gestión del sistema</h2>

          <div className="admin-dashboard-actions">
            <Link
              to="/admin/usuarios"
              className="admin-dashboard-action"
            >
              <div className="admin-dashboard-action-icon">
                <i className="bi bi-people"></i>
              </div>

              <h3>Gestión de usuarios</h3>

              <p>
                Consulta y administra los usuarios registrados
                en la plataforma.
              </p>

              <span>
                Administrar usuarios
                <i className="bi bi-arrow-right"></i>
              </span>
            </Link>

            <Link
              to="/admin/alertas"
              className="admin-dashboard-action"
            >
              <div className="admin-dashboard-action-icon">
                <i className="bi bi-bell"></i>
              </div>

              <h3>Gestión de alertas</h3>

              <p>
                Supervisa las alertas meteorológicas
                registradas en el sistema.
              </p>

              <span>
                Administrar alertas
                <i className="bi bi-arrow-right"></i>
              </span>
            </Link>

            <Link
              to="/dispositivos"
              className="admin-dashboard-action"
            >
              <div className="admin-dashboard-action-icon">
                <i className="bi bi-cpu"></i>
              </div>

              <h3>Dispositivos ESP32</h3>

              <p>
                Consulta el estado y las lecturas
                de los dispositivos meteorológicos.
              </p>

              <span>
                Ver dispositivos
                <i className="bi bi-arrow-right"></i>
              </span>
            </Link>
          </div>
        </section>

        <section className="admin-dashboard-section">
          <div className="admin-dashboard-section-heading">
            <h2>Actividad reciente</h2>
            <span>Datos de demostración</span>
          </div>

          <div className="admin-dashboard-activity">
            {actividades.map((actividad) => (
              <article
                className="admin-dashboard-activity-item"
                key={actividad.titulo}
              >
                <div className="admin-dashboard-activity-icon">
                  <i className={`bi ${actividad.icono}`}></i>
                </div>

                <div className="admin-dashboard-activity-content">
                  <h3>{actividad.titulo}</h3>
                  <p>{actividad.descripcion}</p>
                </div>

                <span className="admin-dashboard-activity-time">
                  {actividad.tiempo}
                </span>
              </article>
            ))}
          </div>
        </section>

      </div>
    </main>
  );
}