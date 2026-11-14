using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Migración 004 (autenticación segura) contra PostgreSQL real, en una base AISLADA (*_test, CLIMAPLUS_TEST_DB).
/// Se omiten si la variable no existe. Cada prueba trabaja en una transacción que se revierte: no deja datos.
/// Complementa database/scripts/pruebas/09_integridad_autenticacion.sql.
/// </summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class AutenticacionPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    private static async Task<List<string>> LeerAsync(ClimaPlusDbContext db, string sql)
    {
        var conexion = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var comando = new NpgsqlCommand(sql, conexion, (NpgsqlTransaction?)db.Database.CurrentTransaction?.GetDbTransaction());
        await using var lector = await comando.ExecuteReaderAsync();
        var filas = new List<string>();
        while (await lector.ReadAsync())
        {
            filas.Add(string.Join('|', Enumerable.Range(0, lector.FieldCount).Select(lector.GetValue)));
        }

        return filas;
    }

    [PostgreSqlFact]
    public async Task Esquema004_ColumnasNuevasDeUsuario()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();

        Assert.Equal(
            new[]
            {
                "correo_verificado|boolean|NO|false",
                "password_hash|character varying|YES|",
                "sello_seguridad|uuid|NO|gen_random_uuid()",
            },
            await LeerAsync(db, "SELECT column_name, data_type, is_nullable, coalesce(column_default, '') FROM information_schema.columns " +
                                "WHERE table_schema = current_schema() AND table_name = 'usuario' " +
                                "AND column_name IN ('password_hash', 'correo_verificado', 'sello_seguridad') ORDER BY column_name"));
    }

    [PostgreSqlFact]
    public async Task Esquema004_RestriccionesEIndicesDeLasTablasNuevas()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Solo CHECK (c), PRIMARY KEY (p), FOREIGN KEY (f) y UNIQUE (u), con su tipo. Desde PostgreSQL 18 los NOT NULL
        // también se guardan en pg_constraint (contype = 'n', nombres "<tabla>_<columna>_not_null"); se comprueban
        // aparte, columna por columna, para que la prueba valga igual en PostgreSQL 16 y en 18.
        const string sqlRestricciones =
            "SELECT conname, contype::text FROM pg_constraint WHERE conrelid = '{0}'::regclass " +
            "AND contype IN ('c', 'f', 'p', 'u') ORDER BY conname";

        Assert.Equal(
            new[]
            {
                "ck_desafio_autenticacion_codigo|c", "ck_desafio_autenticacion_envio|c", "ck_desafio_autenticacion_estado|c",
                "ck_desafio_autenticacion_finalizado|c", "ck_desafio_autenticacion_intentos|c", "ck_desafio_autenticacion_proposito|c",
                "ck_desafio_autenticacion_reenvios|c", "ck_desafio_autenticacion_sujeto_externo|c", "ck_desafio_autenticacion_verificado|c",
                "fk_desafio_autenticacion_usuario|f", "pk_desafio_autenticacion|p",
            },
            await LeerAsync(db, string.Format(System.Globalization.CultureInfo.InvariantCulture, sqlRestricciones, "desafio_autenticacion")));

        Assert.Equal(
            new[]
            {
                "ck_identidad_externa_correo_minusculas|c", "ck_identidad_externa_proveedor|c", "ck_identidad_externa_sujeto_no_vacio|c",
                "fk_identidad_externa_usuario|f", "pk_identidad_externa|p",
            },
            await LeerAsync(db, string.Format(System.Globalization.CultureInfo.InvariantCulture, sqlRestricciones, "identidad_externa")));

        // Ningún tipo de restricción inesperado (por ejemplo, EXCLUDE o triggers de restricción): solo c, f, p, u y,
        // en PostgreSQL 18, n.
        Assert.Empty(await LeerAsync(db,
            "SELECT conname, contype::text FROM pg_constraint " +
            "WHERE conrelid IN ('desafio_autenticacion'::regclass, 'identidad_externa'::regclass) " +
            "AND contype NOT IN ('c', 'f', 'p', 'u', 'n')"));

        // Nulabilidad exacta de cada columna (equivale a los NOT NULL de la migración 004 en cualquier versión).
        Assert.Equal(
            new[]
            {
                "desafio_autenticacion|codigo_hash|YES", "desafio_autenticacion|creado_en|NO", "desafio_autenticacion|enviado_en|YES",
                "desafio_autenticacion|estado|NO", "desafio_autenticacion|expira_en|NO", "desafio_autenticacion|finalizado_en|YES",
                "desafio_autenticacion|id_desafio|NO", "desafio_autenticacion|id_usuario|NO", "desafio_autenticacion|intentos|NO",
                "desafio_autenticacion|proposito|NO", "desafio_autenticacion|reenvios|NO", "desafio_autenticacion|sujeto_externo|YES",
                "desafio_autenticacion|verificado_en|YES",
                "identidad_externa|correo_proveedor|NO", "identidad_externa|fecha_vinculacion|NO", "identidad_externa|id_identidad|NO",
                "identidad_externa|id_usuario|NO", "identidad_externa|proveedor|NO", "identidad_externa|sujeto|NO",
            },
            await LeerAsync(db, "SELECT table_name, column_name, is_nullable FROM information_schema.columns " +
                                "WHERE table_schema = current_schema() AND table_name IN ('desafio_autenticacion', 'identidad_externa') " +
                                "ORDER BY table_name, column_name"));

        // ON DELETE CASCADE en ambas llaves foráneas (confdeltype = 'c').
        Assert.Equal(
            new[] { "fk_desafio_autenticacion_usuario|c", "fk_identidad_externa_usuario|c" },
            await LeerAsync(db, "SELECT conname, confdeltype FROM pg_constraint " +
                                "WHERE conname IN ('fk_desafio_autenticacion_usuario', 'fk_identidad_externa_usuario') ORDER BY conname"));

        var indices = await LeerAsync(db, "SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = current_schema() " +
                                          "AND tablename IN ('desafio_autenticacion', 'identidad_externa') AND indexname LIKE 'uq_%' ORDER BY indexname");
        Assert.Equal(3, indices.Count);
        Assert.Contains(indices, i => i.StartsWith("uq_desafio_autenticacion_vivo|CREATE UNIQUE INDEX", StringComparison.Ordinal)
                                      && i.Contains("WHERE", StringComparison.Ordinal) && i.Contains("PENDIENTE", StringComparison.Ordinal));
        Assert.Contains(indices, i => i.StartsWith("uq_identidad_externa_proveedor_sujeto|CREATE UNIQUE INDEX", StringComparison.Ordinal));
        Assert.Contains(indices, i => i.StartsWith("uq_identidad_externa_usuario_proveedor|CREATE UNIQUE INDEX", StringComparison.Ordinal));
    }

    [PostgreSqlFact]
    public async Task Esquema004_UsuarioNuevo_SinVerificar_ConSelloPropio_YSinContrasenaPermitido()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();

        var local = new Usuario { Nombre = "Prueba 004", Correo = $"m004-{Guid.NewGuid():N}@ejemplo.com", PasswordHash = "no-es-un-hash-real", FechaCreacion = DateTime.UtcNow };
        var google = new Usuario { Nombre = "Prueba 004 Google", Correo = $"m004g-{Guid.NewGuid():N}@ejemplo.com", PasswordHash = null, FechaCreacion = DateTime.UtcNow };
        db.Usuarios.AddRange(local, google);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var leidos = await db.Usuarios.AsNoTracking().Where(u => u.IdUsuario == local.IdUsuario || u.IdUsuario == google.IdUsuario).ToListAsync();
        Assert.All(leidos, u => Assert.False(u.CorreoVerificado));
        Assert.Equal(2, leidos.Select(u => u.SelloSeguridad).Distinct().Count());
        Assert.Null(leidos.Single(u => u.IdUsuario == google.IdUsuario).PasswordHash);
    }

    [PostgreSqlFact]
    public async Task Esquema004_UnSoloDesafioVivoPorUsuarioYProposito()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();
        var usuario = new Usuario { Nombre = "Prueba 004", Correo = $"m004d-{Guid.NewGuid():N}@ejemplo.com", PasswordHash = "x", FechaCreacion = DateTime.UtcNow };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        DesafioAutenticacion Nuevo(string proposito) => new()
        {
            IdDesafio = Guid.NewGuid(),
            IdUsuario = usuario.IdUsuario,
            Proposito = proposito,
            Estado = EstadosDesafio.Pendiente,
            CodigoHash = new string('a', 64),
            ExpiraEn = DateTime.UtcNow.AddMinutes(5),
            EnviadoEn = DateTime.UtcNow,
        };

        db.DesafiosAutenticacion.Add(Nuevo(PropositosDesafio.InicioSesion));
        db.DesafiosAutenticacion.Add(Nuevo(PropositosDesafio.RecuperarPassword));   // otro propósito convive
        await db.SaveChangesAsync();

        // Un segundo desafío vivo del mismo propósito lo rechaza el índice único parcial.
        db.DesafiosAutenticacion.Add(Nuevo(PropositosDesafio.InicioSesion));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("uq_desafio_autenticacion_vivo", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Esquema005_CheckDePropositos_AceptaCambiarPassword_YRechazaDesconocidos()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();

        // Definición real de la restricción en la base de pruebas, tras la migración 005.
        var definicion = Assert.Single(await LeerAsync(db,
            "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_desafio_autenticacion_proposito' " +
            "AND conrelid = 'desafio_autenticacion'::regclass"));
        foreach (var proposito in PropositosDesafio.Todos)
        {
            Assert.Contains($"'{proposito}'", definicion);
        }

        var usuario = new Usuario { Nombre = "Prueba 005", Correo = $"m005-{Guid.NewGuid():N}@ejemplo.com", PasswordHash = "x", FechaCreacion = DateTime.UtcNow };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();

        DesafioAutenticacion Nuevo(string proposito) => new()
        {
            IdDesafio = Guid.NewGuid(),
            IdUsuario = usuario.IdUsuario,
            Proposito = proposito,
            Estado = EstadosDesafio.Pendiente,
            CodigoHash = new string('b', 64),
            ExpiraEn = DateTime.UtcNow.AddMinutes(5),
            EnviadoEn = DateTime.UtcNow,
        };

        // Todos los propósitos, incluido CAMBIAR_PASSWORD, conviven (uno vivo por propósito).
        foreach (var proposito in PropositosDesafio.Todos.Where(p => p != PropositosDesafio.VincularGoogle))
        {
            db.DesafiosAutenticacion.Add(Nuevo(proposito));
        }

        await db.SaveChangesAsync();

        // Un segundo CAMBIAR_PASSWORD vivo lo rechaza el índice único parcial, igual que los demás propósitos.
        await tx.CreateSavepointAsync("segundo_vivo");
        db.DesafiosAutenticacion.Add(Nuevo(PropositosDesafio.CambiarPassword));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("uq_desafio_autenticacion_vivo", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
        await tx.RollbackToSavepointAsync("segundo_vivo");
        db.ChangeTracker.Clear();

        // Un propósito desconocido sigue rechazado por el CHECK.
        db.DesafiosAutenticacion.Add(Nuevo("OTRO_PROPOSITO"));
        error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("ck_desafio_autenticacion_proposito", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }

    [PostgreSqlFact]
    public async Task Esquema004_IdentidadGoogle_UnSubUnaCuenta_YUnaCuentaUnGoogle()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();
        Usuario Nuevo(string prefijo) => new()
        {
            Nombre = "Prueba Google", Correo = $"{prefijo}-{Guid.NewGuid():N}@ejemplo.com", PasswordHash = null, FechaCreacion = DateTime.UtcNow,
        };
        IdentidadExterna Identidad(long idUsuario, string sujeto) => new()
        {
            IdUsuario = idUsuario, Proveedor = ProveedoresIdentidad.Google, Sujeto = sujeto,
            CorreoProveedor = $"g-{Guid.NewGuid():N}@gmail.com", FechaVinculacion = DateTime.UtcNow,
        };

        var a = Nuevo("g5a");
        var b = Nuevo("g5b");
        db.Usuarios.AddRange(a, b);
        await db.SaveChangesAsync();
        var sujeto = "sub-" + Guid.NewGuid().ToString("N");
        db.IdentidadesExternas.Add(Identidad(a.IdUsuario, sujeto));
        await db.SaveChangesAsync();

        // El mismo "sub" de Google no puede quedar en dos cuentas (vinculación indebida / carrera).
        await tx.CreateSavepointAsync("mismo_sub");
        db.IdentidadesExternas.Add(Identidad(b.IdUsuario, sujeto));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("uq_identidad_externa_proveedor_sujeto", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
        await tx.RollbackToSavepointAsync("mismo_sub");
        db.ChangeTracker.Clear();

        // Una cuenta no puede tener dos cuentas de Google.
        db.IdentidadesExternas.Add(Identidad(a.IdUsuario, "sub-" + Guid.NewGuid().ToString("N")));
        error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("uq_identidad_externa_usuario_proveedor", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
    }
}
