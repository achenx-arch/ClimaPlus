using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.Services.Clima.Modelos;

namespace ClimaPlus.Api.DTOs.Historial;

/// <summary>
/// Fila de la tabla de M08. En PRONOSTICO las mediciones son null (la pantalla muestra "—").
/// Para "Consultar de nuevo": tipo + ubicacion.idUbicacion → GET /api/clima/actual o /pronostico.
/// </summary>
public sealed record ConsultaHistorialResponse(
    long IdConsulta,
    string Tipo,
    DateTime FechaConsultaUtc,
    UbicacionResponse Ubicacion,
    decimal? Temperatura,
    short? Humedad,
    decimal? VelocidadViento,
    short? CodigoCondicion,
    string? Descripcion);

/// <summary>Detalle de una consulta propia: Actual o Pronostico según el tipo (el otro es null).</summary>
public sealed record ConsultaHistorialDetalleResponse(
    long IdConsulta,
    string Tipo,
    DateTime FechaConsultaUtc,
    UbicacionResponse Ubicacion,
    ClimaActualHistorialResponse? Actual,
    PronosticoHistorialResponse? Pronostico);

/// <summary>Clima actual guardado. Detalle es null si la consulta no tiene datos extra.</summary>
public sealed record ClimaActualHistorialResponse(
    decimal Temperatura,
    decimal SensacionTermica,
    short Humedad,
    decimal VelocidadViento,
    short CodigoCondicion,
    string Descripcion,
    DetalleClimaActual? Detalle);

/// <summary>Pronóstico guardado (mismo formato que GET /api/clima/pronostico). Dias vacío si no se pudo leer.</summary>
public sealed record PronosticoHistorialResponse(int? ZonaHorariaSegundos, IReadOnlyList<DiaPronostico> Dias);
