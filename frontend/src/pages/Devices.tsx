import { useState } from "react";
import { Link } from "react-router-dom";
import "./Alerts.css";
import "./Devices.css";
import ResultadosUbicacion from "../components/ResultadosUbicacion";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useBusquedaUbicaciones } from "../hooks/useBusquedaUbicaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import {
  listarDispositivos,
  registrarDispositivo,
  actualizarDispositivo,
  cambiarEstadoDispositivo,
} from "../services/dispositivosService";
import type { Dispositivo, Ubicacion } from "../types/api";
import { lugar } from "../utils/clima";

// M11 – Mis dispositivos: GET /api/dispositivos, POST /api/dispositivos,
// PUT /api/dispositivos/{id}, PATCH /api/dispositivos/{id}/estado.

/** "Hace N min" o "Nunca conectado". */
function textoUltimaConexion(ultimaConexion: string | null): string {
  if (!ultimaConexion) return "Nunca conectado";
  const hace = Math.round((Date.now() - new Date(ultimaConexion).getTime()) / 60_000);
  if (hace < 2) return "Hace un momento";
  return `Hace ${hace} min`;
}

export default function Devices() {
  const { usuario } = useAuth();
  const { avisar } = useNotificaciones();

  // Carga de dispositivos
  const dispositivos = useConsulta(
    `dispositivos:${usuario?.idUsuario}`,
    (signal) => listarDispositivos(signal)
  );
  const lista: Dispositivo[] = dispositivos.datos ?? [];

  // Estado de modales
  const [mostrarRegistro, setMostrarRegistro] = useState(false);
  const [mostrarApiKey, setMostrarApiKey] = useState(false);
  const [mostrarEdicion, setMostrarEdicion] = useState<Dispositivo | null>(null);
  const [mostrarConfirmarEstado, setMostrarConfirmarEstado] = useState<Dispositivo | null>(null);

  // Formulario de registro
  const [nombreReg, setNombreReg] = useState("");
  const [ubicacionReg, setUbicacionReg] = useState<Ubicacion | null>(null);
  const [errorNombreReg, setErrorNombreReg] = useState("");
  const [errorUbicacionReg, setErrorUbicacionReg] = useState("");
  const [guardandoReg, setGuardandoReg] = useState(false);
  const [busquedaReg, setBusquedaReg] = useState("");
  const buscadorReg = useBusquedaUbicaciones();

  // Resultado de registro (API Key, una sola vez)
  const [resultadoRegistro, setResultadoRegistro] = useState<{
    apiKey: string;
    aviso: string;
  } | null>(null);
  const [apiKeyCopied, setApiKeyCopied] = useState(false);

  // Formulario de edición
  const [nombreEdit, setNombreEdit] = useState("");
  const [ubicacionEdit, setUbicacionEdit] = useState<Ubicacion | null>(null);
  const [errorNombreEdit, setErrorNombreEdit] = useState("");
  const [guardandoEdit, setGuardandoEdit] = useState(false);
  const [busquedaEdit, setBusquedaEdit] = useState("");
  const buscadorEdit = useBusquedaUbicaciones();

  // Acción de cambio de estado
  const [procesandoEstado, setProcesandoEstado] = useState(false);

  const enLinea = lista.filter((d) => d.enLinea).length;

  // ────────────────────────── Registro ──────────────────────────

  const abrirRegistro = () => {
    setNombreReg("");
    setUbicacionReg(null);
    setErrorNombreReg("");
    setErrorUbicacionReg("");
    setBusquedaReg("");
    buscadorReg.limpiar();
    setMostrarRegistro(true);
  };

  const cerrarRegistro = () => {
    setMostrarRegistro(false);
    buscadorReg.limpiar();
  };

  const confirmarRegistro = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorNombreReg("");
    setErrorUbicacionReg("");

    if (!nombreReg.trim()) {
      setErrorNombreReg("El nombre es obligatorio.");
      return;
    }
    if (!ubicacionReg) {
      setErrorUbicacionReg("Busca y selecciona una ubicación.");
      return;
    }

    setGuardandoReg(true);
    try {
      const resultado = await registrarDispositivo(nombreReg.trim(), ubicacionReg.idUbicacion);
      // Actualiza la lista en memoria
      dispositivos.actualizar((actuales) => [resultado.dispositivo, ...(actuales ?? [])]);
      cerrarRegistro();
      setResultadoRegistro({ apiKey: resultado.apiKey, aviso: resultado.aviso });
      setMostrarApiKey(true);
    } catch (error) {
      if (error instanceof ApiError) {
        if (error.status === 409) {
          setErrorNombreReg(error.errorDe("nombre") ?? "Ya tienes un dispositivo con ese nombre.");
          return;
        }
        if (error.status === 404) {
          setErrorUbicacionReg("La ubicación no existe. Busca otra.");
          return;
        }
      }
      avisar(mensajeDeError(error, "No se pudo registrar el dispositivo."), "error");
    } finally {
      setGuardandoReg(false);
    }
  };

  const cerrarApiKey = () => {
    setMostrarApiKey(false);
    // La clave se borra del estado al cerrar (RN-BD-28)
    setResultadoRegistro(null);
    setApiKeyCopied(false);
  };

  const copiarApiKey = () => {
    if (resultadoRegistro) {
      void navigator.clipboard.writeText(resultadoRegistro.apiKey);
      setApiKeyCopied(true);
    }
  };

  // ────────────────────────── Edición ──────────────────────────

  const abrirEdicion = (dispositivo: Dispositivo) => {
    setNombreEdit(dispositivo.nombre);
    setUbicacionEdit(dispositivo.ubicacion);
    setErrorNombreEdit("");
    setBusquedaEdit("");
    buscadorEdit.limpiar();
    setMostrarEdicion(dispositivo);
  };

  const cerrarEdicion = () => {
    setMostrarEdicion(null);
    buscadorEdit.limpiar();
  };

  const confirmarEdicion = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!mostrarEdicion) return;
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
        mostrarEdicion.idDispositivo,
        nombreEdit.trim(),
        ubicacionEdit.idUbicacion
      );
      dispositivos.actualizar((actuales) =>
        actuales?.map((d) => (d.idDispositivo === actualizado.idDispositivo ? actualizado : d))
      );
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

  // ────────────────────────── Activar / Desactivar ──────────────────────────

  const confirmarCambioEstado = async () => {
    if (!mostrarConfirmarEstado) return;
    setProcesandoEstado(true);
    try {
      const actualizado = await cambiarEstadoDispositivo(
        mostrarConfirmarEstado.idDispositivo,
        !mostrarConfirmarEstado.activo
      );
      dispositivos.actualizar((actuales) =>
        actuales?.map((d) => (d.idDispositivo === actualizado.idDispositivo ? actualizado : d))
      );
      setMostrarConfirmarEstado(null);
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

  return (
    <main className="devices-page">
      <div className="alerts-layout">

        <div className="devices-container">
          <span className="devices-label">DISPOSITIVOS</span>

          <div className="devices-header">
            <div>
              <h1>Mis dispositivos</h1>
              <p>Dispositivos ESP32 vinculados a tu cuenta.</p>
            </div>

            <button
              type="button"
              className="devices-register-button"
              onClick={abrirRegistro}
            >
              <i className="bi bi-plus-lg"></i>
              Registrar dispositivo
            </button>
          </div>

          {/* RESUMEN */}
          <section className="devices-summary">
            <div className="devices-summary-card">
              <i className="bi bi-pc-display"></i>
              <div>
                <span>Total dispositivos</span>
                <strong>{dispositivos.datos ? lista.length : "—"}</strong>
                <small>Vinculados a tu cuenta</small>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-wifi"></i>
              <div>
                <span>En línea</span>
                <strong>{dispositivos.datos ? enLinea : "—"}</strong>
                <small>Lectura en los últimos 5 min</small>
              </div>
            </div>

            <div className="devices-summary-card">
              <i className="bi bi-wifi-off"></i>
              <div>
                <span>Fuera de línea</span>
                <strong>{dispositivos.datos ? lista.length - enLinea : "—"}</strong>
                <small>Sin lectura reciente</small>
              </div>
            </div>
          </section>

          {/* CARGA */}
          {dispositivos.cargando && !dispositivos.datos && (
            <div className="text-center py-4" role="status">
              <div className="spinner-border text-primary" aria-hidden="true"></div>
              <p className="mt-2">Cargando tus dispositivos...</p>
            </div>
          )}

          {/* ERROR */}
          {dispositivos.error !== undefined && (
            <div className="alert alert-danger" role="alert">
              {mensajeDeError(dispositivos.error, "No se pudieron cargar los dispositivos.")}
              <button type="button" className="btn btn-link btn-sm" onClick={dispositivos.recargar}>
                Reintentar
              </button>
            </div>
          )}

          {/* VACÍO */}
          {dispositivos.datos !== undefined && lista.length === 0 && (
            <div className="device-details-empty">
              <i className="bi bi-cpu"></i>
              <h2>Aún no tienes dispositivos</h2>
              <p>Registra tu primer ESP32 para empezar a recibir lecturas ambientales.</p>
            </div>
          )}

          {/* TARJETAS */}
          {lista.length > 0 && (
            <section className="devices-section">
              <div className="devices-section-header">
                <h2>Tus dispositivos ESP32</h2>
                <span>{lista.length} {lista.length === 1 ? "dispositivo" : "dispositivos"}</span>
              </div>

              <div className="devices-grid">
                {lista.map((dispositivo) => (
                  <article key={dispositivo.idDispositivo} className="devices-card">
                    <div className="devices-card-header">
                      <div className="devices-card-icon">
                        <i className="bi bi-cpu"></i>
                      </div>

                      <div style={{ display: "flex", gap: "8px", alignItems: "center" }}>
                        {/* Badge Activo/Inactivo */}
                        <span
                          className={`devices-status ${dispositivo.activo ? "connected" : "disconnected"}`}
                        >
                          {dispositivo.activo ? "Activo" : "Inactivo"}
                        </span>
                        {/* Punto En línea / Fuera de línea */}
                        <span
                          title={dispositivo.enLinea ? "En línea" : "Fuera de línea"}
                          style={{
                            display: "inline-block",
                            width: 10,
                            height: 10,
                            borderRadius: "50%",
                            background: dispositivo.enLinea ? "#22c55e" : "#94a3b8",
                            flexShrink: 0,
                          }}
                          aria-label={dispositivo.enLinea ? "En línea" : "Fuera de línea"}
                        />
                      </div>
                    </div>

                    <h3>{dispositivo.nombre}</h3>

                    <p className="devices-location">
                      <i className="bi bi-geo-alt"></i>
                      {lugar(dispositivo.ubicacion)}
                    </p>

                    <p className="devices-last-reading">
                      <i className="bi bi-clock"></i>
                      {textoUltimaConexion(dispositivo.ultimaConexion)}
                    </p>

                    {dispositivo.ultimaLectura && (
                      <>
                        <div className="devices-readings-title">Última lectura</div>
                        <div className="devices-readings">
                          <div>
                            <i className="bi bi-thermometer-half"></i>
                            <span>Temperatura</span>
                            <strong>
                              {dispositivo.ultimaLectura.temperatura !== null
                                ? `${dispositivo.ultimaLectura.temperatura} °C`
                                : "—"}
                            </strong>
                          </div>

                          <div>
                            <i className="bi bi-droplet"></i>
                            <span>Humedad</span>
                            <strong>
                              {dispositivo.ultimaLectura.humedad !== null
                                ? `${dispositivo.ultimaLectura.humedad}%`
                                : "—"}
                            </strong>
                          </div>

                          <div>
                            <i className="bi bi-cloud-rain"></i>
                            <span>Lluvia</span>
                            <strong>
                              {dispositivo.ultimaLectura.lluvia === null
                                ? "—"
                                : dispositivo.ultimaLectura.lluvia
                                ? "Sí"
                                : "No"}
                            </strong>
                          </div>

                          <div>
                            <i className="bi bi-brightness-high"></i>
                            <span>Luz</span>
                            <strong>
                              {dispositivo.ultimaLectura.luz !== null
                                ? `${dispositivo.ultimaLectura.luz}%`
                                : "—"}
                            </strong>
                          </div>
                        </div>
                      </>
                    )}

                    <div className="devices-card-actions">
                      <Link
                        to={`/dispositivos/${dispositivo.idDispositivo}`}
                        className="devices-details-button"
                      >
                        <i className="bi bi-eye"></i>
                        Ver detalle
                      </Link>

                      <button
                        type="button"
                        className="devices-edit-button"
                        onClick={(e) => {
                          e.stopPropagation();
                          abrirEdicion(dispositivo);
                        }}
                        aria-label={`Editar ${dispositivo.nombre}`}
                      >
                        <i className="bi bi-pencil"></i>
                        Editar
                      </button>

                      <button
                        type="button"
                        className="devices-edit-button"
                        onClick={(e) => {
                          e.stopPropagation();
                          setMostrarConfirmarEstado(dispositivo);
                        }}
                        aria-label={`${dispositivo.activo ? "Desactivar" : "Activar"} ${dispositivo.nombre}`}
                      >
                        <i className={`bi bi-${dispositivo.activo ? "pause-circle" : "play-circle"}`}></i>
                        {dispositivo.activo ? "Desactivar" : "Activar"}
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            </section>
          )}
        </div>
      </div>

      {/* ── MODAL REGISTRO ───────────────────────────────── */}
      {mostrarRegistro && (
        <div
          className="devices-modal-overlay"
          onClick={cerrarRegistro}
        >
          <div
            className="devices-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-registro-titulo"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="devices-modal-header">
              <div>
                <h2 id="modal-registro-titulo">Registrar dispositivo</h2>
                <p>Agrega un nuevo ESP32 a tu cuenta.</p>
              </div>
              <button
                type="button"
                className="devices-modal-close"
                onClick={cerrarRegistro}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={(e) => { void confirmarRegistro(e); }}>
              {/* Nombre */}
              <div className="devices-form-group">
                <label htmlFor="reg-nombre">Nombre del dispositivo</label>
                <input
                  id="reg-nombre"
                  type="text"
                  value={nombreReg}
                  onChange={(e) => setNombreReg(e.target.value)}
                  placeholder="Ej. Sensor de terraza"
                  maxLength={100}
                  required
                />
                {errorNombreReg && (
                  <div className="devices-form-error" role="alert">{errorNombreReg}</div>
                )}
              </div>

              {/* Ubicación */}
              <div className="devices-form-group">
                <label htmlFor="reg-busqueda-ubicacion">Ubicación</label>

                {ubicacionReg ? (
                  <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 12px", background: "#f1f8ff", borderRadius: "8px" }}>
                    <i className="bi bi-geo-alt-fill" style={{ color: "#087db8" }}></i>
                    <span style={{ flex: 1, fontSize: "14px" }}>
                      <strong>{ubicacionReg.nombre}</strong>{" "}
                      <span style={{ color: "#7895b5" }}>{lugar(ubicacionReg)}</span>
                    </span>
                    <button
                      type="button"
                      style={{ background: "none", border: "none", cursor: "pointer", color: "#7895b5", fontSize: "16px" }}
                      onClick={() => { setUbicacionReg(null); setBusquedaReg(""); buscadorReg.limpiar(); }}
                      aria-label="Cambiar ubicación"
                    >
                      <i className="bi bi-x"></i>
                    </button>
                  </div>
                ) : (
                  <form
                    onSubmit={(e) => { e.preventDefault(); void buscadorReg.buscar(busquedaReg); }}
                    style={{ display: "flex", gap: "8px" }}
                  >
                    <input
                      id="reg-busqueda-ubicacion"
                      type="search"
                      value={busquedaReg}
                      onChange={(e) => setBusquedaReg(e.target.value)}
                      placeholder="Buscar ciudad o municipio"
                      maxLength={100}
                      style={{ flex: 1, padding: "10px 12px", border: "1px solid #d9e9f7", borderRadius: "8px", fontSize: "14px" }}
                    />
                    <button
                      type="submit"
                      className="devices-save-button"
                      style={{ padding: "10px 14px" }}
                      disabled={buscadorReg.buscando}
                    >
                      {buscadorReg.buscando ? "..." : "Buscar"}
                    </button>
                  </form>
                )}

                {buscadorReg.error && (
                  <div className="devices-form-error" role="alert">{buscadorReg.error}</div>
                )}
                {buscadorReg.resultados && (
                  <ResultadosUbicacion
                    resultados={buscadorReg.resultados}
                    alElegir={(u) => {
                      setUbicacionReg(u);
                      setErrorUbicacionReg("");
                      buscadorReg.limpiar();
                    }}
                    textoAccion="Seleccionar"
                  />
                )}
                {errorUbicacionReg && (
                  <div className="devices-form-error" role="alert">{errorUbicacionReg}</div>
                )}
              </div>

              <div className="devices-modal-actions">
                <button type="button" className="devices-cancel-button" onClick={cerrarRegistro}>
                  Cancelar
                </button>
                <button type="submit" className="devices-save-button" disabled={guardandoReg}>
                  <i className="bi bi-check2"></i>
                  {guardandoReg ? "Registrando..." : "Registrar"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL API KEY (se muestra UNA SOLA VEZ) ─────── */}
      {mostrarApiKey && resultadoRegistro && (
        <div className="devices-modal-overlay">
          <div
            className="devices-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-apikey-titulo"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="devices-modal-header">
              <div>
                <h2 id="modal-apikey-titulo">¡Dispositivo registrado!</h2>
                <p>Copia tu API Key ahora. No podrás verla de nuevo.</p>
              </div>
            </div>

            <div style={{ marginBottom: "20px" }}>
              <label style={{ display: "block", fontSize: "13px", fontWeight: 700, color: "#123455", marginBottom: "8px" }}>
                API Key del dispositivo
              </label>
              <div style={{
                display: "flex",
                alignItems: "center",
                gap: "10px",
                padding: "14px",
                background: "#f1f8ff",
                border: "1px solid #d9e9f7",
                borderRadius: "10px",
              }}>
                <code
                  id="api-key-valor"
                  style={{ flex: 1, fontFamily: "monospace", fontSize: "13px", wordBreak: "break-all", color: "#075580" }}
                >
                  {resultadoRegistro.apiKey}
                </code>
                <button
                  type="button"
                  className="devices-save-button"
                  onClick={copiarApiKey}
                  style={{ flexShrink: 0, padding: "10px 14px" }}
                  aria-label="Copiar API Key"
                >
                  <i className={`bi bi-${apiKeyCopied ? "check2" : "clipboard"}`}></i>
                  {apiKeyCopied ? "Copiada" : "Copiar"}
                </button>
              </div>
            </div>

            <div
              className="devices-form-error"
              style={{ background: "#fff7e0", borderColor: "#f5c842", color: "#7d5a0a" }}
              role="note"
            >
              <i className="bi bi-exclamation-triangle" style={{ marginRight: "8px" }}></i>
              {resultadoRegistro.aviso}
            </div>

            <div className="devices-modal-actions" style={{ marginTop: "20px" }}>
              <button
                type="button"
                className="devices-save-button"
                onClick={cerrarApiKey}
              >
                <i className="bi bi-check-circle"></i>
                Ya la guardé
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── MODAL EDICIÓN ────────────────────────────────── */}
      {mostrarEdicion && (
        <div
          className="devices-modal-overlay"
          onClick={cerrarEdicion}
        >
          <div
            className="devices-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-edicion-titulo"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="devices-modal-header">
              <div>
                <h2 id="modal-edicion-titulo">Editar dispositivo</h2>
                <p>Modifica el nombre o la ubicación.</p>
              </div>
              <button
                type="button"
                className="devices-modal-close"
                onClick={cerrarEdicion}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <form onSubmit={(e) => { void confirmarEdicion(e); }}>
              <div className="devices-form-group">
                <label htmlFor="edit-nombre">Nombre del dispositivo</label>
                <input
                  id="edit-nombre"
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

              {/* Ubicación edición */}
              <div className="devices-form-group">
                <label htmlFor="edit-busqueda-ubicacion">Ubicación</label>

                {ubicacionEdit ? (
                  <div style={{ display: "flex", alignItems: "center", gap: "10px", padding: "10px 12px", background: "#f1f8ff", borderRadius: "8px" }}>
                    <i className="bi bi-geo-alt-fill" style={{ color: "#087db8" }}></i>
                    <span style={{ flex: 1, fontSize: "14px" }}>
                      <strong>{ubicacionEdit.nombre}</strong>{" "}
                      <span style={{ color: "#7895b5" }}>{lugar(ubicacionEdit)}</span>
                    </span>
                    <button
                      type="button"
                      style={{ background: "none", border: "none", cursor: "pointer", color: "#7895b5", fontSize: "16px" }}
                      onClick={() => { setUbicacionEdit(null); setBusquedaEdit(""); buscadorEdit.limpiar(); }}
                      aria-label="Cambiar ubicación"
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
                      id="edit-busqueda-ubicacion"
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
                    alElegir={(u) => {
                      setUbicacionEdit(u);
                      buscadorEdit.limpiar();
                    }}
                    textoAccion="Seleccionar"
                  />
                )}
              </div>

              <div className="devices-modal-actions">
                <button type="button" className="devices-cancel-button" onClick={cerrarEdicion}>
                  Cancelar
                </button>
                <button type="submit" className="devices-save-button" disabled={guardandoEdit}>
                  <i className="bi bi-check2"></i>
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
          className="devices-modal-overlay"
          onClick={() => setMostrarConfirmarEstado(null)}
        >
          <div
            className="devices-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="modal-estado-titulo"
            onClick={(e) => e.stopPropagation()}
            style={{ maxWidth: "420px" }}
          >
            <div className="devices-modal-header">
              <div>
                <h2 id="modal-estado-titulo">
                  {mostrarConfirmarEstado.activo ? "Desactivar dispositivo" : "Activar dispositivo"}
                </h2>
              </div>
              <button
                type="button"
                className="devices-modal-close"
                onClick={() => setMostrarConfirmarEstado(null)}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            {mostrarConfirmarEstado.activo ? (
              <p style={{ color: "#7895b5", fontSize: "14px", marginBottom: "20px" }}>
                Al desactivar <strong>{mostrarConfirmarEstado.nombre}</strong>, la API Key quedará
                revocada de inmediato y el dispositivo recibirá error <code>403</code> en su
                siguiente lectura. Para volver a usarlo deberás activarlo de nuevo.
              </p>
            ) : (
              <p style={{ color: "#7895b5", fontSize: "14px", marginBottom: "20px" }}>
                ¿Quieres activar <strong>{mostrarConfirmarEstado.nombre}</strong>? Podrá volver a
                enviar lecturas con su API Key actual.
              </p>
            )}

            <div className="devices-modal-actions">
              <button
                type="button"
                className="devices-cancel-button"
                onClick={() => setMostrarConfirmarEstado(null)}
              >
                Cancelar
              </button>
              <button
                type="button"
                className="devices-save-button"
                onClick={() => { void confirmarCambioEstado(); }}
                disabled={procesandoEstado}
                style={mostrarConfirmarEstado.activo ? { background: "#c33c3c" } : {}}
              >
                {procesandoEstado
                  ? "Procesando..."
                  : mostrarConfirmarEstado.activo
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