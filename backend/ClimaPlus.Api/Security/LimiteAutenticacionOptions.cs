namespace ClimaPlus.Api.Security;

/// <summary>
/// Límite de peticiones para registro, inicio de sesión y cambio de contraseña
/// (sección "RateLimiting:Autenticacion"). Se aplica por IP y por ruta.
/// </summary>
public sealed class LimiteAutenticacionOptions
{
    public const string Seccion = "RateLimiting:Autenticacion";

    public int PermitLimit { get; set; } = 5;

    public int VentanaSegundos { get; set; } = 60;
}
