import { Link, useParams } from "react-router-dom";
import "./DeviceDetails.css";

type Dispositivo = {
  id: string;
  nombre: string;
  ubicacion: string;
  conectado: boolean;
  temperatura: number;
  humedad: number;
  ultimaLectura: string;
  identificador: string;
};

const dispositivos: Dispositivo[] = [
  {
    id: "1",
    nombre: "ESP32 - Estación Central",
    ubicacion: "Ciudad de Guatemala",
    conectado: true,
    temperatura: 23,
    humedad: 74,
    ultimaLectura: "Hace 2 minutos",
    identificador: "ESP32-001",
  },
  {
    id: "2",
    nombre: "ESP32 - Estación Antigua",
    ubicacion: "Antigua Guatemala",
    conectado: true,
    temperatura: 20,
    humedad: 81,
    ultimaLectura: "Hace 5 minutos",
    identificador: "ESP32-002",
  },
  {
    id: "3",
    nombre: "ESP32 - Estación Occidente",
    ubicacion: "Quetzaltenango",
    conectado: false,
    temperatura: 16,
    humedad: 78,
    ultimaLectura: "Hace 3 horas",
    identificador: "ESP32-003",
  },
];

export default function DeviceDetails() {
  const { id } = useParams<{ id: string }>();

  const dispositivo = dispositivos.find(
    (item) => item.id === id
  );

  if (!dispositivo) {
    return (
      <main className="device-details-page">
        <div className="device-details-container">
          <Link to="/dispositivos" className="device-details-back">
            <i className="bi bi-arrow-left"></i>
            Volver a dispositivos
          </Link>

          <div className="device-details-empty">
            <i className="bi bi-exclamation-circle"></i>
            <h2>Dispositivo no encontrado</h2>
            <p>No encontramos información del dispositivo solicitado.</p>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="device-details-page">
      <div className="device-details-container">

        <Link to="/dispositivos" className="device-details-back">
          <i className="bi bi-arrow-left"></i>
          Volver a dispositivos
        </Link>

        <span className="device-details-label">
          DISPOSITIVOS IOT
        </span>

        <div className="device-details-header">
          <div>
            <h1>Detalles del dispositivo</h1>
            <p>
              Consulta la información y las lecturas de tu estación
              meteorológica.
            </p>
          </div>

          <span
            className={`device-details-status ${
              dispositivo.conectado ? "connected" : "disconnected"
            }`}
          >
            <i
              className={`bi ${
                dispositivo.conectado
                  ? "bi-wifi"
                  : "bi-wifi-off"
              }`}
            ></i>

            {dispositivo.conectado
              ? "Conectado"
              : "Desconectado"}
          </span>
        </div>

        <section className="device-details-info">
          <div className="device-details-info-icon">
            <i className="bi bi-cpu"></i>
          </div>

          <div>
            <h2>{dispositivo.nombre}</h2>

            <p>
              <i className="bi bi-geo-alt"></i>
              {dispositivo.ubicacion}
            </p>

            <span>
              Identificador: {dispositivo.identificador}
            </span>
          </div>
        </section>

        <section className="device-details-section">
          <div className="device-details-section-header">
            <div>
              <h2>Lecturas de sensores</h2>
              <p>
                Últimos valores registrados por el dispositivo.
              </p>
            </div>

            <span className="device-details-demo">
              Datos de demostración
            </span>
          </div>

          <div className="device-details-grid">

            <article className="device-details-sensor">
              <div className="device-details-sensor-icon">
                <i className="bi bi-thermometer-half"></i>
              </div>

              <span>Temperatura</span>

              <h3>{dispositivo.temperatura}°C</h3>

              <p>
                <i className="bi bi-thermometer"></i>
                Temperatura ambiental
              </p>
            </article>

            <article className="device-details-sensor">
              <div className="device-details-sensor-icon">
                <i className="bi bi-droplet"></i>
              </div>

              <span>Humedad relativa</span>

              <h3>{dispositivo.humedad}%</h3>

              <p>
                <i className="bi bi-moisture"></i>
                Humedad del ambiente
              </p>
            </article>

          </div>
        </section>

        <section className="device-details-section">
          <h2>Información del dispositivo</h2>

          <div className="device-details-data">

            <div className="device-details-data-row">
              <span>
                <i className="bi bi-cpu"></i>
                Identificador
              </span>

              <strong>{dispositivo.identificador}</strong>
            </div>

            <div className="device-details-data-row">
              <span>
                <i className="bi bi-geo-alt"></i>
                Ubicación
              </span>

              <strong>{dispositivo.ubicacion}</strong>
            </div>

            <div className="device-details-data-row">
              <span>
                <i className="bi bi-wifi"></i>
                Estado
              </span>

              <strong>
                {dispositivo.conectado
                  ? "Conectado"
                  : "Desconectado"}
              </strong>
            </div>

            <div className="device-details-data-row">
              <span>
                <i className="bi bi-clock-history"></i>
                Última lectura
              </span>

              <strong>{dispositivo.ultimaLectura}</strong>
            </div>

          </div>
        </section>

        <div className="device-details-note">
          <i className="bi bi-info-circle"></i>

          <p>
            Los valores mostrados son de demostración.
            Las lecturas reales estarán disponibles cuando
            se conecten los dispositivos ESP32 al sistema.
          </p>
        </div>

      </div>
    </main>
  );
}