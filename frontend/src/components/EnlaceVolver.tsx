import { Link } from "react-router-dom";
import "./Layouts.css";

/**
 * Navegación contextual "Volver" a un destino explícito y existente (no usa navigate(-1),
 * que podría sacar al usuario de la aplicación si entró por un enlace directo).
 */
export default function EnlaceVolver({ to, children }: { to: string; children: string }) {
  return (
    <Link to={to} className="cp-volver">
      <i className="bi bi-arrow-left" aria-hidden="true"></i>
      {children}
    </Link>
  );
}
