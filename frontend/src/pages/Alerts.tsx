import { useState } from "react";
import { Link, NavLink } from "react-router-dom";
import "./Alerts.css";

type Alerta = {
  id: number;
  titulo: string;
  ubicacion: string;
  descripcion: string;
  detalle: string;
  fecha: string;
  icono: string;
  tipo: "lluvia" | "calor" | "frio";
  leida: boolean;
};

const alertasIniciales: Alerta[] = [
  {
    id: 1,
    titulo: "Lluvia",
    ubicacion: "Antigua Guatemala",
    descripcion:
      "Se detectó lluvia en tu ubicación. Toma precauciones antes de salir.",
    detalle: "Detección: lluvia presente · Sin umbral numérico",
    fecha: "7 oct. 2026 - 08:20",
    icono: "bi-cloud-rain",
    tipo: "lluvia",
    leida: false,
  },
  {
    id: 2,
    titulo: "Temperatura alta",
    ubicacion: "Ciudad de Guatemala",
    descripcion:
      "La temperatura detectada supera el umbral de tu configuración.",
    detalle: "Detectada: 32 °C · Umbral: 30 °C",
    fecha: "6 oct. 2026 - 13:30",
    icono: "bi-thermometer-high",
    tipo: "calor",
    leida: false,
  },
  {
    id: 3,
    titulo: "Temperatura baja",
    ubicacion: "Quetzaltenango",
    descripcion:
      "La temperatura detectada es inferior al umbral configurado. Lleva abrigo.",
    detalle: "Detectada: 12 °C · Umbral: 14 °C",
    fecha: "6 oct. 2026 - 18:00",
    icono: "bi-thermometer-low",
    tipo: "frio",
    leida: true,
  },
];

type Filtro = "todas" | "nuevas" | "leidas";

export default function Alerts() {
  const [alertas, setAlertas] = useState<Alerta[]>(() => {
    try {
      const guardadas = localStorage.getItem("climaplus_alertas");

      if (guardadas) {
        const datos = JSON.parse(guardadas);

        if (Array.isArray(datos)) {
          return datos;
        }
      }
    } catch {
      console.error("No se pudieron cargar las alertas");
    }

    return alertasIniciales;
  });

  const [filtro, setFiltro] = useState<Filtro>("todas");
  const [detalleAbierto, setDetalleAbierto] = useState<number | null>(
    null
  );

  const nuevas = alertas.filter((alerta) => !alerta.leida).length;
  const leidas = alertas.filter((alerta) => alerta.leida).length;

  const alertasFiltradas = alertas.filter((alerta) => {
    if (filtro === "nuevas") return !alerta.leida;
    if (filtro === "leidas") return alerta.leida;
    return true;
  });

  const marcarComoLeida = (id: number) => {
    const actualizadas = alertas.map((alerta) =>
      alerta.id === id ? { ...alerta, leida: true } : alerta
    );

    setAlertas(actualizadas);

    localStorage.setItem(
      "climaplus_alertas",
      JSON.stringify(actualizadas)
    );
  };

  return (
    <main className="alerts-page">
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
              {nuevas > 0 && (
                <span className="alerts-sidebar-count">{nuevas}</span>
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
        <div className="alerts-container">
          <span className="alerts-label">ALERTAS</span>

          <div className="alerts-header">
            <div>
              <h1>Mis alertas</h1>
              <p>
                Consulta las alertas ambientales detectadas para tus
                ubicaciones.
              </p>
            </div>

            <Link
              to="/configuracion-alertas"
              className="alerts-settings"
            >
              <i className="bi bi-sliders"></i>
              Configurar alertas
            </Link>
          </div>

          {/* RESUMEN */}
          <section className="alerts-summary">
            <article className="alerts-summary-card">
              <div className="alerts-summary-icon new">
                <i className="bi bi-bell"></i>
              </div>

              <div>
                <span>Alertas nuevas</span>
                <strong>{nuevas}</strong>
                <small>Pendientes de leer</small>
              </div>
            </article>

            <article className="alerts-summary-card">
              <div className="alerts-summary-icon read">
                <i className="bi bi-check2-all"></i>
              </div>

              <div>
                <span>Alertas leídas</span>
                <strong>{leidas}</strong>
                <small>Revisadas por ti</small>
              </div>
            </article>

            <article className="alerts-summary-card">
              <div className="alerts-summary-icon settings">
                <i className="bi bi-sliders"></i>
              </div>

              <div>
                <span>Configuraciones activas</span>
                <strong>3</strong>
                <small>Reglas habilitadas (demo)</small>
              </div>
            </article>
          </section>

          {/* FILTROS */}
          <div className="alerts-filters">
            <div className="alerts-filter-buttons">
              <button
                type="button"
                className={filtro === "todas" ? "active" : ""}
                onClick={() => setFiltro("todas")}
              >
                Todas
              </button>

              <button
                type="button"
                className={filtro === "nuevas" ? "active" : ""}
                onClick={() => setFiltro("nuevas")}
              >
                Nuevas
              </button>

              <button
                type="button"
                className={filtro === "leidas" ? "active" : ""}
                onClick={() => setFiltro("leidas")}
              >
                Leídas
              </button>
            </div>

            <span>
              {alertasFiltradas.length}{" "}
              {alertasFiltradas.length === 1 ? "alerta" : "alertas"}
            </span>
          </div>

          {/* TARJETAS */}
          <section className="alerts-grid">
            {alertasFiltradas.map((alerta) => (
              <article className="alerts-card" key={alerta.id}>
                <div className="alerts-card-top">
                  <div className={`alerts-card-icon ${alerta.tipo}`}>
                    <i className={`bi ${alerta.icono}`}></i>
                  </div>

                  <span
                    className={`alerts-badge ${
                      alerta.leida ? "read" : "new"
                    }`}
                  >
                    {alerta.leida ? "LEÍDA" : "NUEVA"}
                  </span>
                </div>

                <h3>{alerta.titulo}</h3>
                <strong className="alerts-card-location">
                  {alerta.ubicacion}
                </strong>

                <p>{alerta.descripcion}</p>

                <div className="alerts-card-detail">
                  {alerta.detalle}
                </div>

                <div className="alerts-card-date">
                  <span>Fecha y hora de detección</span>
                  <small>{alerta.fecha}</small>
                </div>

                <div className="alerts-card-actions">
                  {!alerta.leida && (
                    <button
                      type="button"
                      onClick={() => marcarComoLeida(alerta.id)}
                    >
                      <i className="bi bi-check2"></i>
                      Marcar como leída
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() =>
                      setDetalleAbierto(
                        detalleAbierto === alerta.id
                          ? null
                          : alerta.id
                      )
                    }
                  >
                    <i className="bi bi-chevron-right"></i>
                    {detalleAbierto === alerta.id
                      ? "Ocultar detalle"
                      : "Ver detalle"}
                  </button>
                </div>

                {detalleAbierto === alerta.id && (
                  <div className="alerts-expanded-detail">
                    <strong>Detalle de la alerta</strong>
                    <p>{alerta.descripcion}</p>
                    <p>{alerta.detalle}</p>
                    <small>Ubicación: {alerta.ubicacion}</small>
                  </div>
                )}
              </article>
            ))}
          </section>

          {alertasFiltradas.length === 0 && (
            <div className="alerts-empty">
              <i className="bi bi-bell-slash"></i>
              <h3>No hay alertas en esta categoría</h3>
              <p>Prueba seleccionando otro filtro.</p>
            </div>
          )}

          <footer className="alerts-footer">
            Datos de ejemplo · ClimaPlus Fase 1 · Octubre 2026
          </footer>
        </div>
      </div>
    </main>
  );
}