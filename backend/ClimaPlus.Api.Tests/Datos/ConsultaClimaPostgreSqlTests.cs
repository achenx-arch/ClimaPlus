using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;
using ClimaPlus.Api.Services.Clima;
using ClimaPlus.Api.Services.Clima.Modelos;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// E-02 y E-03 contra PostgreSQL real, en una base AISLADA (*_test, variable CLIMAPLUS_TEST_DB).
/// Se omiten si la variable no existe. Cada prueba trabaja en una transacción que se revierte:
/// no deja datos. Equivalen a database/scripts/pruebas/E02_E03_consulta_clima.sql.
/// </summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class ConsultaClimaPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Contexto + transacción que se revierte al terminar, con una ubicación y un usuario de prueba.</summary>
    private sealed class Escenario : IAsyncDisposable
    {
        private Escenario(ClimaPlusDbContext db, IDbContextTransaction tx)
        {
            Db = db;
            Tx = tx;
        }

        public ClimaPlusDbContext Db { get; }

        private IDbContextTransaction Tx { get; }

        public long IdUbicacion { get; private set; }

        public long IdUsuario { get; private set; }

        public static async Task<Escenario> CrearAsync()
        {
            var db = BaseDatosPruebas.CrearContexto();
            var tx = await db.Database.BeginTransactionAsync();
            var escenario = new Escenario(db, tx);

            var ubicacion = new Ubicacion
            {
                Nombre = "Prueba E02",
                Pais = "GT",
                Latitud = Math.Round((decimal)Random.Shared.NextDouble() * 80m, 4),
                Longitud = Math.Round((decimal)Random.Shared.NextDouble() * 170m, 4),
                FechaCreacion = DateTime.UtcNow,
            };
            var usuario = new Usuario
            {
                Nombre = "Prueba E02",
                Correo = $"e02-{Guid.NewGuid():N}@ejemplo.com",
                PasswordHash = "no-es-un-hash-real",
                Rol = Roles.Usuario,
                Activo = true,
                FechaCreacion = DateTime.UtcNow,
            };
            db.AddRange(ubicacion, usuario);
            await db.SaveChangesAsync();

            escenario.IdUbicacion = ubicacion.IdUbicacion;
            escenario.IdUsuario = usuario.IdUsuario;
            return escenario;
        }

        public ConsultaClima Actual(long? idUsuario, short humedad = 74, string? detalle = "{\"version\":1,\"presionHpa\":1018}") => new()
        {
            IdUsuario = idUsuario,
            IdUbicacion = IdUbicacion,
            TipoConsulta = TiposConsulta.Actual,
            Temperatura = 22.5m,
            SensacionTermica = 23m,
            Humedad = humedad,
            VelocidadViento = 3.3m,
            CodigoCondicion = 802,
            DescripcionCondicion = "nubes dispersas",
            Detalle = detalle,
            FechaConsulta = DateTime.UtcNow,
        };

        public ConsultaClima Pronostico(long? idUsuario, string? detalle) => new()
        {
            IdUsuario = idUsuario,
            IdUbicacion = IdUbicacion,
            TipoConsulta = TiposConsulta.Pronostico,
            Detalle = detalle,
            FechaConsulta = DateTime.UtcNow,
        };

        /// <summary>Guarda y espera el rechazo de PostgreSQL; devuelve el error original.</summary>
        public async Task<PostgresException> FallaAlGuardarAsync(ConsultaClima consulta)
        {
            Db.ConsultasClima.Add(consulta);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
            return Assert.IsType<PostgresException>(error.InnerException);
        }

        public async ValueTask DisposeAsync()
        {
            await Tx.RollbackAsync();
            await Tx.DisposeAsync();
            await Db.DisposeAsync();
        }
    }

    private static async Task<ConsultaClima> GuardarYLeerAsync(Escenario e, ConsultaClima consulta)
    {
        e.Db.ConsultasClima.Add(consulta);
        await e.Db.SaveChangesAsync();
        e.Db.ChangeTracker.Clear();
        return await e.Db.ConsultasClima.AsNoTracking().SingleAsync(c => c.IdConsulta == consulta.IdConsulta);
    }

    // ------------------------------------------------------------------ Esquema

    [PostgreSqlFact]
    public async Task Esquema_TieneLasDosMigracionesYLasColumnasNuevas()
    {
        await using var e = await Escenario.CrearAsync();
        var conexion = (NpgsqlConnection)e.Db.Database.GetDbConnection();

        async Task<List<string>> LeerAsync(string sql)
        {
            await using var comando = new NpgsqlCommand(sql, conexion, (NpgsqlTransaction)e.Db.Database.CurrentTransaction!.GetDbTransaction());
            await using var lector = await comando.ExecuteReaderAsync();
            var filas = new List<string>();
            while (await lector.ReadAsync())
            {
                filas.Add(string.Join('|', Enumerable.Range(0, lector.FieldCount).Select(lector.GetValue)));
            }

            return filas;
        }

        Assert.Equal(
            new[] { "20261016052608_InitialCreate", "20261028062000_ConsultaClimaUsuarioOpcional", "20261028062100_ConsultaClimaDetalle" },
            await LeerAsync("SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\""));

        Assert.Equal(
            new[] { "detalle|jsonb|YES", "id_usuario|bigint|YES" },
            await LeerAsync("SELECT column_name, data_type, is_nullable FROM information_schema.columns " +
                            "WHERE table_schema = current_schema() AND table_name = 'consulta_clima' AND column_name IN ('id_usuario', 'detalle') ORDER BY column_name"));

        Assert.Equal(
            new[] { "12" },
            await LeerAsync("SELECT count(*) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'consulta_clima'"));

        Assert.Equal(
            new[]
            {
                "ck_consulta_clima_codigo_condicion", "ck_consulta_clima_detalle_objeto", "ck_consulta_clima_detalle_pronostico",
                "ck_consulta_clima_humedad", "ck_consulta_clima_mediciones_tipo", "ck_consulta_clima_sensacion_termica",
                "ck_consulta_clima_temperatura", "ck_consulta_clima_tipo_consulta", "ck_consulta_clima_velocidad_viento",
            },
            await LeerAsync("SELECT conname FROM pg_constraint WHERE conrelid = 'consulta_clima'::regclass AND contype = 'c' ORDER BY conname"));

        Assert.Equal(
            new[] { "alerta", "configuracion_alerta", "consulta_clima", "dispositivo", "favorito", "lectura_sensor", "ubicacion", "usuario" },
            await LeerAsync("SELECT table_name FROM information_schema.tables WHERE table_schema = current_schema() " +
                            "AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory' ORDER BY table_name"));
    }

    // ------------------------------------------------------------------ E-02

    [PostgreSqlFact]
    public async Task E02_ConsultaAnonima_SeGuardaConIdUsuarioNull()
    {
        await using var e = await Escenario.CrearAsync();

        var leida = await GuardarYLeerAsync(e, e.Actual(idUsuario: null));

        Assert.Null(leida.IdUsuario);
        Assert.Equal(TiposConsulta.Actual, leida.TipoConsulta);
    }

    [PostgreSqlFact]
    public async Task E02_ConsultaAutenticada_ConservaSuUsuario()
    {
        await using var e = await Escenario.CrearAsync();

        var leida = await GuardarYLeerAsync(e, e.Actual(e.IdUsuario));

        Assert.Equal(e.IdUsuario, leida.IdUsuario);
    }

    [PostgreSqlFact]
    public async Task E02_UsuarioInexistente_LoRechazaLaFk()
    {
        await using var e = await Escenario.CrearAsync();

        var error = await e.FallaAlGuardarAsync(e.Actual(idUsuario: long.MaxValue));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_consulta_clima_usuario", error.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task UbicacionInexistente_LaRechazaLaFk()
    {
        await using var e = await Escenario.CrearAsync();
        var consulta = e.Actual(idUsuario: null);
        consulta.IdUbicacion = long.MaxValue;

        var error = await e.FallaAlGuardarAsync(consulta);

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState);
        Assert.Equal("fk_consulta_clima_ubicacion", error.ConstraintName);
    }

    // ------------------------------------------------------------------ E-03

    [PostgreSqlFact]
    public async Task E03_ActualConDetalle_SeGuardaYSeLeeComoJson()
    {
        await using var e = await Escenario.CrearAsync();
        Assert.True(ClimaTransformador.IntentarTransformarActual(
            RespuestasOpenWeather.ClimaActual().Deserialize<ClimaActualProveedor>(Web), out var clima, out _));
        var json = DetalleSerializador.Serializar(clima!.Detalle);

        var leida = await GuardarYLeerAsync(e, e.Actual(null, detalle: json));

        using var documento = JsonDocument.Parse(leida.Detalle!);
        Assert.Equal(1, documento.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(1018, documento.RootElement.GetProperty("presionHpa").GetInt32());
        Assert.Equal("2026-10-27T18:20:00Z", documento.RootElement.GetProperty("observadoEnUtc").GetString());
    }

    [PostgreSqlFact]
    public async Task E03_PronosticoConDetalle_ConservaDiasYBloques()
    {
        await using var e = await Escenario.CrearAsync();
        Assert.True(ClimaTransformador.IntentarTransformarPronostico(
            RespuestasOpenWeather.Pronostico(new DateTime(2026, 10, 27, 21, 0, 0, DateTimeKind.Utc)).Deserialize<PronosticoProveedor>(Web),
            new DateTime(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc), out var pronostico, out _));

        var leida = await GuardarYLeerAsync(e, e.Pronostico(e.IdUsuario, DetalleSerializador.Serializar(pronostico!)));

        Assert.Null(leida.Temperatura);
        using var documento = JsonDocument.Parse(leida.Detalle!);
        var dias = documento.RootElement.GetProperty("dias");
        Assert.Equal(5, dias.GetArrayLength());
        Assert.Equal("2026-10-27", dias[0].GetProperty("fecha").GetString());
        Assert.Equal(3, dias[0].GetProperty("bloques").GetArrayLength());
        Assert.Equal(35, dias.EnumerateArray().Sum(d => d.GetProperty("bloques").GetArrayLength()));
    }

    [PostgreSqlFact]
    public async Task E03_ConsultaActualSinDetalle_SigueSiendoValida()
    {
        // Compatibilidad: las filas existentes (antes de E-03) quedan con detalle NULL.
        await using var e = await Escenario.CrearAsync();

        var leida = await GuardarYLeerAsync(e, e.Actual(e.IdUsuario, detalle: null));

        Assert.Null(leida.Detalle);
    }

    [PostgreSqlFact]
    public async Task E03_PronosticoSinDetalle_LoRechazaElCheck()
    {
        await using var e = await Escenario.CrearAsync();

        var error = await e.FallaAlGuardarAsync(e.Pronostico(null, detalle: null));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_consulta_clima_detalle_pronostico", error.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task E03_DetalleQueNoEsObjeto_LoRechazaElCheck()
    {
        await using var e = await Escenario.CrearAsync();

        var error = await e.FallaAlGuardarAsync(e.Pronostico(null, "[1,2,3]"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Equal("ck_consulta_clima_detalle_objeto", error.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task E03_DetalleQueNoEsJson_LoRechazaPostgreSql()
    {
        await using var e = await Escenario.CrearAsync();

        var error = await e.FallaAlGuardarAsync(e.Pronostico(null, "esto no es json"));

        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, error.SqlState);
    }

    // ------------------------------------------------------------------ Regresión de los CHECK originales

    [PostgreSqlFact]
    public async Task Regresion_HumedadFueraDeRango_SigueRechazandose()
    {
        await using var e = await Escenario.CrearAsync();

        var error = await e.FallaAlGuardarAsync(e.Actual(null, humedad: 150));

        Assert.Equal("ck_consulta_clima_humedad", error.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Regresion_PronosticoConMediciones_SigueRechazandose()
    {
        await using var e = await Escenario.CrearAsync();
        var consulta = e.Pronostico(null, "{\"version\":1}");
        consulta.Temperatura = 20m;

        var error = await e.FallaAlGuardarAsync(consulta);

        Assert.Equal("ck_consulta_clima_mediciones_tipo", error.ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Regresion_ActualSinMediciones_SigueRechazandose()
    {
        await using var e = await Escenario.CrearAsync();
        var consulta = e.Actual(null);
        consulta.Humedad = null;

        var error = await e.FallaAlGuardarAsync(consulta);

        Assert.Equal("ck_consulta_clima_mediciones_tipo", error.ConstraintName);
    }
}
