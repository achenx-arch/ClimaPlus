
import { useState } from "react";
import { Link, NavLink } from "react-router-dom";
import "./Alerts.css";
import "./Devices.css";

const dispositivos = [
  {
    id: 1,
    nombre: "ESP32 - Estación Central",
    ubicacion: "Ciudad de Guatemala",
    estado: "Conectado",
    temperatura: 23,
    humedad: 74,
    ultimaLectura: "Hace 2 minutos",
  },
  {
    id: 2,
    nombre: "ESP32 - Estación Antigua",
    ubicacion: "Antigua Guatemala",
    estado: "Conectado",
    temperatura: 20,
    humedad: 81,
    ultimaLectura: "Hace 5 minutos",
  },
  {
    id: 3,
    nombre: "ESP32 - Estación Occidente",
    ubicacion: "Quetzaltenango",
    estado: "Desconectado",
    temperatura: 16,
    humedad: 78,
    ultimaLectura: "Hace 3 horas",
  },
];

export default function Devices() {
  const [dispositivoSeleccionado, setDispositivoSeleccionado] =
    useState<number | null>(null);

  const conectados = dispositivos.filter(
    (dispositivo) => dispositivo.estado === "Conectado"
  ).length;

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

        {/* CONTENIDO PRINCIPAL */}
        <div className="devices-container">
          <span className="devices-label">DISPOSITIVOS IOT</span>

          <div className="devices-header">
            <div>
              <h1>Dispositivos ESP32</h1>
              <p>
                Monitorea los dispositivos y sensores meteorológicos
                conectados a ClimaPlus.
              </p>
            </div>
          </div>

          {/* RESUMEN */}
          <div className="devices-summary">
            <div className="devices-summary-card">
              <i className="bi bi-cpu"></i>
              <div>
                <strong>{dispositivos.length}</strong>
                <span>Total de dispositivos</span>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-wifi"></i>
              <div>
                <strong>{conectados}</strong>
                <span>Conectados</span>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-wifi-off"></i>
              <div>
                <strong>{dispositivos.length - conectados}</strong>
                <span>Desconectados</span>
              </div>
            </div>
          </div>

          {/* DISPOSITIVOS */}
          <section className="devices-section">
            <div className="devices-section-header">
              <h2>Mis dispositivos</h2>
              <span>Datos de demostración</span>
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

                  <div className="devices-readings">
                    <div>
                      <i className="bi bi-thermometer-half"></i>
                      <strong>{dispositivo.temperatura}°C</strong>
                      <span>Temperatura</span>
                    </div>

                    <div>
                      <i className="bi bi-droplet"></i>
                      <strong>{dispositivo.humedad}%</strong>
                      <span>Humedad</span>
                    </div>
                  </div>

                  <p className="devices-last-reading">
                    <i className="bi bi-clock"></i>
                    Última lectura: {dispositivo.ultimaLectura}
                  </p>

                  <Link
                    to={`/dispositivos/${dispositivo.id}`}
                    className="devices-details-button"
                  >
                    Ver detalles
                    <i className="bi bi-arrow-right"></i>
                  </Link>
                </article>
              ))}
            </div>
          </section>
        </div>
      </div>
    </main>
  );
}