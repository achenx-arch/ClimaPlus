import { Link } from "react-router-dom";
import { useAuth } from "../context/auth";
import "../components/Layouts.css";

/** Ruta inexistente: ofrece volver a destinos válidos según la sesión. */
export default function NoEncontrada() {
  const { estado } = useAuth();

  return (
    <main className="container py-4">
      <section className="cp-aviso" aria-labelledby="no-encontrada-titulo">
        <div className="cp-aviso-icono">
          <i className="bi bi-signpost-split" aria-hidden="true"></i>
        </div>
        <h1 id="no-encontrada-titulo">Página no encontrada</h1>
        <p>La dirección que abriste no existe en ClimaPlus.</p>
        <div className="cp-aviso-acciones">
          {estado === "autenticado" && (
            <Link to="/dashboard" className="btn btn-primary px-4">
              Volver a mi dashboard
            </Link>
          )}
          <Link to="/" className={estado === "autenticado" ? "btn btn-link" : "btn btn-primary px-4"}>
            Ir al Inicio
          </Link>
        </div>
      </section>
    </main>
  );
}
