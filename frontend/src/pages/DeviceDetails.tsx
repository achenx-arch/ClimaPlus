import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import "./Alerts.css";
import "./DeviceDetails.css";
import ResultadosUbicacion from "../components/ResultadosUbicacion";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useBusquedaUbicaciones } from "../hooks/useBusquedaUbicaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import {
  obtenerDispositivo,
  actualizarDispositivo,
  cambiarEstadoDispositivo,
  listarLecturasDispositivo,
  listarAlertasDispositivo,
} from "../services/dispositivosService";
import type { Dispositivo, LecturaDispositivo, Alerta, Ubicacion } from "../types/api";
import {
  CLASE_TIPO_ALERTA,
  ICONOS_TIPO_ALERTA,
  NOMBRES_TIPO_ALERTA,
  lugar,
} from "../utils/clima";
import { fechaHora } from "../utils/fechas";

// M12 – Detalle de dispositivo: GET /api/dispositivos/{id},
// GET /api/dispositivos/{id}/lecturas, GET /api/dispositivos/{id}/alertas,
// PUT /api/dispositivos/{id}, PATCH /api/dispositivos/{id}/estado.



export default function DeviceDetails() {
  const { id } = useParams<{ id: string }>();
  const idNum = Number(id);
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const { avisar } = useNotificaciones();

  // Carga del dispositivo
  const dispositivo = useConsulta(
    id ? `dispositivo:${id}` : null,
    (signal) => obtenerDispositivo(idNum, signal)
  );
  const dev: Dispositivo | undefined = dispositivo.datos;

  // Paginación de lecturas
  const [paginaLecturas, setPaginaLecturas] = useState(1);
  const [versionLecturas, setVersionLecturas] = useState(0);
  const lecturas = useConsulta(
    id ? `lecturas:${id}:${paginaLecturas}:${versionLecturas}:${usuario?.idUsuario}` : null,
    (signal) => listarLecturasDispositivo(idNum, paginaLecturas, signal)
  );

  // Paginación de alertas
  const [paginaAlertas, setPaginaAlertas] = useState(1);
  const alertas = useConsulta(
    id ? `alertas-disp:${id}:${paginaAlertas}:${usuario?.idUsuario}` : null,
    (signal) => listarAlertasDispositivo(idNum, paginaAlertas, signal)
  );

  // Estado de modales
  const [mostrarEdicion, setMostrarEdicion] = useState(false);
  const [mostrarConfirmarEstado, setMostrarConfirmarEstado] = useState(false);

  // Formulario de edición
  const [nombreEdit, setNombreEdit] = useState("");
  const [ubicacionEdit, setUbicacionEdit] = useState<Ubicacion | null>(null);
  const [errorNombreEdit, setErrorNombreEdit] = useState("");
  const [guardandoEdit, setGuardandoEdit] = useState(false);
  const [busquedaEdit, setBusquedaEdit] = useState("");
  const buscadorEdit = useBusquedaUbicaciones();

  const [procesandoEstado, setProcesandoEstado] = useState(false);

  const abrirEdicion = () => {
    if (!dev) return;
    setNombreEdit(dev.nombre);
    setUbicacionEdit(dev.ubicacion);
    setErrorNombreEdit("");
    setBusquedaEdit("");
    buscadorEdit.limpiar();
    setMostrarEdicion(true);
  };

  const cerrarEdicion = () => {
    setMostrarEdicion(false);
    buscadorEdit.limpiar();
  };

  const confirmarEdicion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!dev) return;
    setErrorNombreEdit("");

    if (!nombreEdit.trim()) {
      setErrorNombreEdit("El nombre es obligatorio.");
      return;
    }
    if (!ubicacionEdit) {
      setErrorNombreEdit("Selecciona una ubicación.");
      return;
    }

    setGuardandoEdit(true);
    try {
      const actualizado = await actualizarDispositivo(
        dev.idDispositivo,
        nombreEdit.trim(),
        ubicacionEdit.idUbicacion
      );
      dispositivo.actualizar(() => actualizado);
      cerrarEdicion();
      avisar("Dispositivo actualizado correctamente.");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setErrorNombreEdit(error.errorDe("nombre") ?? "Ya tienes un dispositivo con ese nombre.");
        return;
      }
      avisar(mensajeDeError(error, "No se pudo actualizar el dispositivo."), "error");
    } finally {
      setGuardandoEdit(false);
    }
  };

  const confirmarCambioEstado = async () => {
    if (!dev) return;
    setProcesandoEstado(true);
    try {
      const actualizado = await cambiarEstadoDispositivo(dev.idDispositivo, !dev.activo);
      dispositivo.actualizar(() => actualizado);
      setMostrarConfirmarEstado(false);
      avisar(
        actualizado.activo
          ? "Dispositivo activado correctamente."
          : "Dispositivo desactivado. La API Key queda revocada."
      );
    } catch (error) {
      avisar(mensajeDeError(error, "No se pudo cambiar el estado del dispositivo."), "error");
    } finally {
      setProcesandoEstado(false);
    }
  };

  // ── 404 ──────────────────────────────────────────────────────
  if (
    dispositivo.error !== undefined &&
    dispositivo.error instanceof ApiError &&
    dispositivo.error.status === 404
  ) {
    return (
      <main className="device-details-page">
        <div className="alerts-layout">
          <div className="device-details-container">
            <Link to="/dispositivos" className="device-details-breadcrumb">
              Mis dispositivos
            </Link>
            <div className="device-details-empty">
              <i className="bi bi-exclamation-circle"></i>
              <h2>El dispositivo no existe</h2>
              <p>No encontramos información del dispositivo solicitado o no te pertenece.</p>
              <Link to="/dispositivos">Volver a mis dispositivos</Link>
            </div>
          </div>
        </div>
      </main>
    );
  }

  // ── CARGA ─────────────────────────────────────────────────────
  if (dispositivo.cargando && !dev) {
    return (
      <main className="device-details-page">
        <div className="alerts-layout">
          <div className="device-details-container">
            <div className="text-center py-5" role="status">
              <div className="spinner-border text-primary" aria-hidden="true"></div>
              <p className="mt-2">Cargando dispositivo...</p>
            </div>
          </div>
        </div>
      </main>
    );
  }

  // ── ERROR GENÉRICO ────────────────────────────────────────────
  if (dispositivo.error !== undefined && !dev) {
    return (
      <main className="device-details-page">
        <div className="alerts-layout">
          <div className="device-details-container">
            <Link to="/dispositivos" className="device-details-breadcrumb">
              Mis dispositivos
            </Link>
            <div className="alert alert-danger" role="alert">
              {mensajeDeError(dispositivo.error, "No se pudo cargar el dispositivo.")}
              <button type="button" className="btn btn-link btn-sm" onClick={dispositivo.recargar}>
                Reintentar
              </button>
            </div>
          </div>
        </div>
      </main>
    );
  }

  if (!dev) return null;

  const ul = dev.ultimaLectura;
  const listaLecturas: LecturaDispositivo[] = lecturas.datos?.elementos ?? [];
  const listaAlertas: Alerta[] = alertas.datos?.elementos ?? [];

  return (
    <main className="device-details-page">
      <div className="alerts-layout">

        <div className="device-details-container">
          {/* RUTA */}
          <div className="device-details-breadcrumb">
            <Link to="/dispositivos">Mis dispositivos</Link>
            <span>/ {dev.nombre}</span>
          </div>

          {/* ENCABEZADO */}
          <span className="device-details-label">DETALLE DISPOSITIVO</span>

          <div className="device-details-header">
            <div>
              <h1>{dev.nombre}</h1>
              <p>{lugar(dev.ubicacion)} · ESP32</p>
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
                onClick={() => setMostrarConfirmarEstado(true)}
              >
                {dev.activo ? "Desactivar" : "Activar"}
              </button>

              <button
                type="button"
                className="device-details-edit"
                onClick={() => navigate("/dispositivos")}
              >
                <i className="bi bi-arrow-left"></i>
                Volver
              </button>
            </div>
          </div>

          {/* ESTADO */}
          <section className="device-details-status-panel">
            <div className="device-details-status-left">
              <div className="device-details-info-icon">
                <i className="bi bi-cpu"></i>
              </div>

              <div>
                <span>Estado del dispositivo</span>
                <div className="device-details-badges">
                  <span className={dev.activo ? "device-details-badge active" : "device-details-badge inactive"}>
                    {dev.activo ? "Activo" : "Inactivo"}
                  </span>
                  <span className={dev.enLinea ? "device-details-badge connected" : "device-details-badge disconnected"}>
                    {dev.enLinea ? "En línea" : "Fuera de línea"}
                  </span>
                </div>
              </div>
            </div>

            <div className="device-details-last-connection">
              <span>Última conexión</span>
              <strong>
                {dev.ultimaConexion
                  ? fechaHora(dev.ultimaConexion)
                  : "Nunca conectado"}
              </strong>
            </div>
          </section>

          {/* 4 TARJETAS ÚLTIMA LECTURA */}
          <section className="device-details-section">
            <div className="device-details-section-header">
              <h2>Última lectura</h2>
              {ul && <span>{fechaHora(ul.fechaLectura)}</span>}
            </div>

            <div className="device-details-grid">
              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-thermometer-half"></i>
                </div>
                <div>
                  <span>Temperatura</span>
                  <h3>{ul?.temperatura !== null && ul?.temperatura !== undefined ? `${ul.temperatura} °C` : "—"}</h3>
                  <p>Sensor BME280</p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-droplet"></i>
                </div>
                <div>
                  <span>Humedad</span>
                  <h3>{ul?.humedad !== null && ul?.humedad !== undefined ? `${ul.humedad}%` : "—"}</h3>
                  <p>Humedad relativa</p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-cloud-rain"></i>
                </div>
                <div>
                  <span>Lluvia</span>
                  <h3>
                    {ul?.lluvia === null || ul?.lluvia === undefined
                      ? "—"
                      : ul.lluvia
                      ? "Sí"
                      : "No"}
                  </h3>
                  <p>Sensor LM393</p>
                </div>
              </article>

              <article className="device-details-sensor">
                <div className="device-details-sensor-icon">
                  <i className="bi bi-brightness-high"></i>
                </div>
                <div>
                  <span>Luz</span>
                  <h3>{ul?.luz !== null && ul?.luz !== undefined ? `${ul.luz}%` : "—"}</h3>
                  <p>LDR normalizado</p>
                </div>
              </article>
            </div>
          </section>

          {/* TABLA DE LECTURAS PAGINADA */}
          <section className="device-details-section">
            <div className="device-details-section-header">
              <h2>Historial de lecturas</h2>
              <button
                type="button"
                className="device-details-edit"
                onClick={() => setVersionLecturas((v) => v + 1)}
                disabled={lecturas.cargando}
              >
                <i className="bi bi-arrow-clockwise"></i>
                Actualizar
              </button>
            </div>

            {lecturas.cargando && !lecturas.datos && (
              <div className="text-center py-3" role="status">
                <div className="spinner-border spinner-border-sm text-primary" aria-hidden="true"></div>
                <span className="ms-2">Cargando lecturas...</span>
              </div>
            )}

            {lecturas.error && (
              <div className="alert alert-danger" role="alert">
                {mensajeDeError(lecturas.error, "No se pudieron cargar las lecturas.")}
              </div>
            )}

            {lecturas.datos && (
              <>
                <div className="device-details-table-wrapper">
                  <table className="device-details-table">
                    <thead>
                      <tr>
                        <th>Fecha y hora</th>
                        <th>Temperatura</th>
                        <th>Humedad</th>
                        <th>Lluvia</th>
                        <th>Luz</th>
                      </tr>
                    </thead>
                    <tbody>
                      {listaLecturas.length > 0 ? (
                        listaLecturas.map((l) => (
                          <tr key={l.idLectura}>
                            <td>{fechaHora(l.fechaLectura)}</td>
                            <td>{l.temperatura !== null ? `${l.temperatura} °C` : "—"}</td>
                            <td>{l.humedad !== null ? `${l.humedad}%` : "—"}</td>
                            <td>{l.lluvia === null ? "—" : l.lluvia ? "Sí" : "No"}</td>
                            <td>{l.luz !== null ? `${l.luz}%` : "—"}</td>
                          </tr>
                        ))
                      ) : (
                        <tr>
                          <td colSpan={5} className="device-details-no-data">
                            Este dispositivo todavía no tiene lecturas registradas.
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>

                {/* Paginación */}
                {lecturas.datos.totalPaginas > 1 && (
                  <div className="device-details-pagination" style={{ display: "flex", justifyContent: "center", gap: "10px", marginTop: "16px" }}>
                    <button
                      type="button"
                      className="devices-cancel-button"
                      onClick={() => setPaginaLecturas((p) => Math.max(1, p - 1))}
                      disabled={paginaLecturas <= 1}
                    >
                      Anterior
                    </button>
                    <span style={{ alignSelf: "center", fontSize: "13px", color: "#7895b5" }}>
                      Página {lecturas.datos.pagina} de {lecturas.datos.totalPaginas} · {lecturas.datos.totalElementos} lecturas
                    </span>
                    <button
                      type="button"
                      className="devices-cancel-button"
                      onClick={() => setPaginaLecturas((p) => Math.min(lecturas.datos!.totalPaginas, p + 1))}
                      disabled={paginaLecturas >= lecturas.datos.totalPaginas}
                    >
                      Siguiente
                    </button>
                  </div>
                )}

                <div className="device-details-table-footer">
                  {lecturas.datos.totalElementos} lecturas registradas · Más recientes primero
                </div>
              </>
            )}
          </section>

          {/* ALERTAS DEL DISPOSITIVO */}
          <section className="device-details-section">
            <div className="device-details-section-header">
              <h2>Alertas de este dispositivo</h2>
              {alertas.datos && (
                <span>{alertas.datos.totalElementos} alertas generadas</span>
              )}
            </div>

            {alertas.cargando && !alertas.datos && (
              <div className="text-center py-3" role="status">
                <div className="spinner-border spinner-border-sm text-primary" aria-hidden="true"></div>
                <span className="ms-2">Cargando alertas...</span>
              </div>
            )}

            {alertas.datos !== undefined && listaAlertas.length === 0 && (
              <p style={{ color: "#7895b5", fontSize: "14px" }}>
                Este dispositivo no ha generado alertas todavía.
              </p>
            )}

            {listaAlertas.length > 0 && (
              <div style={{ display: "flex", flexDirection: "column", gap: "12px" }}>
                {listaAlertas.map((alerta) => (
                  <div
                    key={alerta.idAlerta}
                    style={{
                      display: "flex",
                      gap: "14px",
                      padding: "14px",
                      background: "#ffffff",
                      border: "1px solid #d9e9f7",
                      borderRadius: "12px",
                    }}
                  >
                    <div
                      style={{
                        width: 40,
                        height: 40,
                        borderRadius: "10px",
                        background:
                          CLASE_TIPO_ALERTA[alerta.tipo] === "lluvia"
                            ? "#e0f2ff"
                            : CLASE_TIPO_ALERTA[alerta.tipo] === "calor"
                            ? "#fff3e0"
                            : "#e8f5e9",
                        display: "grid",
                        placeItems: "center",
                        flexShrink: 0,
                      }}
                    >
                      <i
                        className={`bi ${ICONOS_TIPO_ALERTA[alerta.tipo]}`}
                        style={{
                          color:
                            CLASE_TIPO_ALERTA[alerta.tipo] === "lluvia"
                              ? "#087db8"
                              : CLASE_TIPO_ALERTA[alerta.tipo] === "calor"
                              ? "#e65100"
                              : "#2e7d32",
                        }}
                      ></i>
                    </div>
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: "8px", marginBottom: "4px" }}>
                        <strong style={{ fontSize: "14px", color: "#123455" }}>
                          {NOMBRES_TIPO_ALERTA[alerta.tipo]}
                        </strong>
                        <span
                          style={{
                            padding: "3px 10px",
                            borderRadius: "20px",
                            fontSize: "11px",
                            fontWeight: 700,
                            background: alerta.estado === "NUEVA" ? "#e0f2ff" : "#f1f5f9",
                            color: alerta.estado === "NUEVA" ? "#087db8" : "#7895b5",
                          }}
                        >
                          {alerta.estado === "NUEVA" ? "Nueva" : "Leída"}
                        </span>
                      </div>
                      <p style={{ fontSize: "13px", color: "#7895b5", margin: "0 0 4px" }}>
                        {alerta.mensaje}
                      </p>
                      <small style={{ fontSize: "12px", color: "#9bb0c6" }}>
                        {fechaHora(alerta.fechaGeneracion)}
                      </small>
                    </div>
                  </div>
                ))}
              </div>
            )}

            {alertas.datos && alertas.datos.totalPaginas > 1 && (
              <div style={{ display: "flex", justifyContent: "center", gap: "10px", marginTop: "16px" }}>
                <button
                  type="button"
                  className="devices-cancel-button"
                  onClick={() => setPaginaAlertas((p) => Math.max(1, p - 1))}
                  disabled={paginaAlertas <= 1}
                >
                  Anterior
                </button>
                <span style={{ alignSelf: "center", fontSize: "13px", color: "#7895b5" }}>
                  {alertas.datos.pagina} / {alertas.datos.totalPaginas}
                </span>
                <button
                  type="button"
                  className="devices-cancel-button"
                  onClick={() => setPaginaAlertas((p) => Math.min(alertas.datos!.totalPaginas, p + 1))}
                  disabled={paginaAlertas >= alertas.datos.totalPaginas}
                >
                  Siguiente
                </button>
              </div>
            )}
          </section>
        </div>
      </div>

      {/* ── MODAL EDICIÓN ────────────────────────────────── */}
      {mostrarEdicion && (
        <div
          className="device-details-modal-overlay"
          onClick={cerrarEdicion}
        >
          <div
            className="device-details-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-edit-titulo"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="device-details-modal-header">
              <h2 id="modal-edit-titulo">Editar dispositivo</h2>
              <button
                type="button"
                onClick={cerrarEdicion}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={(e) => { void confirmarEdicion(e); }}>
              <div className="device-details-form-group">
                <label htmlFor="det-edit-nombre">Nombre del dispositivo</label>
                <input
                  id="det-edit-nombre"
                  type="text"
                  value={nombreEdit}
                  onChange={(e) => setNombreEdit(e.target.value)}
                  maxLength={100}
                  required
                />
                {errorNombreEdit && (
                  <div className="devices-form-error" role="alert">{errorNombreEdit}</div>
                )}
              </div>

              <div className="device-details-form-group">
                <label htmlFor="det-edit-ubicacion">Ubicación</label>

                {ubicacionEdit ? (
                  <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 12px", background: "#f1f8ff", borderRadius: "8px" }}>
                    <i className="bi bi-geo-alt-fill" style={{ color: "#087db8" }}></i>
                    <span style={{ flex: 1, fontSize: "14px" }}>
                      <strong>{ubicacionEdit.nombre}</strong>{" "}
                      <span style={{ color: "#7895b5" }}>{lugar(ubicacionEdit)}</span>
                    </span>
                    <button
                      type="button"
                      style={{ background: "none", border: "none", cursor: "pointer", color: "#7895b5" }}
                      onClick={() => { setUbicacionEdit(null); setBusquedaEdit(""); buscadorEdit.limpiar(); }}
                    >
                      <i className="bi bi-x"></i>
                    </button>
                  </div>
                ) : (
                  <form
                    onSubmit={(e) => { e.preventDefault(); void buscadorEdit.buscar(busquedaEdit); }}
                    style={{ display: "flex", gap: "8px" }}
                  >
                    <input
                      id="det-edit-ubicacion"
                      type="search"
                      value={busquedaEdit}
                      onChange={(e) => setBusquedaEdit(e.target.value)}
                      placeholder="Buscar ciudad o municipio"
                      maxLength={100}
                      style={{ flex: 1, padding: "10px 12px", border: "1px solid #d9e9f7", borderRadius: "8px", fontSize: "14px" }}
                    />
                    <button
                      type="submit"
                      className="devices-save-button"
                      style={{ padding: "10px 14px" }}
                      disabled={buscadorEdit.buscando}
                    >
                      {buscadorEdit.buscando ? "..." : "Buscar"}
                    </button>
                  </form>
                )}

                {buscadorEdit.error && (
                  <div className="devices-form-error" role="alert">{buscadorEdit.error}</div>
                )}
                {buscadorEdit.resultados && (
                  <ResultadosUbicacion
                    resultados={buscadorEdit.resultados}
                    alElegir={(u) => { setUbicacionEdit(u); buscadorEdit.limpiar(); }}
                    textoAccion="Seleccionar"
                  />
                )}
              </div>

              <div className="device-details-modal-actions">
                <button type="button" onClick={cerrarEdicion}>Cancelar</button>
                <button type="submit" disabled={guardandoEdit}>
                  {guardandoEdit ? "Guardando..." : "Guardar cambios"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL CONFIRMAR CAMBIO DE ESTADO ─────────────── */}
      {mostrarConfirmarEstado && (
        <div
          className="device-details-modal-overlay"
          onClick={() => setMostrarConfirmarEstado(false)}
        >
          <div
            className="device-details-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-estado-titulo"
            onClick={(e) => e.stopPropagation()}
            style={{ maxWidth: "420px" }}
          >
            <div className="device-details-modal-header">
              <h2 id="modal-estado-titulo">
                {dev.activo ? "Desactivar dispositivo" : "Activar dispositivo"}
              </h2>
              <button
                type="button"
                onClick={() => setMostrarConfirmarEstado(false)}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            {dev.activo ? (
              <p style={{ color: "#7895b5", fontSize: "14px", marginBottom: "20px" }}>
                Al desactivar <strong>{dev.nombre}</strong>, la API Key quedará revocada de inmediato
                y el dispositivo recibirá error <code>403</code> en su siguiente lectura.
              </p>
            ) : (
              <p style={{ color: "#7895b5", fontSize: "14px", marginBottom: "20px" }}>
                ¿Activar <strong>{dev.nombre}</strong>? Podrá volver a enviar lecturas.
              </p>
            )}

            <div className="device-details-modal-actions">
              <button type="button" onClick={() => setMostrarConfirmarEstado(false)}>
                Cancelar
              </button>
              <button
                type="button"
                onClick={() => { void confirmarCambioEstado(); }}
                disabled={procesandoEstado}
                style={dev.activo ? { background: "#c33c3c", color: "#fff", border: "none" } : {}}
              >
                {procesandoEstado
                  ? "Procesando..."
                  : dev.activo
                  ? "Sí, desactivar"
                  : "Sí, activar"}
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}