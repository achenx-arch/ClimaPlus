// Cliente HTTP único para el backend ClimaPlus (ASP.NET Core 8).
// - URL base desde VITE_API_URL (desarrollo: http://localhost:5208).
// - Authorization: Bearer solo cuando la ruta lo requiere o es opcional y hay sesión.
// - Errores application/problem+json (RFC 9457) convertidos en ApiError.
// - Nunca escribe el token ni los cuerpos en la consola.

export const API_URL: string = (
  import.meta.env.VITE_API_URL || "http://localhost:5208"
).replace(/\/+$/, "");

/** Cómo se envía el token en una petición. */
export type ModoAutenticacion = "requerida" | "opcional" | "ninguna";

export type Errores = Record<string, string[]>;

/** Error HTTP normalizado. status 0 = sin conexión con el servidor. */
export class ApiError extends Error {
  readonly status: number;
  readonly titulo: string | null;
  readonly errores: Errores;
  readonly retryAfterSegundos: number | null;

  constructor(
    status: number,
    mensaje: string,
    opciones: { titulo?: string | null; errores?: Errores; retryAfterSegundos?: number | null } = {}
  ) {
    super(mensaje);
    this.name = "ApiError";
    this.status = status;
    this.titulo = opciones.titulo ?? null;
    this.errores = opciones.errores ?? {};
    this.retryAfterSegundos = opciones.retryAfterSegundos ?? null;
  }

  /** Primer mensaje de un campo (claves camelCase del JSON). */
  errorDe(campo: string): string | undefined {
    return this.errores[campo]?.[0];
  }
}

export const MENSAJE_SIN_CONEXION =
  "No se pudo conectar con el servidor. Verifica que el backend esté disponible.";

const MENSAJES_POR_ESTADO: Record<number, string> = {
  400: "Revisa los datos ingresados.",
  401: "Tu sesión expiró. Inicia sesión de nuevo.",
  403: "Acceso denegado.",
  404: "No se encontró el recurso solicitado.",
  409: "La operación entra en conflicto con datos existentes.",
  429: "Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.",
  500: "Ocurrió un error inesperado en el servidor.",
  502: "El servicio meteorológico respondió con datos no válidos.",
  503: "El servicio no está disponible en este momento.",
  504: "El servicio meteorológico tardó demasiado en responder.",
};

function mensajePorEstado(status: number): string {
  return MENSAJES_POR_ESTADO[status] ?? "Ocurrió un error al comunicarse con el servidor.";
}

// ---------------------------------------------------------------- sesión

// El token vive en memoria (contrato-auth-perfil.md, sección 7). El contexto de
// autenticación lo establece al iniciar o restaurar la sesión y lo borra al cerrarla.
let tokenActual: string | null = null;
let manejadorSesionExpirada: () => void = () => {};
// Todas las peticiones que llevan token se cancelan juntas al cerrar la sesión.
let controlSesion = new AbortController();

export function establecerToken(token: string | null): void {
  tokenActual = token;
}

export function hayToken(): boolean {
  return tokenActual !== null;
}

/**
 * Cancela todas las peticiones en curso que llevan token (AbortError, que la interfaz ignora):
 * así ninguna respuesta privada llega después de cerrar la sesión.
 */
export function cancelarSolicitudesDeSesion(): void {
  controlSesion.abort();
  controlSesion = new AbortController();
}

/** Une la cancelación de la pantalla con la de la sesión. */
function unirSenales(senal: AbortSignal | undefined, sesion: AbortSignal): AbortSignal {
  if (!senal) return sesion;
  if (typeof AbortSignal.any === "function") return AbortSignal.any([senal, sesion]);
  const union = new AbortController();
  const abortar = () => union.abort();
  if (senal.aborted || sesion.aborted) union.abort();
  senal.addEventListener("abort", abortar, { once: true });
  sesion.addEventListener("abort", abortar, { once: true });
  return union.signal;
}

/** Qué hacer cuando una petición con token recibe 401 (sesión vencida, inválida o cuenta desactivada). */
export function alExpirarSesion(manejador: () => void): void {
  manejadorSesionExpirada = manejador;
}

// ---------------------------------------------------------------- petición

export type ValorQuery = string | number | boolean | null | undefined;

export interface OpcionesSolicitud {
  metodo?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  cuerpo?: unknown;
  query?: Record<string, ValorQuery>;
  autenticacion?: ModoAutenticacion;
  signal?: AbortSignal;
}

export function construirUrl(ruta: string, query?: Record<string, ValorQuery>): string {
  const url = `${API_URL}${ruta.startsWith("/") ? ruta : `/${ruta}`}`;
  if (!query) {
    return url;
  }

  const parametros = new URLSearchParams();
  for (const [clave, valor] of Object.entries(query)) {
    if (valor !== undefined && valor !== null && valor !== "") {
      parametros.append(clave, String(valor));
    }
  }

  const texto = parametros.toString();
  return texto ? `${url}?${texto}` : url;
}

async function leerProblema(respuesta: Response): Promise<ApiError> {
  const status = respuesta.status;
  const retry = Number(respuesta.headers.get("Retry-After"));
  const retryAfterSegundos = Number.isFinite(retry) && retry > 0 ? retry : null;

  let titulo: string | null = null;
  let detalle: string | null = null;
  let errores: Errores = {};

  const tipo = respuesta.headers.get("Content-Type") ?? "";
  if (tipo.includes("json")) {
    try {
      const datos = (await respuesta.json()) as {
        title?: unknown;
        detail?: unknown;
        errors?: unknown;
      };
      titulo = typeof datos.title === "string" ? datos.title : null;
      detalle = typeof datos.detail === "string" ? datos.detail : null;
      if (datos.errors && typeof datos.errors === "object") {
        errores = Object.fromEntries(
          Object.entries(datos.errors as Record<string, unknown>).map(([clave, valor]) => [
            clave,
            Array.isArray(valor) ? valor.map(String) : [String(valor)],
          ])
        );
      }
    } catch {
      // Cuerpo no legible: se usa el mensaje por estado.
    }
  }

  // Un 500 nunca muestra detalles internos (contrato-auth-perfil.md, sección 5).
  let mensaje: string;
  const mensajesCampos = Object.values(errores).flat();
  if (status >= 500 && status !== 502 && status !== 503 && status !== 504) {
    mensaje = mensajePorEstado(500);
  } else if (mensajesCampos.length > 0) {
    mensaje = mensajesCampos.join(" ");
  } else {
    mensaje = detalle ?? mensajePorEstado(status);
  }

  if (status === 429 && retryAfterSegundos && !detalle) {
    mensaje = `${mensajePorEstado(429)} Intenta de nuevo en ${retryAfterSegundos} s.`;
  }

  return new ApiError(status, mensaje, { titulo, errores, retryAfterSegundos });
}

/**
 * Envía una petición y devuelve el JSON tipado (undefined en 204).
 * Lanza ApiError para cualquier respuesta no exitosa o sin conexión, y
 * DOMException "AbortError" si se canceló con signal.
 */
export async function solicitar<T>(ruta: string, opciones: OpcionesSolicitud = {}): Promise<T> {
  const { metodo = "GET", cuerpo, query, autenticacion = "requerida", signal } = opciones;

  const encabezados: Record<string, string> = { Accept: "application/json" };
  if (cuerpo !== undefined) {
    encabezados["Content-Type"] = "application/json";
  }

  const token = autenticacion === "ninguna" ? null : tokenActual;
  if (token) {
    encabezados.Authorization = `Bearer ${token}`;
  } else if (autenticacion === "requerida") {
    // Sin sesión no se llama al backend: la ruta privada respondería 401.
    throw new ApiError(401, mensajePorEstado(401));
  }

  const senalSesion = token ? controlSesion.signal : null;
  let respuesta: Response;
  try {
    respuesta = await fetch(construirUrl(ruta, query), {
      method: metodo,
      headers: encabezados,
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
      signal: senalSesion ? unirSenales(signal, senalSesion) : signal,
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw error;
    }
    throw new ApiError(0, MENSAJE_SIN_CONEXION);
  }

  // La sesión se cerró mientras llegaba la respuesta: se descarta como cancelada.
  if (senalSesion?.aborted) {
    throw new DOMException("Sesión cerrada", "AbortError");
  }

  if (!respuesta.ok) {
    const problema = await leerProblema(respuesta);
    // Un 401 con token significa sesión vencida, inválida o cuenta desactivada.
    if (problema.status === 401 && token) {
      manejadorSesionExpirada();
    }
    throw problema;
  }

  if (respuesta.status === 204) {
    return undefined as T;
  }

  const texto = await respuesta.text();
  return (texto ? JSON.parse(texto) : undefined) as T;
}

/** true si el error es una cancelación (se ignora en la interfaz). */
export function esCancelacion(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

/** Mensaje apto para mostrar al usuario a partir de cualquier error. */
export function mensajeDeError(error: unknown, porDefecto = "Ocurrió un error inesperado."): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  return porDefecto;
}
