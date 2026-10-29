using System.Globalization;
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

    private readonly string _nombreBaseDatos = "climaplus-pruebas-" + Guid.NewGuid().ToString("N");

    protected virtual int LimiteAutenticacion => 1000;

    protected virtual int LimiteClima => 1000;

    /// <summary>"" simula que la clave no está configurada.</summary>
    protected virtual string ClaveOpenWeather => ClaveOpenWeatherPruebas;

    public ProveedorOpenWeatherSimulado Proveedor { get; } = new();

    public LogsCapturados Logs { get; } = new();

    /// <summary>Reloj de la API (TimeProvider); las pruebas de caché lo adelantan.</summary>
    public RelojPruebas Reloj { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuracion) =>
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ClimaPlusDb"] = "Host=no-se-usa-en-pruebas",
                ["Jwt:Clave"] = ClaveJwtPruebas,
                ["RateLimiting:Autenticacion:PermitLimit"] = LimiteAutenticacion.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Autenticacion:VentanaSegundos"] = "60",
                ["RateLimiting:Clima:PermitLimit"] = LimiteClima.ToString(CultureInfo.InvariantCulture),
                ["RateLimiting:Clima:VentanaSegundos"] = "60",
                ["OpenWeather:ApiKey"] = ClaveOpenWeather,
                // Dominio reservado .invalid: aunque fallara el simulador, nunca se llegaría a OpenWeather real.
                ["OpenWeather:BaseUrl"] = "https://api.openweather.pruebas.invalid",
            }));

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
        });
    }
}

/// <summary>Igual que la fábrica normal, pero con un límite de 3 intentos para probar el rate limiting.</summary>
public sealed class ClimaPlusApiFactoryLimiteBajo : ClimaPlusApiFactory
{
    public const int Limite = 3;

    protected override int LimiteAutenticacion => Limite;

    protected override int LimiteClima => Limite;
}

/// <summary>API sin OpenWeather:ApiKey: las rutas de clima deben responder 503 sin llamar al proveedor.</summary>
public sealed class ClimaPlusApiFactorySinClaveOpenWeather : ClimaPlusApiFactory
{
    protected override string ClaveOpenWeather => string.Empty;
}
