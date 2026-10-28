import { NavLink } from "react-router-dom";
import "./Dashboard.css";

export default function Dashboard() {
  return (
    <main className="dashboard-page">
      <div className="dashboard-layout">

        {/* MENÚ LATERAL */}
        <aside className="dashboard-sidebar">
          <span className="dashboard-sidebar-title">
            MI ESPACIO
          </span>

          <nav className="dashboard-sidebar-nav">
            <NavLink to="/dashboard" end>
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
            </NavLink>

            <NavLink to="/dispositivos">
              <i className="bi bi-display"></i>
              Mis dispositivos
            </NavLink>
          </nav>

          <div className="dashboard-sidebar-bottom">
            <NavLink to="/perfil">
              <i className="bi bi-person"></i>
              Perfil
            </NavLink>

            <button type="button" disabled title="Disponible al conectar el inicio de sesión">
              <i className="bi bi-box-arrow-right"></i>
              Cerrar sesión
            </button>
          </div>
        </aside>

        {/* CONTENIDO PRINCIPAL */}
        <div className="dashboard-container">
          <span className="dashboard-label">
            DASHBOARD
          </span>

          <h1>Hola, Usuario</h1>

          <p className="dashboard-description">
            Tus ubicaciones, últimas consultas y alertas en un solo lugar.
          </p>

          {/* TARJETAS DE RESUMEN */}
          <section className="dashboard-summary">

            <article className="dashboard-summary-card">
              <div className="dashboard-summary-icon">
                <i className="bi bi-star"></i>
              </div>

              <div>
                <span>Favoritos</span>
                <strong>3</strong>
                <small>Ubicaciones guardadas</small>
              </div>
            </article>

            <article className="dashboard-summary-card">
              <div className="dashboard-summary-icon">
                <i className="bi bi-bell"></i>
              </div>

              <div>
                <span>Alertas nuevas</span>
                <strong>2</strong>
                <small>Pendientes de leer</small>
              </div>
            </article>

            <article className="dashboard-summary-card">
              <div className="dashboard-summary-icon">
                <i className="bi bi-display"></i>
              </div>

              <div>
                <span>Dispositivos activos</span>
                <strong>2</strong>
                <small>Vinculados a tu cuenta</small>
              </div>
            </article>

          </section>
          {/* UBICACIONES FAVORITAS */}
<section className="dashboard-favorites">
  <div className="dashboard-section-header">
    <h2>Mis ubicaciones favoritas</h2>

    <button type="button" className="dashboard-refresh" disabled>
      <i className="bi bi-arrow-clockwise"></i>
      Actualizar clima
    </button>
  </div>

<div className="dashboard-info">
  <i className="bi bi-info-circle"></i>
  <span>
    Se muestran datos de demostración. La actualización estará
    disponible al conectar el servicio meteorológico.
  </span>
</div>
  {/* TARJETAS DE UBICACIONES FAVORITAS */}
<div className="dashboard-weather-grid">

  {/* CIUDAD DE GUATEMALA */}
  <article className="dashboard-weather-card">
    <div className="dashboard-weather-header">
      <div>
        <h3>Ciudad de Guatemala</h3>
        <span>Guatemala, GT</span>
      </div>
      <i className="bi bi-star-fill"></i>
    </div>

    <div className="dashboard-weather-main">
      <div>
        <strong>22°</strong>
        <p>Parcialmente nublado</p>
      </div>
      <i className="bi bi-cloud-sun"></i>
    </div>

    <div className="dashboard-weather-details">
      <span>
        <i className="bi bi-droplet-half"></i> 74% humedad
      </span>
      <span>
        <i className="bi bi-wind"></i> 12 km/h
      </span>
    </div>

    <div className="dashboard-weather-updated">
      <i className="bi bi-clock"></i>
      <span>Última consulta almacenada: 7 oct. 2026 - 08:30 a. m.</span>
    </div>
  </article>

  {/* ANTIGUA GUATEMALA */}
  <article className="dashboard-weather-card">
    <div className="dashboard-weather-header">
      <div>
        <h3>Antigua Guatemala</h3>
        <span>Sacatepéquez, GT</span>
      </div>
      <i className="bi bi-star"></i>
    </div>

    <div className="dashboard-weather-main">
      <div>
        <strong>20°</strong>
        <p>Lluvia ligera</p>
      </div>
      <i className="bi bi-cloud-drizzle"></i>
    </div>

    <div className="dashboard-weather-details">
      <span>
        <i className="bi bi-droplet-half"></i> 81% humedad
      </span>
      <span>
        <i className="bi bi-wind"></i> 8 km/h
      </span>
    </div>

    <div className="dashboard-weather-updated">
      <i className="bi bi-clock"></i>
      <span>Última consulta almacenada: 6 oct. 2026 - 05:15 p. m.</span>
    </div>
  </article>

  {/* QUETZALTENANGO */}
  <article className="dashboard-weather-card">
    <div className="dashboard-weather-header">
      <div>
        <h3>Quetzaltenango</h3>
        <span>Quetzaltenango, GT</span>
      </div>
      <i className="bi bi-star"></i>
    </div>

    <div className="dashboard-weather-main">
      <div>
        <strong>16°</strong>
        <p>Nublado</p>
      </div>
      <i className="bi bi-cloud"></i>
    </div>

    <div className="dashboard-weather-details">
      <span>
        <i className="bi bi-droplet-half"></i> 78% humedad
      </span>
      <span>
        <i className="bi bi-wind"></i> 10 km/h
      </span>
    </div>

    <div className="dashboard-weather-updated">
      <i className="bi bi-clock"></i>
      <span>Última consulta almacenada: 6 oct. 2026 - 06:40 a. m.</span>
    </div>
  </article>

</div>
</section>
{/* ========================================
    ALERTAS RECIENTES
======================================== */}

<section className="dashboard-alerts">

  <div className="dashboard-alerts-header">
    <h2>Alertas recientes</h2>
    <a href="/alertas">
      Ver todas las alertas
      <i className="bi bi-chevron-right"></i>
    </a>
  </div>

  <div className="dashboard-alerts-grid">

    {/* ALERTA 1 */}
    <article className="dashboard-alert-card">
      <div className="dashboard-alert-top">
        <div className="dashboard-alert-icon rain">
          <i className="bi bi-cloud-rain"></i>
        </div>
        <span className="dashboard-alert-badge new">Nueva</span>
      </div>

      <h3>Lluvia intensa</h3>
      <strong>Antigua Guatemala</strong>

      <p>
        Posibles acumulaciones de agua. Evita zonas propensas
        a inundaciones.
      </p>

      <small>7 oct. 2026 - 08:20 a. m.</small>
    </article>

    {/* ALERTA 2 */}
    <article className="dashboard-alert-card">
      <div className="dashboard-alert-top">
        <div className="dashboard-alert-icon heat">
          <i className="bi bi-sun"></i>
        </div>
        <span className="dashboard-alert-badge new">Nueva</span>
      </div>

      <h3>Temperatura alta</h3>
      <strong>Ciudad de Guatemala</strong>

      <p>
        Temperatura superior al umbral configurado.
        Valor detectado: 34 °C - Umbral: 32 °C.
      </p>

      <small>7 oct. 2026 - 08:00 a. m.</small>
    </article>

    {/* ALERTA 3 */}
    <article className="dashboard-alert-card">
      <div className="dashboard-alert-top">
        <div className="dashboard-alert-icon cold">
          <i className="bi bi-thermometer-low"></i>
        </div>
        <span className="dashboard-alert-badge read">Leída</span>
      </div>

      <h3>Descenso de temperatura</h3>
      <strong>Quetzaltenango</strong>

      <p>
        Se esperan temperaturas bajas durante la noche.
        Lleva abrigo.
      </p>

      <small>6 oct. 2026 - 06:00 p. m.</small>
    </article>

  </div>
</section>
        </div>
      </div>
    </main>
  );
}