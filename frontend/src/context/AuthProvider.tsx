import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { alExpirarSesion, cancelarSolicitudesDeSesion, establecerToken } from "../services/apiClient";
import * as auth from "../services/authService";
import { limpiarPeticionesClima } from "../services/climaService";
import type { Usuario } from "../types/api";
import { AuthContext, type EstadoSesion, type ValorAuth } from "./auth";

// Sesión (contrato-auth-perfil.md, secciones 6 y 7):
// - El token se guarda en memoria. Con "Recordarme" también en sessionStorage, para
//   sobrevivir a una recarga de la pestaña (nunca en localStorage).
// - Solo se guardan el token y su expiración; nunca la contraseña ni el usuario.
// - Al restaurar se valida con GET /api/auth/me; si falla, la sesión se descarta.
// - Se cierra sola al llegar a expiraEn y ante cualquier 401 con token (→ M02 con aviso).
// - "Cerrar sesión" olvida el token y vuelve a Inicio (→ M01). Pasa por el estado "cerrando"
//   hasta que la URL es "/": React Router actualiza la ubicación dentro de startTransition, así
//   que el cambio de sesión puede pintarse antes que la nueva ruta; durante ese instante
//   RutaProtegida envía a "/" en lugar de a /login.

export const CLAVE_SESION = "climaplus_sesion";

const MENSAJE_EXPIRADA = "Tu sesión expiró. Inicia sesión de nuevo.";

interface SesionGuardada {
  token: string;
  expiraEn: string;
}

function leerSesionGuardada(): SesionGuardada | null {
  try {
    const texto = sessionStorage.getItem(CLAVE_SESION);
    if (!texto) return null;
    const datos = JSON.parse(texto) as Partial<SesionGuardada>;
    if (typeof datos.token !== "string" || typeof datos.expiraEn !== "string") return null;
    if (!(new Date(datos.expiraEn).getTime() > Date.now())) return null;   // vencida o fecha inválida
    return { token: datos.token, expiraEn: datos.expiraEn };
  } catch {
    return null;
  }
}

function borrarSesionGuardada(): void {
  try {
    sessionStorage.removeItem(CLAVE_SESION);
  } catch {
    // Almacenamiento no disponible: no hay nada que borrar.
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate();
  const location = useLocation();

  // La sesión guardada se lee una sola vez, antes del primer render de las páginas,
  // para que sus primeras peticiones ya lleven el token.
  const [sesionInicial] = useState<SesionGuardada | null>(() => {
    const guardada = leerSesionGuardada();
    if (!guardada) borrarSesionGuardada();
    establecerToken(guardada?.token ?? null);
    return guardada;
  });

  const [estado, setEstado] = useState<EstadoSesion>(sesionInicial ? "cargando" : "anonimo");
  const [usuario, setUsuario] = useState<Usuario | null>(null);
  const [expiraEn, setExpiraEn] = useState<string | null>(sesionInicial?.expiraEn ?? null);
  const [mensajeSesion, setMensajeSesion] = useState<string | null>(null);

  /** Borra todo lo privado del frontend: token, peticiones en curso, sesión guardada y usuario. */
  const limpiarSesion = useCallback(() => {
    cancelarSolicitudesDeSesion();
    establecerToken(null);
    limpiarPeticionesClima();
    borrarSesionGuardada();
    setUsuario(null);
    setExpiraEn(null);
  }, []);

  /** Sesión terminada por el servidor o por expiración: las rutas privadas envían a M02. */
  const terminarSesion = useCallback((mensaje: string | null) => {
    limpiarSesion();
    setEstado("anonimo");
    setMensajeSesion(mensaje);
  }, [limpiarSesion]);

  // Fin del cierre manual: en cuanto la URL es Inicio, la sesión queda simplemente anónima.
  if (estado === "cerrando" && location.pathname === "/") {
    setEstado("anonimo");
  }

  const navigateRef = useRef(navigate);
  useEffect(() => {
    navigateRef.current = navigate;
  }, [navigate]);

  // Mientras se valida una sesión restaurada, un 401 solo la descarta (sin redirigir).
  const restaurando = useRef(sesionInicial !== null);

  // Cualquier 401 con token: sesión terminada y vuelta a M02.
  useEffect(() => {
    alExpirarSesion(() => {
      if (restaurando.current) return;
      terminarSesion(MENSAJE_EXPIRADA);
      navigateRef.current("/login", { replace: true });
    });
    return () => alExpirarSesion(() => {});
  }, [terminarSesion]);

  // Restauración controlada.
  useEffect(() => {
    if (!sesionInicial) return;
    const control = new AbortController();
    auth
      .obtenerUsuarioActual(control.signal)
      .then((actual) => {
        restaurando.current = false;
        setUsuario(actual);
        setEstado("autenticado");
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        restaurando.current = false;
        terminarSesion(null);
      });
    return () => control.abort();
  }, [sesionInicial, terminarSesion]);

  // Cierre automático al llegar a expiraEn.
  useEffect(() => {
    if (!expiraEn) return;
    const restante = new Date(expiraEn).getTime() - Date.now();
    const temporizador = window.setTimeout(() => {
      terminarSesion(MENSAJE_EXPIRADA);
      navigateRef.current("/login", { replace: true });
    }, Math.max(0, Math.min(restante, 2_147_000_000)));
    return () => window.clearTimeout(temporizador);
  }, [expiraEn, terminarSesion]);

  const iniciarSesion = useCallback(async (correo: string, password: string, recordar: boolean) => {
    const respuesta = await auth.iniciarSesion({ correo, password });
    restaurando.current = false;
    establecerToken(respuesta.token);
    try {
      if (recordar) {
        sessionStorage.setItem(CLAVE_SESION, JSON.stringify({ token: respuesta.token, expiraEn: respuesta.expiraEn }));
      } else {
        sessionStorage.removeItem(CLAVE_SESION);
      }
    } catch {
      // Sin almacenamiento: la sesión queda solo en memoria.
    }
    setUsuario(respuesta.usuario);
    setExpiraEn(respuesta.expiraEn);
    setMensajeSesion(null);
    setEstado("autenticado");
    return respuesta.usuario;
  }, []);

  const cerrarSesion = useCallback(() => {
    // El backend no tiene endpoint de logout: los JWT no se guardan en el servidor (D-01),
    // así que cerrar sesión es olvidar el token en el navegador y volver a Inicio (M01).
    // Todo lo privado se borra en el mismo instante (nada privado queda en pantalla).
    limpiarSesion();
    setMensajeSesion(null);
    setEstado(location.pathname === "/" ? "anonimo" : "cerrando");
    navigateRef.current("/", { replace: true });
  }, [limpiarSesion, location.pathname]);

  const cerrarSesionConMensaje = useCallback((mensaje: string) => {
    terminarSesion(mensaje);
    navigateRef.current("/login", { replace: true });
  }, [terminarSesion]);

  const valor = useMemo<ValorAuth>(
    () => ({
      estado,
      usuario,
      esAdministrador: usuario?.rol === "ADMINISTRADOR",
      mensajeSesion,
      iniciarSesion,
      cerrarSesion,
      cerrarSesionConMensaje,
      actualizarUsuario: setUsuario,
      limpiarMensajeSesion: () => setMensajeSesion(null),
    }),
    [estado, usuario, mensajeSesion, iniciarSesion, cerrarSesion, cerrarSesionConMensaje]
  );

  return <AuthContext.Provider value={valor}>{children}</AuthContext.Provider>;
}
