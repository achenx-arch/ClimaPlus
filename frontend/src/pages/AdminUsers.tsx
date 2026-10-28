import { useEffect, useState } from "react";
import AdminSidebar from "../components/AdminSidebar";
import "./AdminUsers.css";

type EstadoUsuario = "Activo" | "Inactivo";
type RolUsuario = "ADMIN" | "USUARIO";
type FiltroUsuario = "todos" | "activos" | "inactivos";

interface Usuario {
  id: number;
  nombre: string;
  correo: string;
  rol: RolUsuario;
  estado: EstadoUsuario;
  fecha: string;
}

// Datos temporales para visualizar la pantalla.
// Se reemplazarán por los usuarios reales cuando conectemos el backend.
const usuariosIniciales: Usuario[] = [
  {
    id: 1,
    nombre: "Carlos Méndez",
    correo: "carlos.mendez@example.com",
    rol: "USUARIO",
    estado: "Activo",
    fecha: "1 oct. 2026",
  },
  {
    id: 2,
    nombre: "Ana López",
    correo: "ana.lopez@example.com",
    rol: "USUARIO",
    estado: "Activo",
    fecha: "7 oct. 2026",
  },
  {
    id: 3,
    nombre: "María Hernández",
    correo: "maria.hernandez@example.com",
    rol: "ADMIN",
    estado: "Activo",
    fecha: "15 sep. 2026",
  },
  {
    id: 4,
    nombre: "Diego Morales",
    correo: "diego.morales@example.com",
    rol: "USUARIO",
    estado: "Inactivo",
    fecha: "28 sep. 2026",
  },
  {
  id: 5,
  nombre: "Luis Gómez",
  correo: "luis.gomez@example.com",
  rol: "USUARIO",
  estado: "Activo",
  fecha: "2 oct. 2026",
},
{
  id: 6,
  nombre: "Sofía Pérez",
  correo: "sofia.perez@example.com",
  rol: "USUARIO",
  estado: "Inactivo",
  fecha: "30 sep. 2026",
},
];

export default function AdminUsers() {
  const [usuarios, setUsuarios] = useState<Usuario[]>(usuariosIniciales);
  const [busqueda, setBusqueda] = useState("");
  const [filtro, setFiltro] = useState<FiltroUsuario>("todos");
  const [usuarioSeleccionadoId, setUsuarioSeleccionadoId] = useState<
    number | null
  >(null);
  const [usuarioPendienteId, setUsuarioPendienteId] = useState<
    number | null
  >(null);

  const usuarioSeleccionado =
    usuarios.find((usuario) => usuario.id === usuarioSeleccionadoId) ?? null;

  const usuarioPendiente =
    usuarios.find((usuario) => usuario.id === usuarioPendienteId) ?? null;

  const totalUsuarios = usuarios.length;

  const usuariosActivos = usuarios.filter(
    (usuario) => usuario.estado === "Activo"
  ).length;

  const usuariosInactivos = usuarios.filter(
    (usuario) => usuario.estado === "Inactivo"
  ).length;

  const usuariosFiltrados = usuarios.filter((usuario) => {
    const coincideEstado =
      filtro === "todos" ||
      (filtro === "activos" && usuario.estado === "Activo") ||
      (filtro === "inactivos" && usuario.estado === "Inactivo");

    const textoBusqueda = busqueda.trim().toLowerCase();

    const coincideBusqueda =
      usuario.nombre.toLowerCase().includes(textoBusqueda) ||
      usuario.correo.toLowerCase().includes(textoBusqueda);

    return coincideEstado && coincideBusqueda;
  });

  const cambiarEstadoUsuario = (id: number) => {
    setUsuarios((usuariosActuales) =>
      usuariosActuales.map((usuario) =>
        usuario.id === id
          ? {
              ...usuario,
              estado: (usuario.estado === "Activo"
                ? "Inactivo"
                : "Activo") as EstadoUsuario,
            }
          : usuario
      )
    );

    setUsuarioPendienteId(null);
  };

  const cerrarVentanas = () => {
    setUsuarioSeleccionadoId(null);
    setUsuarioPendienteId(null);
  };

  // Cerrar ventanas con la tecla Escape.
  useEffect(() => {
    if (usuarioSeleccionadoId === null && usuarioPendienteId === null) {
      return;
    }

    const cerrarConEscape = (evento: KeyboardEvent) => {
      if (evento.key === "Escape") {
        cerrarVentanas();
      }
    };

    document.addEventListener("keydown", cerrarConEscape);

    return () => {
      document.removeEventListener("keydown", cerrarConEscape);
    };
  }, [usuarioSeleccionadoId, usuarioPendienteId]);

  return (
    <div className="admin-users-layout">
      <AdminSidebar />

      <main className="admin-users-page">
        <div className="admin-users-container">

          <span className="admin-users-label">ADMINISTRACIÓN</span>

          <div className="admin-users-header">
            <h1>Gestión de usuarios</h1>
            <p>
              Consulta y administra los usuarios registrados en ClimaPlus.
            </p>
          </div>

          {/* Buscador */}
          <div className="admin-users-search">
            <label htmlFor="buscar-usuario">Buscar usuario</label>

            <div className="admin-users-search-box">
              <i className="bi bi-search"></i>

              <input
                id="buscar-usuario"
                type="search"
                placeholder="Nombre o correo electrónico"
                value={busqueda}
                onChange={(evento) => setBusqueda(evento.target.value)}
              />

              <button
                type="button"
                onClick={() => {
                  document.getElementById("buscar-usuario")?.focus();
                }}
              >
                Buscar
              </button>
            </div>
          </div>

          {/* Filtros y contadores */}
          <div className="admin-users-toolbar">
            <div className="admin-users-filters">
              <button
                type="button"
                className={filtro === "todos" ? "active" : ""}
                onClick={() => setFiltro("todos")}
                aria-pressed={filtro === "todos"}
              >
                Todos
              </button>

              <button
                type="button"
                className={filtro === "activos" ? "active" : ""}
                onClick={() => setFiltro("activos")}
                aria-pressed={filtro === "activos"}
              >
                Activos
              </button>

              <button
                type="button"
                className={filtro === "inactivos" ? "active" : ""}
                onClick={() => setFiltro("inactivos")}
                aria-pressed={filtro === "inactivos"}
              >
                Inactivos
              </button>
            </div>

            <div className="admin-users-count">
              {totalUsuarios} registrados · {usuariosActivos} activos ·{" "}
              {usuariosInactivos} inactivos
            </div>
          </div>

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
                {usuariosFiltrados.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="admin-users-empty">
                      <i className="bi bi-search"></i>
                      <p>
                        No se encontraron usuarios con los criterios
                        seleccionados.
                      </p>

                      <button
                        type="button"
                        onClick={() => {
                          setBusqueda("");
                          setFiltro("todos");
                        }}
                      >
                        Limpiar filtros
                      </button>
                    </td>
                  </tr>
                ) : (
                  usuariosFiltrados.map((usuario) => (
                    <tr key={usuario.id}>
                      <td className="admin-users-name">
                        {usuario.nombre}
                      </td>

                      <td>{usuario.correo}</td>

                      <td>
                        <span
                          className={`admin-users-role ${
                            usuario.rol === "ADMIN" ? "admin" : "user"
                          }`}
                        >
                          {usuario.rol}
                        </span>
                      </td>

                      <td>
                        <span
                          className={`admin-users-status ${
                            usuario.estado === "Activo"
                              ? "active"
                              : "inactive"
                          }`}
                        >
                          {usuario.estado}
                        </span>
                      </td>

                      <td>{usuario.fecha}</td>

                      <td>
                        <div className="admin-users-actions">
                          <button
                            type="button"
                            onClick={() =>
                              setUsuarioSeleccionadoId(usuario.id)
                            }
                          >
                            Ver
                          </button>

                          <span aria-hidden="true">|</span>

                          <button
                            type="button"
                            className="admin-users-action-button"
                            onClick={() => setUsuarioPendienteId(usuario.id)}
                          >
                            {usuario.estado === "Activo"
                              ? "Desactivar"
                              : "Activar"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Información */}
          <div className="admin-users-info">
            <i className="bi bi-info-circle"></i>
            <span>
              La desactivación es lógica: impide el acceso sin borrar la
              cuenta ni su información. No se realiza eliminación física
              de usuarios.
            </span>
          </div>

          <div className="admin-users-footer">
            Mostrando {usuariosFiltrados.length} de {totalUsuarios} usuarios
          </div>
        </div>
      </main>

      {/* Ventana de detalles */}
      {usuarioSeleccionado && (
        <div
          className="admin-users-modal-overlay"
          onMouseDown={(evento) => {
            if (evento.target === evento.currentTarget) {
              cerrarVentanas();
            }
          }}
        >
          <div
            className="admin-users-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="titulo-detalles-usuario"
          >
            <div className="admin-users-modal-header">
              <h2 id="titulo-detalles-usuario">
                Detalles del usuario
              </h2>

              <button
                type="button"
                onClick={cerrarVentanas}
                aria-label="Cerrar ventana"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <div className="admin-users-modal-body">
              <p>
                <strong>Nombre:</strong> {usuarioSeleccionado.nombre}
              </p>

              <p>
                <strong>Correo:</strong> {usuarioSeleccionado.correo}
              </p>

              <p>
                <strong>Rol:</strong> {usuarioSeleccionado.rol}
              </p>

              <p>
                <strong>Estado:</strong> {usuarioSeleccionado.estado}
              </p>

              <p>
                <strong>Fecha de registro:</strong>{" "}
                {usuarioSeleccionado.fecha}
              </p>
            </div>

            <button type="button" onClick={cerrarVentanas}>
              Cerrar
            </button>
          </div>
        </div>
      )}

      {/* Confirmación de cambio de estado */}
      {usuarioPendiente && (
        <div
          className="admin-users-modal-overlay"
          onMouseDown={(evento) => {
            if (evento.target === evento.currentTarget) {
              cerrarVentanas();
            }
          }}
        >
          <div
            className="admin-users-modal admin-users-confirm-modal"
            role="dialog"
            aria-modal="true"
            aria-labelledby="titulo-confirmacion-usuario"
          >
            <div className="admin-users-modal-header">
              <h2 id="titulo-confirmacion-usuario">
                Confirmar acción
              </h2>

              <button
                type="button"
                onClick={cerrarVentanas}
                aria-label="Cerrar ventana"
              >
                <i className="bi bi-x-lg"></i>
              </button>
            </div>

            <div className="admin-users-modal-body">
              <p>
                ¿Estás seguro de que deseas{" "}
                <strong>
                  {usuarioPendiente.estado === "Activo"
                    ? "desactivar"
                    : "activar"}
                </strong>{" "}
                la cuenta de <strong>{usuarioPendiente.nombre}</strong>?
              </p>

              <p>
                {usuarioPendiente.estado === "Activo"
                  ? "La cuenta quedará inactiva, pero su información se conservará."
                  : "La cuenta volverá a estar activa."}
              </p>
            </div>

            <div className="admin-users-confirm-actions">
              <button
                type="button"
                className="admin-users-cancel-button"
                onClick={cerrarVentanas}
              >
                Cancelar
              </button>

              <button
                type="button"
                className="admin-users-confirm-button"
                onClick={() => cambiarEstadoUsuario(usuarioPendiente.id)}
              >
                Confirmar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}