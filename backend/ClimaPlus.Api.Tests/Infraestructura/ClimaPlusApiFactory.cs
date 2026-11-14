using System.Globalization;
using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Integraciones.OpenWeather;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Levanta la API completa en memoria (pipeline real: validación, JWT, autorización,
/// rate limiting) y reemplaza PostgreSQL por EF Core InMemory.
/// InMemory NO aplica CHECK ni UNIQUE de PostgreSQL: esas restricciones se prueban con SQL (Integrante 1).
/// OpenWeather se sustituye por <see cref="ProveedorOpenWeatherSimulado"/>: ninguna prueba sale a internet
/// y la API key de pruebas es falsa (la real de User Secrets no se carga en el entorno Testing).
/// </summary>
public class ClimaPlusApiFactory : WebApplicationFactory<Program>
{
    public const string ClaveJwtPruebas = "clave-de-pruebas-solo-para-tests-0123456789-abcdef";

    /// <summary>Valor falso, solo para comprobar que la clave nunca aparece en respuestas ni logs.</summary>
    public const string ClaveOpenWeatherPruebas = "clave-falsa-openweather-solo-pruebas-9f8e7d6c";

    /// <summary>Clave HMAC de los códigos solo para pruebas: distinta de la del JWT, como exige la API.</summary>
    public const string ClaveHmacPruebas = "clave-hmac-codigos-solo-pruebas-5a4b3c2d1e0f-987654";

    private readonly string _nombreBaseDatos = "climaplus-pruebas-" + Guid.NewGuid().ToString("N");

    protected virtual int LimiteAutenticacion => 1000;

    protected virtual int LimiteClima => 1000;

    protected virtual int LimiteDispositivos => 1000;

    protected virtual int LimiteGeneral => 100_000;

    protected virtual int MaxIntentosLogin => 5;

    /// <summary>POST /api/auth/register por IP (producción: 5 cada 10 minutos).</summary>
    protected virtual int LimiteRegistro => 1000;

    /// <summary>Endpoints que validan un código, por IP (producción: 20 por minuto).</summary>
    protected virtual int LimiteCodigos => 1000;

    /// <summary>POST /api/auth/reenviar-codigo por IP (producción: 10 por hora).</summary>
    protected virtual int LimiteReenvio => 1000;

    /// <summary>Solicitar recuperación / establecer contraseña, por IP (producción: 5 cada 15 minutos).</summary>
    protected virtual int LimiteRecuperacion => 1000;

    /// <summary>"" simula que la clave no está configurada.</summary>
    protected virtual string ClaveOpenWeather => ClaveOpenWeatherPruebas;

    /// <summary>Client ID de Google de pruebas (vacío = "Continuar con Google" desactivado).</summary>
    protected virtual string ClientIdGoogle => GoogleFalso.ClientId;

    public ProveedorOpenWeatherSimulado Proveedor { get; } = new();

    public LogsCapturados Logs { get; } = new();

    /// <summary>Reloj de la API (TimeProvider); las pruebas de caché lo adelantan.</summary>
    public RelojPruebas Reloj { get; } = new();

    /// <summary>Correos "enviados" por la API (autenticación segura). Nunca se conecta a un servidor SMTP.</summary>
    public EnviadorCorreoFalso Correos { get; } = new();

    /// <summary>Configuración adicional de una fábrica derivada (por ejemplo, el proxy inverso).</summary>
    protected virtual void AgregarConfiguracion(IDictionary<string, string?> valores)
    {
    }

    /// <summary>Servicios adicionales de una fábrica derivada.</summary>
    protected virtual void AgregarServicios(IServiceCollection servicios)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        var valores = new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClimaPlusDb"] = "Host=no-se-usa-en-pruebas",
                ["Jwt:Clave"] = ClaveJwtPruebas,
                ["RateLimiting:Autenticacion:PermitLimit"] = LimiteAutenticacion.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Autenticacion:VentanaSegundos"] = "60",
                ["RateLimiting:Clima:PermitLimit"] = LimiteClima.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Clima:VentanaSegundos"] = "60",
                ["RateLimiting:Dispositivos:PermitLimit"] = LimiteDispositivos.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Dispositivos:VentanaSegundos"] = "60",
                ["RateLimiting:General:PermitLimit"] = LimiteGeneral.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:General:VentanaSegundos"] = "60",
                ["RateLimiting:Registro:PermitLimit"] = LimiteRegistro.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Registro:VentanaSegundos"] = "600",
                ["RateLimiting:Codigos:PermitLimit"] = LimiteCodigos.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Codigos:VentanaSegundos"] = "60",
                ["RateLimiting:Reenvio:PermitLimit"] = LimiteReenvio.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Reenvio:VentanaSegundos"] = "3600",
                ["RateLimiting:Recuperacion:PermitLimit"] = LimiteRecuperacion.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Recuperacion:VentanaSegundos"] = "900",
                ["Codigos:ClaveHmac"] = ClaveHmacPruebas,
                ["Google:ClientId"] = ClientIdGoogle,
                ["Seguridad:IntentosLogin:MaxFallidos"] = MaxIntentosLogin.ToString(CultureInfo.InvariantCulture),
                ["OpenWeather:ApiKey"] = ClaveOpenWeather,
                // Dominio reservado .invalid: aunque fallara el simulador, nunca se llegaría a OpenWeather real.
                ["OpenWeather:BaseUrl"] = "https://api.openweather.pruebas.invalid",
            };
        AgregarConfiguracion(valores);
        builder.ConfigureAppConfiguration((_, configuracion) => configuracion.AddInMemoryCollection(valores));

        builder.ConfigureLogging(logging => logging.AddProvider(Logs));

        builder.ConfigureTestServices(servicios =>
        {
            var registrosNpgsql = servicios
                .Where(d => d.ServiceType == typeof(DbContextOptions<ClimaPlusDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var registro in registrosNpgsql)
            {
                servicios.Remove(registro);
            }

            servicios.AddDbContext<ClimaPlusDbContext>(opciones => opciones.UseInMemoryDatabase(_nombreBaseDatos));

            // Endpoint de prueba solo para administradores (no existe en la API real).
            servicios.AddControllers().AddApplicationPart(typeof(PruebasController).Assembly);

            // OpenWeather simulado (D-27).
            servicios.AddHttpClient(OpenWeatherClient.NombreCliente)
                .ConfigurePrimaryHttpMessageHandler(() => Proveedor)
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

            // Reloj controlable (ClimaService y RegistroConsultaClima usan TimeProvider).
            servicios.AddSingleton<TimeProvider>(Reloj);

            // Correo en memoria (autenticación segura): ningún correo sale a internet. Reintentos rápidos.
            servicios.AddSingleton<IEmailSender>(Correos);

            // Google sin Internet: claves públicas de prueba en lugar del documento de descubrimiento real. El
            // validador real (Microsoft.IdentityModel) comprueba firma, emisor, audiencia y vencimiento con ellas.
            servicios.AddSingleton<Microsoft.IdentityModel.Protocols.IConfigurationManager<Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration>>(
                GoogleFalso.Configuracion());
            servicios.AddSingleton<IColaCorreos>(sp => new ColaCorreosObservada(sp.GetRequiredService<ColaCorreos>()));
            servicios.AddSingleton(sp =>
            {
                var servicio = new ServicioEnvioCorreos(
                    sp.GetRequiredService<ColaCorreos>(), sp.GetRequiredService<IEmailSender>(), sp.GetRequiredService<ILoggerFactory>());
                servicio.EsperasEntreIntentos = new[] { TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10) };
                return servicio;
            });

            AgregarServicios(servicios);
        });
    }
}

/// <summary>Igual que la fábrica normal, pero con un límite de 3 intentos para probar el rate limiting.</summary>
public sealed class ClimaPlusApiFactoryLimiteBajo : ClimaPlusApiFactory
{
    public const int Limite = 3;

    protected override int LimiteAutenticacion => Limite;

    protected override int LimiteClima => Limite;

    protected override int LimiteDispositivos => Limite;
}

/// <summary>Fase 8: límite general de 10 peticiones por minuto, para probar que también protege las rutas con sesión.</summary>
public sealed class ClimaPlusApiFactoryLimiteGeneral : ClimaPlusApiFactory
{
    public const int Limite = 10;

    protected override int LimiteGeneral => Limite;
}

/// <summary>Autenticación segura: límites de producción para registro, códigos, reenvíos y recuperación (5, 20, 10 y 5 por IP).</summary>
public sealed class ClimaPlusApiFactoryLimitesCodigos : ClimaPlusApiFactory
{
    public const int Registro = 5;
    public const int Codigos = 20;
    public const int Reenvio = 10;
    public const int Recuperacion = 5;

    protected override int LimiteRegistro => Registro;

    protected override int LimiteRecuperacion => Recuperacion;

    protected override int LimiteCodigos => Codigos;

    protected override int LimiteReenvio => Reenvio;
}

/// <summary>
/// Simula clientes en distintas IP: la cabecera de pruebas <see cref="FiltroIpConexion.Cabecera"/> fija la IP de la
/// conexión (bloqueos por cuenta + IP del inicio de sesión).
/// </summary>
public sealed class ClimaPlusApiFactoryVariasIp : ClimaPlusApiFactory
{
    protected override void AgregarServicios(IServiceCollection servicios) =>
        servicios.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, FiltroIpConexion>();
}

/// <summary>API sin Google:ClientId: "Continuar con Google" desactivado (503).</summary>
public sealed class ClimaPlusApiFactorySinGoogle : ClimaPlusApiFactory
{
    protected override string ClientIdGoogle => string.Empty;
}

/// <summary>API sin OpenWeather:ApiKey: las rutas de clima deben responder 503 sin llamar al proveedor.</summary>
public sealed class ClimaPlusApiFactorySinClaveOpenWeather : ClimaPlusApiFactory
{
    protected override string ClaveOpenWeather => string.Empty;
}
