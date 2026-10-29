namespace ClimaPlus.Api.Integraciones.OpenWeather;

/// <summary>
/// Configuración de OpenWeather (sección "OpenWeather").
/// ApiKey es secreta: solo en User Secrets (OpenWeather:ApiKey) o en la variable de entorno
/// OpenWeather__ApiKey. Nunca en appsettings.json, en el código, en las pruebas ni en los logs.
/// </summary>
public sealed class OpenWeatherOptions
{
    public const string Seccion = "OpenWeather";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.openweathermap.org";

    public int TimeoutSegundos { get; set; } = 8;

    public string Unidades { get; set; } = "metric";

    public string Idioma { get; set; } = "es";
}
