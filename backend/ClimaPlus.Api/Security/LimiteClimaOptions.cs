namespace ClimaPlus.Api.Security;

/// <summary>
/// Límite de peticiones para /api/clima/* (D-23, sección "RateLimiting:Clima").
/// Se aplica por IP a todas las rutas de clima juntas, porque cada una puede llamar a OpenWeather.
/// </summary>
public sealed class LimiteClimaOptions
{
    public const string Seccion = "RateLimiting:Clima";

    public int PermitLimit { get; set; } = 30;

    public int VentanaSegundos { get; set; } = 60;
}
