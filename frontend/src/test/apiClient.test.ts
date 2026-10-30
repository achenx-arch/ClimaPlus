import { describe, expect, it, vi } from "vitest";
import {
  API_URL,
  ApiError,
  MENSAJE_SIN_CONEXION,
  alExpirarSesion,
  cancelarSolicitudesDeSesion,
  construirUrl,
  establecerToken,
  solicitar,
} from "../services/apiClient";
import { crearServidor, problema } from "./servidorFalso";

describe("cliente HTTP", () => {
  it("usa VITE_API_URL como base (http://localhost:5208) y omite parámetros vacíos", () => {
    expect(API_URL).toBe("http://localhost:5208");
    expect(construirUrl("/api/historial", { tipo: "ACTUAL", idUbicacion: undefined, desde: "", pagina: 2 })).toBe(
      "http://localhost:5208/api/historial?tipo=ACTUAL&pagina=2"
    );
  });

  it("codifica fechas con zona horaria en la query", () => {
    expect(construirUrl("/api/historial", { desde: "2026-10-01T00:00:00+02:00" })).toContain(
      "desde=2026-10-01T00%3A00%3A00%2B02%3A00"
    );
  });

  it("envía Authorization: Bearer solo cuando hay token y la ruta no es pública", async () => {
    const servidor = crearServidor()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("POST", "/api/auth/login", { cuerpo: {} })
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [] });

    establecerToken("abc.def.ghi");
    await solicitar("/api/favoritos");
    await solicitar("/api/auth/login", { metodo: "POST", cuerpo: {}, autenticacion: "ninguna" });
    await solicitar("/api/clima/ubicaciones", { autenticacion: "opcional" });
    establecerToken(null);
    await solicitar("/api/clima/ubicaciones", { autenticacion: "opcional" });

    expect(servidor.peticiones.map((p) => p.encabezados.authorization)).toEqual([
      "Bearer abc.def.ghi",
      undefined,
      "Bearer abc.def.ghi",
      undefined,
    ]);
  });

  it("sin token, una ruta privada falla con 401 sin llamar al backend", async () => {
    const servidor = crearServidor();

    await expect(solicitar("/api/favoritos")).rejects.toMatchObject({ status: 401 });
    expect(servidor.peticiones).toHaveLength(0);
  });

  it("interpreta un 400 application/problem+json con errores por campo (camelCase)", async () => {
    crearServidor().en(
      "POST",
      "/api/auth/register",
      problema(400, "Datos inválidos", { correo: ["Ingresa un correo válido."], password: ["La contraseña debe tener entre 8 y 128 caracteres."] })
    );

    const error = (await solicitar("/api/auth/register", { metodo: "POST", cuerpo: {}, autenticacion: "ninguna" }).catch((e) => e)) as ApiError;

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.errorDe("correo")).toBe("Ingresa un correo válido.");
    expect(error.message).toContain("Ingresa un correo válido.");
  });

  it.each([
    [403, "Acceso denegado"],
    [404, "La consulta no existe."],
    [409, "Ya tienes una regla de ese tipo para esta ubicación."],
  ])("muestra el detail del problema en %i", async (status, detail) => {
    establecerToken("t");
    crearServidor().en("GET", "/api/x", problema(status, detail));

    await expect(solicitar("/api/x")).rejects.toMatchObject({ status, message: detail });
  });

  it("429 conserva Retry-After", async () => {
    crearServidor().en("POST", "/api/auth/login", {
      status: 429,
      cuerpo: { title: "Demasiadas solicitudes", status: 429 },
      encabezados: { "Retry-After": "42" },
    });

    const error = (await solicitar("/api/auth/login", { metodo: "POST", cuerpo: {}, autenticacion: "ninguna" }).catch((e) => e)) as ApiError;

    expect(error.status).toBe(429);
    expect(error.retryAfterSegundos).toBe(42);
    expect(error.message).toContain("42 s");
  });

  it("500 nunca muestra detalles internos", async () => {
    establecerToken("t");
    crearServidor().en("GET", "/api/x", problema(500, "NullReferenceException en ClimaService"));

    const error = (await solicitar("/api/x").catch((e) => e)) as ApiError;

    expect(error.status).toBe(500);
    expect(error.message).not.toContain("NullReference");
  });

  it("502/503/504 muestran el detail del backend (errores del proveedor)", async () => {
    crearServidor().en("GET", "/api/clima/actual", problema(503, "El servicio meteorológico no está disponible."));

    await expect(solicitar("/api/clima/actual", { autenticacion: "opcional" })).rejects.toMatchObject({
      status: 503,
      message: "El servicio meteorológico no está disponible.",
    });
  });

  it("un 401 con token avisa a la sesión; sin token (credenciales inválidas) no", async () => {
    const manejador = vi.fn();
    alExpirarSesion(manejador);
    crearServidor()
      .en("GET", "/api/favoritos", problema(401, "Falta el token de acceso, no es válido o expiró."))
      .en("POST", "/api/auth/login", problema(401, "Correo o contraseña incorrectos."));

    await expect(solicitar("/api/auth/login", { metodo: "POST", cuerpo: {}, autenticacion: "ninguna" })).rejects.toMatchObject({ status: 401 });
    expect(manejador).not.toHaveBeenCalled();

    establecerToken("vencido");
    await expect(solicitar("/api/favoritos")).rejects.toMatchObject({ status: 401 });
    expect(manejador).toHaveBeenCalledTimes(1);
    alExpirarSesion(() => {});
  });

  it("204 devuelve undefined", async () => {
    establecerToken("t");
    crearServidor().en("DELETE", "/api/favoritos/7", { status: 204 });

    await expect(solicitar("/api/favoritos/7", { metodo: "DELETE" })).resolves.toBeUndefined();
  });

  it("sin conexión devuelve status 0 con un mensaje claro", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    await expect(solicitar("/api/clima/ubicaciones", { autenticacion: "opcional" })).rejects.toMatchObject({
      status: 0,
      message: MENSAJE_SIN_CONEXION,
    });
  });

  it("propaga la cancelación como AbortError", async () => {
    crearServidor().en("GET", "/api/clima/ubicaciones", { cuerpo: [] });
    const control = new AbortController();
    control.abort();

    await expect(solicitar("/api/clima/ubicaciones", { autenticacion: "opcional", signal: control.signal })).rejects.toMatchObject({
      name: "AbortError",
    });
  });

  it("al cerrar sesión cancela las peticiones privadas pendientes sin tratarlas como 401", async () => {
    const manejador = vi.fn();
    alExpirarSesion(manejador);
    vi.stubGlobal(
      "fetch",
      vi.fn(
        (_url: string, init: RequestInit) =>
          new Promise((_resolver, rechazar) => {
            init.signal?.addEventListener("abort", () => rechazar(new DOMException("Cancelada", "AbortError")));
          })
      )
    );
    establecerToken("t");

    const pendiente = solicitar("/api/historial");
    cancelarSolicitudesDeSesion();

    await expect(pendiente).rejects.toMatchObject({ name: "AbortError" });
    expect(manejador).not.toHaveBeenCalled();
    alExpirarSesion(() => {});
  });

  it("descarta una respuesta privada que llega después del cierre de sesión", async () => {
    let responder: (r: Response) => void = () => {};
    vi.stubGlobal("fetch", vi.fn(() => new Promise<Response>((resolver) => (responder = resolver))));
    establecerToken("t");

    const pendiente = solicitar("/api/favoritos");
    cancelarSolicitudesDeSesion();
    responder(new Response("[]", { status: 200, headers: { "Content-Type": "application/json" } }));

    await expect(pendiente).rejects.toMatchObject({ name: "AbortError" });
  });

  it("las peticiones públicas sin token no se cancelan al cerrar sesión", async () => {
    crearServidor().en("GET", "/api/clima/ubicaciones", { cuerpo: [] });

    const publica = solicitar("/api/clima/ubicaciones", { autenticacion: "opcional" });
    cancelarSolicitudesDeSesion();

    await expect(publica).resolves.toEqual([]);
  });

  it("nunca escribe el token en la consola", async () => {
    const consola = [vi.spyOn(console, "log"), vi.spyOn(console, "error"), vi.spyOn(console, "warn"), vi.spyOn(console, "info")];
    establecerToken("token-secreto-123");
    crearServidor()
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/historial", problema(401, "Expiró"));

    await solicitar("/api/favoritos");
    await solicitar("/api/historial").catch(() => {});

    for (const espia of consola) {
      for (const llamada of espia.mock.calls) {
        expect(JSON.stringify(llamada)).not.toContain("token-secreto-123");
      }
    }
  });
});
