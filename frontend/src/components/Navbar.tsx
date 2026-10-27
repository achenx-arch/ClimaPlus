import { NavLink } from 'react-router-dom'

function Navbar() {
  return (
    <nav className="navbar navbar-expand-lg bg-white border-bottom">
      <div className="container py-2">

        <NavLink className="navbar-brand fw-bold" to="/">
          <span className="bg-primary text-white rounded-3 px-2 py-2 me-2">
            <i className="bi bi-cloud"></i>
          </span>

          <span style={{ color: '#17324D' }}>
            Clima
          </span>

          <span style={{ color: '#38BDF8' }}>
            Plus
          </span>
        </NavLink>

        <button
          className="navbar-toggler"
          type="button"
          data-bs-toggle="collapse"
          data-bs-target="#navbarClimaPlus"
          aria-controls="navbarClimaPlus"
          aria-expanded="false"
          aria-label="Mostrar navegación"
        >
          <span className="navbar-toggler-icon"></span>
        </button>

        <div
          className="collapse navbar-collapse"
          id="navbarClimaPlus"
        >
          <ul className="navbar-nav mx-auto gap-lg-4">

            <li className="nav-item">
              <NavLink className="nav-link" to="/">
                Inicio
              </NavLink>
            </li>

            <li className="nav-item">
              <NavLink className="nav-link" to="/clima">
                Clima actual
              </NavLink>
            </li>

            <li className="nav-item">
              <NavLink className="nav-link" to="/pronostico">
                Pronóstico
              </NavLink>
            </li>

          </ul>

          <div className="d-flex gap-3 align-items-center">

            <NavLink
              to="/login"
              className="text-decoration-none text-secondary"
            >
              Iniciar sesión
            </NavLink>

            <NavLink
              to="/registro"
              className="btn btn-primary px-4"
            >
              Crear cuenta
            </NavLink>

          </div>
        </div>
      </div>
    </nav>
  )
}

export default Navbar