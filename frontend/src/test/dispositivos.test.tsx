import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import Devices from "../pages/Devices";
import DeviceDetails from "../pages/DeviceDetails";
import { conSesion, renderizar } from "./renderizar";
import { crearServidor, problema } from "./servidorFalso";
import { coban, pagina, usuario } from "./datos";
import type { Dispositivo, LecturaDispositivo } from "../types/api";

// ─── Datos de prueba ──────────────────────────────────────────────────────────

const ubicacionBase = coban;

function dispositivo(cambios: Partial<Dispositivo> = {}): Dispositivo {
  return {
    idDispositivo: 5,
    nombre: "ESP32 Patio",
    activo: true,
    enLinea: true,
    ultimaConexion: "2026-10-29T22:15:53.969139Z",
    fechaCreacion: "2026-10-29T22:15:53.672513Z",
    ubicacion: ubicacionBase,
    ultimaLectura: {
      idLectura: 3,
      fechaLectura: "2026-10-29T22:15:53.969139Z",
      temperatura: 33.40,
      humedad: 40.00,
      lluvia: false,
      luz: 90,
    },
    ...cambios,
  };
}

function lectura(cambios: Partial<LecturaDispositivo> = {}): LecturaDispositivo {
  return {
    idLectura: 1,
    fechaLectura: "2026-10-29T22:15:53.969139Z",
    temperatura: 24.5,
    humedad: 68,
    lluvia: false,
    luz: 72,
    ...cambios,
  };
}

function servidorConSesion(nuevas = 0) {
  const servidor = crearServidor();
  conSesion(servidor, usuario, nuevas);
  return servidor;
}

// ─── M11 – Mis dispositivos ───────────────────────────────────────────────────

describe("M11 – mis dispositivos", () => {
  it("muestra la lista de dispositivos desde la API", async () => {
    servidorConSesion().en("GET", "/api/dispositivos", {
      cuerpo: [dispositivo()],
    });
    renderizar(<Devices />, { ruta: "/dispositivos" });

    expect(await screen.findByRole("heading", { name: "ESP32 Patio" })).toBeInTheDocument();
    expect(screen.getByText("33.4 °C")).toBeInTheDocument();
    expect(screen.getByText("40%")).toBeInTheDocument();
    expect(screen.getByText("Activo")).toBeInTheDocument();
  });

  it("muestra el estado vacío cuando no hay dispositivos", async () => {
    servidorConSesion().en("GET", "/api/dispositivos", { cuerpo: [] });
    renderizar(<Devices />, { ruta: "/dispositivos" });

    expect(await screen.findByText("Aún no tienes dispositivos")).toBeInTheDocument();
  });

  it("muestra la API Key una sola vez y la borra al cerrar el modal", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/dispositivos", { cuerpo: [] })
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [coban] })
      .en("POST", "/api/dispositivos", {
        status: 201,
        cuerpo: {
          dispositivo: dispositivo(),
          apiKey: "cpk_CLAVE_SUPER_SECRETA_DE_PRUEBA",
          aviso: "Guárdala ahora; no podrás verla de nuevo.",
        },
      });
    renderizar(<Devices />, { ruta: "/dispositivos" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText("Aún no tienes dispositivos");

    // Abrir modal de registro
    await usuarioEvento.click(screen.getByRole("button", { name: /Registrar dispositivo/i }));

    // Completar nombre
    await usuarioEvento.type(screen.getByLabelText("Nombre del dispositivo"), "ESP32 Patio");

    // Buscar y seleccionar ubicación
    await usuarioEvento.type(
      screen.getByPlaceholderText("Buscar ciudad o municipio"),
      "Cobán"
    );
    await usuarioEvento.click(screen.getByRole("button", { name: /Buscar/i }));
    await usuarioEvento.click(
      await screen.findByRole("button", { name: /Cobán.*Seleccionar/ })
    );

    // Enviar el formulario
    await usuarioEvento.click(screen.getByRole("button", { name: /^Registrar$/i }));

    // El modal de API Key debe aparecer con la clave
    const apiKeyModal = await screen.findByRole("dialog", { name: /registrado/i });
    expect(within(apiKeyModal).getByText("cpk_CLAVE_SUPER_SECRETA_DE_PRUEBA")).toBeInTheDocument();

    // Cerrar con "Ya la guardé"
    await usuarioEvento.click(within(apiKeyModal).getByRole("button", { name: /ya la guardé/i }));

    // La clave ya no debe estar en el DOM
    expect(screen.queryByText("cpk_CLAVE_SUPER_SECRETA_DE_PRUEBA")).not.toBeInTheDocument();

    expect(servidor.a("/api/dispositivos", "POST")[0].cuerpo).toEqual({
      nombre: "ESP32 Patio",
      idUbicacion: 12,
    });
  });

  it("el 409 muestra el mensaje de error en el campo nombre", async () => {
    servidorConSesion()
      .en("GET", "/api/dispositivos", { cuerpo: [] })
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [coban] })
      .en(
        "POST",
        "/api/dispositivos",
        problema(409, "Nombre duplicado", { nombre: ["Ya tienes un dispositivo con ese nombre."] })
      );
    renderizar(<Devices />, { ruta: "/dispositivos" });
    const usuarioEvento = userEvent.setup();
    await screen.findByText("Aún no tienes dispositivos");

    await usuarioEvento.click(screen.getByRole("button", { name: /Registrar dispositivo/i }));
    await usuarioEvento.type(screen.getByLabelText("Nombre del dispositivo"), "ESP32 Patio");
    await usuarioEvento.type(
      screen.getByPlaceholderText("Buscar ciudad o municipio"),
      "Cobán"
    );
    await usuarioEvento.click(screen.getByRole("button", { name: /Buscar/i }));
    await usuarioEvento.click(await screen.findByRole("button", { name: /Cobán.*Seleccionar/ }));
    await usuarioEvento.click(screen.getByRole("button", { name: /^Registrar$/i }));

    expect(
      await screen.findByText("Ya tienes un dispositivo con ese nombre.")
    ).toBeInTheDocument();
  });

  it("desactivar pide confirmación y envía PATCH { activo: false }", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/dispositivos", { cuerpo: [dispositivo()] })
      .en("PATCH", "/api/dispositivos/5/estado", {
        cuerpo: dispositivo({ activo: false, enLinea: false }),
      });
    renderizar(<Devices />, { ruta: "/dispositivos" });
    const usuarioEvento = userEvent.setup();
    await screen.findByRole("heading", { name: "ESP32 Patio" });

    // Clic en Desactivar
    await usuarioEvento.click(
      screen.getByRole("button", { name: /Desactivar ESP32 Patio/ })
    );

    // Debe aparecer modal de confirmación
    const dialogo = await screen.findByRole("dialog", { name: /Desactivar dispositivo/ });
    expect(within(dialogo).getByText(/API Key quedará revocada/)).toBeInTheDocument();

    // Confirmar
    await usuarioEvento.click(within(dialogo).getByRole("button", { name: /Sí, desactivar/ }));

    await waitFor(() =>
      expect(servidor.a("/api/dispositivos/5/estado", "PATCH")[0].cuerpo).toEqual({ activo: false })
    );
  });
});

// ─── M12 – Detalle de dispositivo ────────────────────────────────────────────

describe("M12 – detalle de dispositivo", () => {
  it("muestra las 4 tarjetas de sensores con la última lectura", async () => {
    servidorConSesion()
      .en("GET", "/api/dispositivos/5", { cuerpo: dispositivo() })
      .en("GET", "/api/dispositivos/5/lecturas", { cuerpo: pagina([lectura()]) })
      .en("GET", "/api/dispositivos/5/alertas", { cuerpo: pagina([]) });
    renderizar(<DeviceDetails />, { ruta: "/dispositivos/5", patron: "/dispositivos/:id" });

    // Temperatura
    expect(await screen.findByText("33.4 °C")).toBeInTheDocument();
    // Humedad
    expect(screen.getByText("40%")).toBeInTheDocument();
    // Lluvia
    expect(screen.getAllByText("No").length).toBeGreaterThan(0);
    // Luz
    expect(screen.getByText("90%")).toBeInTheDocument();
  });

  it("pagina las lecturas con los botones Anterior / Siguiente", async () => {
    const servidor = servidorConSesion()
      .en("GET", "/api/dispositivos/5", { cuerpo: dispositivo() })
      .en("GET", "/api/dispositivos/5/lecturas", (p) => ({
        cuerpo: pagina([lectura()], {
          pagina: Number(p.url.searchParams.get("pagina") ?? 1),
          totalElementos: 25,
          totalPaginas: 2,
        }),
      }))
      .en("GET", "/api/dispositivos/5/alertas", { cuerpo: pagina([]) });
    renderizar(<DeviceDetails />, { ruta: "/dispositivos/5", patron: "/dispositivos/:id" });
    const usuarioEvento = userEvent.setup();

    await screen.findByText("33.4 °C");

    await usuarioEvento.click(await screen.findByRole("button", { name: "Siguiente" }));

    await waitFor(() =>
      expect(
        servidor
          .a("/api/dispositivos/5/lecturas")
          .some((p) => p.url.searchParams.get("pagina") === "2")
      ).toBe(true)
    );
  });

  it("muestra '—' en todos los sensores cuando ultimaLectura es null", async () => {
    servidorConSesion()
      .en("GET", "/api/dispositivos/5", {
        cuerpo: dispositivo({ ultimaLectura: null, ultimaConexion: null }),
      })
      .en("GET", "/api/dispositivos/5/lecturas", { cuerpo: pagina([]) })
      .en("GET", "/api/dispositivos/5/alertas", { cuerpo: pagina([]) });
    renderizar(<DeviceDetails />, { ruta: "/dispositivos/5", patron: "/dispositivos/:id" });

    await screen.findByText("ESP32 Patio");
    const dashes = screen.getAllByText("—");
    // Al menos 4 (temperatura, humedad, lluvia, luz)
    expect(dashes.length).toBeGreaterThanOrEqual(4);
  });

  it("muestra mensaje de 404 cuando el dispositivo no existe", async () => {
    servidorConSesion().en(
      "GET",
      "/api/dispositivos/999",
      problema(404, "El dispositivo no existe.")
    );
    renderizar(<DeviceDetails />, { ruta: "/dispositivos/999", patron: "/dispositivos/:id" });

    expect(await screen.findByText("El dispositivo no existe")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Volver a mis dispositivos/ })).toBeInTheDocument();
  });
});
