using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// E-02 y E-03 sin conectarse a PostgreSQL: el modelo EF Core, la lista de migraciones, que el
/// snapshot coincida con el modelo (equivale a "dotnet ef migrations has-pending-model-changes")
/// y que el SQL generado por EF sea exactamente el de database/migrations/002 y 003.
/// </summary>
public sealed class ModeloConsultaClimaTests
{
    private const string Inicial = "20261016052608_InitialCreate";
    private const string UsuarioOpcional = "20261028062000_ConsultaClimaUsuarioOpcional";
    private const string Detalle = "20261028062100_ConsultaClimaDetalle";

    private static IEntityType Entidad(DbContext db) =>
        db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ConsultaClima))!;

    [Fact]
    public void E02_IdUsuarioEsOpcional_YLaFkSigueSiendoRestrict()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var entidad = Entidad(db);

        var idUsuario = entidad.FindProperty(nameof(Domain.Entities.ConsultaClima.IdUsuario))!;
        Assert.True(idUsuario.IsNullable);
        Assert.Equal("bigint", idUsuario.GetColumnType());

        var fk = Assert.Single(entidad.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(Usuario));
        Assert.False(fk.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal("fk_consulta_clima_usuario", fk.GetConstraintName());

        // La ubicación sigue siendo obligatoria.
        Assert.False(entidad.FindProperty(nameof(Domain.Entities.ConsultaClima.IdUbicacion))!.IsNullable);
    }

    [Fact]
    public void E03_DetalleEsJsonbOpcional_ConSusDosCheck()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var entidad = Entidad(db);

        var detalle = entidad.FindProperty(nameof(Domain.Entities.ConsultaClima.Detalle))!;
        Assert.True(detalle.IsNullable);
        Assert.Equal("jsonb", detalle.GetColumnType());
        Assert.Equal("detalle", detalle.GetColumnName());

        var checks = entidad.GetCheckConstraints().ToDictionary(c => c.ModelName, c => c.Sql);
        Assert.Equal(9, checks.Count);
        Assert.Equal("detalle IS NULL OR jsonb_typeof(detalle) = 'object'", checks["ck_consulta_clima_detalle_objeto"]);
        Assert.Equal("tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL", checks["ck_consulta_clima_detalle_pronostico"]);
    }

    [Fact]
    public void ElResto_DelModeloNoCambia_OchoTablasY26Check()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var modelo = db.GetService<IDesignTimeModel>().Model;

        Assert.Equal(
            new[] { "alerta", "configuracion_alerta", "consulta_clima", "dispositivo", "favorito", "lectura_sensor", "ubicacion", "usuario" },
            modelo.GetEntityTypes().Select(e => e.GetTableName()!).Order().ToArray());
        Assert.Equal(26, modelo.GetEntityTypes().Sum(e => e.GetCheckConstraints().Count()));
        Assert.Equal(12, Entidad(db).GetProperties().Count());
    }

    [Fact]
    public void Migraciones_EstanEnOrden_YNoHayCambiosPendientes()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();

        Assert.Equal(new[] { Inicial, UsuarioOpcional, Detalle }, db.Database.GetMigrations().ToArray());

        // Mismo criterio que "dotnet ef migrations has-pending-model-changes".
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable)
        {
            snapshot = mutable.FinalizeModel();
        }

        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);
        var diferencias = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(diferencias.Count == 0,
            "El snapshot no coincide con el modelo: " + string.Join(", ", diferencias.Select(d => d.GetType().Name)));
    }

    [Fact]
    public void Script002_SoloQuitaNotNullDeIdUsuario_YCoincideConElArchivo()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();

        var script = db.GetService<IMigrator>().GenerateScript(Inicial, UsuarioOpcional);

        Assert.Equal(
            new[] { "ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;" },
            Sentencias(script));
        Assert.Equal(Normalizar(script), Normalizar(LeerArchivo("002_ConsultaClimaUsuarioOpcional.sql")));
    }

    [Fact]
    public void Script003_SoloAgregaDetalleYSusCheck_YCoincideConElArchivo()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();

        var script = db.GetService<IMigrator>().GenerateScript(UsuarioOpcional, Detalle);

        Assert.Equal(
            new[]
            {
                "ALTER TABLE consulta_clima ADD detalle jsonb;",
                "ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_objeto CHECK (detalle IS NULL OR jsonb_typeof(detalle) = 'object');",
                "ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_pronostico CHECK (tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL);",
            },
            Sentencias(script));
        Assert.Equal(Normalizar(script), Normalizar(LeerArchivo("003_ConsultaClimaDetalle.sql")));
    }

    [Fact]
    public void ScriptsNuevos_NoContienenDropNiCreateNiOtrasTablas()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var script = db.GetService<IMigrator>().GenerateScript(Inicial, Detalle).ToUpperInvariant();

        Assert.DoesNotContain("DROP", script.Replace("DROP NOT NULL", string.Empty));
        Assert.DoesNotContain("CREATE TABLE", script);
        Assert.DoesNotContain("DELETE", script);
        Assert.DoesNotContain("TRUNCATE", script);
        foreach (var tabla in new[] { "ALERTA ", "CONFIGURACION_ALERTA", "DISPOSITIVO", "FAVORITO", "LECTURA_SENSOR", "UBICACION ", "USUARIO " })
        {
            Assert.DoesNotContain("ALTER TABLE " + tabla, script);
        }
    }

    /// <summary>Sentencias del script sin la transacción ni el registro en __EFMigrationsHistory.</summary>
    private static string[] Sentencias(string script) =>
        Normalizar(script)
            .Where(l => l is not "START TRANSACTION;" and not "COMMIT;"
                     && !l.StartsWith("INSERT INTO \"__EFMigrationsHistory\"", StringComparison.Ordinal)
                     && !l.StartsWith("VALUES (", StringComparison.Ordinal))
            .ToArray();

    private static string[] Normalizar(string texto) =>
        texto.TrimStart('﻿')
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

    /// <summary>Lee database/migrations/{archivo} buscando la raíz del repositorio desde la carpeta de las pruebas.</summary>
    private static string LeerArchivo(string archivo)
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var ruta = Path.Combine(carpeta.FullName, "database", "migrations", archivo);
            if (File.Exists(ruta))
            {
                return File.ReadAllText(ruta);
            }
        }

        throw new FileNotFoundException($"No se encontró database/migrations/{archivo}.");
    }
}
