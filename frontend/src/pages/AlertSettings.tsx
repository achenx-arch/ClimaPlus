import { useState } from "react";
import { Link, NavLink } from "react-router-dom";
import "./AlertSettings.css";
import "./Alerts.css";

type TipoAlerta = "LLUVIA" | "TEMPERATURA_ALTA" | "TEMPERATURA_BAJA";

type Configuracion = {
  id: number;
  ubicacion: string;
  tipo: TipoAlerta;
  umbral: number | null;
  activa: boolean;
};

const configuracionesIniciales: Configuracion[] = [
  {
    id: 1,
    ubicacion: "Antigua Guatemala",
    tipo: "LLUVIA",
    umbral: null,
    activa: true,
  },
  {
    id: 2,
    ubicacion: "Ciudad de Guatemala",
    tipo: "TEMPERATURA_ALTA",
    umbral: 30,
    activa: true,
  },
  {
    id: 3,
    ubicacion: "Quetzaltenango",
    tipo: "TEMPERATURA_BAJA",
    umbral: 14,
    activa: true,
  },
  {
    id: 4,
    ubicacion: "Todas mis ubicaciones",
    tipo: "TEMPERATURA_ALTA",
    umbral: 35,
    activa: false,
  },
];

const nombresTipos: Record<TipoAlerta, string> = {
  LLUVIA: "LLUVIA",
  TEMPERATURA_ALTA: "TEMPERATURA_ALTA",
  TEMPERATURA_BAJA: "TEMPERATURA_BAJA",
};

function cargarConfiguraciones(): Configuracion[] {
  try {
    const guardadas = localStorage.getItem(
      "climaplus_reglas_alertas"
    );

    if (guardadas !== null) {
      const datos = JSON.parse(guardadas);

      if (Array.isArray(datos)) {
        return datos;
      }
    }
  } catch {
    console.error("No se pudieron cargar las configuraciones");
  }

  return configuracionesIniciales;
}

export default function AlertSettings() {
  const [configuraciones, setConfiguraciones] =
    useState<Configuracion[]>(cargarConfiguraciones);

  const [mostrarFormulario, setMostrarFormulario] = useState(false);
  const [editandoId, setEditandoId] = useState<number | null>(null);

  const [ubicacion, setUbicacion] = useState("Ciudad de Guatemala");
  const [tipo, setTipo] = useState<TipoAlerta>("TEMPERATURA_ALTA");
  const [umbral, setUmbral] = useState("30");
  const [activa, setActiva] = useState(true);
  const [mensaje, setMensaje] = useState("");

  const activas = configuraciones.filter(
    (configuracion) => configuracion.activa
  ).length;

  const inactivas = configuraciones.length - activas;

  const guardarEnLocalStorage = (datos: Configuracion[]) => {
    localStorage.setItem(
      "climaplus_reglas_alertas",
      JSON.stringify(datos)
    );
  };

  const abrirFormularioNuevo = () => {
    setEditandoId(null);
    setUbicacion("Ciudad de Guatemala");
    setTipo("TEMPERATURA_ALTA");
    setUmbral("30");
    setActiva(true);
    setMensaje("");
    setMostrarFormulario(true);
  };

  const editarConfiguracion = (configuracion: Configuracion) => {
    setEditandoId(configuracion.id);
    setUbicacion(configuracion.ubicacion);
    setTipo(configuracion.tipo);
    setUmbral(
      configuracion.umbral === null
        ? ""
        : String(configuracion.umbral)
    );
    setActiva(configuracion.activa);
    setMensaje("");
    setMostrarFormulario(true);
  };

  const cerrarFormulario = () => {
    setMostrarFormulario(false);
    setEditandoId(null);
  };

  const cambiarEstado = (id: number) => {
    const actualizadas = configuraciones.map((configuracion) =>
      configuracion.id === id
        ? { ...configuracion, activa: !configuracion.activa }
        : configuracion
    );

    setConfiguraciones(actualizadas);
    guardarEnLocalStorage(actualizadas);
  };

  const guardarConfiguracion = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();

    const requiereUmbral = tipo !== "LLUVIA";
    const valorUmbral = requiereUmbral ? Number(umbral) : null;

    if (
      requiereUmbral &&
      (umbral.trim() === "" ||
        !Number.isFinite(valorUmbral) ||
        valorUmbral === null ||
        valorUmbral < -50 ||
        valorUmbral > 60)
    ) {
      setMensaje("Ingresa una temperatura válida entre -50 y 60 °C.");
      return;
    }

    let actualizadas: Configuracion[];

    if (editandoId !== null) {
      actualizadas = configuraciones.map((configuracion) =>
        configuracion.id === editandoId
          ? {
              ...configuracion,
              ubicacion,
              tipo,
              umbral: valorUmbral,
              activa,
            }
          : configuracion
      );
    } else {
      const nuevaConfiguracion: Configuracion = {
        id: Date.now(),
        ubicacion,
        tipo,
        umbral: valorUmbral,
        activa,
      };

      actualizadas = [...configuraciones, nuevaConfiguracion];
    }

    setConfiguraciones(actualizadas);
    guardarEnLocalStorage(actualizadas);
    cerrarFormulario();
    setMensaje("Configuración guardada correctamente.");
  };
return (
  <main className="alert-settings-page">
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

          <NavLink to="/alertas" className="active">
            <i className="bi bi-bell"></i>
            Alertas
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
      <div className="alert-settings-container">
        <span className="alert-settings-label">ALERTAS</span>

        <div className="alert-settings-header">
          <div>
            <h1>Configuración de alertas</h1>
            <p>
              Define cuándo ClimaPlus debe generar alertas para tus
              ubicaciones.
            </p>
          </div>
        </div>

        <div className="alert-settings-toolbar">
          <Link to="/alertas" className="alert-settings-back">
            <i className="bi bi-arrow-left"></i>
            Volver a alertas
          </Link>

          <button
            type="button"
            className="alert-settings-new"
            onClick={abrirFormularioNuevo}
          >
            <i className="bi bi-plus-lg"></i>
            Nueva configuración
          </button>
        </div>

        <section className="alert-settings-section">
          <div className="alert-settings-section-header">
            <h2>Tus configuraciones</h2>
            <span>
              {activas} activas · {inactivas} inactivas
            </span>
          </div>

          <div className="alert-settings-table-wrapper">
            <table className="alert-settings-table">
              <thead>
                <tr>
                  <th>Ubicación</th>
                  <th>Tipo de alerta</th>
                  <th>Umbral</th>
                  <th>Estado</th>
                  <th>Acciones</th>
                </tr>
              </thead>

              <tbody>
                {configuraciones.map((configuracion) => (
                  <tr key={configuracion.id}>
                    <td>{configuracion.ubicacion}</td>

                    <td>
                      <span className="alert-settings-type">
                        {nombresTipos[configuracion.tipo]}
                      </span>
                    </td>

                    <td>
                      {configuracion.umbral === null
                        ? "No aplica"
                        : `${configuracion.umbral} °C`}
                    </td>

                    <td>
                      <span
                        className={`alert-settings-status ${
                          configuracion.activa ? "active" : "inactive"
                        }`}
                      >
                        {configuracion.activa ? "Activa" : "Inactiva"}
                      </span>
                    </td>

                    <td>
                      <div className="alert-settings-row-actions">
                        <button
                          type="button"
                          onClick={() =>
                            editarConfiguracion(configuracion)
                          }
                        >
                          Editar
                        </button>

                        <button
                          type="button"
                          onClick={() =>
                            cambiarEstado(configuracion.id)
                          }
                        >
                          {configuracion.activa
                            ? "Desactivar"
                            : "Activar"}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div className="alert-settings-table-footer">
              {configuraciones.length} configuraciones · El umbral
              solo aplica a las alertas de temperatura.
            </div>
          </div>

          <div className="alert-settings-info">
            <i className="bi bi-info-circle"></i>
            <span>
              LLUVIA detecta la presencia de lluvia y no solicita un
              umbral numérico. Las alertas de temperatura utilizan
              valores en °C.
            </span>
          </div>
        </section>

        {mensaje && (
          <p className="alert-settings-message" role="status">
            {mensaje}
          </p>
        )}

        {mostrarFormulario && (
          <div
            className="alert-settings-modal-overlay"
            onClick={cerrarFormulario}
          >
            <div
              className="alert-settings-modal"
              role="dialog"
              aria-modal="true"
              aria-labelledby="alert-settings-modal-title"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="alert-settings-modal-header">
                <h2 id="alert-settings-modal-title">
                  {editandoId !== null
                    ? "Editar configuración de alerta"
                    : "Nueva configuración de alerta"}
                </h2>

                <button
                  type="button"
                  className="alert-settings-close"
                  onClick={cerrarFormulario}
                  aria-label="Cerrar formulario"
                >
                  <i className="bi bi-x-lg"></i>
                </button>
              </div>

              <form onSubmit={guardarConfiguracion}>
                <div className="alert-settings-field">
                  <label htmlFor="alert-location">Ubicación</label>

                  <select
                    id="alert-location"
                    value={ubicacion}
                    onChange={(e) => setUbicacion(e.target.value)}
                  >
                    <option>Ciudad de Guatemala</option>
                    <option>Antigua Guatemala</option>
                    <option>Quetzaltenango</option>
                    <option>Todas mis ubicaciones</option>
                  </select>

                  <small>
                    Selecciona una ubicación o todas tus ubicaciones.
                  </small>
                </div>

                <div className="alert-settings-field">
                  <label htmlFor="alert-type">Tipo de alerta</label>

                  <select
                    id="alert-type"
                    value={tipo}
                    onChange={(e) => {
                      const nuevoTipo = e.target.value as TipoAlerta;
                      setTipo(nuevoTipo);

                      if (
                        nuevoTipo !== "LLUVIA" &&
                        umbral.trim() === ""
                      ) {
                        setUmbral("30");
                      }
                    }}
                  >
                    <option value="LLUVIA">Lluvia</option>
                    <option value="TEMPERATURA_ALTA">
                      Temperatura alta
                    </option>
                    <option value="TEMPERATURA_BAJA">
                      Temperatura baja
                    </option>
                  </select>
                </div>

                {tipo !== "LLUVIA" && (
                  <div className="alert-settings-field">
                    <label htmlFor="alert-threshold">
                      Valor umbral
                    </label>

                    <div className="alert-settings-threshold">
                      <input
                        id="alert-threshold"
                        type="number"
                        min="-50"
                        max="60"
                        step="0.1"
                        value={umbral}
                        onChange={(e) => setUmbral(e.target.value)}
                        required
                      />
                      <span>°C</span>
                    </div>

                    <small>
                      Define la temperatura que activará la alerta.
                    </small>
                  </div>
                )}

                <label className="alert-settings-active-field">
                  <input
                    type="checkbox"
                    checked={activa}
                    onChange={(e) => setActiva(e.target.checked)}
                  />
                  Activa
                </label>

                {mensaje && (
                  <p className="alert-settings-form-message" role="alert">
                    {mensaje}
                  </p>
                )}

                <div className="alert-settings-form-actions">
                  <button
                    type="button"
                    className="alert-settings-cancel"
                    onClick={cerrarFormulario}
                  >
                    Cancelar
                  </button>

                  <button
                    type="submit"
                    className="alert-settings-save"
                  >
                    Guardar configuración
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        <footer className="alert-settings-footer">
          Datos de ejemplo · ClimaPlus Fase 1 · Octubre 2026
        </footer>
        </div>
    </div>
  </main>
);
}