namespace ClimaPlus.Api.Security;

/// <summary>
/// Configuración del JWT (sección "Jwt").
/// Emisor, Audiencia y MinutosExpiracion van en appsettings.json.
/// La Clave es secreta: User Secrets en desarrollo o la variable de entorno Jwt__Clave.
/// </summary>
public sealed class JwtOptions
{
    public const string Seccion = "Jwt";

    public string Clave { get; set; } = string.Empty;

    public string Emisor { get; set; } = string.Empty;

    public string Audiencia { get; set; } = string.Empty;

    public int MinutosExpiracion { get; set; } = 60;
}
