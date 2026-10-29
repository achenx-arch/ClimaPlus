import { Link } from "react-router-dom";
import "./Layouts.css";

/** Pantalla para una cuenta sin el rol requerido. No muestra nada de la pantalla protegida. */
export default function AccesoDenegado() {
  return (
    <main className="container py-4">
      <section className="cp-aviso" role="alert" aria-labelledby="acceso-denegado-titulo">
        <div className="cp-aviso-icono">
          <i className="bi bi-shield-lock" aria-hidden="true"></i>
        </div>
        <h1 id="acceso-denegado-titulo">Acceso denegado</h1>
        <p>
          Tu cuenta no tiene permisos para ver esta pantalla. Las opciones de administración
          solo están disponibles para cuentas con rol de administrador.
        </p>
        <p className="small mb-0">
          Si crees que es un error, contacta a un administrador de ClimaPlus.
        </p>
        <div className="cp-aviso-acciones">
          <Link to="/dashboard" className="btn btn-primary px-4">
            Volver a mi dashboard
          </Link>
          <Link to="/" className="btn btn-link">
            Ir al Inicio
          </Link>
        </div>
      </section>
    </main>
  );
}
