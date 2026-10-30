namespace ClimaPlus.Api.Security;

/// <summary>
/// Límite general por IP para TODA la API (sección "RateLimiting:General", Fase 8, OWASP A04). Se suma a los
/// límites específicos de autenticación, clima y dispositivos, y protege las rutas con sesión (favoritos, alertas,
/// dispositivos…) del abuso automatizado. 300 por minuto no afecta el uso normal de la aplicación.
/// </summary>
public sealed class LimiteGeneralOptions
{
    public const string Seccion = "RateLimiting:General";

    public int PermitLimit { get; set; } = 300;

    public int VentanaSegundos { get; set; } = 60;
}
