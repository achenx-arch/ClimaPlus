import { useEffect, useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import "./Favorites.css";
import "./Dashboard.css";

type CiudadFavorita = {
  nombre: string;
  ubicacion: string;
  temperatura: number;
  condicion: string;
  icono: string;
  humedad: number;
  viento: number;
  fecha: string;
};

export default function Favorites() {
  const navigate = useNavigate();

  const [busqueda, setBusqueda] = useState("");

  const [ciudadesFavoritas, setCiudadesFavoritas] = useState<CiudadFavorita[]>(
    () => {
      const guardadas = localStorage.getItem("climaplus_favoritos");

      if (guardadas !== null) {
        try {
          const datos = JSON.parse(guardadas);

          if (Array.isArray(datos)) {
            return datos;
          }
        } catch {
          console.error("No se pudieron cargar los favoritos");
        }
      }

      return [
        {
          nombre: "Ciudad de Guatemala",
          ubicacion: "Guatemala, GT",
          temperatura: 22,
          condicion: "Parcialmente nublado",
          icono: "bi-cloud-sun",
          humedad: 74,
          viento: 12,
          fecha: "7 oct. 2026 - 08:30 a. m.",
        },
        {
          nombre: "Antigua Guatemala",
          ubicacion: "Sacatepéquez, GT",
          temperatura: 20,
          condicion: "Lluvia ligera",
          icono: "bi-cloud-drizzle",
          humedad: 81,
          viento: 8,
          fecha: "6 oct. 2026 - 05:15 p. m.",
        },
        {
          nombre: "Quetzaltenango",
          ubicacion: "Quetzaltenango, GT",
          temperatura: 16,
          condicion: "Nublado",
          icono: "bi-cloud",
          humedad: 78,
          viento: 10,
          fecha: "6 oct. 2026 - 06:40 a. m.",
        },
      ];
    }
  );

  useEffect(() => {
    localStorage.setItem(
      "climaplus_favoritos",
      JSON.stringify(ciudadesFavoritas)
    );
  }, [ciudadesFavoritas]);

  const quitarFavorito = (nombre: string) => {
    setCiudadesFavoritas((ciudadesActuales) =>
      ciudadesActuales.filter((ciudad) => ciudad.nombre !== nombre)
    );
  };

  const ciudadesFiltradas = ciudadesFavoritas.filter((ciudad) =>
    `${ciudad.nombre} ${ciudad.ubicacion}`
      .toLocaleLowerCase("es")
      .includes(busqueda.trim().toLocaleLowerCase("es"))
  );

  return (
    <main className="favorites-page">
      <div className="favorites-layout">

        {/* MENÚ LATERAL */}
        <aside className="dashboard-sidebar">
          <span className="dashboard-sidebar-title">
            MI ESPACIO
          </span>

          <nav className="dashboard-sidebar-nav">
            <NavLink to="/dashboard" end>
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

            <NavLink to="/alertas">
              <i className="bi bi-bell"></i>
              Alertas
            </NavLink>

            <NavLink to="/dispositivos">
              <i className="bi bi-display"></i>
              Mis dispositivos
            </NavLink>
          </nav>

          <div className="dashboard-sidebar-bottom">
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
        <div className="favorites-container">

          <span className="favorites-label">
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
              disabled
            >
              <i className="bi bi-arrow-clockwise"></i>
              Actualizar clima
            </button>
          </div>

          {/* BUSCADOR */}
          <section className="favorites-search-section">
            <label htmlFor="favorites-search">
              Buscador de ubicaciones
            </label>

            <div className="favorites-search-box">
              <div className="favorites-search-input">
                <i className="bi bi-search"></i>

                <input
                  id="favorites-search"
                  type="search"
                  placeholder="Buscar ciudad o municipio"
                  value={busqueda}
                  onChange={(e) => setBusqueda(e.target.value)}
                  maxLength={100}
                />
              </div>

              <button
                type="button"
                onClick={() => {
                  document
                    .getElementById("favorites-search")
                    ?.focus();
                }}
              >
                Buscar
              </button>
            </div>
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
                Se muestra tu última consulta guardada.
                La actualización del clima estará disponible
                al conectar el servicio meteorológico.
              </span>
            </div>

            {/* SIN FAVORITOS */}
            {ciudadesFavoritas.length === 0 && (
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
              {ciudadesFiltradas.map((ciudad) => (
                <article
                  className="favorites-weather-card"
                  key={ciudad.nombre}
                >
                  <div className="favorites-weather-header">
                    <div>
                      <h3>{ciudad.nombre}</h3>
                      <span>{ciudad.ubicacion}</span>
                    </div>

                    <i className="bi bi-star"></i>
                  </div>

                  <div className="favorites-weather-main">
                    <div>
                      <strong>{ciudad.temperatura}°</strong>
                      <p>{ciudad.condicion}</p>
                    </div>

                    <i className={`bi ${ciudad.icono}`}></i>
                  </div>

                  <div className="favorites-weather-details">
                    <span>
                      <i className="bi bi-droplet-half"></i>
                      {ciudad.humedad}% humedad
                    </span>

                    <span>
                      <i className="bi bi-wind"></i>
                      {ciudad.viento} km/h
                    </span>
                  </div>

                  <div className="favorites-weather-updated">
                    <i className="bi bi-clock"></i>

                    <span>
                      Última consulta almacenada: {ciudad.fecha}
                    </span>
                  </div>

                  <div className="favorites-weather-actions">
                    <button
                      type="button"
                      className="favorites-view-button"
                      onClick={() =>
                        navigate(
                          `/clima?ciudad=${encodeURIComponent(
                            ciudad.nombre
                          )}`
                        )
                      }
                    >
                      <i className="bi bi-cloud-sun"></i>
                      Ver clima
                    </button>

                    <button
                      type="button"
                      className="favorites-remove-button"
                      onClick={() =>
                        quitarFavorito(ciudad.nombre)
                      }
                    >
                      <i className="bi bi-star"></i>
                      Quitar de favoritos
                    </button>
                  </div>
                </article>
              ))}
            </div>
          </section>

        </div>
      </div>
    </main>
  );
}