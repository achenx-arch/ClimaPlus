using System.Globalization;
using ClimaPlus.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Levanta la API completa en memoria (pipeline real: validación, JWT, autorización,
/// rate limiting) y reemplaza PostgreSQL por EF Core InMemory.
/// InMemory NO aplica CHECK ni UNIQUE de PostgreSQL: esas restricciones se prueban con SQL (Integrante 1).
/// </summary>
public class ClimaPlusApiFactory : WebApplicationFactory<Program>
{
    public const string ClaveJwtPruebas = "clave-de-pruebas-solo-para-tests-0123456789-abcdef";

    private readonly string _nombreBaseDatos = "climaplus-pruebas-" + Guid.NewGuid().ToString("N");

    protected virtual int LimiteAutenticacion => 1000;

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
            }));

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
        });
    }
}

/// <summary>Igual que la fábrica normal, pero con un límite de 3 intentos para probar el rate limiting.</summary>
public sealed class ClimaPlusApiFactoryLimiteBajo : ClimaPlusApiFactory
{
    public const int Limite = 3;

    protected override int LimiteAutenticacion => Limite;
}
