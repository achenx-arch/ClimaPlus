using System.Text.Json.Serialization;

namespace ClimaPlus.Api.Integraciones.OpenWeather.Modelos;

// Modelos de la respuesta de OpenWeather (plan gratuito). Todos los campos son opcionales
// aquí; la validación de lo obligatorio se hace en ClimaTransformador.

/// <summary>GET /data/2.5/weather (clima actual).</summary>
public sealed class ClimaActualProveedor
{
    [JsonPropertyName("weather")]
    public List<CondicionProveedor>? Condiciones { get; set; }

    [JsonPropertyName("main")]
    public PrincipalProveedor? Principal { get; set; }

    [JsonPropertyName("wind")]
    public VientoProveedor? Viento { get; set; }

    /// <summary>Hora de la observación, Unix UTC.</summary>
    [JsonPropertyName("dt")]
    public long? Fecha { get; set; }

    [JsonPropertyName("sys")]
    public SistemaProveedor? Sistema { get; set; }

    /// <summary>Desfase de la ubicación respecto de UTC, en segundos.</summary>
    [JsonPropertyName("timezone")]
    public int? ZonaHoraria { get; set; }
}

/// <summary>GET /data/2.5/forecast (5 días cada 3 horas).</summary>
public sealed class PronosticoProveedor
{
    [JsonPropertyName("list")]
    public List<BloqueProveedor>? Bloques { get; set; }

    [JsonPropertyName("city")]
    public CiudadProveedor? Ciudad { get; set; }
}

public sealed class BloqueProveedor
{
    /// <summary>Hora del pronóstico, Unix UTC.</summary>
    [JsonPropertyName("dt")]
    public long? Fecha { get; set; }

    [JsonPropertyName("main")]
    public PrincipalProveedor? Principal { get; set; }

    [JsonPropertyName("weather")]
    public List<CondicionProveedor>? Condiciones { get; set; }

    [JsonPropertyName("wind")]
    public VientoProveedor? Viento { get; set; }

    /// <summary>Probabilidad de precipitación de 0 a 1.</summary>
    [JsonPropertyName("pop")]
    public decimal? ProbabilidadPrecipitacion { get; set; }

    [JsonPropertyName("sys")]
    public ParteDelDiaProveedor? ParteDelDia { get; set; }
}

public sealed class CondicionProveedor
{
    [JsonPropertyName("id")]
    public int? Codigo { get; set; }

    [JsonPropertyName("description")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("icon")]
    public string? Icono { get; set; }
}

public sealed class PrincipalProveedor
{
    [JsonPropertyName("temp")]
    public decimal? Temperatura { get; set; }

    [JsonPropertyName("feels_like")]
    public decimal? SensacionTermica { get; set; }

    [JsonPropertyName("temp_min")]
    public decimal? TemperaturaMin { get; set; }

    [JsonPropertyName("temp_max")]
    public decimal? TemperaturaMax { get; set; }

    [JsonPropertyName("pressure")]
    public decimal? Presion { get; set; }

    [JsonPropertyName("humidity")]
    public decimal? Humedad { get; set; }
}

public sealed class VientoProveedor
{
    [JsonPropertyName("speed")]
    public decimal? Velocidad { get; set; }

    [JsonPropertyName("deg")]
    public decimal? Direccion { get; set; }
}

public sealed class SistemaProveedor
{
    [JsonPropertyName("sunrise")]
    public long? Amanecer { get; set; }

    [JsonPropertyName("sunset")]
    public long? Atardecer { get; set; }
}

public sealed class ParteDelDiaProveedor
{
    /// <summary>"d" = día, "n" = noche.</summary>
    [JsonPropertyName("pod")]
    public string? Parte { get; set; }
}

public sealed class CiudadProveedor
{
    [JsonPropertyName("timezone")]
    public int? ZonaHoraria { get; set; }
}
