import { useState } from "react";
import { Link, NavLink } from "react-router-dom";
import "./Alerts.css";
import "./Devices.css";

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

const STORAGE_KEY = "climaplus_dispositivos";

export default function Devices() {
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

  const [dispositivoSeleccionado, setDispositivoSeleccionado] =
    useState<number | null>(null);

  const [mostrarFormulario, setMostrarFormulario] = useState(false);

  const [nombre, setNombre] = useState("");
  const [ubicacion, setUbicacion] = useState("");
  const [identificador, setIdentificador] = useState("");

  const [error, setError] = useState("");
  const [mensaje, setMensaje] = useState("");

  const conectados = dispositivos.filter(
    (dispositivo) => dispositivo.estado === "Conectado"
  ).length;

  const desconectados = dispositivos.length - conectados;

  const alertasNuevas = (() => {
    try {
      const guardadas = localStorage.getItem("climaplus_alertas");

      if (!guardadas) return 2;

      const datos = JSON.parse(guardadas);

      if (!Array.isArray(datos)) return 2;

      return datos.filter((alerta) => !alerta.leida).length;
    } catch {
      return 2;
    }
  })();

  const guardarDispositivos = (actualizados: Dispositivo[]) => {
    setDispositivos(actualizados);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(actualizados));
  };

  const cerrarFormulario = () => {
    setMostrarFormulario(false);
    setNombre("");
    setUbicacion("");
    setIdentificador("");
    setError("");
  };

  const registrarDispositivo = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setError("");
    setMensaje("");

    const nombreLimpio = nombre.trim();
    const ubicacionLimpia = ubicacion.trim();
    const identificadorLimpio = identificador.trim().toUpperCase();

    if (!nombreLimpio || !ubicacionLimpia || !identificadorLimpio) {
      setError("Todos los campos son obligatorios.");
      return;
    }

    const identificadorExiste = dispositivos.some(
      (dispositivo) =>
        dispositivo.identificador.toUpperCase() === identificadorLimpio
    );

    if (identificadorExiste) {
      setError("Ya existe un dispositivo con ese identificador.");
      return;
    }

    const nuevoDispositivo: Dispositivo = {
      id: Math.max(0, ...dispositivos.map((d) => d.id)) + 1,
      identificador: identificadorLimpio,
      nombre: nombreLimpio,
      ubicacion: ubicacionLimpia,
      estado: "Desconectado",
      temperatura: null,
      humedad: null,
      lluvia: null,
      luminosidad: null,
      ultimaLectura: "Sin lecturas registradas",
    };

    guardarDispositivos([...dispositivos, nuevoDispositivo]);

    setMensaje("Dispositivo registrado correctamente.");
    cerrarFormulario();
  };

  return (
    <main className="devices-page">
      <div className="alerts-layout">
        {/* MENÚ LATERAL */}
        <aside className="alerts-sidebar">
          <span className="alerts-sidebar-title">MI ESPACIO</span>

          <nav className="alerts-sidebar-nav">
            <NavLink to="/dashboard">
              <i className="bi bi-grid"></i>
              Dashboard
            </NavLink>

            <NavLink to="/clima">
              <i className="bi bi-cloud-sun"></i>
              Clima
            </NavLink>

            <NavLink to="/pronostico">
              <i className="bi bi-calendar3"></i>
              Pronóstico
            </NavLink>

            <NavLink to="/favoritos">
              <i className="bi bi-star"></i>
              Favoritos
            </NavLink>

            <NavLink to="/historial">
              <i className="bi bi-clock-history"></i>
              Historial
            </NavLink>

            <NavLink to="/alertas">
              <i className="bi bi-bell"></i>
              Alertas
              {alertasNuevas > 0 && (
                <span className="alerts-sidebar-count">
                  {alertasNuevas}
                </span>
              )}
            </NavLink>

            <NavLink to="/dispositivos">
              <i className="bi bi-display"></i>
              Mis dispositivos
            </NavLink>
          </nav>

          <div className="alerts-sidebar-bottom">
            <NavLink to="/perfil">
              <i className="bi bi-person"></i>
              Perfil
            </NavLink>

            <button
              type="button"
              disabled
              title="Disponible al conectar el inicio de sesión"
            >
              <i className="bi bi-box-arrow-right"></i>
              Cerrar sesión
            </button>
          </div>
        </aside>

        {/* CONTENIDO */}
        <div className="devices-container">
          <span className="devices-label">DISPOSITIVOS</span>

          <div className="devices-header">
            <div>
              <h1>Mis dispositivos</h1>
              <p>
                Consulta los dispositivos ambientales vinculados a tu cuenta.
              </p>
            </div>

            <button
              type="button"
              className="devices-register-button"
              onClick={() => {
                setMensaje("");
                setMostrarFormulario(true);
              }}
            >
              <i className="bi bi-plus-lg"></i>
              Registrar dispositivo
            </button>
          </div>

          {mensaje && (
            <div className="devices-success" role="status">
              <i className="bi bi-check-circle"></i>
              {mensaje}
            </div>
          )}

          {/* RESUMEN */}
          <section className="devices-summary">
            <div className="devices-summary-card">
              <i className="bi bi-pc-display"></i>
              <div>
                <span>Total dispositivos</span>
                <strong>{dispositivos.length}</strong>
                <small>Vinculados a tu cuenta</small>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-cpu"></i>
              <div>
                <span>Activos</span>
                <strong>{conectados}</strong>
                <small>Dispositivos conectados</small>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-wifi-off"></i>
              <div>
                <span>Sin conexión</span>
                <strong>{desconectados}</strong>
                <small>Según última conexión</small>
              </div>
            </div>
          </section>

          <div className="devices-info">
            <i className="bi bi-info-circle"></i>
            La conexión se deriva de la última conexión registrada;
            no muestra funcionamiento en vivo. Las lecturas corresponden
            a datos almacenados.
          </div>

          {/* TARJETAS */}
          <section className="devices-section">
            <div className="devices-section-header">
              <h2>Tus dispositivos ESP32</h2>
              <span>{dispositivos.length} dispositivos registrados</span>
            </div>

            <div className="devices-grid">
              {dispositivos.map((dispositivo) => (
                <article
                  key={dispositivo.id}
                  className={`devices-card ${
                    dispositivoSeleccionado === dispositivo.id
                      ? "selected"
                      : ""
                  }`}
                  onClick={() =>
                    setDispositivoSeleccionado(dispositivo.id)
                  }
                >
                  <div className="devices-card-header">
                    <div className="devices-card-icon">
                      <i className="bi bi-cpu"></i>
                    </div>

                    <span
                      className={`devices-status ${
                        dispositivo.estado === "Conectado"
                          ? "connected"
                          : "disconnected"
                      }`}
                    >
                      {dispositivo.estado}
                    </span>
                  </div>

                  <h3>{dispositivo.nombre}</h3>

                  <p className="devices-location">
                    <i className="bi bi-geo-alt"></i>
                    {dispositivo.ubicacion}
                  </p>

                  <div className="devices-identifier">
                    {dispositivo.identificador}
                  </div>

                  <p className="devices-last-reading">
                    <i className="bi bi-clock"></i>
                    {dispositivo.ultimaLectura}
                  </p>

                  <div className="devices-readings-title">
                    Lectura más reciente
                  </div>

                  <div className="devices-readings">
                    <div>
                      <i className="bi bi-thermometer-half"></i>
                      <span>Temperatura</span>
                      <strong>
                        {dispositivo.temperatura !== null
                          ? `${dispositivo.temperatura} °C`
                          : "Sin datos"}
                      </strong>
                    </div>

                    <div>
                      <i className="bi bi-droplet"></i>
                      <span>Humedad</span>
                      <strong>
                        {dispositivo.humedad !== null
                          ? `${dispositivo.humedad}%`
                          : "Sin datos"}
                      </strong>
                    </div>

                    <div>
                      <i className="bi bi-cloud-rain"></i>
                      <span>Lluvia</span>
                      <strong>{dispositivo.lluvia ?? "Sin datos"}</strong>
                    </div>

                    <div>
                      <i className="bi bi-brightness-high"></i>
                      <span>Luz</span>
                      <strong>
                        {dispositivo.luminosidad !== null
                          ? `${dispositivo.luminosidad} lx`
                          : "Sin datos"}
                      </strong>
                    </div>
                  </div>

                  <div className="devices-card-actions">
                    <Link
                      to={`/dispositivos/${dispositivo.id}`}
                      className="devices-details-button"
                    >
                      Ver dispositivo
                    </Link>

                    <Link
  to={`/dispositivos/${dispositivo.id}`}
  className="devices-edit-button"
  onClick={(e) => e.stopPropagation()}
>
  <i className="bi bi-pencil"></i>
  Editar
</Link>
                  </div>
                </article>
              ))}
            </div>
          </section>

          <p className="devices-note">
            La clave del dispositivo se entregará al registrarlo mediante
            el backend. No se muestra ninguna clave API en esta vista.
          </p>

          <footer className="devices-footer">
            Datos de ejemplo · ClimaPlus Fase 1 · Octubre 2026
          </footer>
        </div>
      </div>

      {/* MODAL DE REGISTRO */}
      {mostrarFormulario && (
        <div
          className="devices-modal-overlay"
          onClick={cerrarFormulario}
        >
          <div
            className="devices-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="devices-modal-title"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="devices-modal-header">
              <div>
                <h2 id="devices-modal-title">Registrar dispositivo</h2>
                <p>Agrega un nuevo dispositivo ESP32 a tu cuenta.</p>
              </div>

              <button
                type="button"
                className="devices-modal-close"
                onClick={cerrarFormulario}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={registrarDispositivo}>
              <div className="devices-form-group">
                <label htmlFor="device-name">
                  Nombre del dispositivo
                </label>
                <input
                  id="device-name"
                  type="text"
                  value={nombre}
                  onChange={(e) => setNombre(e.target.value)}
                  placeholder="Ej. Sensor de terraza"
                  maxLength={80}
                  required
                />
              </div>

              <div className="devices-form-group">
                <label htmlFor="device-location">Ubicación</label>
                <input
                  id="device-location"
                  type="text"
                  value={ubicacion}
                  onChange={(e) => setUbicacion(e.target.value)}
                  placeholder="Ej. Ciudad de Guatemala"
                  maxLength={100}
                  required
                />
              </div>

              <div className="devices-form-group">
                <label htmlFor="device-id">
                  Identificador del ESP32
                </label>
                <input
                  id="device-id"
                  type="text"
                  value={identificador}
                  onChange={(e) => setIdentificador(e.target.value)}
                  placeholder="Ej. ESP32-004"
                  maxLength={40}
                  required
                />
              </div>

              {error && (
                <div className="devices-form-error" role="alert">
                  {error}
                </div>
              )}

              <div className="devices-modal-actions">
                <button
                  type="button"
                  className="devices-cancel-button"
                  onClick={cerrarFormulario}
                >
                  Cancelar
                </button>

                <button
                  type="submit"
                  className="devices-save-button"
                >
                  <i className="bi bi-check2"></i>
                  Registrar
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </main>
  );
}