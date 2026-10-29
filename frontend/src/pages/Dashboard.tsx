import { useState } from "react";
import { Link } from "react-router-dom";
import "./Dashboard.css";
import { claveSesion, useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useConsulta } from "../hooks/useConsulta";
import { mensajeDeError } from "../services/apiClient";
import { listarAlertas } from "../services/alertasService";
import { obtenerClimaActual } from "../services/climaService";
import { listarFavoritos } from "../services/favoritosService";
import type { ClimaActual } from "../types/api";
import {
  CLASE_TIPO_ALERTA,
  ICONOS_TIPO_ALERTA,
  NOMBRES_TIPO_ALERTA,
  capitalizar,
  grados,
  humedad,
  iconoCondicion,
  lugar,
  viento,
} from "../utils/clima";
import { fechaHora } from "../utils/fechas";

/** Clases de Dashboard.css para el icono de cada tipo de alerta. */
const CLASE_ICONO_DASHBOARD = { lluvia: "rain", calor: "heat", frio: "cold" } as const;

// M06 – Dashboard: GET /api/favoritos (con la última consulta de cada uno), GET /api/alertas/contador
// y GET /api/alertas (las 3 más recientes). Los dispositivos ESP32 llegan en otra fase.
export default function Dashboard() {
  const { usuario } = useAuth();
  const { nuevas, notificarAlertasGeneradas, avisar } = useNotificaciones();
  const [actualizando, setActualizando] = useState(false);
  const [climaReciente, setClimaReciente] = useState<Record<number, ClimaActual>>({});

  const favoritos = useConsulta(`favoritos:${usuario?.idUsuario}`, (signal) => listarFavoritos(signal));
  const alertas = useConsulta(`alertas-recientes:${usuario?.idUsuario}:${nuevas}`, (signal) => listarAlertas({ pagina: 1 }, signal));
  const listaFavoritos = favoritos.datos ?? [];
  const recientes = (alertas.datos?.elementos ?? []).slice(0, 3);

  const actualizarClima = async () => {
    setActualizando(true);
    let fallidas = 0;
    for (const favorito of listaFavoritos) {
      try {
        const clima = await obtenerClimaActual(favorito.ubicacion.idUbicacion, claveSesion(usuario));
        setClimaReciente((actual) => ({ ...actual, [favorito.ubicacion.idUbicacion]: clima }));
        notificarAlertasGeneradas(clima.alertasGeneradas);
      } catch {
        fallidas++;
      }
    }
    setActualizando(false);
    favoritos.recargar();
    if (fallidas > 0) avisar(`No se pudo actualizar el clima de ${fallidas} ubicación(es).`, "error");
  };

  return (
    <main className="dashboard-page">
      <div className="dashboard-layout">


        {/* CONTENIDO PRINCIPAL */}
        <div className="dashboard-container">
          <span className="dashboard-label">
            DASHBOARD
          </span>

          <h1>Hola, {usuario?.nombre ?? "Usuario"}</h1>

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
                <strong>{favoritos.datos ? listaFavoritos.length : "—"}</strong>
                <small>Ubicaciones guardadas</small>
              </div>
            </article>

            <article className="dashboard-summary-card">
              <div className="dashboard-summary-icon">
                <i className="bi bi-bell"></i>
              </div>

              <div>
                <span>Alertas nuevas</span>
                <strong>{nuevas}</strong>
                <small>Pendientes de leer</small>
              </div>
            </article>

            <article className="dashboard-summary-card">
              <div className="dashboard-summary-icon">
                <i className="bi bi-display"></i>
              </div>

              <div>
                <span>Dispositivos activos</span>
                <strong>—</strong>
                <small>Disponible en la fase de dispositivos ESP32</small>
              </div>
            </article>

          </section>
          {/* UBICACIONES FAVORITAS */}
<section className="dashboard-favorites">
  <div className="dashboard-section-header">
    <h2>Mis ubicaciones favoritas</h2>

    <button
      type="button"
      className="dashboard-refresh"
      onClick={actualizarClima}
      disabled={actualizando || listaFavoritos.length === 0}
    >
      <i className="bi bi-arrow-clockwise"></i>
      {actualizando ? "Actualizando..." : "Actualizar clima"}
    </button>
  </div>

<div className="dashboard-info">
  <i className="bi bi-info-circle"></i>
  <span>
    Para cada favorito se muestra su última consulta registrada.
    "Actualizar clima" consulta las condiciones actuales.
  </span>
</div>

  {favoritos.error !== undefined && (
    <div className="alert alert-danger" role="alert">
      {mensajeDeError(favoritos.error, "No se pudieron cargar tus favoritos.")}
    </div>
  )}

  {favoritos.datos !== undefined && listaFavoritos.length === 0 && (
    <p className="text-secondary">
      Aún no tienes ubicaciones favoritas. <Link to="/favoritos">Agregar favoritos</Link>
    </p>
  )}

  {/* TARJETAS DE UBICACIONES FAVORITAS */}
<div className="dashboard-weather-grid">
  {listaFavoritos.map((favorito) => {
    const reciente = climaReciente[favorito.ubicacion.idUbicacion];
    const ultima = favorito.ultimaConsulta;
    const descripcion = reciente?.descripcion ?? ultima?.descripcion ?? null;
    const fechaConsulta = reciente?.consulta.fechaConsultaUtc ?? ultima?.fechaConsulta ?? null;

    return (
  <article className="dashboard-weather-card" key={favorito.idFavorito}>
    <div className="dashboard-weather-header">
      <div>
        <h3>{favorito.ubicacion.nombre}</h3>
        <span>{lugar(favorito.ubicacion)}</span>
      </div>
      <i className="bi bi-star-fill"></i>
    </div>

    <div className="dashboard-weather-main">
      <div>
        <strong>{grados(reciente?.temperatura ?? ultima?.temperatura)}</strong>
        <p>{descripcion ? capitalizar(descripcion) : "Sin consultas todavía"}</p>
      </div>
      <i className={`bi ${iconoCondicion(reciente?.codigoCondicion ?? ultima?.codigoCondicion, reciente?.detalle.esDeDia ?? true)}`}></i>
    </div>

    {reciente && (
    <div className="dashboard-weather-details">
      <span>
        <i className="bi bi-droplet-half"></i> {humedad(reciente.humedad)} humedad
      </span>
      <span>
        <i className="bi bi-wind"></i> {viento(reciente.velocidadViento)}
      </span>
    </div>
    )}

    <div className="dashboard-weather-updated">
      <i className="bi bi-clock"></i>
      <span>
        {fechaConsulta ? `Última consulta almacenada: ${fechaHora(fechaConsulta)}` : "Aún no hay consultas"}
      </span>
    </div>

    <Link className="small" to={`/clima?idUbicacion=${favorito.ubicacion.idUbicacion}`}>
      Ver clima ahora
    </Link>
  </article>
    );
  })}
</div>
</section>
{/* ========================================
    ALERTAS RECIENTES
======================================== */}

<section className="dashboard-alerts">

  <div className="dashboard-alerts-header">
    <h2>Alertas recientes</h2>
    <Link to="/alertas">
      Ver todas las alertas
      <i className="bi bi-chevron-right"></i>
    </Link>
  </div>

  {alertas.datos !== undefined && recientes.length === 0 && (
    <p className="text-secondary">
      Sin alertas por ahora. <Link to="/configuracion-alertas">Configurar alertas</Link>
    </p>
  )}

  <div className="dashboard-alerts-grid">
    {recientes.map((alerta) => (
    <article className="dashboard-alert-card" key={alerta.idAlerta}>
      <div className="dashboard-alert-top">
        <div className={`dashboard-alert-icon ${CLASE_ICONO_DASHBOARD[CLASE_TIPO_ALERTA[alerta.tipo]]}`}>
          <i className={`bi ${ICONOS_TIPO_ALERTA[alerta.tipo]}`}></i>
        </div>
        <span className={`dashboard-alert-badge ${alerta.estado === "NUEVA" ? "new" : "read"}`}>
          {alerta.estado === "NUEVA" ? "Nueva" : "Leída"}
        </span>
      </div>

      <h3>{NOMBRES_TIPO_ALERTA[alerta.tipo]}</h3>
      <strong>{alerta.ubicacion.nombre}</strong>

      <p>{alerta.mensaje}</p>

      <small>{fechaHora(alerta.fechaGeneracion)}</small>
    </article>
    ))}
  </div>
</section>
        </div>
      </div>
    </main>
  );
}
