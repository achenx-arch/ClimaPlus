import { useState } from "react";
import { Link } from "react-router-dom";
import "./AlertSettings.css";
import "./Alerts.css";
import { useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import {
  actualizarConfiguracion,
  cambiarEstadoConfiguracion,
  crearConfiguracion,
  eliminarConfiguracion,
  listarConfiguraciones,
} from "../services/configuracionAlertasService";
import { listarFavoritos } from "../services/favoritosService";
import type { ConfiguracionAlerta, TipoAlerta, Ubicacion } from "../types/api";
import { NOMBRES_TIPO_ALERTA, lugar } from "../utils/clima";

const nombresTipos: Record<TipoAlerta, string> = {
  LLUVIA: "LLUVIA",
  TEMPERATURA_ALTA: "TEMPERATURA_ALTA",
  TEMPERATURA_BAJA: "TEMPERATURA_BAJA",
};

/** Umbral válido: número entre -50 y 60 con hasta 2 decimales (RN-BD-18). */
function validarUmbral(texto: string): string | null {
  const valor = Number(texto);
  if (texto.trim() === "" || !Number.isFinite(valor) || valor < -50 || valor > 60) {
    return "Ingresa una temperatura válida entre -50 y 60 °C.";
  }
  if (!/^-?\d+(\.\d{1,2})?$/.test(texto.trim())) {
    return "El umbral admite como máximo 2 decimales.";
  }
  return null;
}

// M10 – Configuración de alertas: /api/configuraciones-alerta (listar, crear, editar,
// activar/pausar y eliminar). Las reglas validan en el backend RN-BD-16 a RN-BD-21.
export default function AlertSettings() {
  const { usuario } = useAuth();
  const { avisar } = useNotificaciones();

  const reglas = useConsulta(`configuraciones:${usuario?.idUsuario}`, (signal) => listarConfiguraciones(signal));
  const favoritos = useConsulta(`favoritos:${usuario?.idUsuario}`, (signal) => listarFavoritos(signal));
  const configuraciones: ConfiguracionAlerta[] = reglas.datos ?? [];

  const [mostrarFormulario, setMostrarFormulario] = useState(false);
  const [editandoId, setEditandoId] = useState<number | null>(null);

  const [ubicacion, setUbicacion] = useState("");
  const [tipo, setTipo] = useState<TipoAlerta>("TEMPERATURA_ALTA");
  const [umbral, setUmbral] = useState("30");
  const [activa, setActiva] = useState(true);
  const [mensaje, setMensaje] = useState("");
  const [guardando, setGuardando] = useState(false);
  const [procesando, setProcesando] = useState<number | null>(null);

  // Ubicaciones elegibles: favoritos y las ya usadas en reglas (todas vienen de la búsqueda, D-22).
  const opcionesUbicacion = new Map<number, Ubicacion>();
  for (const favorito of favoritos.datos ?? []) opcionesUbicacion.set(favorito.ubicacion.idUbicacion, favorito.ubicacion);
  for (const regla of configuraciones) {
    if (regla.ubicacion) opcionesUbicacion.set(regla.ubicacion.idUbicacion, regla.ubicacion);
  }

  const activas = configuraciones.filter(
    (configuracion) => configuracion.activa
  ).length;

  const inactivas = configuraciones.length - activas;

  const abrirFormularioNuevo = () => {
    setEditandoId(null);
    setUbicacion("");
    setTipo("TEMPERATURA_ALTA");
    setUmbral("30");
    setActiva(true);
    setMensaje("");
    setMostrarFormulario(true);
  };

  const editarConfiguracion = (configuracion: ConfiguracionAlerta) => {
    setEditandoId(configuracion.idConfiguracion);
    setUbicacion(configuracion.ubicacion ? String(configuracion.ubicacion.idUbicacion) : "");
    setTipo(configuracion.tipo);
    setUmbral(
      configuracion.valorUmbral === null
        ? ""
        : String(configuracion.valorUmbral)
    );
    setActiva(configuracion.activa);
    setMensaje("");
    setMostrarFormulario(true);
  };

  const cerrarFormulario = () => {
    setMostrarFormulario(false);
    setEditandoId(null);
  };

  const reemplazar = (actualizada: ConfiguracionAlerta) =>
    reglas.actualizar((actuales) =>
      actuales?.map((c) => (c.idConfiguracion === actualizada.idConfiguracion ? actualizada : c))
    );

  const cambiarEstado = async (configuracion: ConfiguracionAlerta) => {
    setProcesando(configuracion.idConfiguracion);
    try {
      reemplazar(await cambiarEstadoConfiguracion(configuracion.idConfiguracion, !configuracion.activa));
      avisar(configuracion.activa ? "Regla pausada" : "Regla activada");
    } catch (error) {
      avisar(mensajeDeError(error, "No se pudo cambiar el estado de la regla."), "error");
      if (error instanceof ApiError && error.status === 404) reglas.recargar();
    } finally {
      setProcesando(null);
    }
  };

  const eliminar = async (configuracion: ConfiguracionAlerta) => {
    const descripcion = `${NOMBRES_TIPO_ALERTA[configuracion.tipo]} · ${configuracion.ubicacion?.nombre ?? "Todas mis ubicaciones"}`;
    if (!window.confirm(`¿Eliminar la regla "${descripcion}"?`)) return;
    setProcesando(configuracion.idConfiguracion);
    try {
      await eliminarConfiguracion(configuracion.idConfiguracion);
      reglas.actualizar((actuales) => actuales?.filter((c) => c.idConfiguracion !== configuracion.idConfiguracion));
      avisar("Regla eliminada");
    } catch (error) {
      avisar(mensajeDeError(error, "No se pudo eliminar la regla."), "error");
      if (error instanceof ApiError && error.status === 404) reglas.recargar();
    } finally {
      setProcesando(null);
    }
  };

  const guardarConfiguracion = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setMensaje("");

    const requiereUmbral = tipo !== "LLUVIA";
    if (requiereUmbral) {
      const invalido = validarUmbral(umbral);
      if (invalido) {
        setMensaje(invalido);
        return;
      }
    }

    const datos = {
      tipo,
      idUbicacion: ubicacion ? Number(ubicacion) : null,
      valorUmbral: requiereUmbral ? Number(umbral) : null,
      activa,
    };

    setGuardando(true);
    try {
      if (editandoId !== null) {
        reemplazar(await actualizarConfiguracion(editandoId, datos));
      } else {
        const nueva = await crearConfiguracion(datos);
        reglas.actualizar((actuales) => [...(actuales ?? []), nueva]);
      }
      cerrarFormulario();
      avisar("Configuración guardada correctamente.");
    } catch (error) {
      // 400 (validación) y 409 (RN-BD-19 duplicada o RN-BD-21 alta ≤ baja) traen el mensaje del backend.
      setMensaje(mensajeDeError(error, "No se pudo guardar la configuración."));
    } finally {
      setGuardando(false);
    }
  };
return (
  <main className="alert-settings-page">
    <div className="alerts-layout">


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
                {reglas.cargando && !reglas.datos && (
                  <tr>
                    <td colSpan={5} className="text-center py-3">Cargando configuraciones...</td>
                  </tr>
                )}

                {reglas.error !== undefined && (
                  <tr>
                    <td colSpan={5} className="text-danger py-3" role="alert">
                      {mensajeDeError(reglas.error, "No se pudieron cargar tus configuraciones.")}
                    </td>
                  </tr>
                )}

                {reglas.datos !== undefined && configuraciones.length === 0 && (
                  <tr>
                    <td colSpan={5} className="text-center py-3">
                      Aún no tienes reglas. Usa "Nueva configuración" para crear la primera.
                    </td>
                  </tr>
                )}

                {configuraciones.map((configuracion) => (
                  <tr key={configuracion.idConfiguracion}>
                    <td>{configuracion.ubicacion?.nombre ?? "Todas mis ubicaciones"}</td>

                    <td>
                      <span className="alert-settings-type">
                        {nombresTipos[configuracion.tipo]}
                      </span>
                    </td>

                    <td>
                      {configuracion.valorUmbral === null
                        ? "No aplica"
                        : `${configuracion.valorUmbral} °C`}
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
                          onClick={() => void cambiarEstado(configuracion)}
                          disabled={procesando === configuracion.idConfiguracion}
                        >
                          {configuracion.activa
                            ? "Desactivar"
                            : "Activar"}
                        </button>

                        <button
                          type="button"
                          onClick={() => void eliminar(configuracion)}
                          disabled={procesando === configuracion.idConfiguracion}
                        >
                          Eliminar
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
              valores en °C. Una regla de una ubicación prevalece sobre
              la regla de "Todas mis ubicaciones" del mismo tipo.
            </span>
          </div>
        </section>

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

              <form onSubmit={guardarConfiguracion} noValidate>
                <div className="alert-settings-field">
                  <label htmlFor="alert-location">Ubicación</label>

                  <select
                    id="alert-location"
                    value={ubicacion}
                    onChange={(e) => setUbicacion(e.target.value)}
                  >
                    <option value="">Todas mis ubicaciones</option>
                    {[...opcionesUbicacion.values()].map((opcion) => (
                      <option key={opcion.idUbicacion} value={opcion.idUbicacion}>
                        {opcion.nombre}
                        {lugar(opcion) ? ` (${lugar(opcion)})` : ""}
                      </option>
                    ))}
                  </select>

                  <small>
                    Selecciona una de tus ubicaciones favoritas o todas tus ubicaciones.
                    Para otra ciudad, agrégala primero a <Link to="/favoritos">favoritos</Link>.
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
                        step="0.01"
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
                    disabled={guardando}
                  >
                    {guardando ? "Guardando..." : "Guardar configuración"}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        <footer className="alert-settings-footer">
          Las alertas se evalúan al consultar el clima actual con tu sesión · ClimaPlus
        </footer>
        </div>
    </div>
  </main>
);
}