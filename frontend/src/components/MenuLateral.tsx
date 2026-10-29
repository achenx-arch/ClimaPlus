import { useState, type ReactNode } from "react";
import { Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../context/auth";
import AdminSidebar from "./AdminSidebar";
import UserSidebar from "./UserSidebar";
import "./Layouts.css";

/**
 * Layout con menú lateral persistente. En escritorio el menú queda fijo a la izquierda;
 * en móviles y tabletas (< 992 px) se abre como offcanvas con el botón "Menú".
 * Se usa como elemento de ruta (renderiza <Outlet />) o envolviendo children.
 */
function LayoutConMenu({ tipo, children }: { tipo: "usuario" | "admin"; children?: ReactNode }) {
  const location = useLocation();
  // El menú móvil queda abierto solo en la ruta donde se abrió: al navegar se cierra solo.
  const [abiertoEn, setAbiertoEn] = useState<string | null>(null);
  const abierto = abiertoEn === location.pathname;
  const cerrar = () => setAbiertoEn(null);
  const titulo = tipo === "admin" ? "Menú de administración" : "Menú de mi espacio";
  const Menu = tipo === "admin" ? AdminSidebar : UserSidebar;

  return (
    <div className={`cp-layout ${tipo === "admin" ? "cp-layout-admin" : ""}`}>
      <div className="cp-layout-menu">
        <Menu />
      </div>

      <div className="cp-layout-contenido">
        <div className="cp-barra-movil">
          <button
            type="button"
            aria-controls="cp-menu-movil"
            aria-expanded={abierto}
            onClick={() => setAbiertoEn(location.pathname)}
          >
            <i className="bi bi-list" aria-hidden="true"></i>
            {titulo}
          </button>
        </div>

        {children ?? <Outlet />}
      </div>

      {abierto && (
        <>
          <div
            id="cp-menu-movil"
            className="offcanvas offcanvas-start show cp-menu-movil"
            role="dialog"
            aria-modal="true"
            aria-label={titulo}
            tabIndex={-1}
            onKeyDown={(e) => {
              if (e.key === "Escape") cerrar();
            }}
          >
            <div className="offcanvas-header border-bottom">
              <h2 className="offcanvas-title h6 mb-0">{titulo}</h2>
              <button type="button" className="btn-close" aria-label="Cerrar menú" onClick={cerrar}></button>
            </div>
            <div className="offcanvas-body">
              <Menu alNavegar={cerrar} />
            </div>
          </div>
          <div className="offcanvas-backdrop fade show" onClick={cerrar}></div>
        </>
      )}
    </div>
  );
}

export function LayoutUsuario({ children }: { children?: ReactNode }) {
  return <LayoutConMenu tipo="usuario">{children}</LayoutConMenu>;
}

export function LayoutAdmin({ children }: { children?: ReactNode }) {
  return <LayoutConMenu tipo="admin">{children}</LayoutConMenu>;
}

/** Clima y pronóstico son públicos: con sesión se muestran dentro de "Mi espacio", sin sesión a pantalla completa. */
export function LayoutSegunSesion() {
  const { estado } = useAuth();
  return estado === "autenticado" ? <LayoutUsuario /> : <Outlet />;
}
