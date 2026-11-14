using ClimaPlus.Api.Data;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Fase 7: /health también comprueba que la base tenga todas las migraciones del código (una base sin la 004 hacía
/// fallar el inicio de sesión con 500). Solo se lee el historial de migraciones; nunca se aplica nada.
/// </summary>
public sealed class MigracionesPendientesTests
{
    [Fact]
    public async Task ProveedorNoRelacional_NoTieneMigracionesPendientes_YLaSaludEsHealthy()
    {
        var opciones = new DbContextOptionsBuilder<ClimaPlusDbContext>()
            .UseInMemoryDatabase($"migraciones-{Guid.NewGuid():N}")
            .Options;
        await using var db = new ClimaPlusDbContext(opciones);

        Assert.Empty(await VerificacionBaseDatos.MigracionesPendientesAsync(db));
        var salud = await new VerificacionBaseDatos(db).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, salud.Status);
    }
}

/// <summary>Contra la base aislada *_test (CLIMAPLUS_TEST_DB), ya migrada por el fixture.</summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class MigracionesPendientesPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    [PostgreSqlFact]
    public async Task BaseDePruebasMigrada_SinPendientes_YLaSaludEsHealthy()
    {
        await using var db = BaseDatosPruebas.CrearContexto();

        Assert.Empty(await VerificacionBaseDatos.MigracionesPendientesAsync(db));
        var salud = await new VerificacionBaseDatos(db).CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, salud.Status);
    }
}
