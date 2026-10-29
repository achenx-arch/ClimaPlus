using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.Services.Clima;
using ClimaPlus.Api.Services.Clima.Modelos;

namespace ClimaPlus.Api.DTOs.Clima;

// Respuestas de GET /api/clima/actual y GET /api/clima/pronostico (D-20, Fase 2E).
// Siempre se construyen con la consulta ya guardada en consulta_clima (guía, sección 4).

/// <summary>Consulta guardada para esta petición. DesdeCache = los datos se reutilizaron sin llamar a OpenWeather (D-23).</summary>
public sealed record ConsultaRegistradaResponse(long IdConsulta, DateTime FechaConsultaUtc, bool DesdeCache);

/// <summary>
/// Respuesta de GET /api/clima/actual?idUbicacion= (M03/M04). AlertasGeneradas (Fase 2H) lista las alertas
/// creadas por esta consulta; siempre está presente y es [] sin sesión o sin alertas nuevas.
/// </summary>
public sealed record ClimaActualResponse(
    UbicacionResponse Ubicacion,
    ConsultaRegistradaResponse Consulta,
    decimal Temperatura,
    decimal SensacionTermica,
    short Humedad,
    decimal VelocidadViento,
    short CodigoCondicion,
    string Descripcion,
    DetalleClimaActual Detalle,
    IReadOnlyList<AlertaGeneradaResponse> AlertasGeneradas)
{
    public static ClimaActualResponse Desde(UbicacionResponse ubicacion, ClimaActualGuardado guardado, IReadOnlyList<AlertaGeneradaResponse> alertasGeneradas) =>
        new(
            ubicacion,
            new ConsultaRegistradaResponse(guardado.IdConsulta, guardado.FechaConsultaUtc, guardado.DesdeCache),
            guardado.Clima.Temperatura,
            guardado.Clima.SensacionTermica,
            guardado.Clima.Humedad,
            guardado.Clima.VelocidadViento,
            guardado.Clima.CodigoCondicion,
            guardado.Clima.Descripcion,
            guardado.Clima.Detalle,
            alertasGeneradas);
}

/// <summary>Respuesta de GET /api/clima/pronostico?idUbicacion= (M05).</summary>
public sealed record PronosticoResponse(
    UbicacionResponse Ubicacion,
    ConsultaRegistradaResponse Consulta,
    int ZonaHorariaSegundos,
    IReadOnlyList<DiaPronostico> Dias)
{
    public static PronosticoResponse Desde(UbicacionResponse ubicacion, PronosticoGuardado guardado) =>
        new(
            ubicacion,
            new ConsultaRegistradaResponse(guardado.IdConsulta, guardado.FechaConsultaUtc, guardado.DesdeCache),
            guardado.Pronostico.ZonaHorariaSegundos,
            guardado.Pronostico.Dias);
}
