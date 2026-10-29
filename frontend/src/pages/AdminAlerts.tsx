import { useState } from "react";
import "./AdminAlerts.css";
import { useConsulta } from "../hooks/useConsulta";
import { mensajeDeError } from "../services/apiClient";
import { listarAlertasAdmin, obtenerAlertaAdmin } from "../services/alertasService";
import type { AlertaAdmin, EstadoAlerta, TipoAlerta } from "../types/api";
import { numero } from "../utils/clima";
import { fecha, hora } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

function origen(alerta: AlertaAdmin): string {
  return alerta.origen.tipo === "ESP32"
    ? `Sensor${alerta.origen.nombreDispositivo ? ` (${alerta.origen.nombreDispositivo})` : ""}`
    : "Clima";
}

const ESTADO_VISIBLE: Record<EstadoAlerta, string> = { NUEVA: "NUEVA", LEIDA: "LEÍDA" };

// M15 – Alertas del sistema (solo ADMINISTRADOR, solo lectura): GET /api/admin/alertas con
// filtros de estado, tipo y usuario, y GET /api/admin/alertas/{id}. El administrador no marca
// alertas ajenas como leídas: eso solo lo hace su destinatario (RN-BD-26).
export default function AdminAlerts() {
  const [estado, setEstado] = useState("todos");
  const [tipo, setTipo] = useState("todos");
  const [ubicacion, setUbicacion] = useState("todas");
  const [usuario, setUsuario] = useState("todos");
  const [pagina, setPagina] = useState(1);
  const [alertaSeleccionadaId, setAlertaSeleccionadaId] = useState<
    number | null
  >(null);

  const filtro = {
    estado: estado === "todos" ? undefined : (estado as EstadoAlerta),
    tipo: tipo === "todos" ? undefined : (tipo as TipoAlerta),
    idUsuario: usuario === "todos" ? undefined : Number(usuario),
    pagina,
  };

  const lista = useConsulta(`admin-alertas:${JSON.stringify(filtro)}`, (signal) => listarAlertasAdmin(filtro, signal));
  const totales = useConsulta("admin-alertas-totales", async (signal) => {
    const [todas, nuevas, leidas] = await Promise.all([
      listarAlertasAdmin({}, signal),
      listarAlertasAdmin({ estado: "NUEVA" }, signal),
      listarAlertasAdmin({ estado: "LEIDA" }, signal),
    ]);
    return { total: todas.totalElementos, nuevas: nuevas.totalElementos, leidas: leidas.totalElementos };
  });
  const detalle = useConsulta(
    alertaSeleccionadaId === null ? null : `admin-alerta:${alertaSeleccionadaId}`,
    (signal) => obtenerAlertaAdmin(alertaSeleccionadaId!, signal)
  );

  const alertas: AlertaAdmin[] = lista.datos?.elementos ?? [];
  const totalPaginas = lista.datos?.totalPaginas ?? 0;

  // Opciones de los filtros a partir de las alertas cargadas.
  const [usuariosVistos, setUsuariosVistos] = useState(new Map<number, string>());
  const nuevosUsuarios = alertas.filter((alerta) => !usuariosVistos.has(alerta.usuario.idUsuario));
  if (nuevosUsuarios.length > 0) {
    const copia = new Map(usuariosVistos);
    for (const alerta of nuevosUsuarios) copia.set(alerta.usuario.idUsuario, alerta.usuario.nombre);
    setUsuariosVistos(copia);
  }
  const ubicaciones = [...new Set(alertas.map((alerta) => alerta.ubicacion.nombre))];

  // La API no filtra por ubicación: este filtro se aplica sobre la página cargada.
  const alertasFiltradas =
    ubicacion === "todas" ? alertas : alertas.filter((alerta) => alerta.ubicacion.nombre === ubicacion);

  const cambiar = (aplicar: () => void) => {
    aplicar();
    setPagina(1);
  };

  const total = totales.datos?.total;
  const nuevas = totales.datos?.nuevas;
  const leidas = totales.datos?.leidas;
  const alertaSeleccionada = detalle.datos ?? null;

  return (
    <div className="admin-alerts-layout">

      <main className="admin-alerts-page">
        <div className="admin-alerts-container">
          <EnlaceVolver to="/admin">Volver al panel administrativo</EnlaceVolver>

          <div className="admin-alerts-heading">
            <span>ADMINISTRACIÓN / ALERTAS</span>
            <h1>Alertas del sistema</h1>
            <p>
              Consulta las alertas ambientales generadas por ClimaPlus.
            </p>
          </div>

          <div className="admin-alerts-stats">
            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon">
                <i className="bi bi-bell"></i>
              </div>
              <div>
                <span>Total</span>
                <strong>{total ?? "—"}</strong>
                <small>Alertas generadas</small>
              </div>
            </article>

            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon warning">
                <i className="bi bi-bell"></i>
              </div>
              <div>
                <span>Nuevas</span>
                <strong>{nuevas ?? "—"}</strong>
                <small>Pendientes de leer</small>
              </div>
            </article>

            <article className="admin-alerts-stat">
              <div className="admin-alerts-stat-icon">
                <i className="bi bi-check2"></i>
              </div>
              <div>
                <span>Leídas</span>
                <strong>{leidas ?? "—"}</strong>
                <small>Alertas revisadas</small>
              </div>
            </article>
          </div>

          <div className="admin-alerts-filters">
            <label>
              Estado
              <select value={estado} onChange={(e) => cambiar(() => setEstado(e.target.value))}>
                <option value="todos">Todos</option>
                <option value="NUEVA">Nuevas</option>
                <option value="LEIDA">Leídas</option>
              </select>
            </label>

            <label>
              Tipo
              <select value={tipo} onChange={(e) => cambiar(() => setTipo(e.target.value))}>
                <option value="todos">Todos los tipos</option>
                <option value="LLUVIA">Lluvia</option>
                <option value="TEMPERATURA_BAJA">Temperatura baja</option>
                <option value="TEMPERATURA_ALTA">Temperatura alta</option>
              </select>
            </label>

            <label>
              Ubicación
              <select
                value={ubicacion}
                onChange={(e) => setUbicacion(e.target.value)}
              >
                <option value="todas">Todas las ubicaciones</option>
                {ubicaciones.map((lugar) => (
                  <option key={lugar} value={lugar}>
                    {lugar}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Usuario
              <select
                value={usuario}
                onChange={(e) => cambiar(() => setUsuario(e.target.value))}
              >
                <option value="todos">Todos los usuarios</option>
                {[...usuariosVistos.entries()].map(([idUsuario, nombre]) => (
                  <option key={idUsuario} value={idUsuario}>
                    {nombre}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <section className="admin-alerts-section">
            <div className="admin-alerts-section-heading">
              <h2>Alertas generadas</h2>
              <span>
                Mostrando {alertasFiltradas.length} de {lista.datos?.totalElementos ?? 0} alertas
              </span>
            </div>

            <div className="admin-alerts-table-wrapper">
              <table className="admin-alerts-table">
                <thead>
                  <tr>
                    <th>Usuario</th>
                    <th>Ubicación</th>
                    <th>Tipo</th>
                    <th>Mensaje</th>
                    <th>Origen</th>
                    <th>Estado</th>
                    <th>Fecha / hora</th>
                    <th>Acción</th>
                  </tr>
                </thead>

                <tbody>
                  {lista.error !== undefined ? (
                    <tr>
                      <td colSpan={8} className="admin-alerts-empty text-danger" role="alert">
                        {mensajeDeError(lista.error, "No se pudieron cargar las alertas.")}
                      </td>
                    </tr>
                  ) : lista.cargando && !lista.datos ? (
                    <tr>
                      <td colSpan={8} className="admin-alerts-empty">Cargando alertas...</td>
                    </tr>
                  ) : alertasFiltradas.length === 0 ? (
                    <tr>
                      <td colSpan={8} className="admin-alerts-empty">
                        No se encontraron alertas con los filtros seleccionados.
                      </td>
                    </tr>
                  ) : (
                    alertasFiltradas.map((alerta) => (
                      <tr key={alerta.idAlerta}>
                        <td className="admin-alerts-user">{alerta.usuario.nombre}</td>
                        <td>{alerta.ubicacion.nombre}</td>
                        <td>
                          <span className="admin-alerts-type">
                            {alerta.tipo}
                          </span>
                        </td>
                        <td>{alerta.mensaje}</td>
                        <td>{origen(alerta)}</td>
                        <td>
                          <span
                            className={`admin-alerts-status ${
                              alerta.estado === "NUEVA" ? "new" : "read"
                            }`}
                          >
                            {ESTADO_VISIBLE[alerta.estado]}
                          </span>
                        </td>
                        <td>
                          {fecha(alerta.fechaGeneracion)}
                          <br />
                          <small>{hora(alerta.fechaGeneracion)}</small>
                        </td>
                        <td>
                          <button
                            type="button"
                            className="admin-alerts-detail-button"
                            onClick={() => setAlertaSeleccionadaId(alerta.idAlerta)}
                          >
                            Ver detalle
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>

              <div className="admin-alerts-table-footer d-flex justify-content-between align-items-center flex-wrap gap-2">
                <span>
                  Página {pagina} de {Math.max(totalPaginas, 1)} · El filtro de ubicación se aplica a la página actual
                </span>
                <span className="d-flex gap-2">
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    onClick={() => setPagina((p) => Math.max(1, p - 1))}
                    disabled={pagina <= 1}
                  >
                    Anterior
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm btn-outline-secondary"
                    onClick={() => setPagina((p) => p + 1)}
                    disabled={pagina >= totalPaginas}
                  >
                    Siguiente
                  </button>
                </span>
              </div>
            </div>
          </section>

          <div className="admin-alerts-info">
            <i className="bi bi-info-circle"></i>
            <span>
              Origen Clima: consulta de clima actual. Origen Sensor: lectura
              de un dispositivo ESP32. Solo lluvia, temperatura alta y
              temperatura baja.
            </span>
          </div>

          <footer className="admin-alerts-footer">
            Alertas del sistema · Solo lectura · ClimaPlus
          </footer>
        </div>
      </main>

      {alertaSeleccionadaId !== null && (
        <div
          className="admin-alerts-overlay"
          onMouseDown={(e) => {
            if (e.target === e.currentTarget) {
              setAlertaSeleccionadaId(null);
            }
          }}
        >
          <div
            className="admin-alerts-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="admin-alerts-modal-title"
          >
            <div className="admin-alerts-modal-header">
              <h2 id="admin-alerts-modal-title">Detalle de alerta</h2>
              <button
                type="button"
                onClick={() => setAlertaSeleccionadaId(null)}
                aria-label="Cerrar"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <div className="admin-alerts-modal-body">
              {detalle.cargando && <p>Cargando detalle...</p>}
              {detalle.error !== undefined && (
                <p className="text-danger" role="alert">
                  {mensajeDeError(detalle.error, "No se pudo cargar el detalle.")}
                </p>
              )}
              {alertaSeleccionada && (
                <>
                  <p><strong>Usuario:</strong> {alertaSeleccionada.usuario.nombre} ({alertaSeleccionada.usuario.correo})</p>
                  <p><strong>Ubicación:</strong> {alertaSeleccionada.ubicacion.nombre}</p>
                  <p><strong>Tipo:</strong> {alertaSeleccionada.tipo}</p>
                  <p><strong>Mensaje:</strong> {alertaSeleccionada.mensaje}</p>
                  {alertaSeleccionada.valorDetectado !== null && alertaSeleccionada.valorUmbral !== null && (
                    <p>
                      <strong>Valor detectado / umbral:</strong> {numero(alertaSeleccionada.valorDetectado, 2)} °C /{" "}
                      {numero(alertaSeleccionada.valorUmbral, 2)} °C
                    </p>
                  )}
                  <p><strong>Origen:</strong> {origen(alertaSeleccionada)}</p>
                  <p><strong>Estado:</strong> {ESTADO_VISIBLE[alertaSeleccionada.estado]}</p>
                  <p>
                    <strong>Fecha:</strong> {fecha(alertaSeleccionada.fechaGeneracion)} ·{" "}
                    {hora(alertaSeleccionada.fechaGeneracion)}
                  </p>
                </>
              )}
            </div>

            <div className="admin-alerts-modal-actions">
              <button
                type="button"
                className="secondary"
                onClick={() => setAlertaSeleccionadaId(null)}
              >
                Cerrar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
