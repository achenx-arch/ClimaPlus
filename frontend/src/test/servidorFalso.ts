import { vi } from "vitest";

// Sustituto de fetch para las pruebas: cada ruta responde lo que la prueba define y
// todas las peticiones quedan registradas (método, URL, encabezados y cuerpo).

export interface PeticionRegistrada {
  metodo: string;
  url: URL;
  encabezados: Record<string, string>;
  cuerpo: unknown;
}

export interface RespuestaFalsa {
  status?: number;
  cuerpo?: unknown;
  encabezados?: Record<string, string>;
}

type Manejador = (peticion: PeticionRegistrada) => RespuestaFalsa | Promise<RespuestaFalsa>;

interface Ruta {
  metodo: string;
  ruta: string | RegExp;
  manejador: Manejador;
}

export function crearServidor() {
  const rutas: Ruta[] = [];
  const peticiones: PeticionRegistrada[] = [];

  const fetchFalso = vi.fn(async (entrada: RequestInfo | URL, init: RequestInit = {}) => {
    const url = new URL(typeof entrada === "string" ? entrada : entrada.toString());
    const metodo = (init.method ?? "GET").toUpperCase();
    const encabezados = Object.fromEntries(
      Object.entries((init.headers ?? {}) as Record<string, string>).map(([clave, valor]) => [clave.toLowerCase(), valor])
    );
    const peticion: PeticionRegistrada = {
      metodo,
      url,
      encabezados,
      cuerpo: typeof init.body === "string" ? JSON.parse(init.body) : undefined,
    };
    peticiones.push(peticion);

    if (init.signal?.aborted) {
      throw new DOMException("Cancelada", "AbortError");
    }

    const ruta = [...rutas]
      .reverse()
      .find(
        (r) =>
          r.metodo === metodo &&
          (typeof r.ruta === "string" ? url.pathname === r.ruta : r.ruta.test(url.pathname))
      );
    if (!ruta) {
      return new Response(JSON.stringify({ title: "No encontrado", status: 404, detail: `Sin ruta falsa: ${metodo} ${url.pathname}` }), {
        status: 404,
        headers: { "Content-Type": "application/problem+json" },
      });
    }

    const { status = 200, cuerpo, encabezados: extra = {} } = await ruta.manejador(peticion);
    const esProblema = status >= 400;
    return new Response(cuerpo === undefined || status === 204 ? null : JSON.stringify(cuerpo), {
      status,
      headers: {
        "Content-Type": esProblema ? "application/problem+json" : "application/json; charset=utf-8",
        ...extra,
      },
    });
  });

  vi.stubGlobal("fetch", fetchFalso);

  const servidor = {
    peticiones,
    fetch: fetchFalso,
    en(metodo: string, ruta: string | RegExp, respuesta: RespuestaFalsa | Manejador) {
      rutas.push({ metodo, ruta, manejador: typeof respuesta === "function" ? respuesta : () => respuesta });
      return servidor;
    },
    /** Peticiones registradas a una ruta (y método opcional). */
    a(ruta: string, metodo?: string) {
      return peticiones.filter((p) => p.url.pathname === ruta && (!metodo || p.metodo === metodo));
    },
  };
  return servidor;
}

export function problema(status: number, detail: string, errors?: Record<string, string[]>, encabezados?: Record<string, string>): RespuestaFalsa {
  return {
    status,
    cuerpo: { title: "Error", status, detail, ...(errors ? { errors } : {}) },
    encabezados,
  };
}
