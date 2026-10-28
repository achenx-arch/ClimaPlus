import { useMemo, useState } from "react";
import AdminSidebar from "../components/AdminSidebar";
import "./AdminAlerts.css";

type EstadoAlerta = "NUEVA" | "LEÍDA";
type TipoAlerta = "LLUVIA" | "TEMPERATURA_BAJA" | "TEMPERATURA_ALTA";
type OrigenAlerta = "Clima" | "Sensor";

interface Alerta {
  id: number;
  usuario: string;
  ubicacion: string;
  tipo: TipoAlerta;
  mensaje: string;
  origen: OrigenAlerta;
  estado: EstadoAlerta;
  fecha: string;
  hora: string;
}

// Datos temporales. Se reemplazarán al integrar el backend.
const alertasIniciales: Alerta[] = [
  {
    id: 1,
    usuario: "Carlos Méndez",
    ubicacion: "Antigua Guatemala",
    tipo: "LLUVIA",
    mensaje: "Se detectó lluvia en la ubicación.",
    origen: "Clima",
    estado: "NUEVA",
    fecha: "7 oct. 2026",
    hora: "08:20",
  },
  {
    id: 2,
    usuario: "Ana López",
    ubicacion: "Antigua Guatemala",
    tipo: "TEMPERATURA_BAJA",
    mensaje: "13 °C detectados; umbral configurado de 15 °C.",
    origen: "Sensor",
    estado: "LEÍDA",
    fecha: "7 oct. 2026",
    hora: "08:15",
  },
  {
    id: 3,
    usuario: "Luis Gómez",
    ubicacion: "Cobán",
    tipo: "LLUVIA",
    mensaje: "El sensor ESP32 detectó lluvia.",
    origen: "Sensor",
    estado: "NUEVA",
    fecha: "7 oct. 2026",
    hora: "07:50",
  },
  {
    id: 4,
    usuario: "Carlos Méndez",
    ubicacion: "Quetzaltenango",
    tipo: "TEMPERATURA_BAJA",
    mensaje: "12 °C detectados, por debajo del umbral de 14 °C.",
    origen: "Clima",
    estado: "LEÍDA",
    fecha: "6 oct. 2026",
    hora: "18:00",
  },
  {
    id: 5,
    usuario: "Carlos Méndez",
    ubicacion: "Ciudad de Guatemala",
    tipo: "TEMPERATURA_ALTA",
    mensaje: "32 °C detectados, por encima del umbral de 30 °C.",
    origen: "Clima",
    estado: "NUEVA",
    fecha: "6 oct. 2026",
    hora: "13:30",
  },
];

export default function AdminAlerts() {
  const [alertas, setAlertas] = useState<Alerta[]>(alertasIniciales);
  const [estado, setEstado] = useState("todos");
  const [tipo, setTipo] = useState("todos");
  const [ubicacion, setUbicacion] = useState("todas");
  const [usuario, setUsuario] = useState("todos");
  const [alertaSeleccionadaId, setAlertaSeleccionadaId] = useState<
    number | null
  >(null);

  const alertaSeleccionada =
    alertas.find((alerta) => alerta.id === alertaSeleccionadaId) ?? null;

  const ubicaciones = [...new Set(alertas.map((alerta) => alerta.ubicacion))];
  const usuarios = [...new Set(alertas.map((alerta) => alerta.usuario))];

  const total = alertas.length;
  const nuevas = alertas.filter((alerta) => alerta.estado === "NUEVA").length;
  const leidas = alertas.filter((alerta) => alerta.estado === "LEÍDA").length;

  const alertasFiltradas = useMemo(
    () =>
      alertas.filter((alerta) => {
        return (
          (estado === "todos" || alerta.estado === estado) &&
          (tipo === "todos" || alerta.tipo === tipo) &&
          (ubicacion === "todas" || alerta.ubicacion === ubicacion) &&
          (usuario === "todos" || alerta.usuario === usuario)
        );
      }),
    [alertas, estado, tipo, ubicacion, usuario]
  );

  const marcarComoLeida = (id: number) => {
    setAlertas((anteriores) =>
      anteriores.map((alerta) =>
        alerta.id === id ? { ...alerta, estado: "LEÍDA" } : alerta
      )
    );
  };

  return (
    <div className="admin-alerts-layout">
      <AdminSidebar />

      <main className="admin-alerts-page">
        <div className="admin-alerts-container">
          <div className="admin-alerts-heading">
            <span>ADMINISTRACIÓN / ALERTAS</span>
            <h1>Alertas del sistema</h1>
            <p>
              Consulta las alertas ambientales generadas por ClimaPlus.
            </p>
          </div>

          <div className="admin-alerts-stats">
            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon">
                <i className="bi bi-bell"></i>
              </div>
              <div>
                <span>Total</span>
                <strong>{total}</strong>
                <small>Alertas generadas</small>
              </div>
            </article>

            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon warning">
                <i className="bi bi-bell"></i>
              </div>
              <div>
                <span>Nuevas</span>
                <strong>{nuevas}</strong>
                <small>Pendientes de leer</small>
              </div>
            </article>

            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon">
                <i className="bi bi-check2"></i>
              </div>
              <div>
                <span>Leídas</span>
                <strong>{leidas}</strong>
                <small>Alertas revisadas</small>
              </div>
            </article>
          </div>

          <div className="admin-alerts-filters">
            <label>
              Estado
              <select value={estado} onChange={(e) => setEstado(e.target.value)}>
                <option value="todos">Todos</option>
                <option value="NUEVA">Nuevas</option>
                <option value="LEÍDA">Leídas</option>
              </select>
            </label>

            <label>
              Tipo
              <select value={tipo} onChange={(e) => setTipo(e.target.value)}>
                <option value="todos">Todos los tipos</option>
                <option value="LLUVIA">Lluvia</option>
                <option value="TEMPERATURA_BAJA">Temperatura baja</option>
                <option value="TEMPERATURA_ALTA">Temperatura alta</option>
              </select>
            </label>

            <label>
              Ubicación
              <select
                value={ubicacion}
                onChange={(e) => setUbicacion(e.target.value)}
              >
                <option value="todas">Todas las ubicaciones</option>
                {ubicaciones.map((lugar) => (
                  <option key={lugar} value={lugar}>
                    {lugar}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Usuario
              <select
                value={usuario}
                onChange={(e) => setUsuario(e.target.value)}
              >
                <option value="todos">Todos los usuarios</option>
                {usuarios.map((nombre) => (
                  <option key={nombre} value={nombre}>
                    {nombre}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <section className="admin-alerts-section">
            <div className="admin-alerts-section-heading">
              <h2>Alertas generadas</h2>
              <span>
                Mostrando {alertasFiltradas.length} de {total} alertas
              </span>
            </div>

            <div className="admin-alerts-table-wrapper">
              <table className="admin-alerts-table">
                <thead>
                  <tr>
                    <th>Usuario</th>
                    <th>Ubicación</th>
                    <th>Tipo</th>
                    <th>Mensaje</th>
                    <th>Origen</th>
                    <th>Estado</th>
                    <th>Fecha / hora</th>
                    <th>Acción</th>
                  </tr>
                </thead>

                <tbody>
                  {alertasFiltradas.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="admin-alerts-empty">
                        No se encontraron alertas con los filtros seleccionados.
                      </td>
                    </tr>
                  ) : (
                    alertasFiltradas.map((alerta) => (
                      <tr key={alerta.id}>
                        <td className="admin-alerts-user">{alerta.usuario}</td>
                        <td>{alerta.ubicacion}</td>
                        <td>
                          <span className="admin-alerts-type">
                            {alerta.tipo}
                          </span>
                        </td>
                        <td>{alerta.mensaje}</td>
                        <td>{alerta.origen}</td>
                        <td>
                          <span
                            className={`admin-alerts-status ${
                              alerta.estado === "NUEVA" ? "new" : "read"
                            }`}
                          >
                            {alerta.estado}
                          </span>
                        </td>
                        <td>
                          {alerta.fecha}
                          <br />
                          <small>{alerta.hora}</small>
                        </td>
                        <td>
                          <button
                            type="button"
                            className="admin-alerts-detail-button"
                            onClick={() => setAlertaSeleccionadaId(alerta.id)}
                          >
                            Ver detalle
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>

              <div className="admin-alerts-table-footer">
                Selección de alertas de ejemplo · Estados de lectura por usuario
              </div>
            </div>
          </section>

          <div className="admin-alerts-info">
            <i className="bi bi-info-circle"></i>
            <span>
              Origen Clima: consulta de clima actual. Origen Sensor: lectura
              de un dispositivo ESP32. Solo lluvia, temperatura alta y
              temperatura baja.
            </span>
          </div>

          <footer className="admin-alerts-footer">
            Datos de ejemplo · ClimaPlus
          </footer>
        </div>
      </main>

      {alertaSeleccionada && (
        <div
          className="admin-alerts-overlay"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) {
              setAlertaSeleccionadaId(null);
            }
          }}
        >
          <div
            className="admin-alerts-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="admin-alerts-modal-title"
          >
            <div className="admin-alerts-modal-header">
              <h2 id="admin-alerts-modal-title">Detalle de alerta</h2>
              <button
                type="button"
                onClick={() => setAlertaSeleccionadaId(null)}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <div className="admin-alerts-modal-body">
              <p><strong>Usuario:</strong> {alertaSeleccionada.usuario}</p>
              <p><strong>Ubicación:</strong> {alertaSeleccionada.ubicacion}</p>
              <p><strong>Tipo:</strong> {alertaSeleccionada.tipo}</p>
              <p><strong>Mensaje:</strong> {alertaSeleccionada.mensaje}</p>
              <p><strong>Origen:</strong> {alertaSeleccionada.origen}</p>
              <p><strong>Estado:</strong> {alertaSeleccionada.estado}</p>
              <p>
                <strong>Fecha:</strong> {alertaSeleccionada.fecha} ·{" "}
                {alertaSeleccionada.hora}
              </p>
            </div>

            <div className="admin-alerts-modal-actions">
              {alertaSeleccionada.estado === "NUEVA" && (
                <button
                  type="button"
                  onClick={() => marcarComoLeida(alertaSeleccionada.id)}
                >
                  Marcar como leída
                </button>
              )}

              <button
                type="button"
                className="secondary"
                onClick={() => setAlertaSeleccionadaId(null)}
              >
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}