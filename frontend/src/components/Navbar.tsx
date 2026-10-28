import { NavLink, useLocation } from "react-router-dom";

function Navbar() {
  const location = useLocation();

  // Identificar las pantallas administrativas
  const esAdministrador = location.pathname.startsWith("/admin");

  // Identificar las pantallas privadas del usuario
  const rutasUsuario = [
    "/dashboard",
    "/favoritos",
    "/historial",
    "/alertas",
    "/configuracion-alertas",
    "/dispositivos",
    "/perfil",
  ];

  const esUsuario =
    rutasUsuario.some(
      (ruta) =>
        location.pathname === ruta ||
        location.pathname.startsWith(`${ruta}/`)
    );

  // Temporal: después se obtendrá de la sesión real
  const sesionDemostracion = esAdministrador || esUsuario;

  const nombreUsuario = esAdministrador
    ? "Administrador"
    : "Amilcar Chen";

  const rutaPerfil = esAdministrador
    ? "/admin/perfil"
    : "/perfil";

  return (
    <nav className="navbar navbar-expand-lg bg-white border-bottom">
      <div className="container py-2">

        <NavLink className="navbar-brand fw-bold" to="/">
          <span className="bg-primary text-white rounded-3 px-2 py-2 me-2">
            <i className="bi bi-cloud"></i>
          </span>

          <span style={{ color: "#17324D" }}>
            Clima
          </span>

          <span style={{ color: "#38BDF8" }}>
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

            {sesionDemostracion ? (
              <>
                {/* Notificaciones */}
                <NavLink
                  to="/alertas"
                  className="text-decoration-none position-relative"
                  title="Notificaciones"
                  aria-label="Ver notificaciones"
                  style={{
                    color: "#075d87",
                    fontSize: "20px",
                  }}
                >
                  <i className="bi bi-bell"></i>
                </NavLink>

                {/* Menú del usuario */}
                <div className="dropdown">

                  <button
                    type="button"
                    className="btn d-flex align-items-center gap-2 border-0"
                    data-bs-toggle="dropdown"
                    aria-expanded="false"
                    style={{ color: "#17324D" }}
                  >
                    <span
                      className="d-flex align-items-center justify-content-center"
                      style={{
                        width: "36px",
                        height: "36px",
                        background: "#dff3ff",
                        borderRadius: "10px",
                        color: "#075d87",
                      }}
                    >
                      <i className="bi bi-person"></i>
                    </span>

                    <span className="fw-semibold">
                      {nombreUsuario}
                    </span>

                    <i className="bi bi-chevron-down small"></i>
                  </button>

                  <ul className="dropdown-menu dropdown-menu-end shadow-sm">
                    <li>
                      <NavLink
                        className="dropdown-item"
                        to={rutaPerfil}
                      >
                        <i className="bi bi-person me-2"></i>
                        Mi perfil
                      </NavLink>
                    </li>

                    <li>
                      <hr className="dropdown-divider" />
                    </li>

                    <li>
                      <NavLink
                        className="dropdown-item"
                        to="/login"
                      >
                        <i className="bi bi-box-arrow-right me-2"></i>
                        Cerrar sesión
                      </NavLink>
                    </li>
                  </ul>

                </div>
              </>
            ) : (
              <>
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
              </>
            )}

          </div>
        </div>
      </div>
    </nav>
  );
}

export default Navbar;