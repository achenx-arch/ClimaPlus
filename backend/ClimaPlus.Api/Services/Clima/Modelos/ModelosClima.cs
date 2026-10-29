using System.Text.Json;

namespace ClimaPlus.Api.Services.Clima.Modelos;

// Modelos normalizados del clima: ya validados contra los CHECK de consulta_clima y
// listos para guardarse (columnas principales + detalle JSONB, formato de la sección 6
// de docs/enmiendas/E-02-E-03-especificacion-tecnica.md). Independientes de OpenWeather.

/// <summary>Clima actual normalizado. Las propiedades principales corresponden a columnas de consulta_clima.</summary>
public sealed class ClimaActualNormalizado
{
    public required decimal Temperatura { get; init; }

    public required decimal SensacionTermica { get; init; }

    public required short Humedad { get; init; }

    public required decimal VelocidadViento { get; init; }

    public required short CodigoCondicion { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Datos extra de M04 (columna detalle, E-03).</summary>
    public required DetalleClimaActual Detalle { get; init; }
}

/// <summary>detalle de una consulta ACTUAL (versión 1).</summary>
public sealed class DetalleClimaActual
{
    public int Version => 1;

    public required DateTime ObservadoEnUtc { get; init; }

    public required int ZonaHorariaSegundos { get; init; }

    public required bool EsDeDia { get; init; }

    public decimal? TemperaturaMin { get; init; }

    public decimal? TemperaturaMax { get; init; }

    public int? PresionHpa { get; init; }

    public int? DireccionVientoGrados { get; init; }

    public DateTime? AmanecerUtc { get; init; }

    public DateTime? AtardecerUtc { get; init; }
}

/// <summary>detalle de una consulta PRONOSTICO (versión 1). Las columnas de medición quedan NULL.</summary>
public sealed class DetallePronostico
{
    public int Version => 1;

    public required int ZonaHorariaSegundos { get; init; }

    public required IReadOnlyList<DiaPronostico> Dias { get; init; }
}

/// <summary>Un día del pronóstico, agrupado por la fecha local de la ubicación (D-25).</summary>
public sealed class DiaPronostico
{
    public required DateOnly Fecha { get; init; }

    /// <summary>true si el día tiene los 8 bloques de 3 horas.</summary>
    public required bool Completo { get; init; }

    public required decimal TemperaturaMax { get; init; }

    public required decimal TemperaturaMin { get; init; }

    /// <summary>Condición del bloque más cercano al mediodía local.</summary>
    public required short CodigoCondicion { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Probabilidad de lluvia máxima del día, en % (0–100).</summary>
    public required int ProbabilidadLluvia { get; init; }

    public required IReadOnlyList<BloquePronostico> Bloques { get; init; }
}

/// <summary>Bloque de 3 horas del pronóstico.</summary>
public sealed class BloquePronostico
{
    public required DateTime FechaHoraUtc { get; init; }

    public required decimal Temperatura { get; init; }

    public required short Humedad { get; init; }

    public required decimal VelocidadViento { get; init; }

    public required short CodigoCondicion { get; init; }

    public required string Descripcion { get; init; }

    /// <summary>Probabilidad de lluvia en % (0–100).</summary>
    public required int ProbabilidadLluvia { get; init; }

    public required bool EsDeDia { get; init; }
}

/// <summary>Serialización del contenido de la columna detalle (camelCase, fechas ISO 8601 en UTC).</summary>
public static class DetalleSerializador
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Serializar(DetalleClimaActual detalle) => JsonSerializer.Serialize(detalle, Opciones);

    public static string Serializar(DetallePronostico detalle) => JsonSerializer.Serialize(detalle, Opciones);
}
