import { useEffect, useEffectEvent, useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from "react-router-dom";
import "./CurrentWeather.css";
import ResultadosUbicacion from "../components/ResultadosUbicacion";
import { claveSesion, useAuth } from "../context/auth";
import { useNotificaciones } from "../context/notificaciones";
import { validarBusqueda } from "../hooks/useBusquedaUbicaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { buscarUbicaciones, obtenerClimaActual } from "../services/climaService";
import { agregarFavorito, listarFavoritos } from "../services/favoritosService";
import type { Ubicacion } from "../types/api";
import {
  capitalizar,
  direccionViento,
  grados,
  humedad,
  iconoCondicion,
  lugar,
  nivelHumedad,
  numero,
  viento,
} from "../utils/clima";
import { hora, horaEnUbicacion } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

/** idUbicacion válido de la URL (contrato: entero ≥ 1), o null. */
function leerIdUbicacion(valor: string | null): number | null {
  const id = Number(valor);
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}

// M04 – Clima actual. GET /api/clima/ubicaciones para buscar y GET /api/clima/actual?idUbicacion=
// para el resultado. Con sesión la consulta entra al historial y puede generar alertas
// (alertasGeneradas → toast "Nueva alerta: …").
export default function CurrentWeather() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { estado, usuario } = useAuth();
  const { notificarAlertasGeneradas, avisar } = useNotificaciones();
  const autenticado = estado === "autenticado";

  const idUbicacion = leerIdUbicacion(searchParams.get("idUbicacion"));
  // "q" llega desde el buscador de M01; "ciudad" se mantiene por compatibilidad con enlaces anteriores.
  const busquedaURL = searchParams.get("q") ?? searchParams.get("ciudad") ?? "";

  const [city, setCity] = useState(busquedaURL);
  const [guardandoFavorito, setGuardandoFavorito] = useState(false);

  // Se espera a validar la sesión restaurada para que la consulta quede (o no) en el historial correcto.
  const claveClima = idUbicacion !== null && estado !== "cargando" ? `actual:${idUbicacion}:${claveSesion(usuario)}` : null;
  const clima = useConsulta(claveClima, () => obtenerClimaActual(idUbicacion!, claveSesion(usuario)));
  const datos = clima.datos;

  const favoritos = useConsulta(autenticado ? `favoritos:${usuario?.idUsuario}` : null, (signal) => listarFavoritos(signal));
  const esFavorito =
    datos !== undefined &&
    (favoritos.datos ?? []).some((favorito) => favorito.ubicacion.idUbicacion === datos.ubicacion.idUbicacion);

  // Toast por cada alerta nueva; [] no muestra nada. Se deduplica por idAlerta.
  const alRecibirAlertas = useEffectEvent(() => notificarAlertasGeneradas(datos?.alertasGeneradas));
  useEffect(() => {
    if (datos) alRecibirAlertas();
  }, [datos]);

  // Búsqueda declarativa: el texto viaja en ?q= (también desde M01) y la ubicación elegida en ?idUbicacion=.
  const errorBusquedaURL = idUbicacion === null && busquedaURL.trim() ? validarBusqueda(busquedaURL) : null;
  const claveBusqueda =
    idUbicacion === null && busquedaURL.trim() && !errorBusquedaURL ? `ubicaciones:${busquedaURL.trim()}` : null;
  const busqueda = useConsulta(claveBusqueda, (signal) => buscarUbicaciones(busquedaURL, signal));
  const resultados = busqueda.datos;
  const errorBusqueda =
    errorBusquedaURL ??
    (busqueda.error
      ? mensajeDeError(busqueda.error, "No se pudo buscar la ubicación.")
      : resultados && resultados.length === 0
        ? "No encontramos esa ubicación. Revisa el nombre e inténtalo de nuevo."
        : "");

  const seleccionar = (ubicacion: Ubicacion) => {
    setCity(ubicacion.nombre);
    setSearchParams({ idUbicacion: String(ubicacion.idUbicacion) });
  };

  const handleSearch = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const texto = city.trim();
    if (!texto) {
      return;
    }
    setSearchParams({ q: texto });
  };

  const agregarFavoritoActual = async () => {
    if (!datos) return;

    if (!autenticado) {
      navigate("/login", { state: { desde: `${location.pathname}${location.search}` } });
      return;
    }

    setGuardandoFavorito(true);
    try {
      const nuevo = await agregarFavorito(datos.ubicacion.idUbicacion);
      favoritos.actualizar((actuales) => [nuevo, ...(actuales ?? [])]);
      avisar("Ubicación agregada a favoritos");
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        favoritos.recargar();
        avisar("Esta ubicación ya está en tus favoritos.", "info");
      } else {
        avisar(mensajeDeError(error, "No se pudo guardar el favorito."), "error");
      }
    } finally {
      setGuardandoFavorito(false);
    }
  };

  const errorClima =
    clima.error instanceof ApiError && clima.error.status === 404
      ? "La ubicación no existe. Búscala de nuevo."
      : clima.error
        ? mensajeDeError(clima.error, "No se pudo consultar el clima.")
        : "";

  // Un solo resultado: se abre directamente.
  if (idUbicacion === null && resultados && resultados.length === 1) {
    return <Navigate to={`/clima?idUbicacion=${resultados[0].idUbicacion}`} replace />;
  }

  return (
    <main className="current-weather-page">
      <div className="current-weather-container">

        {autenticado ? (
          <EnlaceVolver to="/dashboard">Volver a mi dashboard</EnlaceVolver>
        ) : (
          <EnlaceVolver to="/">Volver al Inicio</EnlaceVolver>
        )}

        <span className="current-weather-label d-block">
          CLIMA ACTUAL
        </span>

        <h1>¿Cómo está el clima hoy?</h1>

        <p>
          Consulta las condiciones meteorológicas en cualquier ciudad.
        </p>

        {/* Buscador de ciudades */}
        <form
          className="weather-search"
          onSubmit={handleSearch}
          role="search"
        >
          <i className="bi bi-search" aria-hidden="true"></i>

          <input
            type="text"
            placeholder="Ciudad o municipio"
            aria-label="Buscar ciudad o municipio"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            maxLength={100}
          />

          <button type="submit" disabled={busqueda.cargando}>
            {busqueda.cargando ? "Buscando..." : "Consultar clima"}
          </button>
        </form>

        {errorBusqueda && (
          <div className="alert alert-warning mt-3" role="alert">
            {errorBusqueda}
          </div>
        )}

        {resultados && resultados.length > 1 && (
          <ResultadosUbicacion resultados={resultados} alElegir={seleccionar} />
        )}

        {/* Contenedor de resultados */}
        <div className="weather-results">

          {/* Tarjeta principal del clima */}
          <div className="weather-main-card">

            {!datos && (
              <div className="py-4 text-center" role="status">
                {clima.cargando ? (
                  <>
                    <div className="spinner-border mb-3" aria-hidden="true"></div>
                    <p className="mb-0">Consultando el clima...</p>
                  </>
                ) : errorClima ? (
                  <p className="mb-0" role="alert">
                    <i className="bi bi-exclamation-triangle me-2"></i>
                    {errorClima}
                  </p>
                ) : (
                  <p className="mb-0">
                    <i className="bi bi-search me-2"></i>
                    Busca una ciudad para ver sus condiciones actuales.
                  </p>
                )}
              </div>
            )}

            {datos && (
              <>
            <div className="weather-card-header">
              <div>
                <h3>
                  <i className="bi bi-geo-alt"></i>
                  {datos.ubicacion.nombre}
                </h3>

                <p>
                  {lugar(datos.ubicacion)} · Consultado a las {hora(datos.consulta.fechaConsultaUtc)}
                </p>
              </div>

              <div className="weather-header-actions">
  <button
    type="button"
    className="weather-add-favorite"
    onClick={agregarFavoritoActual}
    disabled={guardandoFavorito || esFavorito}
    title={
      autenticado
        ? "Guardar ciudad en favoritos"
        : "Inicia sesión para guardar favoritos"
    }
  >
    <i className={`bi ${esFavorito ? "bi-star-fill" : "bi-star"}`}></i>
    {!autenticado
      ? "Inicia sesión para guardar favoritos"
      : esFavorito
        ? "En tus favoritos"
        : "Guardar en favoritos"}
  </button>

  <span className="weather-now-badge">
    AHORA
  </span>
</div>
            </div>

            {/* Temperatura y condición */}
            <div className="weather-card-body">
              <div>
                <div className="weather-temperature">
                  {grados(datos.temperatura)}
                </div>

                <h4>
                  {capitalizar(datos.descripcion)}
                </h4>

                <p>
                  Máx. {grados(datos.detalle.temperaturaMax)} ·
                  Mín. {grados(datos.detalle.temperaturaMin)}
                </p>
              </div>

              <div className="weather-condition-icon">
                <i className={`bi ${iconoCondicion(datos.codigoCondicion, datos.detalle.esDeDia)}`}></i>
              </div>
            </div>

            {/* Estadísticas */}
            <div className="weather-stats">

              <div className="weather-stat">
                <span>SENSACIÓN</span>
                <strong>{grados(datos.sensacionTermica)}</strong>
                <small>Térmica</small>
              </div>

              <div className="weather-stat">
                <span>HUMEDAD</span>
                <strong>{humedad(datos.humedad)}</strong>
                <small>{nivelHumedad(datos.humedad)}</small>
              </div>

              <div className="weather-stat">
                <span>VIENTO</span>
                <strong>{viento(datos.velocidadViento)}</strong>
                <small>{direccionViento(datos.detalle.direccionVientoGrados)}</small>
              </div>

            </div>
              </>
            )}
          </div>

          {/* Panel lateral */}
          <aside className="weather-sidebar">

            <div className="weather-details-card">
              <h3>Detalles de hoy</h3>

              <div className="weather-detail-row">
                <span>Sensación térmica</span>
                <strong>{datos ? `${numero(datos.sensacionTermica)} °C` : "—"}</strong>
              </div>

              <div className="weather-detail-row">
                <span>Presión atmosférica</span>
                <strong>
                  {datos?.detalle.presionHpa != null
                    ? `${datos.detalle.presionHpa.toLocaleString("es-GT")} hPa`
                    : "—"}
                </strong>
              </div>

              <div className="weather-detail-row">
                <span>Amanecer</span>
                <strong>{datos ? horaEnUbicacion(datos.detalle.amanecerUtc, datos.detalle.zonaHorariaSegundos) : "—"}</strong>
              </div>

              <div className="weather-detail-row">
                <span>Atardecer</span>
                <strong>{datos ? horaEnUbicacion(datos.detalle.atardecerUtc, datos.detalle.zonaHorariaSegundos) : "—"}</strong>
              </div>
            </div>

            {/* Pronóstico */}
            <div className="weather-forecast-card">
              <h3>¿Quieres planificar tu semana?</h3>

              <p>
                Revisa los cambios de temperatura y probabilidad de lluvia.
              </p>

              <Link
                to={datos ? `/pronostico?idUbicacion=${datos.ubicacion.idUbicacion}` : "/pronostico"}
                className="weather-forecast-button"
              >
                Ver pronóstico
                <i className="bi bi-chevron-right"></i>
              </Link>
            </div>

          </aside>
        </div>

      </div>
    </main>
  );
}
