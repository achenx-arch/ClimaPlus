import { render } from "@testing-library/react";
import { StrictMode, type ReactElement } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import Avisos from "../components/Avisos";
import { AuthProvider, CLAVE_SESION } from "../context/AuthProvider";
import { NotificacionesProvider } from "../context/NotificacionesProvider";
import type { Usuario } from "../types/api";
import type { crearServidor } from "./servidorFalso";

export const TOKEN_PRUEBA = "token.de.prueba";

/** Deja una sesión "recordada" para que AuthProvider la restaure con GET /api/auth/me. */
export function conSesion(servidor: ReturnType<typeof crearServidor>, usuario: Usuario, nuevas = 0) {
  sessionStorage.setItem(
    CLAVE_SESION,
    JSON.stringify({ token: TOKEN_PRUEBA, expiraEn: new Date(Date.now() + 3_600_000).toISOString() })
  );
  servidor.en("GET", "/api/auth/me", { cuerpo: usuario });
  servidor.en("GET", "/api/alertas/contador", { cuerpo: { nuevas } });
}

function UbicacionActual() {
  const location = useLocation();
  return <output data-testid="ruta">{`${location.pathname}${location.search}`}</output>;
}

/** Renderiza una pantalla con router, sesión y notificaciones reales. */
export function renderizar(
  elemento: ReactElement,
  { ruta = "/", patron = "*", rutasExtra }: { ruta?: string; patron?: string; rutasExtra?: ReactElement } = {}
) {
  // StrictMode como en main.tsx: monta dos veces los efectos en desarrollo.
  return render(
    <StrictMode>
    <MemoryRouter initialEntries={[ruta]}>
      <AuthProvider>
        <NotificacionesProvider>
          <Routes>
            {rutasExtra}
            <Route path={patron} element={elemento} />
            <Route path="/login" element={<p>Pantalla de inicio de sesión</p>} />
          </Routes>
          <UbicacionActual />
          <Avisos />
        </NotificacionesProvider>
      </AuthProvider>
    </MemoryRouter>
    </StrictMode>
  );
}
