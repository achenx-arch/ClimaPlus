import { useState } from "react";
import { Link } from "react-router-dom";
import "./Alerts.css";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useConsulta } from "../hooks/useConsulta";
import { mensajeDeError } from "../services/apiClient";
import { listarAlertas, marcarAlertaLeida } from "../services/alertasService";
import { listarConfiguraciones } from "../services/configuracionAlertasService";
import type { Alerta, EstadoAlerta } from "../types/api";
import { CLASE_TIPO_ALERTA, ICONOS_TIPO_ALERTA, NOMBRES_TIPO_ALERTA, numero } from "../utils/clima";
import { fechaHora } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

type Filtro = "todas" | "nuevas" | "leidas";

const ESTADO_POR_FILTRO: Record<Filtro, EstadoAlerta | undefined> = {
  todas: undefined,
  nuevas: "NUEVA",
  leidas: "LEIDA",
};

function detalleAlerta(alerta: Alerta): string {
  if (alerta.tipo === "LLUVIA" || alerta.valorDetectado === null || alerta.valorUmbral === null) {
    return "Detección: lluvia presente · Sin umbral numérico";
  }
  return `Detectada: ${numero(alerta.valorDetectado, 2)} °C · Umbral: ${numero(alerta.valorUmbral, 2)} °C`;
}

// M09 – Alertas: GET /api/alertas (20 por página), GET /api/alertas/contador y
// PATCH /api/alertas/{id}/estado. Las alertas las genera el motor al consultar el clima actual.
export default function Alerts() {
  const { usuario } = useAuth();
  const { nuevas, refrescarContador, avisar } = useNotificaciones();

  const [filtro, setFiltro] = useState<Filtro>("todas");
  const [pagina, setPagina] = useState(1);
  const [detalleAbierto, setDetalleAbierto] = useState<number | null>(null);
  const [marcando, setMarcando] = useState<number | null>(null);

  const estado = ESTADO_POR_FILTRO[filtro];
  const lista = useConsulta(`alertas:${usuario?.idUsuario}:${filtro}:${pagina}`, (signal) =>
    listarAlertas({ estado, pagina }, signal)
  );
  const leidasTotal = useConsulta(`alertas-leidas:${usuario?.idUsuario}:${nuevas}`, (signal) =>
    listarAlertas({ estado: "LEIDA", pagina: 1 }, signal)
  );
  const configuraciones = useConsulta(`configuraciones:${usuario?.idUsuario}`, (signal) => listarConfiguraciones(signal));

  const alertasFiltradas: Alerta[] = lista.datos?.elementos ?? [];
  const totalFiltro = lista.datos?.totalElementos ?? 0;
  const totalPaginas = lista.datos?.totalPaginas ?? 0;
  const leidas = leidasTotal.datos?.totalElementos;
  const configuracionesActivas = configuraciones.datos?.filter((c) => c.activa).length;

  const cambiarFiltro = (nuevo: Filtro) => {
    setFiltro(nuevo);
    setPagina(1);
  };

  const marcarComoLeida = async (idAlerta: number) => {
    setMarcando(idAlerta);
    try {
      const actualizada = await marcarAlertaLeida(idAlerta);
      lista.actualizar((pagina) =>
        pagina && {
          ...pagina,
          elementos:
            filtro === "nuevas"
              ? pagina.elementos.filter((a) => a.idAlerta !== idAlerta)
              : pagina.elementos.map((a) => (a.idAlerta === idAlerta ? actualizada : a)),
        }
      );
      await refrescarContador();
    } catch (error) {
      avisar(mensajeDeError(error, "No se pudo marcar la alerta como leída."), "error");
    } finally {
      setMarcando(null);
    }
  };

  return (
    <main className="alerts-page">
      <div className="alerts-layout">

        {/* CONTENIDO PRINCIPAL */}
        <div className="alerts-container">
          <EnlaceVolver to="/dashboard">Volver a mi dashboard</EnlaceVolver>

          <span className="alerts-label d-block">ALERTAS</span>

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
                <strong>{leidas ?? "—"}</strong>
                <small>Revisadas por ti</small>
              </div>
            </article>

            <article className="alerts-summary-card">
              <div className="alerts-summary-icon settings">
                <i className="bi bi-sliders"></i>
              </div>

              <div>
                <span>Configuraciones activas</span>
                <strong>{configuracionesActivas ?? "—"}</strong>
                <small>Reglas habilitadas</small>
              </div>
            </article>
          </section>

          {/* FILTROS */}
          <div className="alerts-filters">
            <div className="alerts-filter-buttons">
              <button
                type="button"
                className={filtro === "todas" ? "active" : ""}
                onClick={() => cambiarFiltro("todas")}
              >
                Todas
              </button>

              <button
                type="button"
                className={filtro === "nuevas" ? "active" : ""}
                onClick={() => cambiarFiltro("nuevas")}
              >
                Nuevas
              </button>

              <button
                type="button"
                className={filtro === "leidas" ? "active" : ""}
                onClick={() => cambiarFiltro("leidas")}
              >
                Leídas
              </button>
            </div>

            <span>
              {totalFiltro}{" "}
              {totalFiltro === 1 ? "alerta" : "alertas"}
            </span>
          </div>

          {lista.error !== undefined && (
            <div className="alert alert-danger" role="alert">
              {mensajeDeError(lista.error, "No se pudieron cargar las alertas.")}
              <button type="button" className="btn btn-link btn-sm" onClick={lista.recargar}>
                Reintentar
              </button>
            </div>
          )}

          {lista.cargando && !lista.datos && (
            <div className="text-center py-4" role="status">
              <div className="spinner-border text-primary" aria-hidden="true"></div>
              <p className="mt-2">Cargando alertas...</p>
            </div>
          )}

          {/* TARJETAS */}
          <section className="alerts-grid">
            {alertasFiltradas.map((alerta) => {
              const leida = alerta.estado === "LEIDA";
              return (
              <article className="alerts-card" key={alerta.idAlerta} style={leida ? { opacity: 0.75 } : undefined}>
                <div className="alerts-card-top">
                  <div className={`alerts-card-icon ${CLASE_TIPO_ALERTA[alerta.tipo]}`}>
                    <i className={`bi ${ICONOS_TIPO_ALERTA[alerta.tipo]}`}></i>
                  </div>

                  <span
                    className={`alerts-badge ${
                      leida ? "read" : "new"
                    }`}
                  >
                    {leida ? "LEÍDA" : "NUEVA"}
                  </span>
                </div>

                <h3>{NOMBRES_TIPO_ALERTA[alerta.tipo]}</h3>
                <strong className="alerts-card-location">
                  {alerta.ubicacion.nombre}
                </strong>

                <p>{alerta.mensaje}</p>

                <div className="alerts-card-detail">
                  {detalleAlerta(alerta)}
                </div>

                <div className="alerts-card-date">
                  <span>Fecha y hora de detección</span>
                  <small>{fechaHora(alerta.fechaGeneracion)}</small>
                </div>

                <div className="alerts-card-actions">
                  {!leida && (
                    <button
                      type="button"
                      onClick={() => void marcarComoLeida(alerta.idAlerta)}
                      disabled={marcando === alerta.idAlerta}
                    >
                      <i className="bi bi-check2"></i>
                      Marcar como leída
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() =>
                      setDetalleAbierto(
                        detalleAbierto === alerta.idAlerta
                          ? null
                          : alerta.idAlerta
                      )
                    }
                  >
                    <i className="bi bi-chevron-right"></i>
                    {detalleAbierto === alerta.idAlerta
                      ? "Ocultar detalle"
                      : "Ver detalle"}
                  </button>
                </div>

                {detalleAbierto === alerta.idAlerta && (
                  <div className="alerts-expanded-detail">
                    <strong>Detalle de la alerta</strong>
                    <p>{alerta.mensaje}</p>
                    <p>{detalleAlerta(alerta)}</p>
                    <small>
                      Ubicación: {alerta.ubicacion.nombre} · Origen:{" "}
                      {alerta.origen.tipo === "ESP32"
                        ? `ESP32${alerta.origen.nombreDispositivo ? ` (${alerta.origen.nombreDispositivo})` : ""}`
                        : "OpenWeather"}
                    </small>
                  </div>
                )}
              </article>
              );
            })}
          </section>

          {lista.datos !== undefined && alertasFiltradas.length === 0 && (
            <div className="alerts-empty">
              <i className="bi bi-bell-slash"></i>
              <h3>{filtro === "todas" ? "Sin alertas por ahora" : "No hay alertas en esta categoría"}</h3>
              <p>
                {filtro === "todas" ? (
                  <Link to="/configuracion-alertas">Configurar alertas</Link>
                ) : (
                  "Prueba seleccionando otro filtro."
                )}
              </p>
            </div>
          )}

          {totalPaginas > 1 && (
            <div className="d-flex justify-content-center align-items-center gap-3 my-3">
              <button
                type="button"
                className="btn btn-sm btn-outline-secondary"
                onClick={() => setPagina((p) => Math.max(1, p - 1))}
                disabled={pagina <= 1}
              >
                Anterior
              </button>
              <span>Página {pagina} de {totalPaginas}</span>
              <button
                type="button"
                className="btn btn-sm btn-outline-secondary"
                onClick={() => setPagina((p) => p + 1)}
                disabled={pagina >= totalPaginas}
              >
                Siguiente
              </button>
            </div>
          )}

          <footer className="alerts-footer">
            Alertas generadas al consultar el clima actual con tu sesión · ClimaPlus
          </footer>
        </div>
      </div>
    </main>
  );
}