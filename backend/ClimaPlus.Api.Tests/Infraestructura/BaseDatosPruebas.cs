using ClimaPlus.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Acceso a la base PostgreSQL AISLADA de pruebas (D-27). Solo se usa si existe la variable de
/// entorno CLIMAPLUS_TEST_DB con su cadena de conexión. Por seguridad, el nombre de la base
/// debe terminar en "_test": nunca se toca climaplus_db.
/// </summary>
public static class BaseDatosPruebas
{
    public const string Variable = "CLIMAPLUS_TEST_DB";
    public const string Categoria = "PostgreSQL";

    public static string? CadenaConexion => Environment.GetEnvironmentVariable(Variable);

    public static bool Configurada => !string.IsNullOrWhiteSpace(CadenaConexion);

    /// <summary>Contexto Npgsql contra la base de pruebas, después de validar que es una base "_test".</summary>
    public static ClimaPlusDbContext CrearContexto()
    {
        var cadena = CadenaConexion ?? throw new InvalidOperationException($"Falta la variable {Variable}.");
        var nombre = new NpgsqlConnectionStringBuilder(cadena).Database ?? string.Empty;
        if (!nombre.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{Variable} debe apuntar a una base de datos de pruebas cuyo nombre termine en '_test' (se recibió '{nombre}').");
        }

        var opciones = new DbContextOptionsBuilder<ClimaPlusDbContext>().UseNpgsql(cadena).Options;
        return new ClimaPlusDbContext(opciones);
    }

    /// <summary>
    /// Contexto Npgsql que NUNCA se conecta: sirve para leer el modelo, las migraciones y generar su SQL.
    /// </summary>
    public static ClimaPlusDbContext CrearContextoSinConexion()
    {
        var opciones = new DbContextOptionsBuilder<ClimaPlusDbContext>()
            .UseNpgsql("Host=no-se-conecta.invalid;Database=solo_modelo")
            .Options;
        return new ClimaPlusDbContext(opciones);
    }
}

/// <summary>[Fact] que se omite si no está configurada la base PostgreSQL de pruebas.</summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (!BaseDatosPruebas.Configurada)
        {
            Skip = $"Prueba de PostgreSQL omitida: define {BaseDatosPruebas.Variable} con una base aislada (*_test).";
        }
    }
}

/// <summary>
/// Prepara la base de pruebas una vez por clase: aplica las migraciones SOLO a la base "_test".
/// No borra la base ni datos; cada prueba trabaja dentro de una transacción que se revierte.
/// </summary>
public sealed class BaseDatosPruebasFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        if (!BaseDatosPruebas.Configurada)
        {
            return;
        }

        await using var db = BaseDatosPruebas.CrearContexto();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
