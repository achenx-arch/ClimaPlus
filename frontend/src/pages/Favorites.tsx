import { useState } from "react";
import { useNavigate } from "react-router-dom";
import "./Favorites.css";
import "./Dashboard.css";
import ResultadosUbicacion from "../components/ResultadosUbicacion";
import { claveSesion, useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { useBusquedaUbicaciones } from "../hooks/useBusquedaUbicaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { obtenerClimaActual } from "../services/climaService";
import { agregarFavorito, eliminarFavorito, listarFavoritos } from "../services/favoritosService";
import type { ClimaActual, Favorito, Ubicacion } from "../types/api";
import { capitalizar, grados, humedad, iconoCondicion, lugar, viento } from "../utils/clima";
import { fechaHora } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

// M07 – Favoritos: GET/POST/DELETE /api/favoritos. Agregar: buscar con GET /api/clima/ubicaciones,
// elegir el resultado y enviar su idUbicacion (contrato-favoritos.md). Sin localStorage.
export default function Favorites() {
  const navigate = useNavigate();
  const { usuario } = useAuth();
  const { avisar, notificarAlertasGeneradas } = useNotificaciones();

  const [busqueda, setBusqueda] = useState("");
  const buscador = useBusquedaUbicaciones();
  const [procesando, setProcesando] = useState<number | null>(null);
  const [actualizando, setActualizando] = useState(false);
  const [climaReciente, setClimaReciente] = useState<Record<number, ClimaActual>>({});

  const favoritos = useConsulta(`favoritos:${usuario?.idUsuario}`, (signal) => listarFavoritos(signal));
  const ciudadesFavoritas: Favorito[] = favoritos.datos ?? [];

  const quitarFavorito = async (favorito: Favorito) => {
    if (!window.confirm(`¿Quitar ${favorito.ubicacion.nombre} de tus favoritos?`)) {
      return;
    }
    setProcesando(favorito.idFavorito);
    try {
      await eliminarFavorito(favorito.idFavorito);
      favoritos.actualizar((actuales) => actuales?.filter((f) => f.idFavorito !== favorito.idFavorito));
      avisar("Ubicación eliminada de favoritos");
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        favoritos.recargar();
      }
      avisar(mensajeDeError(error, "No se pudo quitar el favorito."), "error");
    } finally {
      setProcesando(null);
    }
  };

  const agregar = async (ubicacion: Ubicacion) => {
    setProcesando(ubicacion.idUbicacion);
    try {
      const nuevo = await agregarFavorito(ubicacion.idUbicacion);
      favoritos.actualizar((actuales) => [nuevo, ...(actuales ?? [])]);
      buscador.limpiar();
      setBusqueda("");
      avisar("Ubicación agregada a favoritos");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        avisar("Esta ubicación ya está en tus favoritos.", "info");
      } else {
        avisar(mensajeDeError(error, "No se pudo agregar el favorito."), "error");
      }
    } finally {
      setProcesando(null);
    }
  };

  // "Actualizar clima": una consulta ACTUAL por favorito (con sesión, cada una entra al historial
  // y puede generar alertas). Se hacen en serie para no superar el límite de 30 por minuto.
  const actualizarClima = async () => {
    setActualizando(true);
    let fallidas = 0;
    for (const favorito of ciudadesFavoritas) {
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
    if (fallidas > 0) {
      avisar(`No se pudo actualizar el clima de ${fallidas} ubicación(es).`, "error");
    }
  };

  const ciudadesFiltradas = ciudadesFavoritas.filter((favorito) =>
    `${favorito.ubicacion.nombre} ${lugar(favorito.ubicacion)}`
      .toLocaleLowerCase("es")
      .includes(busqueda.trim().toLocaleLowerCase("es"))
  );

  return (
    <main className="favorites-page">
      <div className="favorites-layout">


        {/* CONTENIDO PRINCIPAL */}
        <div className="favorites-container">

          <EnlaceVolver to="/dashboard">Volver a mi dashboard</EnlaceVolver>

          <span className="favorites-label d-block">
            FAVORITOS
          </span>

          {/* ENCABEZADO */}
          <div className="favorites-header">
            <div>
              <h1>Mis favoritos</h1>
              <p>
                Tus ubicaciones guardadas para consultarlas rápidamente.
              </p>
            </div>

            <button
              type="button"
              className="favorites-refresh"
              onClick={actualizarClima}
              disabled={actualizando || ciudadesFavoritas.length === 0}
            >
              <i className="bi bi-arrow-clockwise"></i>
              {actualizando ? "Actualizando..." : "Actualizar clima"}
            </button>
          </div>

          {/* BUSCADOR */}
          <section className="favorites-search-section">
            <label htmlFor="favorites-search">
              Buscador de ubicaciones
            </label>

            <form
              className="favorites-search-box"
              onSubmit={(e) => {
                e.preventDefault();
                void buscador.buscar(busqueda);
              }}
            >
              <div className="favorites-search-input">
                <i className="bi bi-search"></i>

                <input
                  id="favorites-search"
                  type="search"
                  placeholder="Buscar ciudad o municipio"
                  value={busqueda}
                  onChange={(e) => {
                    setBusqueda(e.target.value);
                    if (!e.target.value) buscador.limpiar();
                  }}
                  maxLength={100}
                />
              </div>

              <button type="submit" disabled={buscador.buscando}>
                {buscador.buscando ? "Buscando..." : "Buscar"}
              </button>
            </form>

            <small className="text-secondary d-block mt-2">
              Escribir filtra tus favoritos; "Buscar" encuentra nuevas ubicaciones para agregarlas.
            </small>

            {buscador.error && (
              <div className="alert alert-warning mt-2 mb-0" role="alert">{buscador.error}</div>
            )}

            {buscador.resultados && (
              <ResultadosUbicacion
                resultados={buscador.resultados}
                alElegir={(ubicacion) => void agregar(ubicacion)}
                textoAccion="Agregar a favoritos"
              />
            )}
          </section>

          {/* UBICACIONES GUARDADAS */}
          <section className="favorites-saved">
            <div className="favorites-saved-header">
              <h2>Ubicaciones guardadas</h2>

              <span>
                {ciudadesFavoritas.length}{" "}
                {ciudadesFavoritas.length === 1
                  ? "favorito"
                  : "favoritos"}
              </span>
            </div>

            <div className="favorites-info">
              <i className="bi bi-info-circle"></i>

              <span>
                Se muestra tu última consulta guardada de cada ubicación.
                Usa "Actualizar clima" para consultar las condiciones actuales.
              </span>
            </div>

            {favoritos.cargando && !favoritos.datos && (
              <div className="text-center py-4" role="status">
                <div className="spinner-border text-primary" aria-hidden="true"></div>
                <p className="mt-2">Cargando tus favoritos...</p>
              </div>
            )}

            {favoritos.error !== undefined && (
              <div className="alert alert-danger" role="alert">
                {mensajeDeError(favoritos.error, "No se pudieron cargar tus favoritos.")}
                <button type="button" className="btn btn-link btn-sm" onClick={favoritos.recargar}>
                  Reintentar
                </button>
              </div>
            )}

            {/* SIN FAVORITOS */}
            {favoritos.datos !== undefined && ciudadesFavoritas.length === 0 && (
              <div className="favorites-empty">
                <i className="bi bi-star"></i>

                <h3>No tienes ubicaciones favoritas</h3>

                <p>
                  Guarda tus ciudades favoritas para consultar
                  rápidamente sus condiciones climáticas.
                </p>
              </div>
            )}

            {/* SIN RESULTADOS */}
            {ciudadesFavoritas.length > 0 &&
              ciudadesFiltradas.length === 0 && (
                <div className="favorites-empty">
                  <i className="bi bi-search"></i>

                  <h3>No encontramos esa ubicación</h3>

                  <p>
                    Intenta buscar otra ciudad o municipio
                    entre tus favoritos.
                  </p>
                </div>
              )}

            {/* TARJETAS */}
            <div className="favorites-grid">
              {ciudadesFiltradas.map((favorito) => {
                const reciente = climaReciente[favorito.ubicacion.idUbicacion];
                const ultima = favorito.ultimaConsulta;
                const temperatura = reciente?.temperatura ?? ultima?.temperatura ?? null;
                const codigo = reciente?.codigoCondicion ?? ultima?.codigoCondicion ?? null;
                const descripcion = reciente?.descripcion ?? ultima?.descripcion ?? null;
                const fechaConsulta = reciente?.consulta.fechaConsultaUtc ?? ultima?.fechaConsulta ?? null;

                return (
                <article
                  className="favorites-weather-card"
                  key={favorito.idFavorito}
                >
                  <div className="favorites-weather-header">
                    <div>
                      <h3>{favorito.ubicacion.nombre}</h3>
                      <span>{lugar(favorito.ubicacion)}</span>
                    </div>

                    <i className="bi bi-star-fill"></i>
                  </div>

                  <div className="favorites-weather-main">
                    <div>
                      <strong>{grados(temperatura)}</strong>
                      <p>{descripcion ? capitalizar(descripcion) : "Sin consultas todavía"}</p>
                    </div>

                    <i className={`bi ${iconoCondicion(codigo, reciente?.detalle.esDeDia ?? true)}`}></i>
                  </div>

                  {reciente && (
                  <div className="favorites-weather-details">
                    <span>
                      <i className="bi bi-droplet-half"></i>
                      {humedad(reciente.humedad)} humedad
                    </span>

                    <span>
                      <i className="bi bi-wind"></i>
                      {viento(reciente.velocidadViento)}
                    </span>
                  </div>
                  )}

                  <div className="favorites-weather-updated">
                    <i className="bi bi-clock"></i>

                    <span>
                      {fechaConsulta
                        ? `Última consulta almacenada: ${fechaHora(fechaConsulta)}`
                        : "Aún no hay consultas de esta ubicación"}
                    </span>
                  </div>

                  <div className="favorites-weather-actions">
                    <button
                      type="button"
                      className="favorites-view-button"
                      onClick={() =>
                        navigate(`/clima?idUbicacion=${favorito.ubicacion.idUbicacion}`)
                      }
                    >
                      <i className="bi bi-cloud-sun"></i>
                      Ver clima
                    </button>

                    <button
                      type="button"
                      className="favorites-remove-button"
                      onClick={() => void quitarFavorito(favorito)}
                      disabled={procesando === favorito.idFavorito}
                    >
                      <i className="bi bi-star"></i>
                      Quitar de favoritos
                    </button>
                  </div>
                </article>
                );
              })}
            </div>
          </section>

        </div>
      </div>
    </main>
  );
}