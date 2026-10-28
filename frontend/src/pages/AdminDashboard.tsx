import { Link } from "react-router-dom";
import AdminSidebar from "../components/AdminSidebar";
import "./AdminDashboard.css";

const estadisticas = [
  {
    titulo: "Usuarios registrados",
    valor: "128",
    descripcion: "Cuentas del sistema",
    icono: "bi-people",
    color: "blue",
  },
  {
    titulo: "Usuarios activos",
    valor: "116",
    descripcion: "Cuentas habilitadas",
    icono: "bi-person",
    color: "cyan",
  },
  {
    titulo: "Alertas generadas",
    valor: "24",
    descripcion: "Alertas ambientales",
    icono: "bi-bell",
    color: "orange",
  },
  {
    titulo: "Dispositivos registrados",
    valor: "18",
    descripcion: "Dispositivos ESP32",
    icono: "bi-pc-display",
    color: "blue",
  },
];

const actividades = [
  {
    id: 1,
    icono: "bi-cpu",
    titulo: "Lectura ambiental recibida",
    descripcion: "Terraza Guatemala · ESP32",
    fecha: "7 oct 2026 - 08:30",
  },
  {
    id: 2,
    icono: "bi-cloud-rain",
    titulo: "Alerta de lluvia generada",
    descripcion: "Antigua Guatemala · Origen Clima",
    fecha: "7 oct 2026 - 08:20",
  },
  {
    id: 3,
    icono: "bi-person",
    titulo: "Nueva cuenta registrada",
    descripcion: "Ana López · Rol USUARIO",
    fecha: "7 oct 2026 - 08:10",
  },
  {
    id: 4,
    icono: "bi-person-x",
    titulo: "Cuenta desactivada",
    descripcion: "Diego Morales · Desactivación lógica",
    fecha: "6 oct 2026 - 19:10",
  },
];

const alertasRecientes = [
  {
    id: 1,
    titulo: "Lluvia",
    ubicacion: "Antigua Guatemala",
    fecha: "7 oct 2026 - 08:20",
    estado: "NUEVA",
    tipo: "nueva",
  },
  {
    id: 2,
    titulo: "Temperatura baja",
    ubicacion: "Quetzaltenango",
    fecha: "6 oct 2026 - 18:00",
    estado: "LEÍDA",
    tipo: "leida",
  },
  {
    id: 3,
    titulo: "Temperatura alta",
    ubicacion: "Ciudad de Guatemala",
    fecha: "6 oct 2026 - 13:30",
    estado: "NUEVA",
    tipo: "nueva",
  },
];

export default function AdminDashboard() {
  return (
    <main className="admin-dashboard-page">
      <div className="admin-dashboard-layout">
        {/* MENÚ LATERAL */}
        <AdminSidebar />

        {/* CONTENIDO PRINCIPAL */}
        <div className="admin-dashboard-container">
          <div className="admin-dashboard-heading">
            <span className="admin-dashboard-label">
              ADMINISTRACIÓN
            </span>

            <h1>Panel de administración</h1>

            <p>Resumen general del sistema ClimaPlus.</p>
          </div>

          {/* ESTADÍSTICAS */}
          <section className="admin-dashboard-section">
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

                  <div className="admin-dashboard-stat-content">
                    <span>{estadistica.titulo}</span>

                    <strong>{estadistica.valor}</strong>

                    <small>{estadistica.descripcion}</small>
                  </div>
                </article>
              ))}
            </div>
          </section>

          {/* AVISO DE DATOS DE EJEMPLO */}
          <div className="admin-dashboard-notice">
            <i className="bi bi-info-circle"></i>

            <span>
              Datos de ejemplo. Este resumen es un mockup de
              ClimaPlus Fase 1 y no representa estadísticas de
              una base de datos real.
            </span>
          </div>

          {/* ACTIVIDAD Y ALERTAS */}
          <div className="admin-dashboard-columns">
            {/* ACTIVIDAD RECIENTE */}
            <section className="admin-dashboard-panel">
              <div className="admin-dashboard-panel-heading">
                <h2>Actividad reciente</h2>

                <Link
                  to="/admin/usuarios"
                  className="admin-dashboard-panel-link"
                >
                  Ver usuarios
                </Link>
              </div>

              <div className="admin-dashboard-activity">
                {actividades.map((actividad) => (
                  <article
                    className="admin-dashboard-activity-item"
                    key={actividad.id}
                  >
                    <div className="admin-dashboard-activity-icon">
                      <i className={`bi ${actividad.icono}`}></i>
                    </div>

                    <div className="admin-dashboard-activity-content">
                      <h3>{actividad.titulo}</h3>

                      <p>{actividad.descripcion}</p>

                      <small>{actividad.fecha}</small>
                    </div>
                  </article>
                ))}
              </div>
            </section>

            {/* ALERTAS RECIENTES */}
            <section className="admin-dashboard-panel">
              <div className="admin-dashboard-panel-heading">
                <h2>Alertas recientes</h2>

                <Link
                  to="/admin/alertas"
                  className="admin-dashboard-panel-link"
                >
                  Ver alertas
                </Link>
              </div>

              <div className="admin-dashboard-alerts">
                {alertasRecientes.map((alerta) => (
                  <article
                    className="admin-dashboard-alert-item"
                    key={alerta.id}
                  >
                    <div className="admin-dashboard-alert-top">
                      <h3>{alerta.titulo}</h3>

                      <span
                        className={`admin-dashboard-alert-badge ${alerta.tipo}`}
                      >
                        {alerta.estado}
                      </span>
                    </div>

                    <strong>{alerta.ubicacion}</strong>

                    <small>{alerta.fecha}</small>
                  </article>
                ))}
              </div>
            </section>
          </div>

          {/* PIE DE PÁGINA */}
          <footer className="admin-dashboard-footer">
            Datos de ejemplo · ClimaPlus Fase 1 · 7 oct 2026
          </footer>
        </div>
      </div>
    </main>
  );
}