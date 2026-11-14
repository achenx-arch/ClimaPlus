using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Migraciones 004 (autenticación segura) y 005 (cambio de contraseña con código) sin conectarse a PostgreSQL: el modelo de usuario, desafio_autenticacion e
/// identidad_externa, y que el SQL generado por EF sea exactamente el de database/migrations/004_AutenticacionSegura.sql.
/// Las restricciones se ejecutan de verdad en database/scripts/pruebas/09_integridad_autenticacion.sql.
/// </summary>
public sealed class ModeloAutenticacionTests
{
    public const string AutenticacionSegura = "20261109180000_AutenticacionSegura";
    public const string CambioPassword = "20261113150000_CambioPasswordConCodigo";
    private const string Detalle = "20261028062100_ConsultaClimaDetalle";

    private static IModel Modelo(DbContext db) => db.GetService<IDesignTimeModel>().Model;

    [Fact]
    public void Usuario_PasswordOpcional_CorreoSinVerificarYSelloPorFila()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var usuario = Modelo(db).FindEntityType(typeof(Usuario))!;

        Assert.True(usuario.FindProperty(nameof(Usuario.PasswordHash))!.IsNullable);

        var verificado = usuario.FindProperty(nameof(Usuario.CorreoVerificado))!;
        Assert.False(verificado.IsNullable);
        Assert.False((bool)verificado.GetDefaultValue()!);
        Assert.Equal("correo_verificado", verificado.GetColumnName());

        var sello = usuario.FindProperty(nameof(Usuario.SelloSeguridad))!;
        Assert.False(sello.IsNullable);
        Assert.Equal("uuid", sello.GetColumnType());
        Assert.Equal("gen_random_uuid()", sello.GetDefaultValueSql());

        // Cada usuario nuevo en memoria ya trae un sello distinto (EF lo envía; la BD solo lo pone en filas antiguas).
        Assert.NotEqual(new Usuario().SelloSeguridad, new Usuario().SelloSeguridad);
    }

    [Fact]
    public void Desafio_TieneSusNueveCheck_IndiceVivoParcial_YCascada()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var desafio = Modelo(db).FindEntityType(typeof(DesafioAutenticacion))!;

        Assert.Equal("desafio_autenticacion", desafio.GetTableName());
        Assert.Equal(9, desafio.GetCheckConstraints().Count());
        Assert.Equal("uuid", desafio.FindProperty(nameof(DesafioAutenticacion.IdDesafio))!.GetColumnType());
        Assert.Equal(ValueGenerated.Never, desafio.FindProperty(nameof(DesafioAutenticacion.IdDesafio))!.ValueGenerated);

        var vivo = Assert.Single(desafio.GetIndexes(), i => i.GetDatabaseName() == "uq_desafio_autenticacion_vivo");
        Assert.True(vivo.IsUnique);
        Assert.Equal("estado IN ('PENDIENTE', 'VERIFICADO')", vivo.GetFilter());

        var fk = Assert.Single(desafio.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        Assert.Equal("fk_desafio_autenticacion_usuario", fk.GetConstraintName());
    }

    [Fact]
    public void IdentidadExterna_UnSubPorProveedor_YUnaIdentidadPorUsuario()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var identidad = Modelo(db).FindEntityType(typeof(IdentidadExterna))!;

        Assert.Equal("identidad_externa", identidad.GetTableName());
        Assert.Equal(3, identidad.GetCheckConstraints().Count());
        Assert.Equal(
            new[] { "uq_identidad_externa_proveedor_sujeto", "uq_identidad_externa_usuario_proveedor" },
            identidad.GetIndexes().Where(i => i.IsUnique).Select(i => i.GetDatabaseName()!).Order().ToArray());
        Assert.Equal(DeleteBehavior.Cascade, Assert.Single(identidad.GetForeignKeys()).DeleteBehavior);
    }

    [Fact]
    public void Modelo_TieneDiezTablasY38Check()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var modelo = Modelo(db);

        Assert.Equal(10, modelo.GetEntityTypes().Count());
        Assert.Equal(38, modelo.GetEntityTypes().Sum(e => e.GetCheckConstraints().Count()));
    }

    [Fact]
    public void Script004_CoincideConElArchivo()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();

        var script = db.GetService<IMigrator>().GenerateScript(Detalle, AutenticacionSegura);

        Assert.Equal(
            ModeloConsultaClimaTests.Normalizar(ModeloConsultaClimaTests.LeerArchivo("004_AutenticacionSegura.sql")),
            ModeloConsultaClimaTests.Normalizar(script));
    }

    [Fact]
    public void Desafio_CheckDePropositos_AdmiteCambiarPassword_YConservaLosAnteriores()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var desafio = Modelo(db).FindEntityType(typeof(DesafioAutenticacion))!;
        var check = Assert.Single(desafio.GetCheckConstraints(), c => c.ModelName == "ck_desafio_autenticacion_proposito");

        Assert.Equal(
            "proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD', 'CAMBIAR_PASSWORD')",
            check.Sql);
        // El valor más largo cabe en la columna (varchar(24)): no hace falta cambiarla.
        Assert.Equal(24, desafio.FindProperty(nameof(DesafioAutenticacion.Proposito))!.GetMaxLength());
        Assert.All(PropositosDesafio.Todos, p => Assert.True(p.Length <= 24, p));
    }

    [Fact]
    public void Script005_SoloReemplazaElCheckDePropositos_YCoincideConElArchivo()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();

        var script = db.GetService<IMigrator>().GenerateScript(AutenticacionSegura, CambioPassword);

        Assert.Equal(
            new[]
            {
                "ALTER TABLE desafio_autenticacion DROP CONSTRAINT ck_desafio_autenticacion_proposito;",
                "ALTER TABLE desafio_autenticacion ADD CONSTRAINT ck_desafio_autenticacion_proposito CHECK (proposito IN " +
                "('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD', 'CAMBIAR_PASSWORD'));",
            },
            ModeloConsultaClimaTests.Sentencias(script));
        Assert.Equal(
            ModeloConsultaClimaTests.Normalizar(ModeloConsultaClimaTests.LeerArchivo("005_CambioPasswordConCodigo.sql")),
            ModeloConsultaClimaTests.Normalizar(script));
    }

    [Fact]
    public void Script004_ConservaLosDatos_NoBorraNiActualizaFilas()
    {
        using var db = BaseDatosPruebas.CrearContextoSinConexion();
        var sentencias = ModeloConsultaClimaTests.Sentencias(
            db.GetService<IMigrator>().GenerateScript(Detalle, AutenticacionSegura)).Select(s => s.ToUpperInvariant()).ToArray();

        Assert.DoesNotContain(sentencias, s => s.Contains("DROP TABLE") || s.Contains("DROP COLUMN"));
        Assert.DoesNotContain(sentencias, s => s.StartsWith("DELETE") || s.StartsWith("UPDATE") || s.StartsWith("TRUNCATE"));
        // En usuario solo se quita NOT NULL de password_hash y se agregan las 2 columnas nuevas.
        Assert.Equal(
            new[]
            {
                "ALTER TABLE USUARIO ALTER COLUMN PASSWORD_HASH DROP NOT NULL;",
                "ALTER TABLE USUARIO ADD CORREO_VERIFICADO BOOLEAN NOT NULL DEFAULT FALSE;",
                "ALTER TABLE USUARIO ADD SELLO_SEGURIDAD UUID NOT NULL DEFAULT (GEN_RANDOM_UUID());",
            },
            sentencias.Where(s => s.StartsWith("ALTER TABLE USUARIO")).ToArray());
    }
}
