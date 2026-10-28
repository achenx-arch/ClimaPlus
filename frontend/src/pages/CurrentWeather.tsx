import { useState, type FormEvent } from "react";
import { useSearchParams } from "react-router-dom";
import "./CurrentWeather.css";

// Datos temporales hasta conectar la API meteorológica
const climasDemo = [
  {
    nombre: "Ciudad de Guatemala",
    ubicacion: "Guatemala, GT",
    temperatura: 22,
    condicion: "Parcialmente nublado",
    icono: "bi-cloud-sun",
    maxima: 25,
    minima: 16,
    sensacion: 23,
    humedad: 74,
    viento: 12,
    direccion: "Noreste",
    presion: "1,018 hPa",
    uv: "5 · Moderado",
    atardecer: "6:21 p. m.",
  },
  {
    nombre: "Antigua Guatemala",
    ubicacion: "Sacatepéquez, GT",
    temperatura: 20,
    condicion: "Lluvia ligera",
    icono: "bi-cloud-drizzle",
    maxima: 23,
    minima: 15,
    sensacion: 19,
    humedad: 81,
    viento: 8,
    direccion: "Sureste",
    presion: "1,016 hPa",
    uv: "3 · Moderado",
    atardecer: "6:18 p. m.",
  },
  {
    nombre: "Quetzaltenango",
    ubicacion: "Quetzaltenango, GT",
    temperatura: 16,
    condicion: "Nublado",
    icono: "bi-cloud",
    maxima: 19,
    minima: 10,
    sensacion: 15,
    humedad: 78,
    viento: 10,
    direccion: "Norte",
    presion: "1,020 hPa",
    uv: "4 · Moderado",
    atardecer: "6:15 p. m.",
  },
];

export default function CurrentWeather() {
  const [searchParams, setSearchParams] = useSearchParams();

  const ciudadURL = searchParams.get("ciudad") || "";

  const [city, setCity] = useState(ciudadURL);

  const climaSeleccionado =
    climasDemo.find(
      (clima) =>
        clima.nombre.toLocaleLowerCase("es") ===
        ciudadURL.trim().toLocaleLowerCase("es")
    ) || climasDemo[0];
    
    const agregarFavorito = () => {
  const ciudadEncontrada = climasDemo.find(
    (clima) =>
      clima.nombre.toLocaleLowerCase("es") ===
      ciudadURL.trim().toLocaleLowerCase("es")
  );

  if (!ciudadEncontrada) {
    alert("Por ahora solo puedes guardar ciudades disponibles en la demostración.");
    return;
  }

  const favoritosGuardados = localStorage.getItem("climaplus_favoritos");

  let favoritos = [];

  try {
    const datos = favoritosGuardados
      ? JSON.parse(favoritosGuardados)
      : [];

    favoritos = Array.isArray(datos) ? datos : [];
  } catch {
    favoritos = [];
  }

  const yaExiste = favoritos.some(
    (favorito: { nombre: string }) =>
      favorito.nombre === ciudadEncontrada.nombre
  );

  if (yaExiste) {
    alert("Esta ciudad ya está guardada en tus favoritos.");
    return;
  }

  const nuevoFavorito = {
    nombre: ciudadEncontrada.nombre,
    ubicacion: ciudadEncontrada.ubicacion,
    temperatura: ciudadEncontrada.temperatura,
    condicion: ciudadEncontrada.condicion,
    icono: ciudadEncontrada.icono,
    humedad: ciudadEncontrada.humedad,
    viento: ciudadEncontrada.viento,
    fecha: new Date().toLocaleString("es-GT"),
  };

  localStorage.setItem(
    "climaplus_favoritos",
    JSON.stringify([...favoritos, nuevoFavorito])
  );

  alert(`${ciudadEncontrada.nombre} se agregó a tus favoritos.`);
};
const handleSearch = (e: FormEvent<HTMLFormElement>) => {
  e.preventDefault();

  const ciudadBuscada = city.trim();

  if (!ciudadBuscada) {
    return;
  }

  const climaEncontrado = climasDemo.find(
    (clima) =>
      clima.nombre.toLocaleLowerCase("es") ===
      ciudadBuscada.toLocaleLowerCase("es")
  );

  if (!climaEncontrado) {
    alert("Por ahora esta ciudad no está disponible en la demostración.");
    return;
  }

  setSearchParams({ ciudad: climaEncontrado.nombre });

  const historialGuardado = localStorage.getItem("climaplus_historial");

  let historial = [];

  try {
    const datos = historialGuardado
      ? JSON.parse(historialGuardado)
      : [];

    historial = Array.isArray(datos) ? datos : [];
  } catch {
    historial = [];
  }

  const ahora = new Date();

  const nuevaConsulta = {
    id: Date.now(),
    ciudad: climaEncontrado.nombre,
    ubicacion: climaEncontrado.ubicacion,
    temperatura: climaEncontrado.temperatura,
    condicion: climaEncontrado.condicion,
    icono: climaEncontrado.icono,
    fecha: ahora.toLocaleDateString("es-GT"),
    hora: ahora.toLocaleTimeString("es-GT", {
      hour: "2-digit",
      minute: "2-digit",
    }),
  };

  localStorage.setItem(
    "climaplus_historial",
    JSON.stringify([nuevaConsulta, ...historial])
  );
};

  return (
    <main className="current-weather-page">
      <div className="current-weather-container">

        <span className="current-weather-label">
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
            placeholder="Ciudad, municipio o código postal"
            aria-label="Buscar ciudad o municipio"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            maxLength={100}
          />

          <button type="submit">
            Consultar clima
          </button>
        </form>

        {/* Contenedor de resultados */}
        <div className="weather-results">

          {/* Tarjeta principal del clima */}
          <div className="weather-main-card">

            <div className="weather-card-header">
              <div>
                <h3>
                  <i className="bi bi-geo-alt"></i>
                  {climaSeleccionado.nombre}
                </h3>

                <p>
                  {climaSeleccionado.ubicacion} · Datos de demostración
                </p>
              </div>

              <div className="weather-header-actions">
  <button
    type="button"
    className="weather-add-favorite"
    onClick={agregarFavorito}
    title="Guardar ciudad en favoritos"
  >
    <i className="bi bi-star"></i>
    Guardar en favoritos
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
                  {climaSeleccionado.temperatura}°
                </div>

                <h4>
                  {climaSeleccionado.condicion}
                </h4>

                <p>
                  Máx. {climaSeleccionado.maxima}° ·
                  Mín. {climaSeleccionado.minima}°
                </p>
              </div>

              <div className="weather-condition-icon">
                <i className={`bi ${climaSeleccionado.icono}`}></i>
              </div>
            </div>

            {/* Estadísticas */}
            <div className="weather-stats">

              <div className="weather-stat">
                <span>SENSACIÓN</span>
                <strong>{climaSeleccionado.sensacion}°</strong>
                <small>Térmica</small>
              </div>

              <div className="weather-stat">
                <span>HUMEDAD</span>
                <strong>{climaSeleccionado.humedad}%</strong>
                <small>Moderada</small>
              </div>

              <div className="weather-stat">
                <span>VIENTO</span>
                <strong>{climaSeleccionado.viento} km/h</strong>
                <small>{climaSeleccionado.direccion}</small>
              </div>

            </div>
          </div>

          {/* Panel lateral */}
          <aside className="weather-sidebar">

            <div className="weather-details-card">
              <h3>Detalles de hoy</h3>

              <div className="weather-detail-row">
                <span>Sensación térmica</span>
                <strong>{climaSeleccionado.sensacion}°</strong>
              </div>

              <div className="weather-detail-row">
                <span>Presión atmosférica</span>
                <strong>{climaSeleccionado.presion}</strong>
              </div>

              <div className="weather-detail-row">
                <span>Índice UV</span>
                <strong>{climaSeleccionado.uv}</strong>
              </div>

              <div className="weather-detail-row">
                <span>Atardecer</span>
                <strong>{climaSeleccionado.atardecer}</strong>
              </div>
            </div>

            {/* Pronóstico */}
            <div className="weather-forecast-card">
              <h3>¿Quieres planificar tu semana?</h3>

              <p>
                Revisa los cambios de temperatura y probabilidad de lluvia.
              </p>

              <a
                href="/pronostico"
                className="weather-forecast-button"
              >
                Ver pronóstico
                <i className="bi bi-chevron-right"></i>
              </a>
            </div>

          </aside>
        </div>

      </div>
    </main>
  );
}