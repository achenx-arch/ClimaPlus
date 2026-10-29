import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import './Home.css'

function Home() {
  const [ciudad, setCiudad] = useState('')
  const [error, setError] = useState('')
  const navigate = useNavigate()

  // M01 → M04: la búsqueda se resuelve en /clima con GET /api/clima/ubicaciones.
  const consultarClima = (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault()

    const busqueda = ciudad.trim()

    if (!busqueda) {
      setError('Por favor, ingresa una ciudad.')
      return
    }

    setError('')
    navigate(`/clima?q=${encodeURIComponent(busqueda)}`)
  }

  return (
    <main className="home-page">

      {/* SECCIÓN PRINCIPAL */}
      <section className="home-hero">
        <div className="container">

          <div className="row align-items-center g-5">

            {/* INFORMACIÓN PRINCIPAL */}
            <div className="col-lg-6">

              <div className="home-badge">
                <i className="bi bi-circle-fill"></i>
                INFORMACIÓN METEOROLÓGICA PRECISA
              </div>

              <h1 className="home-title">
                El clima de tu ciudad,
                <span> sin complicaciones.</span>
              </h1>

              <p className="home-description">
                Consulta las condiciones actuales y el pronóstico
                para planificar mejor cada momento de tu día.
              </p>

              {/* BUSCADOR */}
              <form
                className="home-search"
                onSubmit={consultarClima}
              >
                <i className="bi bi-search"></i>

                <input
                  type="text"
                  placeholder="Ciudad o municipio"
                  value={ciudad}
                  onChange={(e) => setCiudad(e.target.value)}
                  aria-label="Buscar ciudad"
                />

                <button type="submit">
                  Consultar clima
                </button>
              </form>

              {error && (
                <p className="text-danger small mt-2" role="alert">{error}</p>
              )}

              <div className="home-benefits">
                <span>
                  <i className="bi bi-check2"></i>
                  Sin registro
                </span>

                <span>
                  <i className="bi bi-check2"></i>
                  Datos actualizados
                </span>

                <span>
                  <i className="bi bi-check2"></i>
                  Pronóstico de 5 días
                </span>
              </div>

            </div>

            {/* TARJETA DEL CLIMA */}
            <div className="col-lg-6">

              <div className="home-weather-card">

                <div className="d-flex justify-content-between align-items-start">
                  <div>
                    <h5>
                      <i className="bi bi-geo-alt-fill me-2"></i>
                      Ciudad de Guatemala
                    </h5>

                    <p className="weather-date">
                      Datos de demostración
                    </p>
                  </div>

                  <span className="weather-now">
                    Ejemplo
                  </span>
                </div>

                <div className="weather-main">

                  <div>
                    <div className="weather-temperature">
                      22°
                    </div>

                    <h4>Parcialmente nublado</h4>

                    <p>Sensación térmica 23°</p>
                  </div>

                  <div className="weather-icon">
                    <i className="bi bi-cloud-sun"></i>
                  </div>

                </div>

                <div className="weather-stats">

                  <div className="weather-stat">
                    <div className="stat-header">
                      <span>SENSACIÓN</span>
                      <i className="bi bi-sun"></i>
                    </div>

                    <strong>23°</strong>
                    <small>Térmica</small>
                  </div>

                  <div className="weather-stat">
                    <div className="stat-header">
                      <span>HUMEDAD</span>
                      <i className="bi bi-droplet"></i>
                    </div>

                    <strong>74%</strong>
                    <small>Moderada</small>
                  </div>

                  <div className="weather-stat">
                    <div className="stat-header">
                      <span>VIENTO</span>
                      <i className="bi bi-wind"></i>
                    </div>

                    <strong>12 km/h</strong>
                    <small>Noreste</small>
                  </div>

                </div>

              </div>

            </div>

          </div>

        </div>
      </section>

      {/* SECCIÓN INFORMATIVA */}
      <section className="home-features">
        <div className="container">

          <div className="features-heading">
            <div>
              <span className="features-label">
                TODO LO QUE NECESITAS
              </span>

              <h2>
                Prepárate para cualquier clima
              </h2>
            </div>

            <p>
              Información clara y útil para tomar mejores
              decisiones antes de salir.
            </p>
          </div>

          <div className="row g-4">

            <div className="col-md-4">
              <Link to="/clima" className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-sun"></i>
                </div>

                <h3>Condiciones actuales</h3>

                <p>
                  Temperatura, humedad, viento y visibilidad
                  en un solo lugar.
                </p>
              </Link>
            </div>

            <div className="col-md-4">
              <Link to="/pronostico" className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-calendar3"></i>
                </div>

                <h3>Pronóstico de 5 días</h3>

                <p>
                  Una vista sencilla de los próximos días
                  para planificar con tiempo.
                </p>
              </Link>
            </div>

            <div className="col-md-4">
              <div className="feature-card">
                <div className="feature-icon">
                  <i className="bi bi-cloud-rain"></i>
                </div>

                <h3>Alertas ambientales</h3>

                <p>
                  Información clara sobre condiciones que
                  pueden afectar tus actividades.
                </p>
              </div>
            </div>

          </div>

        </div>
      </section>

    </main>
  )
}

export default Home