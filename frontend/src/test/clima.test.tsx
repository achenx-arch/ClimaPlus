import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import CurrentWeather from "../pages/CurrentWeather";
import Forecast from "../pages/Forecast";
import Home from "../pages/Home";
import { climaActual, coban, favorito, guatemala, pronostico, usuario } from "./datos";
import { TOKEN_PRUEBA, conSesion, renderizar } from "./renderizar";
import { crearServidor, problema } from "./servidorFalso";

const alertaGenerada = {
  idAlerta: 91,
  tipo: "TEMPERATURA_ALTA" as const,
  mensaje: "Cobán: 33.4 C supera tu umbral de 32 C",
  fechaGeneracion: "2026-10-27T18:21:04.512345Z",
};

describe("M04 – clima actual", () => {
  it("M01 envía la búsqueda a M04, que con un solo resultado muestra el clima real", async () => {
    const servidor = crearServidor()
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [coban] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizar(<Home />, { ruta: "/", patron: "/", rutasExtra: <></> });
    const usuarioEvento = userEvent.setup();

    await usuarioEvento.type(screen.getByLabelText("Buscar ciudad"), "Cobán");
    await usuarioEvento.click(screen.getByRole("button", { name: "Consultar clima" }));

    expect(screen.getByTestId("ruta")).toHaveTextContent("/clima?q=Cob%C3%A1n");
    expect(servidor.peticiones).toHaveLength(0);
  });

  it("busca, abre el único resultado y muestra los campos reales del contrato", async () => {
    const servidor = crearServidor()
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [coban] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizar(<CurrentWeather />, { ruta: "/clima?q=Cob%C3%A1n" });

    expect(await screen.findByRole("heading", { name: "Cobán" })).toBeInTheDocument();
    expect(screen.getByTestId("ruta")).toHaveTextContent("/clima?idUbicacion=12");
    expect(servidor.a("/api/clima/ubicaciones")[0].url.searchParams.get("q")).toBe("Cobán");
    expect(servidor.a("/api/clima/actual")[0].url.searchParams.get("idUbicacion")).toBe("12");

    expect(screen.getAllByText("22°")).toHaveLength(2);   // temperatura 22.46 y sensación 22.1
    expect(screen.getByText("Nubes dispersas")).toBeInTheDocument();
    expect(screen.getByText("70%")).toBeInTheDocument();
    expect(screen.getByText("3.1 m/s")).toBeInTheDocument();
    expect(screen.getByText("Noreste")).toBeInTheDocument();
    expect(screen.getByText("1,018 hPa")).toBeInTheDocument();
    expect(screen.queryByText(/UV/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Ver pronóstico/ })).toHaveAttribute("href", "/pronostico?idUbicacion=12");
  });

  it("sin sesión consulta sin token y no muestra alertas", async () => {
    const servidor = crearServidor().en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    await screen.findByRole("heading", { name: "Cobán" });

    expect(servidor.a("/api/clima/actual")[0].encabezados.authorization).toBeUndefined();
    expect(screen.getByRole("button", { name: /Inicia sesión para guardar favoritos/ })).toBeInTheDocument();
    expect(screen.queryByText(/Nueva alerta/)).not.toBeInTheDocument();
  });

  it("con varios resultados deja elegir la ubicación", async () => {
    crearServidor()
      .en("GET", "/api/clima/ubicaciones", { cuerpo: [guatemala, coban] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizar(<CurrentWeather />, { ruta: "/clima?q=Gua" });

    const lista = await screen.findByLabelText("Resultados de la búsqueda");
    await userEvent.setup().click(within(lista).getByText("Cobán"));

    expect(await screen.findByRole("heading", { name: "Cobán" })).toBeInTheDocument();
    expect(screen.getByTestId("ruta")).toHaveTextContent("/clima?idUbicacion=12");
  });

  it("búsqueda sin resultados muestra un mensaje", async () => {
    crearServidor().en("GET", "/api/clima/ubicaciones", { cuerpo: [] });
    renderizar(<CurrentWeather />, { ruta: "/clima?q=Xyzzy" });

    expect(await screen.findByText(/No encontramos esa ubicación/)).toBeInTheDocument();
  });

  it("búsqueda inválida se valida antes de llamar al backend", async () => {
    const servidor = crearServidor();
    renderizar(<CurrentWeather />, { ruta: "/clima?q=a" });

    expect(await screen.findByText(/entre 2 y 100 caracteres/)).toBeInTheDocument();
    expect(servidor.peticiones).toHaveLength(0);
  });

  it("ubicación inexistente (404) muestra el mensaje", async () => {
    crearServidor().en("GET", "/api/clima/actual", problema(404, "La ubicación no existe. Búscala de nuevo."));
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=999" });

    expect(await screen.findByText("La ubicación no existe. Búscala de nuevo.")).toBeInTheDocument();
  });

  it("proveedor caído (503) muestra el detail del backend", async () => {
    crearServidor().en("GET", "/api/clima/actual", problema(503, "El servicio meteorológico no está disponible."));
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    expect(await screen.findByText("El servicio meteorológico no está disponible.")).toBeInTheDocument();
  });

  it("con sesión envía el token, muestra un toast por alerta generada una sola vez y actualiza la campana", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario, 0);
    servidor
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual({ alertasGeneradas: [alertaGenerada] }) });
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    expect(await screen.findByText("Nueva alerta: Cobán: 33.4 C supera tu umbral de 32 C")).toBeInTheDocument();
    expect(servidor.a("/api/clima/actual")[0].encabezados.authorization).toBe(`Bearer ${TOKEN_PRUEBA}`);
    // StrictMode y los renderizados no duplican ni la consulta ni el toast.
    expect(servidor.a("/api/clima/actual")).toHaveLength(1);
    expect(screen.getAllByText(/Nueva alerta:/)).toHaveLength(1);
    await waitFor(() => expect(servidor.a("/api/alertas/contador").length).toBeGreaterThanOrEqual(2));
  });

  it("alertasGeneradas vacío no muestra notificaciones", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario, 0);
    servidor.en("GET", "/api/favoritos", { cuerpo: [] }).en("GET", "/api/clima/actual", { cuerpo: climaActual() });
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    await screen.findByRole("heading", { name: "Cobán" });

    expect(screen.queryByText(/Nueva alerta/)).not.toBeInTheDocument();
  });

  it("guarda la ubicación en favoritos con POST /api/favoritos", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    servidor
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual() })
      .en("POST", "/api/favoritos", { status: 201, cuerpo: favorito() });
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    await userEvent.setup().click(await screen.findByRole("button", { name: /Guardar en favoritos/ }));

    expect(await screen.findByText("Ubicación agregada a favoritos")).toBeInTheDocument();
    expect(servidor.a("/api/favoritos", "POST")[0].cuerpo).toEqual({ idUbicacion: 12 });
    expect(await screen.findByRole("button", { name: /En tus favoritos/ })).toBeDisabled();
  });

  it("un favorito duplicado (409) se informa sin error", async () => {
    const servidor = crearServidor();
    conSesion(servidor, usuario);
    servidor
      .en("GET", "/api/favoritos", { cuerpo: [] })
      .en("GET", "/api/clima/actual", { cuerpo: climaActual() })
      .en("POST", "/api/favoritos", problema(409, "Esta ubicación ya está en tus favoritos."));
    renderizar(<CurrentWeather />, { ruta: "/clima?idUbicacion=12" });

    await userEvent.setup().click(await screen.findByRole("button", { name: /Guardar en favoritos/ }));

    expect(await screen.findByText("Esta ubicación ya está en tus favoritos.")).toBeInTheDocument();
  });
});

describe("M05 – pronóstico", () => {
  it("muestra los días del contrato y los bloques del día seleccionado", async () => {
    const servidor = crearServidor().en("GET", "/api/clima/pronostico", { cuerpo: pronostico });
    renderizar(<Forecast />, { ruta: "/pronostico?idUbicacion=12" });

    expect((await screen.findAllByText("Lluvia ligera")).length).toBeGreaterThan(0);
    expect(servidor.a("/api/clima/pronostico")[0].url.searchParams.get("idUbicacion")).toBe("12");
    expect(screen.getByText("Cielo claro")).toBeInTheDocument();
    expect(screen.getAllByText("80%").length).toBeGreaterThan(0);
    expect(screen.getByText("Día parcial")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: /Lleva paraguas/ })).toBeInTheDocument();

    // Bloques del primer día: hora local (UTC-6).
    const bloques = screen.getByRole("region", { name: "Bloques de 3 horas" });
    expect(within(bloques).getByText("12:00 a. m.")).toBeInTheDocument();
    expect(within(bloques).getByText("95%")).toBeInTheDocument();

    await userEvent.setup().click(screen.getAllByRole("button", { pressed: false })[0]);
    expect(within(screen.getByRole("region", { name: "Bloques de 3 horas" })).getByText("60%")).toBeInTheDocument();
  });

  it("nunca muestra alertas (el pronóstico no las genera)", async () => {
    crearServidor().en("GET", "/api/clima/pronostico", { cuerpo: pronostico });
    renderizar(<Forecast />, { ruta: "/pronostico?idUbicacion=12" });

    await screen.findAllByText("Lluvia ligera");

    expect(screen.queryByText(/Nueva alerta/)).not.toBeInTheDocument();
  });

  it("error del proveedor (504) muestra el detail", async () => {
    crearServidor().en("GET", "/api/clima/pronostico", problema(504, "El servicio meteorológico tardó demasiado en responder."));
    renderizar(<Forecast />, { ruta: "/pronostico?idUbicacion=12" });

    expect(await screen.findByText("El servicio meteorológico tardó demasiado en responder.")).toBeInTheDocument();
  });
});
