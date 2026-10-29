import { Link } from "react-router-dom";
import "./AdminDashboard.css";
import { useConsulta } from "../hooks/useConsulta";
import { mensajeDeError } from "../services/apiClient";
import { listarAlertasAdmin } from "../services/alertasService";
import { NOMBRES_TIPO_ALERTA } from "../utils/clima";
import { fechaHora } from "../utils/fechas";

/** Módulos de administración y su estado real en el backend. */
const MODULOS = [
  {
    titulo: "Gestión de alertas (M15)",
    descripcion: "Alertas generadas de todos los usuarios, con filtros y detalle.",
    icono: "bi-bell",
    ruta: "/admin/alertas",
    disponible: true,
  },
  {
    titulo: "Gestión de usuarios (M14)",
    descripcion: "Listar, cambiar rol y activar o desactivar cuentas (RF-26 a RF-28).",
    icono: "bi-people",
    ruta: "/admin/usuarios",
    disponible: false,
  },
  {
    titulo: "Estadísticas (M13)",
    descripcion: "Usuarios, consultas por periodo y ubicaciones más consultadas (RF-25).",
    icono: "bi-graph-up",
    ruta: null,
    disponible: false,
  },
  {
    titulo: "Dispositivos ESP32",
    descripcion: "Dispositivos registrados y lecturas (RF-20 a RF-24).",
    icono: "bi-cpu",
    ruta: null,
    disponible: false,
  },
];

// M13 – Panel administrativo. Solo muestra datos reales disponibles: las alertas del sistema
// (GET /api/admin/alertas). Usuarios, estadísticas y dispositivos no tienen endpoints todavía.
export default function AdminDashboard() {
  const resumen = useConsulta("admin-panel", async (signal) => {
    const [todas, nuevas, leidas] = await Promise.all([
      listarAlertasAdmin({}, signal),
      listarAlertasAdmin({ estado: "NUEVA" }, signal),
      listarAlertasAdmin({ estado: "LEIDA" }, signal),
    ]);
    return { todas, nuevas: nuevas.totalElementos, leidas: leidas.totalElementos };
  });

  const datos = resumen.datos;
  const recientes = (datos?.todas.elementos ?? []).slice(0, 3);

  const estadisticas = [
    { titulo: "Alertas generadas", valor: datos?.todas.totalElementos, descripcion: "Todas las cuentas", icono: "bi-bell", color: "orange" },
    { titulo: "Alertas nuevas", valor: datos?.nuevas, descripcion: "Pendientes de leer", icono: "bi-bell-fill", color: "blue" },
    { titulo: "Alertas leídas", valor: datos?.leidas, descripcion: "Revisadas por su destinatario", icono: "bi-check2-all", color: "cyan" },
  ];

  return (
    <main className="admin-dashboard-page">
      <div className="admin-dashboard-layout">
        {/* CONTENIDO PRINCIPAL */}
        <div className="admin-dashboard-container">
          <div className="admin-dashboard-heading">
            <span className="admin-dashboard-label">
              ADMINISTRACIÓN
            </span>

            <h1>Panel de administración</h1>

            <p>Resumen del sistema ClimaPlus con los datos disponibles en el backend.</p>
          </div>

          {/* ESTADÍSTICAS REALES */}
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

                    <strong>{estadistica.valor ?? "—"}</strong>

                    <small>{estadistica.descripcion}</small>
                  </div>
                </article>
              ))}
            </div>
          </section>

          {resumen.error !== undefined && (
            <div className="alert alert-danger" role="alert">
              {mensajeDeError(resumen.error, "No se pudo cargar el resumen de alertas.")}
            </div>
          )}

          <div className="admin-dashboard-notice">
            <i className="bi bi-info-circle"></i>

            <span>
              Los conteos de usuarios, consultas y dispositivos llegarán con los módulos
              pendientes (RF-25 a RF-28 y dispositivos). No se muestran cifras de ejemplo.
            </span>
          </div>

          <div className="admin-dashboard-columns">
            {/* MÓDULOS */}
            <section className="admin-dashboard-panel">
              <div className="admin-dashboard-panel-heading">
                <h2>Módulos de administración</h2>
              </div>

              <div className="admin-dashboard-activity">
                {MODULOS.map((modulo) => (
                  <article className="admin-dashboard-activity-item" key={modulo.titulo}>
                    <div className="admin-dashboard-activity-icon">
                      <i className={`bi ${modulo.icono}`}></i>
                    </div>

                    <div className="admin-dashboard-activity-content">
                      <h3>
                        {modulo.disponible && modulo.ruta ? (
                          <Link to={modulo.ruta}>{modulo.titulo}</Link>
                        ) : (
                          modulo.titulo
                        )}
                      </h3>

                      <p>{modulo.descripcion}</p>

                      <small>{modulo.disponible ? "Disponible" : "Pendiente de implementar"}</small>
                    </div>
                  </article>
                ))}
              </div>
            </section>

            {/* ALERTAS RECIENTES (reales) */}
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
                {resumen.cargando && !datos && <p>Cargando alertas...</p>}
                {datos && recientes.length === 0 && <p>Todavía no hay alertas en el sistema.</p>}
                {recientes.map((alerta) => (
                  <article
                    className="admin-dashboard-alert-item"
                    key={alerta.idAlerta}
                  >
                    <div className="admin-dashboard-alert-top">
                      <h3>{NOMBRES_TIPO_ALERTA[alerta.tipo]}</h3>

                      <span
                        className={`admin-dashboard-alert-badge ${alerta.estado === "NUEVA" ? "nueva" : "leida"}`}
                      >
                        {alerta.estado === "NUEVA" ? "NUEVA" : "LEÍDA"}
                      </span>
                    </div>

                    <strong>{alerta.ubicacion.nombre} · {alerta.usuario.nombre}</strong>

                    <small>{fechaHora(alerta.fechaGeneracion)}</small>
                  </article>
                ))}
              </div>
            </section>
          </div>

          {/* PIE DE PÁGINA */}
          <footer className="admin-dashboard-footer">
            Datos del backend · ClimaPlus
          </footer>
        </div>
      </div>
    </main>
  );
}
