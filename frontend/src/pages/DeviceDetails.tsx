import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import "./Alerts.css";
import "./DeviceDetails.css";

type Dispositivo = {
  id: number;
  identificador: string;
  nombre: string;
  ubicacion: string;
  estado: "Conectado" | "Desconectado";
  temperatura: number | null;
  humedad: number | null;
  lluvia: string | null;
  luminosidad: number | null;
  ultimaLectura: string;
};

type Lectura = {
  fecha: string;
  hora: string;
  temperatura: number;
  humedad: number;
  lluvia: string;
  luminosidad: number;
};

const dispositivosIniciales: Dispositivo[] = [
  {
    id: 1,
    identificador: "ESP32-001",
    nombre: "Terraza Guatemala",
    ubicacion: "Ciudad de Guatemala, Guatemala",
    estado: "Conectado",
    temperatura: 22,
    humedad: 74,
    lluvia: "Sin lluvia",
    luminosidad: 680,
    ultimaLectura: "7 oct 2026 - 08:30",
  },
  {
    id: 2,
    identificador: "ESP32-002",
    nombre: "Jardín Antigua",
    ubicacion: "Antigua Guatemala, Guatemala",
    estado: "Conectado",
    temperatura: 20,
    humedad: 81,
    lluvia: "Lluvia",
    luminosidad: 320,
    ultimaLectura: "7 oct 2026 - 08:25",
  },
  {
    id: 3,
    identificador: "ESP32-003",
    nombre: "Patio Quetzaltenango",
    ubicacion: "Quetzaltenango, Guatemala",
    estado: "Desconectado",
    temperatura: 16,
    humedad: 78,
    lluvia: "Sin lluvia",
    luminosidad: 410,
    ultimaLectura: "6 oct 2026 - 16:40",
  },
];

const historialEjemplo: Record<number, Lectura[]> = {
  1: [
    { fecha: "7 oct 2026", hora: "08:30", temperatura: 22, humedad: 74, lluvia: "Sin lluvia", luminosidad: 680 },
    { fecha: "7 oct 2026", hora: "08:15", temperatura: 21.8, humedad: 75, lluvia: "Sin lluvia", luminosidad: 645 },
    { fecha: "7 oct 2026", hora: "08:00", temperatura: 21.5, humedad: 76, lluvia: "Sin lluvia", luminosidad: 610 },
    { fecha: "7 oct 2026", hora: "07:45", temperatura: 21, humedad: 77, lluvia: "Sin lluvia", luminosidad: 570 },
    { fecha: "7 oct 2026", hora: "07:30", temperatura: 20.8, humedad: 78, lluvia: "Sin lluvia", luminosidad: 525 },
    { fecha: "7 oct 2026", hora: "07:15", temperatura: 20.5, humedad: 79, lluvia: "Sin lluvia", luminosidad: 480 },
  ],
  2: [
    { fecha: "7 oct 2026", hora: "08:25", temperatura: 20, humedad: 81, lluvia: "Lluvia", luminosidad: 320 },
    { fecha: "7 oct 2026", hora: "08:10", temperatura: 19.8, humedad: 82, lluvia: "Lluvia", luminosidad: 305 },
    { fecha: "7 oct 2026", hora: "07:55", temperatura: 19.6, humedad: 83, lluvia: "Lluvia", luminosidad: 290 },
    { fecha: "7 oct 2026", hora: "07:40", temperatura: 19.5, humedad: 82, lluvia: "Sin lluvia", luminosidad: 310 },
    { fecha: "7 oct 2026", hora: "07:25", temperatura: 19.4, humedad: 80, lluvia: "Sin lluvia", luminosidad: 340 },
    { fecha: "7 oct 2026", hora: "07:10", temperatura: 19.2, humedad: 79, lluvia: "Sin lluvia", luminosidad: 365 },
  ],
  3: [
    { fecha: "6 oct 2026", hora: "16:40", temperatura: 16, humedad: 78, lluvia: "Sin lluvia", luminosidad: 410 },
    { fecha: "6 oct 2026", hora: "16:25", temperatura: 16.2, humedad: 77, lluvia: "Sin lluvia", luminosidad: 435 },
    { fecha: "6 oct 2026", hora: "16:10", temperatura: 16.4, humedad: 76, lluvia: "Sin lluvia", luminosidad: 460 },
    { fecha: "6 oct 2026", hora: "15:55", temperatura: 16.6, humedad: 75, lluvia: "Sin lluvia", luminosidad: 485 },
    { fecha: "6 oct 2026", hora: "15:40", temperatura: 16.8, humedad: 74, lluvia: "Sin lluvia", luminosidad: 510 },
    { fecha: "6 oct 2026", hora: "15:25", temperatura: 17, humedad: 73, lluvia: "Sin lluvia", luminosidad: 535 },
  ],
};

const STORAGE_KEY = "climaplus_dispositivos";

export default function DeviceDetails() {
  const { id } = useParams<{ id: string }>();

  const [dispositivos, setDispositivos] = useState<Dispositivo[]>(() => {
    try {
      const guardados = localStorage.getItem(STORAGE_KEY);

      if (guardados) {
        const datos = JSON.parse(guardados);

        if (Array.isArray(datos)) {
          return datos;
        }
      }
    } catch {
      console.error("No se pudieron cargar los dispositivos");
    }

    return dispositivosIniciales;
  });

  const [mostrarEdicion, setMostrarEdicion] = useState(false);
  const [nombreEditado, setNombreEditado] = useState("");
  const [ubicacionEditada, setUbicacionEditada] = useState("");
  const [mensaje, setMensaje] = useState("");

  const dispositivo = dispositivos.find(
    (item) => String(item.id) === id
  );

  const guardarDispositivos = (actualizados: Dispositivo[]) => {
    setDispositivos(actualizados);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(actualizados));
  };

  const abrirEdicion = () => {
    if (!dispositivo) return;

    setNombreEditado(dispositivo.nombre);
    setUbicacionEditada(dispositivo.ubicacion);
    setMostrarEdicion(true);
  };

  const guardarEdicion = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    if (!dispositivo) return;

    if (!nombreEditado.trim() || !ubicacionEditada.trim()) {
      return;
    }

    const actualizados = dispositivos.map((item) =>
      item.id === dispositivo.id
        ? {
            ...item,
            nombre: nombreEditado.trim(),
            ubicacion: ubicacionEditada.trim(),
          }
        : item
    );

    guardarDispositivos(actualizados);
    setMostrarEdicion(false);
    setMensaje("Dispositivo actualizado correctamente.");
  };

  const cambiarEstado = () => {
    if (!dispositivo) return;

    const nuevoEstado =
      dispositivo.estado === "Conectado"
        ? "Desconectado"
        : "Conectado";

    const actualizados: Dispositivo[] = dispositivos.map((item) =>
      item.id === dispositivo.id
        ? { ...item, estado: nuevoEstado }
        : item
    );

    guardarDispositivos(actualizados);

    setMensaje(
      nuevoEstado === "Conectado"
        ? "Dispositivo activado correctamente."
        : "Dispositivo desactivado correctamente."
    );
  };

  if (!dispositivo) {
    return (
      <main className="device-details-page">
        <div className="alerts-layout">

          <div className="device-details-container">
            {/* Módulo ESP32 pendiente: estos datos son de demostración (fase de dispositivos). */}
            <div className="alert alert-warning d-flex gap-2 align-items-start" role="note">
              <i className="bi bi-hourglass-split" aria-hidden="true"></i>
              <span>
                Módulo pendiente (dispositivos ESP32, RF-20 a RF-24). Los datos de esta pantalla son de
                demostración: se guardan solo en este navegador y no se envían al servidor.
              </span>
            </div>
            <Link to="/dispositivos" className="device-details-breadcrumb">
              Mis dispositivos
            </Link>

            <div className="device-details-empty">
              <i className="bi bi-exclamation-circle"></i>
              <h2>Dispositivo no encontrado</h2>
              <p>No encontramos información del dispositivo solicitado.</p>
              <Link to="/dispositivos">Volver a dispositivos</Link>
            </div>
          </div>
        </div>
      </main>
    );
  }

  const historial = historialEjemplo[dispositivo.id] ?? [];

  return (
    <main className="device-details-page">
      <div className="alerts-layout">

        <div className="device-details-container">
          {/* Módulo ESP32 pendiente: estos datos son de demostración (fase de dispositivos). */}
          <div className="alert alert-warning d-flex gap-2 align-items-start" role="note">
            <i className="bi bi-hourglass-split" aria-hidden="true"></i>
            <span>
              Módulo pendiente (dispositivos ESP32, RF-20 a RF-24). Los datos de esta pantalla son de
              demostración: se guardan solo en este navegador y no se envían al servidor.
            </span>
          </div>
          {/* RUTA */}
          <div className="device-details-breadcrumb">
            <Link to="/dispositivos">Mis dispositivos</Link>
            <span>/ Detalle</span>
          </div>

          {/* ENCABEZADO */}
          <span className="device-details-label">
            DETALLE DISPOSITIVO
          </span>

          <div className="device-details-header">
            <div>
              <h1>{dispositivo.nombre}</h1>
              <p>
                {dispositivo.ubicacion} · Dispositivo ambiental ESP32
              </p>
            </div>

            <div className="device-details-actions">
              <button
                type="button"
                className="device-details-edit"
                onClick={abrirEdicion}
              >
                <i className="bi bi-pencil"></i>
                Editar
              </button>

              <button
                type="button"
                className="device-details-toggle"
                onClick={cambiarEstado}
              >
                {dispositivo.estado === "Conectado"
                  ? "Desactivar"
                  : "Activar"}
              </button>
            </div>
          </div>

          {mensaje && (
            <div className="device-details-success" role="status">
              <i className="bi bi-check-circle"></i>
              {mensaje}
            </div>
          )}

          {/* ESTADO */}
          <section className="device-details-status-panel">
            <div className="device-details-status-left">
              <div className="device-details-info-icon">
                <i className="bi bi-cpu"></i>
              </div>

              <div>
                <span>Estado del dispositivo</span>

                <div className="device-details-badges">
                  <span
                    className={
                      dispositivo.estado === "Conectado"
                        ? "device-details-badge active"
                        : "device-details-badge inactive"
                    }
                  >
                    {dispositivo.estado === "Conectado"
                      ? "Activo"
                      : "Inactivo"}
                  </span>

                  <span
                    className={
                      dispositivo.estado === "Conectado"
                        ? "device-details-badge connected"
                        : "device-details-badge disconnected"
                    }
                  >
                    {dispositivo.estado === "Conectado"
                      ? "Conectado"
                      : "Sin conexión"}
                  </span>
                </div>
              </div>
            </div>

            <div className="device-details-last-connection">
              <span>Última conexión registrada</span>
              <strong>{dispositivo.ultimaLectura}</strong>
            </div>
          </section>

          {/* LECTURAS */}
          <section className="device-details-section">
            <div className="device-details-section-header">
              <h2>Lectura más reciente</h2>
              <span>{dispositivo.ultimaLectura}</span>
            </div>

            <div className="device-details-grid">
              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-thermometer-half"></i>
                </div>

                <div>
                  <span>Temperatura</span>
                  <h3>
                    {dispositivo.temperatura !== null
                      ? `${dispositivo.temperatura} °C`
                      : "Sin datos"}
                  </h3>
                  <p>Sensor de temperatura</p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-droplet"></i>
                </div>

                <div>
                  <span>Humedad</span>
                  <h3>
                    {dispositivo.humedad !== null
                      ? `${dispositivo.humedad}%`
                      : "Sin datos"}
                  </h3>
                  <p>Humedad relativa</p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-cloud-rain"></i>
                </div>

                <div>
                  <span>Lluvia</span>
                  <h3>{dispositivo.lluvia ?? "Sin datos"}</h3>
                  <p>
                    {dispositivo.lluvia === "Sin lluvia"
                      ? "No detectada"
                      : dispositivo.lluvia === "Lluvia"
                      ? "Detectada"
                      : "Sin lecturas"}
                  </p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-brightness-high"></i>
                </div>

                <div>
                  <span>Luz</span>
                  <h3>
                    {dispositivo.luminosidad !== null
                      ? `${dispositivo.luminosidad} lx`
                      : "Sin datos"}
                  </h3>
                  <p>Iluminancia</p>
                </div>
              </article>
            </div>
          </section>

          {/* HISTORIAL */}
          <section className="device-details-section">
            <div className="device-details-section-header">
              <h2>Historial reciente de lecturas</h2>
            </div>

            <div className="device-details-table-wrapper">
              <table className="device-details-table">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Hora</th>
                    <th>Temperatura</th>
                    <th>Humedad</th>
                    <th>Lluvia</th>
                    <th>Luz</th>
                  </tr>
                </thead>

                <tbody>
                  {historial.length > 0 ? (
                    historial.map((lectura, index) => (
                      <tr key={index}>
                        <td>{lectura.fecha}</td>
                        <td>{lectura.hora}</td>
                        <td>{lectura.temperatura} °C</td>
                        <td>{lectura.humedad}%</td>
                        <td>{lectura.lluvia}</td>
                        <td>{lectura.luminosidad} lx</td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={6} className="device-details-no-data">
                        Este dispositivo todavía no tiene lecturas registradas.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>

              <div className="device-details-table-footer">
                {historial.length} lecturas almacenadas · Más recientes primero
              </div>
            </div>
          </section>

          {/* AVISO */}
          <div className="device-details-note">
            <i className="bi bi-info-circle"></i>
            <p>
              La conexión se determina según la última conexión registrada.
              Esta vista muestra lecturas almacenadas, no datos en vivo.
            </p>
          </div>

          <footer className="device-details-footer">
            Datos de ejemplo · ClimaPlus Fase 1 · Octubre 2026
          </footer>
        </div>
      </div>

      {/* MODAL DE EDICIÓN */}
      {mostrarEdicion && (
        <div
          className="device-details-modal-overlay"
          onClick={() => setMostrarEdicion(false)}
        >
          <div
            className="device-details-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="device-edit-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="device-details-modal-header">
              <h2 id="device-edit-title">Editar dispositivo</h2>

              <button
                type="button"
                onClick={() => setMostrarEdicion(false)}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={guardarEdicion}>
              <div className="device-details-form-group">
                <label htmlFor="edit-device-name">
                  Nombre del dispositivo
                </label>

                <input
                  id="edit-device-name"
                  type="text"
                  value={nombreEditado}
                  onChange={(e) => setNombreEditado(e.target.value)}
                  maxLength={80}
                  required
                />
              </div>

              <div className="device-details-form-group">
                <label htmlFor="edit-device-location">
                  Ubicación
                </label>

                <input
                  id="edit-device-location"
                  type="text"
                  value={ubicacionEditada}
                  onChange={(e) => setUbicacionEditada(e.target.value)}
                  maxLength={100}
                  required
                />
              </div>

              <div className="device-details-modal-actions">
                <button
                  type="button"
                  onClick={() => setMostrarEdicion(false)}
                >
                  Cancelar
                </button>

                <button type="submit">
                  Guardar cambios
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </main>
  );
}