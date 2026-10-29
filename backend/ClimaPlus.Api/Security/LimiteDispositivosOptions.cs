namespace ClimaPlus.Api.Security;

/// <summary>
/// Límite de POST /api/lecturas (sección "RateLimiting:Dispositivos"). Se aplica por API Key (o por IP si no hay
/// una clave con formato válido). El firmware envía una lectura por minuto: 30 por minuto deja margen para pruebas
/// manuales sin permitir que un dispositivo comprometido llene la tabla lectura_sensor.
/// </summary>
public sealed class LimiteDispositivosOptions
{
    public const string Seccion = "RateLimiting:Dispositivos";

    public int PermitLimit { get; set; } = 30;

    public int VentanaSegundos { get; set; } = 60;
}
