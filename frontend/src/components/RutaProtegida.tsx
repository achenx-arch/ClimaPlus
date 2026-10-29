import type { ReactNode } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../context/auth";
import AccesoDenegado from "./AccesoDenegado";
import type { Rol } from "../types/api";

/**
 * Protege una pantalla. Sin sesión → M02 (volviendo después a la ruta pedida).
 * Con un rol distinto del requerido → pantalla "Acceso denegado" sin cerrar la sesión ni
 * montar la pantalla protegida (así no se llama a ningún endpoint administrativo).
 * Es solo una ayuda visual: quien decide los permisos es el backend.
 */
export default function RutaProtegida({ children, rol }: { children: ReactNode; rol?: Rol }) {
  const { estado, usuario } = useAuth();
  const location = useLocation();

  if (estado === "cargando") {
    return (
      <div className="d-flex justify-content-center align-items-center py-5" role="status">
        <div className="spinner-border text-primary me-2" aria-hidden="true"></div>
        <span>Verificando tu sesión…</span>
      </div>
    );
  }

  // Cierre de sesión manual en curso: el destino es Inicio, nunca M02.
  if (estado === "cerrando") {
    return <Navigate to="/" replace />;
  }

  if (estado !== "autenticado" || !usuario) {
    return <Navigate to="/login" replace state={{ desde: `${location.pathname}${location.search}` }} />;
  }

  if (rol && usuario.rol !== rol) {
    return <AccesoDenegado />;
  }

  return <>{children}</>;
}
