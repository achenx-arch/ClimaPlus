import { useAuth } from "../context/auth";

/** Botón "Cerrar sesión" de los menús laterales de cada pantalla (mismo estilo que antes). */
export default function BotonCerrarSesion() {
  const { cerrarSesion } = useAuth();

  return (
    <button type="button" onClick={cerrarSesion}>
      <i className="bi bi-box-arrow-right"></i>
      Cerrar sesión
    </button>
  );
}
