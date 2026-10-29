using System.Text.Json.Serialization;

namespace ClimaPlus.Api.Integraciones.OpenWeather.Modelos;

/// <summary>Elemento de la respuesta de Geocoding (GET /geo/1.0/direct). Solo los campos usados.</summary>
public sealed class GeocodingUbicacion
{
    [JsonPropertyName("name")]
    public string? Nombre { get; set; }

    [JsonPropertyName("local_names")]
    public Dictionary<string, string>? NombresLocales { get; set; }

    [JsonPropertyName("lat")]
    public decimal? Latitud { get; set; }

    [JsonPropertyName("lon")]
    public decimal? Longitud { get; set; }

    [JsonPropertyName("country")]
    public string? Pais { get; set; }

    [JsonPropertyName("state")]
    public string? Estado { get; set; }
}
