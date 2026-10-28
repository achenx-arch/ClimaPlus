import { useState } from "react";
import { NavLink, useNavigate } from "react-router-dom";
import "./History.css";

type Consulta = {
  id: number;
  ciudad: string;
  ubicacion: string;
  temperatura: number;
  condicion: string;
  fecha: string;
  hora: string;
  icono: string;
  tipo?: "ACTUAL" | "PRONÓSTICO";
};

export default function History() {
  const navigate = useNavigate();

  const [busqueda, setBusqueda] = useState("");
  const [tipoFiltro, setTipoFiltro] = useState("TODOS");
  const [fechaFiltro, setFechaFiltro] = useState("");

  const [consultas, setConsultas] = useState<Consulta[]>(() => {
    try {
      const guardadas = localStorage.getItem("climaplus_historial");

      if (guardadas !== null) {
        const datos = JSON.parse(guardadas);
        return Array.isArray(datos) ? datos : [];
      }

      // Consultas de demostración para visualizar el diseño de Figma.
      return [
        {
          id: 1,
          ciudad: "Ciudad de Guatemala",
          ubicacion: "Guatemala, GT",
          temperatura: 22,
          condicion: "Parcialmente nublado",
          fecha: "7 oct. 2026",
          hora: "08:35",
          icono: "bi-cloud-sun",
          tipo: "PRONÓSTICO",
        },
        {
          id: 2,
          ciudad: "Ciudad de Guatemala",
          ubicacion: "Guatemala, GT",
          temperatura: 22,
          condicion: "Parcialmente nublado",
          fecha: "7 oct. 2026",
          hora: "08:30",
          icono: "bi-cloud-sun",
          tipo: "ACTUAL",
        },
        {
          id: 3,
          ciudad: "Antigua Guatemala",
          ubicacion: "Sacatepéquez, GT",
          temperatura: 20,
          condicion: "Lluvia ligera",
          fecha: "6 oct. 2026",
          hora: "17:20",
          icono: "bi-cloud-drizzle",
          tipo: "PRONÓSTICO",
        },
        {
          id: 4,
          ciudad: "Antigua Guatemala",
          ubicacion: "Sacatepéquez, GT",
          temperatura: 20,
          condicion: "Lluvia ligera",
          fecha: "6 oct. 2026",
          hora: "17:15",
          icono: "bi-cloud-drizzle",
          tipo: "ACTUAL",
        },
        {
          id: 5,
          ciudad: "Quetzaltenango",
          ubicacion: "Quetzaltenango, GT",
          temperatura: 16,
          condicion: "Nublado",
          fecha: "6 oct. 2026",
          hora: "16:40",
          icono: "bi-cloud",
          tipo: "ACTUAL",
        },
      ];
    } catch {
      return [];
    }
  });

  const limpiarHistorial = () => {
    if (
      !window.confirm(
        "¿Estás seguro de que deseas eliminar todo tu historial de consultas?"
      )
    ) {
      return;
    }

    localStorage.setItem("climaplus_historial", JSON.stringify([]));
    setConsultas([]);
  };

  const eliminarConsulta = (id: number) => {
    if (!window.confirm("¿Deseas eliminar esta consulta del historial?")) {
      return;
    }

    const actualizadas = consultas.filter(
      (consulta) => consulta.id !== id
    );

    setConsultas(actualizadas);

    localStorage.setItem(
      "climaplus_historial",
      JSON.stringify(actualizadas)
    );
  };

  const consultasFiltradas = consultas.filter((consulta) => {
    const coincideBusqueda =
      `${consulta.ciudad} ${consulta.ubicacion}`
        .toLocaleLowerCase("es")
        .includes(busqueda.trim().toLocaleLowerCase("es"));

    const coincideTipo =
      tipoFiltro === "TODOS" ||
      (consulta.tipo ?? "ACTUAL") === tipoFiltro;

    const coincideFecha =
      !fechaFiltro ||
      consulta.fecha ===
        new Date(`${fechaFiltro}T12:00:00`).toLocaleDateString(
          "es-GT",
          {
            day: "numeric",
            month: "short",
            year: "numeric",
          }
        );

    return coincideBusqueda && coincideTipo && coincideFecha;
  });

  return (
    <main className="history-page">
      <div className="history-layout">

        {/* MENÚ LATERAL */}
        <aside className="history-sidebar">
          <span className="history-sidebar-title">MI ESPACIO</span>

          <nav className="history-sidebar-nav">
            <NavLink to="/dashboard">
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

            <NavLink to="/historial" className="active">
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

          <div className="history-sidebar-bottom">
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

        {/* CONTENIDO */}
        <div className="history-container">
          <span className="history-label">HISTORIAL</span>

          <header className="history-header">
            <h1>Historial de consultas</h1>
            <p>
              Revisa las consultas de clima y pronóstico realizadas
              anteriormente.
            </p>
          </header>

          {/* FILTROS */}
          <section className="history-filters">
            <div className="history-filter-field">
              <label htmlFor="history-search">
                Buscar ubicación
              </label>

              <div className="history-search-box">
                <i className="bi bi-search"></i>
                <input
                  id="history-search"
                  type="search"
                  placeholder="Ciudad o municipio"
                  value={busqueda}
                  onChange={(e) => setBusqueda(e.target.value)}
                  maxLength={100}
                />
              </div>
            </div>

            <div className="history-filter-field">
              <label htmlFor="history-type">
                Tipo de consulta
              </label>

              <select
                id="history-type"
                value={tipoFiltro}
                onChange={(e) => setTipoFiltro(e.target.value)}
              >
                <option value="TODOS">Todos</option>
                <option value="ACTUAL">Clima actual</option>
                <option value="PRONÓSTICO">Pronóstico</option>
              </select>
            </div>

            <div className="history-filter-field">
              <label htmlFor="history-date">
                Fecha de consulta
              </label>

              <input
                id="history-date"
                type="date"
                value={fechaFiltro}
                onChange={(e) => setFechaFiltro(e.target.value)}
              />
            </div>

            <button
              type="button"
              className="history-filter-button"
              onClick={() => {
                setBusqueda("");
                setTipoFiltro("TODOS");
                setFechaFiltro("");
              }}
            >
              <i className="bi bi-arrow-counterclockwise"></i>
              Limpiar filtros
            </button>
          </section>

          {/* HISTORIAL */}
          <section className="history-records">
            <div className="history-records-header">
              <h2>Consultas anteriores</h2>

              <div className="history-records-actions">
                <span>
                  {consultasFiltradas.length}{" "}
                  {consultasFiltradas.length === 1
                    ? "consulta"
                    : "consultas"}
                </span>

                <button
                  type="button"
                  className="history-clear-button"
                  onClick={limpiarHistorial}
                  disabled={consultas.length === 0}
                >
                  <i className="bi bi-trash3"></i>
                  Limpiar historial
                </button>
              </div>
            </div>

            {consultasFiltradas.length > 0 ? (
              <div className="history-table-wrapper">
                <table className="history-table">
                  <thead>
                    <tr>
                      <th>Ubicación</th>
                      <th>Tipo de consulta</th>
                      <th>Fecha</th>
                      <th>Hora</th>
                      <th>Acción</th>
                    </tr>
                  </thead>

                  <tbody>
                    {consultasFiltradas.map((consulta) => (
                      <tr key={consulta.id}>
                        <td>
                          <div className="history-city">
                            <strong>{consulta.ciudad}</strong>
                            <span>{consulta.ubicacion}</span>
                          </div>
                        </td>

                        <td>
                          <span
                            className={`history-type-badge ${
                              (consulta.tipo ?? "ACTUAL") ===
                              "PRONÓSTICO"
                                ? "forecast"
                                : "current"
                            }`}
                          >
                            {consulta.tipo ?? "ACTUAL"}
                          </span>
                        </td>

                        <td>{consulta.fecha}</td>
                        <td>{consulta.hora}</td>

                        <td>
                          <div className="history-row-actions">
                            <button
                              type="button"
                              className="history-view-button"
                              onClick={() =>
                                navigate(
                                  (consulta.tipo ?? "ACTUAL") ===
                                    "PRONÓSTICO"
                                    ? "/pronostico"
                                    : `/clima?ciudad=${encodeURIComponent(
                                        consulta.ciudad
                                      )}`
                                )
                              }
                            >
                              Ver
                            </button>

                            <button
                              type="button"
                              className="history-delete-button"
                              onClick={() =>
                                eliminarConsulta(consulta.id)
                              }
                              title="Eliminar consulta"
                              aria-label={`Eliminar consulta de ${consulta.ciudad}`}
                            >
                              <i className="bi bi-x-lg"></i>
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                <div className="history-table-footer">
                  Mostrando {consultasFiltradas.length} de{" "}
                  {consultas.length} consultas
                </div>
              </div>
            ) : (
              <div className="history-empty">
                <i className="bi bi-clock-history"></i>
                <h3>
                  {consultas.length === 0
                    ? "Todavía no tienes consultas registradas"
                    : "No encontramos consultas"}
                </h3>
                <p>
                  {consultas.length === 0
                    ? "Las ciudades que consultes aparecerán aquí."
                    : "Prueba con otros filtros de búsqueda."}
                </p>
              </div>
            )}

            <div className="history-info">
              <i className="bi bi-info-circle"></i>
              <span>
                Solo se incluyen consultas realizadas con tu sesión
                iniciada. Este historial no incluye consultas públicas
                anónimas.
              </span>
            </div>
          </section>
        </div>
      </div>
    </main>
  );
}