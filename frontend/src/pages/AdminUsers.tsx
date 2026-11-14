import { useEffect, useState, type FormEvent, type MouseEvent } from "react";
import "./AdminUsers.css";
import EnlaceVolver from "../components/EnlaceVolver";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useConsulta } from "../hooks/useConsulta";
import { mensajeDeError } from "../services/apiClient";
import { cambiarEstadoUsuario, cambiarRolUsuario, listarUsuariosAdmin } from "../services/adminUsuariosService";
import type { EstadoCuenta, Rol, UsuarioAdmin } from "../types/api";
import { fecha } from "../utils/fechas";

type FiltroEstado = "todos" | EstadoCuenta;
type FiltroRol = "todos" | Rol;

const NOMBRE_ROL: Record<Rol, string> = { USUARIO: "Usuario", ADMINISTRADOR: "Administrador" };

const AVISO_SESIONES = "Se cerrarán todas las sesiones abiertas de esta cuenta.";

function metodosAcceso(usuario: UsuarioAdmin): string {
  const metodos = [usuario.tienePassword ? "Contraseña" : null, usuario.tieneGoogle ? "Google" : null].filter(Boolean);
  return metodos.length > 0 ? metodos.join(" y ") : "Ninguno";
}

// M14 – Gestión de usuarios (solo ADMINISTRADOR).
//   RF-26: GET /api/admin/usuarios con búsqueda (nombre o correo), rol, estado y páginas de 20.
//   RF-27: PATCH /api/admin/usuarios/{id}/rol   RF-28: PATCH /api/admin/usuarios/{id}/estado
// El backend impide quitarse el propio rol, desactivar la propia cuenta y dejar el sistema sin
// administradores activos (RN-BD-09); aquí además se ocultan esas acciones en la fila propia.
// No hay borrado de cuentas (RN-BD-08).
export default function AdminUsers() {
  const { usuario: sesion } = useAuth();
  const { avisar } = useNotificaciones();

  const [texto, setTexto] = useState("");
  const [busqueda, setBusqueda] = useState("");
  const [rol, setRol] = useState<FiltroRol>("todos");
  const [estado, setEstado] = useState<FiltroEstado>("todos");
  const [pagina, setPagina] = useState(1);

  const [detalle, setDetalle] = useState<UsuarioAdmin | null>(null);
  const [edicionRol, setEdicionRol] = useState<{ usuario: UsuarioAdmin; rol: Rol } | null>(null);
  const [cambioEstado, setCambioEstado] = useState<UsuarioAdmin | null>(null);
  const [enviando, setEnviando] = useState(false);
  const [errorAccion, setErrorAccion] = useState<string | null>(null);

  const filtro = {
    busqueda: busqueda || undefined,
    rol: rol === "todos" ? undefined : rol,
    estado: estado === "todos" ? undefined : estado,
    pagina,
  };

  const lista = useConsulta(`admin-usuarios:${JSON.stringify(filtro)}`, (signal) => listarUsuariosAdmin(filtro, signal));
  const totales = useConsulta("admin-usuarios-totales", async (signal) => {
    const [todos, activos, inactivos] = await Promise.all([
      listarUsuariosAdmin({}, signal),
      listarUsuariosAdmin({ estado: "ACTIVO" }, signal),
      listarUsuariosAdmin({ estado: "INACTIVO" }, signal),
    ]);
    return { total: todos.totalElementos, activos: activos.totalElementos, inactivos: inactivos.totalElementos };
  });

  const usuarios = lista.datos?.elementos ?? [];
  const totalPaginas = lista.datos?.totalPaginas ?? 0;
  const hayFiltros = busqueda !== "" || rol !== "todos" || estado !== "todos";

  const cambiarFiltro = (aplicar: () => void) => {
    aplicar();
    setPagina(1);
  };

  const buscar = (evento: FormEvent<HTMLFormElement>) => {
    evento.preventDefault();
    cambiarFiltro(() => setBusqueda(texto.trim()));
  };

  const limpiarFiltros = () =>
    cambiarFiltro(() => {
      setTexto("");
      setBusqueda("");
      setRol("todos");
      setEstado("todos");
    });

  const cerrarVentanas = () => {
    if (enviando) return;
    setDetalle(null);
    setEdicionRol(null);
    setCambioEstado(null);
    setErrorAccion(null);
  };

  const hayVentana = detalle !== null || edicionRol !== null || cambioEstado !== null;

  useEffect(() => {
    if (!hayVentana) return;
    const cerrarConEscape = (evento: KeyboardEvent) => {
      if (evento.key === "Escape") cerrarVentanas();
    };
    document.addEventListener("keydown", cerrarConEscape);
    return () => document.removeEventListener("keydown", cerrarConEscape);
  });

  const ejecutar = async (accion: () => Promise<UsuarioAdmin>, exito: (u: UsuarioAdmin) => string) => {
    setEnviando(true);
    setErrorAccion(null);
    try {
      const actualizado = await accion();
      setEdicionRol(null);
      setCambioEstado(null);
      avisar(exito(actualizado), "exito");
      lista.recargar();
      totales.recargar();
    } catch (error) {
      setErrorAccion(mensajeDeError(error, "No se pudo completar el cambio."));
    } finally {
      setEnviando(false);
    }
  };

  const guardarRol = () => {
    if (!edicionRol) return;
    const { usuario, rol: nuevo } = edicionRol;
    void ejecutar(
      () => cambiarRolUsuario(usuario.idUsuario, nuevo),
      (u) => `${u.nombre} ahora tiene el rol ${NOMBRE_ROL[u.rol]}.`
    );
  };

  const confirmarEstado = () => {
    if (!cambioEstado) return;
    const usuario = cambioEstado;
    void ejecutar(
      () => cambiarEstadoUsuario(usuario.idUsuario, !usuario.activo),
      (u) => (u.activo ? `La cuenta de ${u.nombre} está activa.` : `La cuenta de ${u.nombre} quedó desactivada.`)
    );
  };

  const cerrarAlFondo = (evento: MouseEvent<HTMLDivElement>) => {
    if (evento.target === evento.currentTarget) cerrarVentanas();
  };

  return (
    <div className="admin-users-layout">
      <main className="admin-users-page">
        <div className="admin-users-container">
          <EnlaceVolver to="/admin">Volver al panel administrativo</EnlaceVolver>

          <span className="admin-users-label d-block">ADMINISTRACIÓN</span>

          <div className="admin-users-header">
            <h1>Gestión de usuarios</h1>
            <p>Consulta y administra los usuarios registrados en ClimaPlus.</p>
          </div>

          {/* Buscador */}
          <form className="admin-users-search" role="search" onSubmit={buscar}>
            <label htmlFor="buscar-usuario">Buscar usuario</label>
            <div className="admin-users-search-box">
              <i className="bi bi-search" aria-hidden="true"></i>
              <input
                id="buscar-usuario"
                type="search"
                placeholder="Nombre o correo electrónico"
                maxLength={150}
                value={texto}
                onChange={(evento) => setTexto(evento.target.value)}
              />
              <button type="submit">Buscar</button>
            </div>
          </form>

          {/* Filtros y contadores */}
          <div className="admin-users-toolbar">
            <div className="admin-users-filters" role="group" aria-label="Filtrar por estado">
              {(
                [
                  ["todos", "Todos"],
                  ["ACTIVO", "Activos"],
                  ["INACTIVO", "Inactivos"],
                ] as const
              ).map(([valor, nombre]) => (
                <button
                  key={valor}
                  type="button"
                  className={estado === valor ? "active" : ""}
                  aria-pressed={estado === valor}
                  onClick={() => cambiarFiltro(() => setEstado(valor))}
                >
                  {nombre}
                </button>
              ))}

              <label className="visually-hidden" htmlFor="filtro-rol">
                Rol
              </label>
              <select
                id="filtro-rol"
                className="form-select form-select-sm w-auto"
                value={rol}
                onChange={(evento) => cambiarFiltro(() => setRol(evento.target.value as FiltroRol))}
              >
                <option value="todos">Todos los roles</option>
                <option value="USUARIO">Usuarios</option>
                <option value="ADMINISTRADOR">Administradores</option>
              </select>
            </div>

            <div className="admin-users-count">
              {totales.datos
                ? `${totales.datos.total} registrados · ${totales.datos.activos} activos · ${totales.datos.inactivos} inactivos`
                : "—"}
            </div>
          </div>

          <section className="admin-users-section">
            <h2>Usuarios registrados</h2>
            <p>
              {lista.datos
                ? `Mostrando ${usuarios.length} de ${lista.datos.totalElementos} usuarios.`
                : "Visualiza la información y el estado de los usuarios de la plataforma."}
            </p>
          </section>

          <div className="admin-users-table-container">
            <table className="admin-users-table">
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Correo</th>
                  <th>Rol</th>
                  <th>Estado</th>
                  <th>Fecha de registro</th>
                  <th>Acciones</th>
                </tr>
              </thead>

              <tbody>
                {lista.error !== undefined ? (
                  <tr>
                    <td colSpan={6} className="admin-users-empty text-danger" role="alert">
                      {mensajeDeError(lista.error, "No se pudieron cargar los usuarios.")}
                    </td>
                  </tr>
                ) : lista.cargando && !lista.datos ? (
                  <tr>
                    <td colSpan={6} className="admin-users-empty">
                      Cargando usuarios...
                    </td>
                  </tr>
                ) : usuarios.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="admin-users-empty">
                      <i className="bi bi-search" aria-hidden="true"></i>
                      <p>No se encontraron usuarios con los criterios seleccionados.</p>
                      {hayFiltros && (
                        <button type="button" onClick={limpiarFiltros}>
                          Limpiar filtros
                        </button>
                      )}
                    </td>
                  </tr>
                ) : (
                  usuarios.map((usuario) => {
                    const esPropia = usuario.idUsuario === sesion?.idUsuario;
                    return (
                      <tr key={usuario.idUsuario}>
                        <td className="admin-users-name">
                          {usuario.nombre}
                          {esPropia && <small className="text-secondary fw-normal"> (tú)</small>}
                        </td>
                        <td>{usuario.correo}</td>
                        <td>
                          <span className={`admin-users-role ${usuario.rol === "ADMINISTRADOR" ? "admin" : "user"}`}>
                            {usuario.rol}
                          </span>
                        </td>
                        <td>
                          <span className={`admin-users-status ${usuario.activo ? "active" : "inactive"}`}>
                            {usuario.activo ? "Activo" : "Inactivo"}
                          </span>
                        </td>
                        <td>{fecha(usuario.fechaCreacion)}</td>
                        <td>
                          <div className="admin-users-actions">
                            <button
                              type="button"
                              aria-label={`Ver ${usuario.nombre}`}
                              onClick={() => setDetalle(usuario)}
                            >
                              Ver
                            </button>
                            {esPropia ? (
                              <small className="text-secondary ms-1">Tu cuenta</small>
                            ) : (
                              <>
                                <span aria-hidden="true">|</span>
                                <button
                                  type="button"
                                  aria-label={`Cambiar rol de ${usuario.nombre}`}
                                  onClick={() => {
                                    setErrorAccion(null);
                                    setEdicionRol({ usuario, rol: usuario.rol });
                                  }}
                                >
                                  Cambiar rol
                                </button>
                                <span aria-hidden="true">|</span>
                                <button
                                  type="button"
                                  aria-label={`${usuario.activo ? "Desactivar" : "Activar"} a ${usuario.nombre}`}
                                  onClick={() => {
                                    setErrorAccion(null);
                                    setCambioEstado(usuario);
                                  }}
                                >
                                  {usuario.activo ? "Desactivar" : "Activar"}
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="d-flex justify-content-between align-items-center flex-wrap gap-2 mt-3">
            <span className="admin-users-count">
              Página {pagina} de {Math.max(totalPaginas, 1)}
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

          <div className="admin-users-info">
            <i className="bi bi-info-circle" aria-hidden="true"></i>
            <span>
              Las cuentas no se eliminan: se desactivan. Cambiar el rol o el estado cierra las sesiones abiertas de
              esa cuenta. Siempre debe quedar al menos un administrador activo.
            </span>
          </div>

          <footer className="admin-users-footer">Gestión de usuarios · ClimaPlus</footer>
        </div>
      </main>

      {/* Detalle */}
      {detalle && (
        <div className="admin-users-modal-overlay" onMouseDown={cerrarAlFondo}>
          <div className="admin-users-modal" role="dialog" aria-modal="true" aria-labelledby="usuario-detalle-titulo">
            <div className="admin-users-modal-header">
              <h2 id="usuario-detalle-titulo">Detalle del usuario</h2>
              <button type="button" onClick={cerrarVentanas} aria-label="Cerrar">
                <i className="bi bi-x-lg"></i>
              </button>
            </div>
            <div className="admin-users-modal-body">
              <p><strong>Nombre:</strong> {detalle.nombre}</p>
              <p><strong>Correo:</strong> {detalle.correo}</p>
              <p><strong>Rol:</strong> {NOMBRE_ROL[detalle.rol]}</p>
              <p><strong>Estado:</strong> {detalle.activo ? "Activo" : "Inactivo"}</p>
              <p><strong>Correo verificado:</strong> {detalle.correoVerificado ? "Sí" : "No"}</p>
              <p><strong>Métodos de acceso:</strong> {metodosAcceso(detalle)}</p>
              <p><strong>Fecha de registro:</strong> {fecha(detalle.fechaCreacion)}</p>
            </div>
            <button type="button" onClick={cerrarVentanas}>
              Cerrar
            </button>
          </div>
        </div>
      )}

      {/* Cambiar rol (RF-27) */}
      {edicionRol && (
        <div className="admin-users-modal-overlay" onMouseDown={cerrarAlFondo}>
          <div className="admin-users-modal" role="dialog" aria-modal="true" aria-labelledby="usuario-rol-titulo">
            <div className="admin-users-modal-header">
              <h2 id="usuario-rol-titulo">Cambiar rol</h2>
              <button type="button" onClick={cerrarVentanas} aria-label="Cerrar" disabled={enviando}>
                <i className="bi bi-x-lg"></i>
              </button>
            </div>
            <div className="admin-users-modal-body">
              <p>
                <strong>{edicionRol.usuario.nombre}</strong> ({edicionRol.usuario.correo})
              </p>
              <fieldset className="mb-3">
                <legend className="fs-6 fw-bold">Nuevo rol</legend>
                {(["USUARIO", "ADMINISTRADOR"] as const).map((valor) => (
                  <div className="form-check" key={valor}>
                    <input
                      className="form-check-input"
                      type="radio"
                      name="nuevo-rol"
                      id={`rol-${valor}`}
                      value={valor}
                      checked={edicionRol.rol === valor}
                      disabled={enviando}
                      onChange={() => setEdicionRol({ ...edicionRol, rol: valor })}
                    />
                    <label className="form-check-label" htmlFor={`rol-${valor}`}>
                      {NOMBRE_ROL[valor]}
                    </label>
                  </div>
                ))}
              </fieldset>
              <div className="alert alert-warning py-2 mb-0">{AVISO_SESIONES}</div>
              {errorAccion && (
                <div className="alert alert-danger py-2 mt-3 mb-0" role="alert">
                  {errorAccion}
                </div>
              )}
            </div>
            <div className="admin-users-confirm-actions">
              <button type="button" className="admin-users-cancel-button" onClick={cerrarVentanas} disabled={enviando}>
                Cancelar
              </button>
              <button
                type="button"
                className="admin-users-confirm-button"
                onClick={guardarRol}
                disabled={enviando || edicionRol.rol === edicionRol.usuario.rol}
              >
                {enviando ? "Guardando..." : "Guardar rol"}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Activar / desactivar (RF-28) */}
      {cambioEstado && (
        <div className="admin-users-modal-overlay" onMouseDown={cerrarAlFondo}>
          <div
            className="admin-users-modal admin-users-confirm-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="usuario-estado-titulo"
          >
            <div className="admin-users-modal-header">
              <h2 id="usuario-estado-titulo">{cambioEstado.activo ? "Desactivar cuenta" : "Activar cuenta"}</h2>
              <button type="button" onClick={cerrarVentanas} aria-label="Cerrar" disabled={enviando}>
                <i className="bi bi-x-lg"></i>
              </button>
            </div>
            <div className="admin-users-modal-body">
              <p>
                {cambioEstado.activo
                  ? `¿Desactivar la cuenta de ${cambioEstado.nombre}? No podrá iniciar sesión hasta que se reactive.`
                  : `¿Activar la cuenta de ${cambioEstado.nombre}? Podrá volver a iniciar sesión.`}
              </p>
              <div className="alert alert-warning py-2 mb-0">{AVISO_SESIONES}</div>
              {errorAccion && (
                <div className="alert alert-danger py-2 mt-3 mb-0" role="alert">
                  {errorAccion}
                </div>
              )}
            </div>
            <div className="admin-users-confirm-actions">
              <button type="button" className="admin-users-cancel-button" onClick={cerrarVentanas} disabled={enviando}>
                Cancelar
              </button>
              <button type="button" className="admin-users-confirm-button" onClick={confirmarEstado} disabled={enviando}>
                {enviando ? "Guardando..." : cambioEstado.activo ? "Desactivar" : "Activar"}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
