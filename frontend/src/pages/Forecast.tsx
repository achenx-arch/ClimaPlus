import { useState, type FormEvent } from "react";
import { Link, Navigate, useSearchParams } from "react-router-dom";
import "./Forecast.css";
import ResultadosUbicacion from "../components/ResultadosUbicacion";
import { claveSesion, useAuth } from "../context/auth";
import { validarBusqueda } from "../hooks/useBusquedaUbicaciones";
import { useConsulta } from "../hooks/useConsulta";
import { ApiError, mensajeDeError } from "../services/apiClient";
import { buscarUbicaciones, obtenerPronostico } from "../services/climaService";
import type { DiaPronostico, Ubicacion } from "../types/api";
import { capitalizar, grados, humedad, iconoCondicion, lugar, viento } from "../utils/clima";
import { diaCorto, horaEnUbicacion, nombreDia } from "../utils/fechas";
import EnlaceVolver from "../components/EnlaceVolver";

function leerIdUbicacion(valor: string | null): number | null {
  const id = Number(valor);
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}

/** Día con la mayor probabilidad de lluvia (≥ 50 %) para la recomendación. */
function diaConMasLluvia(dias: DiaPronostico[]): DiaPronostico | null {
  const lluviosos = dias.filter((dia) => dia.probabilidadLluvia >= 50);
  if (lluviosos.length === 0) return null;
  return lluviosos.reduce((mayor, dia) => (dia.probabilidadLluvia > mayor.probabilidadLluvia ? dia : mayor));
}

// M05 – Pronóstico de 5 días. GET /api/clima/pronostico?idUbicacion= (días agrupados por fecha
// local, D-25). Nunca genera alertas. Al seleccionar un día se muestran sus bloques de 3 horas.
export default function Forecast() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { estado, usuario } = useAuth();

  const idUbicacion = leerIdUbicacion(searchParams.get("idUbicacion"));
  const busquedaURL = searchParams.get("q") ?? "";

  const [ciudad, setCiudad] = useState(busquedaURL);
  const [fechaSeleccionada, setFechaSeleccionada] = useState<string | null>(null);

  const clave = idUbicacion !== null && estado !== "cargando" ? `pronostico:${idUbicacion}:${claveSesion(usuario)}` : null;
  const pronostico = useConsulta(clave, () => obtenerPronostico(idUbicacion!, claveSesion(usuario)));
  const datos = pronostico.datos;
  const dias = datos?.dias ?? [];
  const diaActivo = dias.find((dia) => dia.fecha === fechaSeleccionada) ?? dias[0] ?? null;

  // Búsqueda declarativa: el texto viaja en ?q= y la ubicación elegida en ?idUbicacion=.
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
    setCiudad(ubicacion.nombre);
    setFechaSeleccionada(null);
    setSearchParams({ idUbicacion: String(ubicacion.idUbicacion) });
  };

  const consultar = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const texto = ciudad.trim();
    if (texto) setSearchParams({ q: texto });
  };

  const error =
    pronostico.error instanceof ApiError && pronostico.error.status === 404
      ? "La ubicación no existe. Búscala de nuevo."
      : pronostico.error
        ? mensajeDeError(pronostico.error, "No se pudo consultar el pronóstico.")
        : "";

  // Escala del gráfico: la temperatura máxima de la semana ocupa 140 px.
  const maximaSemana = Math.max(1, ...dias.map((dia) => Math.abs(dia.temperaturaMax)));
  const altura = (valor: number) => `${Math.max(6, Math.round((Math.abs(valor) / maximaSemana) * 140))}px`;
  const lluvia = diaConMasLluvia(dias);

  // Un solo resultado: se abre directamente.
  if (idUbicacion === null && resultados && resultados.length === 1) {
    return <Navigate to={`/pronostico?idUbicacion=${resultados[0].idUbicacion}`} replace />;
  }

  return (
    <main className="forecast-page">
      <div className="forecast-container">

        {estado === "autenticado" ? (
          <EnlaceVolver to="/dashboard">Volver a mi dashboard</EnlaceVolver>
        ) : (
          <EnlaceVolver to="/">Volver al Inicio</EnlaceVolver>
        )}

        <span className="forecast-label d-block">
          PRONÓSTICO DEL TIEMPO
        </span>

        <h1>Pronóstico de 5 días</h1>

        <p className="forecast-description">
          Consulta las condiciones meteorológicas de los próximos días
          y planifica tus actividades.
        </p>

        {datos && (
          <div className="forecast-location">
            <i className="bi bi-geo-alt-fill"></i>
            <span>
              {datos.ubicacion.nombre}
              {lugar(datos.ubicacion) ? `, ${lugar(datos.ubicacion)}` : ""}
            </span>
            <Link to={`/clima?idUbicacion=${datos.ubicacion.idUbicacion}`} className="ms-3 small">
              Volver al clima actual
            </Link>
          </div>
        )}

        {/* Buscador de ciudades */}
        <form className="forecast-search" onSubmit={consultar} role="search">
          <i className="bi bi-search"></i>

          <input
            type="text"
            placeholder="Ciudad o municipio"
            aria-label="Buscar ciudad"
            value={ciudad}
            onChange={(e) => setCiudad(e.target.value)}
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
          <ResultadosUbicacion resultados={resultados} alElegir={seleccionar} textoAccion="Ver pronóstico" />
        )}

        {!datos && (
          <div className="text-center py-4" role="status">
            {pronostico.cargando ? (
              <>
                <div className="spinner-border text-primary mb-2" aria-hidden="true"></div>
                <p>Consultando el pronóstico...</p>
              </>
            ) : error ? (
              <p className="text-danger" role="alert">{error}</p>
            ) : (
              <p className="text-secondary">Busca una ciudad para ver su pronóstico de 5 días.</p>
            )}
          </div>
        )}

        {datos && dias.length === 0 && (
          <p className="text-secondary text-center py-4">No hay días de pronóstico disponibles para esta ubicación.</p>
        )}

        {/* Tarjetas del pronóstico */}
        {dias.length > 0 && (
        <section className="forecast-grid">
          {dias.map((dia) => (
            <article
              className={`forecast-card ${
                dia.fecha === diaActivo?.fecha ? "forecast-card-today" : ""
              }`}
              key={dia.fecha}
              role="button"
              tabIndex={0}
              aria-pressed={dia.fecha === diaActivo?.fecha}
              onClick={() => setFechaSeleccionada(dia.fecha)}
              onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                  e.preventDefault();
                  setFechaSeleccionada(dia.fecha);
                }
              }}
            >
              <div className="forecast-card-header">
                <h2>{nombreDia(dia.fecha)}</h2>
                <span>{diaCorto(dia.fecha)}</span>
              </div>

              <div className="forecast-icon">
                <i className={`bi ${iconoCondicion(dia.codigoCondicion)}`}></i>
              </div>

              <p className="forecast-condition">
                {capitalizar(dia.descripcion)}
              </p>

              <div className="forecast-temperatures">
                <div>
                  <span>Máxima</span>
                  <strong>{grados(dia.temperaturaMax)}</strong>
                </div>

                <div>
                  <span>Mínima</span>
                  <strong>{grados(dia.temperaturaMin)}</strong>
                </div>
              </div>

              <div className="forecast-rain">
                <i className="bi bi-droplet-fill"></i>
                <span>Prob. de lluvia</span>
                <strong>{dia.probabilidadLluvia}%</strong>
              </div>

              {!dia.completo && (
                <small className="text-secondary d-block mt-2">Día parcial</small>
              )}
            </article>
          ))}
        </section>
        )}

        {/* Bloques de 3 horas del día seleccionado */}
        {datos && diaActivo && (
          <section className="forecast-chart mt-4" aria-label="Bloques de 3 horas">
            <div className="forecast-chart-header">
              <div>
                <h2>{nombreDia(diaActivo.fecha)} · cada 3 horas</h2>
                <p>Hora local de la ubicación</p>
              </div>
            </div>
            <div className="table-responsive">
              <table className="table align-middle mb-0">
                <thead>
                  <tr>
                    <th>Hora</th>
                    <th>Condición</th>
                    <th>Temperatura</th>
                    <th>Humedad</th>
                    <th>Viento</th>
                    <th>Lluvia</th>
                  </tr>
                </thead>
                <tbody>
                  {diaActivo.bloques.map((bloque) => (
                    <tr key={bloque.fechaHoraUtc}>
                      <td>{horaEnUbicacion(bloque.fechaHoraUtc, datos.zonaHorariaSegundos)}</td>
                      <td>
                        <i className={`bi ${iconoCondicion(bloque.codigoCondicion, bloque.esDeDia)} me-2`}></i>
                        {capitalizar(bloque.descripcion)}
                      </td>
                      <td>{grados(bloque.temperatura)}</td>
                      <td>{humedad(bloque.humedad)}</td>
                      <td>{viento(bloque.velocidadViento)}</td>
                      <td>{bloque.probabilidadLluvia}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}

        {/* Gráfico y recomendación */}
        {dias.length > 0 && (
        <div className="forecast-bottom">

          {/* Gráfico de temperaturas */}
          <section className="forecast-chart">

            <div className="forecast-chart-header">
              <div>
                <h2>Temperatura durante la semana</h2>
                <p>Máximas y mínimas estimadas</p>
              </div>

              <div className="forecast-chart-legend">
                <span>🟠 Máx.</span>
                <span>🔵 Mín.</span>
              </div>
            </div>

            <div className="forecast-chart-bars">
              {dias.map((dia) => (
                <div
                  className="forecast-chart-day"
                  key={dia.fecha}
                >
                  <div className="forecast-chart-columns">

                    <div
                      className="forecast-bar-max"
                      style={{
                        height: altura(dia.temperaturaMax),
                      }}
                      title={`Máxima: ${grados(dia.temperaturaMax)}`}
                    ></div>

                    <div
                      className="forecast-bar-min"
                      style={{
                        height: altura(dia.temperaturaMin),
                      }}
                      title={`Mínima: ${grados(dia.temperaturaMin)}`}
                    ></div>

                  </div>

                  <span>{nombreDia(dia.fecha)}</span>
                </div>
              ))}
            </div>

          </section>

          {/* Recomendación meteorológica (derivada de la probabilidad de lluvia) */}
          <div className="forecast-recommendation">

            <i className={`bi ${lluvia ? "bi-cloud-rain" : "bi-sun"}`}></i>

            <h3>
              {lluvia
                ? `Lleva paraguas el ${nombreDia(lluvia.fecha).toLocaleLowerCase("es-GT")}`
                : "Sin lluvias importantes a la vista"}
            </h3>

            <p>
              {lluvia
                ? `La probabilidad de lluvia llega al ${lluvia.probabilidadLluvia}%. Te recomendamos llevar paraguas si tienes actividades al aire libre.`
                : "Ningún día supera el 50 % de probabilidad de lluvia."}
            </p>

          </div>

        </div>
        )}

        {datos && (
          <p className="forecast-note">
            <i className="bi bi-info-circle"></i>
            Pronóstico de OpenWeather agrupado por la fecha local de la ubicación.
            {datos.consulta.desdeCache ? " Datos reutilizados de una consulta reciente (vigencia de 30 minutos)." : ""}
          </p>
        )}

      </div>
    </main>
  );
}
