import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "./History.css";
import { useAuth } from "../context/auth";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { listarFavoritos } from "../services/favoritosService";
import { listarHistorial, obtenerConsultaHistorial } from "../services/historialService";
import type { ConsultaHistorial, TipoConsulta, Ubicacion } from "../types/api";
import { capitalizar, grados, humedad, lugar, numero, viento } from "../utils/clima";
import { diaCorto, fecha, hora, nombreDia, rangoDelDia } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

type FiltroTipo = "TODOS" | TipoConsulta;

const ETIQUETA_TIPO: Record<TipoConsulta, string> = { ACTUAL: "ACTUAL", PRONOSTICO: "PRONÓSTICO" };

// M08 – Historial: GET /api/historial (20 por página, más reciente primero) y
// GET /api/historial/{id}. Solo consultas propias con sesión; el historial es de solo lectura.
export default function History() {
  const navigate = useNavigate();
  const { usuario } = useAuth();

  const [ubicacionFiltro, setUbicacionFiltro] = useState("");
  const [tipoFiltro, setTipoFiltro] = useState<FiltroTipo>("TODOS");
  const [fechaFiltro, setFechaFiltro] = useState("");
  const [pagina, setPagina] = useState(1);
  const [idDetalle, setIdDetalle] = useState<number | null>(null);

  const rango = fechaFiltro ? rangoDelDia(fechaFiltro) : null;
  const filtro = {
    tipo: tipoFiltro === "TODOS" ? undefined : tipoFiltro,
    idUbicacion: ubicacionFiltro ? Number(ubicacionFiltro) : undefined,
    desde: rango?.desde,
    hasta: rango?.hasta,
    pagina,
  };

  const historial = useConsulta(`historial:${usuario?.idUsuario}:${JSON.stringify(filtro)}`, (signal) =>
    listarHistorial(filtro, signal)
  );
  const favoritos = useConsulta(`favoritos:${usuario?.idUsuario}`, (signal) => listarFavoritos(signal));
  const detalle = useConsulta(idDetalle === null ? null : `historial-detalle:${idDetalle}`, (signal) =>
    obtenerConsultaHistorial(idDetalle!, signal)
  );

  const consultas: ConsultaHistorial[] = historial.datos?.elementos ?? [];
  const totalElementos = historial.datos?.totalElementos ?? 0;
  const totalPaginas = historial.datos?.totalPaginas ?? 0;

  // Opciones del filtro de ubicación: favoritos y ubicaciones de la página actual.
  const opcionesUbicacion = new Map<number, Ubicacion>();
  for (const favorito of favoritos.datos ?? []) opcionesUbicacion.set(favorito.ubicacion.idUbicacion, favorito.ubicacion);
  for (const consulta of consultas) opcionesUbicacion.set(consulta.ubicacion.idUbicacion, consulta.ubicacion);

  const cambiarFiltro = (aplicar: () => void) => {
    aplicar();
    setPagina(1);
  };

  const consultarDeNuevo = (tipo: TipoConsulta, idUbicacion: number) =>
    navigate(tipo === "PRONOSTICO" ? `/pronostico?idUbicacion=${idUbicacion}` : `/clima?idUbicacion=${idUbicacion}`);

  const errorDetalle =
    detalle.error instanceof ApiError && detalle.error.status === 404
      ? "La consulta no existe."
      : detalle.error
        ? mensajeDeError(detalle.error, "No se pudo cargar el detalle.")
        : "";

  return (
    <main className="history-page">
      <div className="history-layout">


        {/* CONTENIDO */}
        <div className="history-container">
          <EnlaceVolver to="/dashboard">Volver a mi dashboard</EnlaceVolver>

          <span className="history-label d-block">HISTORIAL</span>

          <header className="history-header">
            <h1>Historial de consultas</h1>
            <p>
              Revisa las consultas de clima y pronóstico realizadas
              anteriormente.
            </p>
          </header>

          {/* FILTROS */}
          <section className="history-filters">
            <div className="history-filter-field">
              <label htmlFor="history-search">
                Ubicación
              </label>

              <div className="history-search-box">
                <i className="bi bi-geo-alt"></i>
                <select
                  id="history-search"
                  value={ubicacionFiltro}
                  onChange={(e) => cambiarFiltro(() => setUbicacionFiltro(e.target.value))}
                  className="border-0 bg-transparent w-100"
                >
                  <option value="">Todas las ubicaciones</option>
                  {[...opcionesUbicacion.values()].map((ubicacion) => (
                    <option key={ubicacion.idUbicacion} value={ubicacion.idUbicacion}>
                      {ubicacion.nombre}
                      {lugar(ubicacion) ? ` (${lugar(ubicacion)})` : ""}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div className="history-filter-field">
              <label htmlFor="history-type">
                Tipo de consulta
              </label>

              <select
                id="history-type"
                value={tipoFiltro}
                onChange={(e) => cambiarFiltro(() => setTipoFiltro(e.target.value as FiltroTipo))}
              >
                <option value="TODOS">Todos</option>
                <option value="ACTUAL">Clima actual</option>
                <option value="PRONOSTICO">Pronóstico</option>
              </select>
            </div>

            <div className="history-filter-field">
              <label htmlFor="history-date">
                Fecha de consulta
              </label>

              <input
                id="history-date"
                type="date"
                value={fechaFiltro}
                onChange={(e) => cambiarFiltro(() => setFechaFiltro(e.target.value))}
              />
            </div>

            <button
              type="button"
              className="history-filter-button"
              onClick={() =>
                cambiarFiltro(() => {
                  setUbicacionFiltro("");
                  setTipoFiltro("TODOS");
                  setFechaFiltro("");
                })
              }
            >
              <i className="bi bi-arrow-counterclockwise"></i>
              Limpiar filtros
            </button>
          </section>

          {/* HISTORIAL */}
          <section className="history-records">
            <div className="history-records-header">
              <h2>Consultas anteriores</h2>

              <div className="history-records-actions">
                <span>
                  {totalElementos}{" "}
                  {totalElementos === 1 ? "consulta" : "consultas"}
                </span>
              </div>
            </div>

            {historial.error !== undefined && (
              <div className="alert alert-danger" role="alert">
                {mensajeDeError(historial.error, "No se pudo cargar el historial.")}
                <button type="button" className="btn btn-link btn-sm" onClick={historial.recargar}>
                  Reintentar
                </button>
              </div>
            )}

            {historial.cargando && consultas.length === 0 && historial.error === undefined && (
              <div className="text-center py-4" role="status">
                <div className="spinner-border text-primary" aria-hidden="true"></div>
                <p className="mt-2">Cargando historial...</p>
              </div>
            )}

            {consultas.length > 0 ? (
              <div className="history-table-wrapper">
                <table className="history-table">
                  <thead>
                    <tr>
                      <th>Ubicación</th>
                      <th>Tipo de consulta</th>
                      <th>Temperatura</th>
                      <th>Fecha</th>
                      <th>Hora</th>
                      <th>Acción</th>
                    </tr>
                  </thead>

                  <tbody>
                    {consultas.map((consulta) => (
                      <tr key={consulta.idConsulta}>
                        <td>
                          <div className="history-city">
                            <strong>{consulta.ubicacion.nombre}</strong>
                            <span>{lugar(consulta.ubicacion)}</span>
                          </div>
                        </td>

                        <td>
                          <span
                            className={`history-type-badge ${
                              consulta.tipo === "PRONOSTICO" ? "forecast" : "current"
                            }`}
                          >
                            {ETIQUETA_TIPO[consulta.tipo]}
                          </span>
                        </td>

                        <td>
                          {/* PRONOSTICO no tiene mediciones: "—" (RF-11) */}
                          {grados(consulta.temperatura)}
                          {consulta.descripcion && (
                            <small className="d-block text-secondary">{capitalizar(consulta.descripcion)}</small>
                          )}
                        </td>

                        <td>{fecha(consulta.fechaConsultaUtc)}</td>
                        <td>{hora(consulta.fechaConsultaUtc)}</td>

                        <td>
                          <div className="history-row-actions">
                            <button
                              type="button"
                              className="history-view-button"
                              onClick={() => setIdDetalle(consulta.idConsulta)}
                            >
                              Ver
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div className="history-table-footer d-flex justify-content-between align-items-center flex-wrap gap-2">
                  <span>
                    Página {historial.datos?.pagina ?? pagina} de {Math.max(totalPaginas, 1)} · {totalElementos} consultas
                  </span>
                  <span className="d-flex gap-2">
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-secondary"
                      onClick={() => setPagina((p) => Math.max(1, p - 1))}
                      disabled={pagina <= 1 || historial.cargando}
                    >
                      Anterior
                    </button>
                    <button
                      type="button"
                      className="btn btn-sm btn-outline-secondary"
                      onClick={() => setPagina((p) => p + 1)}
                      disabled={pagina >= totalPaginas || historial.cargando}
                    >
                      Siguiente
                    </button>
                  </span>
                </div>
              </div>
            ) : (
              historial.datos !== undefined && (
              <div className="history-empty">
                <i className="bi bi-clock-history"></i>
                <h3>
                  {totalElementos === 0 && !filtro.tipo && !filtro.idUbicacion && !rango && pagina === 1
                    ? "Todavía no has consultado el clima"
                    : "No encontramos consultas"}
                </h3>
                <p>
                  {totalElementos === 0 && !filtro.tipo && !filtro.idUbicacion && !rango && pagina === 1
                    ? "Las ciudades que consultes con tu sesión iniciada aparecerán aquí."
                    : "Prueba con otros filtros de búsqueda."}
                </p>
              </div>
              )
            )}

            <div className="history-info">
              <i className="bi bi-info-circle"></i>
              <span>
                Solo se incluyen consultas realizadas con tu sesión
                iniciada. Este historial no incluye consultas públicas
                anónimas.
              </span>
            </div>
          </section>
        </div>
      </div>
      {idDetalle !== null && (
        <>
          <div className="modal d-block" role="dialog" aria-modal="true" aria-labelledby="history-detail-title" tabIndex={-1}>
            <div className="modal-dialog modal-lg modal-dialog-scrollable">
              <div className="modal-content">
                <div className="modal-header">
                  <h2 className="modal-title h5" id="history-detail-title">Detalle de la consulta</h2>
                  <button type="button" className="btn-close" aria-label="Cerrar" onClick={() => setIdDetalle(null)}></button>
                </div>
                <div className="modal-body">
                  {detalle.cargando && (
                    <div className="text-center py-3" role="status">
                      <div className="spinner-border text-primary" aria-hidden="true"></div>
                    </div>
                  )}
                  {errorDetalle && <p className="text-danger" role="alert">{errorDetalle}</p>}
                  {detalle.datos && (
                    <>
                      <p className="mb-1">
                        <strong>{detalle.datos.ubicacion.nombre}</strong>{" "}
                        <span className="text-secondary">{lugar(detalle.datos.ubicacion)}</span>
                      </p>
                      <p className="text-secondary">
                        {ETIQUETA_TIPO[detalle.datos.tipo]} · {fecha(detalle.datos.fechaConsultaUtc)} {hora(detalle.datos.fechaConsultaUtc)}
                      </p>

                      {detalle.datos.actual && (
                        <dl className="row mb-0">
                          <dt className="col-6">Condición</dt>
                          <dd className="col-6">{capitalizar(detalle.datos.actual.descripcion)}</dd>
                          <dt className="col-6">Temperatura</dt>
                          <dd className="col-6">{numero(detalle.datos.actual.temperatura)} °C</dd>
                          <dt className="col-6">Sensación térmica</dt>
                          <dd className="col-6">{numero(detalle.datos.actual.sensacionTermica)} °C</dd>
                          <dt className="col-6">Humedad</dt>
                          <dd className="col-6">{humedad(detalle.datos.actual.humedad)}</dd>
                          <dt className="col-6">Viento</dt>
                          <dd className="col-6">{viento(detalle.datos.actual.velocidadViento)}</dd>
                          {detalle.datos.actual.detalle?.presionHpa != null && (
                            <>
                              <dt className="col-6">Presión</dt>
                              <dd className="col-6">{detalle.datos.actual.detalle.presionHpa} hPa</dd>
                            </>
                          )}
                        </dl>
                      )}

                      {detalle.datos.pronostico && (
                        detalle.datos.pronostico.dias.length === 0 ? (
                          <p className="text-secondary">No hay días guardados en esta consulta.</p>
                        ) : (
                          <div className="table-responsive">
                            <table className="table table-sm align-middle">
                              <thead>
                                <tr>
                                  <th>Día</th>
                                  <th>Condición</th>
                                  <th>Máx.</th>
                                  <th>Mín.</th>
                                  <th>Lluvia</th>
                                </tr>
                              </thead>
                              <tbody>
                                {detalle.datos.pronostico.dias.map((dia) => (
                                  <tr key={dia.fecha}>
                                    <td>{nombreDia(dia.fecha)} {diaCorto(dia.fecha)}</td>
                                    <td>{capitalizar(dia.descripcion)}</td>
                                    <td>{grados(dia.temperaturaMax)}</td>
                                    <td>{grados(dia.temperaturaMin)}</td>
                                    <td>{dia.probabilidadLluvia}%</td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                        )
                      )}
                    </>
                  )}
                </div>
                <div className="modal-footer">
                  {detalle.datos && (
                    <button
                      type="button"
                      className="btn btn-primary"
                      onClick={() => consultarDeNuevo(detalle.datos!.tipo, detalle.datos!.ubicacion.idUbicacion)}
                    >
                      Consultar de nuevo
                    </button>
                  )}
                  <button type="button" className="btn btn-outline-secondary" onClick={() => setIdDetalle(null)}>
                    Cerrar
                  </button>
                </div>
              </div>
            </div>
          </div>
          <div className="modal-backdrop show"></div>
        </>
      )}
    </main>
  );
}
