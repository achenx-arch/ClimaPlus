import "./Forecast.css";

export default function Forecast() {
  const pronostico = [
    {
      dia: "Lunes",
      fecha: "26 oct.",
      icono: "bi-cloud-sun",
      condicion: "Parcialmente nublado",
      maxima: 25,
      minima: 16,
      lluvia: 20,
    },
    {
      dia: "Martes",
      fecha: "27 oct.",
      icono: "bi-cloud",
      condicion: "Nublado",
      maxima: 23,
      minima: 15,
      lluvia: 35,
    },
    {
      dia: "Miércoles",
      fecha: "28 oct.",
      icono: "bi-cloud-drizzle",
      condicion: "Lluvia ligera",
      maxima: 22,
      minima: 16,
      lluvia: 65,
    },
    {
      dia: "Jueves",
      fecha: "29 oct.",
      icono: "bi-cloud-sun",
      condicion: "Parcialmente nublado",
      maxima: 24,
      minima: 16,
      lluvia: 25,
    },
    {
      dia: "Viernes",
      fecha: "30 oct.",
      icono: "bi-sun",
      condicion: "Soleado",
      maxima: 26,
      minima: 17,
      lluvia: 10,
    },
  ];

  return (
    <main className="forecast-page">
      <div className="forecast-container">

        <span className="forecast-label">
          PRONÓSTICO DEL TIEMPO
        </span>

        <h1>Pronóstico de 5 días</h1>

        <p className="forecast-description">
          Consulta las condiciones meteorológicas de los próximos días
          y planifica tus actividades.
        </p>

        <div className="forecast-location">
          <i className="bi bi-geo-alt-fill"></i>
          <span>Ciudad de Guatemala, Guatemala</span>
        </div>

        {/* Buscador de ciudades */}
        <div className="forecast-search">
          <i className="bi bi-search"></i>

          <input
            type="text"
            placeholder="Ciudad, municipio o código postal"
            aria-label="Buscar ciudad"
          />

          <button type="button">
            Consultar clima
          </button>
        </div>

        {/* Tarjetas del pronóstico */}
        <section className="forecast-grid">
          {pronostico.map((dia, index) => (
            <article
              className={`forecast-card ${
                index === 0 ? "forecast-card-today" : ""
              }`}
              key={dia.fecha}
            >
              <div className="forecast-card-header">
                <h2>{dia.dia}</h2>
                <span>{dia.fecha}</span>
              </div>

              <div className="forecast-icon">
                <i className={`bi ${dia.icono}`}></i>
              </div>

              <p className="forecast-condition">
                {dia.condicion}
              </p>

              <div className="forecast-temperatures">
                <div>
                  <span>Máxima</span>
                  <strong>{dia.maxima}°</strong>
                </div>

                <div>
                  <span>Mínima</span>
                  <strong>{dia.minima}°</strong>
                </div>
              </div>

              <div className="forecast-rain">
                <i className="bi bi-droplet-fill"></i>
                <span>Prob. de lluvia</span>
                <strong>{dia.lluvia}%</strong>
              </div>
            </article>
          ))}
        </section>

        {/* Gráfico y recomendación */}
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
              {pronostico.map((dia) => (
                <div
                  className="forecast-chart-day"
                  key={dia.fecha}
                >
                  <div className="forecast-chart-columns">

                    <div
                      className="forecast-bar-max"
                      style={{
                        height: `${dia.maxima * 4}px`,
                      }}
                      title={`Máxima: ${dia.maxima}°`}
                    ></div>

                    <div
                      className="forecast-bar-min"
                      style={{
                        height: `${dia.minima * 4}px`,
                      }}
                      title={`Mínima: ${dia.minima}°`}
                    ></div>

                  </div>

                  <span>{dia.dia}</span>
                </div>
              ))}
            </div>

          </section>

          {/* Recomendación meteorológica */}
          <div className="forecast-recommendation">

            <i className="bi bi-cloud-rain"></i>

            <h3>Lleva paraguas el miércoles</h3>

            <p>
              La probabilidad de lluvia aumenta durante el miércoles.
              Te recomendamos llevar paraguas si tienes actividades
              al aire libre.
            </p>

          </div>

        </div>

        {/* Aviso de datos de demostración */}
        <p className="forecast-note">
          <i className="bi bi-info-circle"></i>
          Los valores mostrados son de demostración.
          Se actualizarán al conectar el servicio meteorológico.
        </p>

      </div>
    </main>
  );
}