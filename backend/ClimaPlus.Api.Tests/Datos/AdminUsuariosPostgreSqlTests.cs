using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Admin;
using ClimaPlus.Api.Services.Admin;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Gestión de usuarios (M14) contra PostgreSQL real, SOLO en la base aislada (*_test, variable CLIMAPLUS_TEST_DB).
/// Se omiten si la variable no existe. Nunca se usa climaplus_db. Cada prueba crea sus propias cuentas (correo
/// "admin-pg-…@ejemplo.com") y las borra al terminar; no toca ninguna otra fila.
/// </summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class AdminUsuariosPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    private static UsuarioAdminService Servicio(ClimaPlus.Api.Data.ClimaPlusDbContext db) => new(db, NullLoggerFactory.Instance);

    private static async Task<List<Usuario>> CrearAsync(string marcador, params string[] roles)
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        var usuarios = roles.Select((rol, i) => new Usuario
        {
            Nombre = $"{marcador} Persona {i}",
            Correo = $"admin-pg-{marcador.ToLowerInvariant()}-{i}@ejemplo.com",
            PasswordHash = "no-es-un-hash-real",
            Rol = rol,
            Activo = true,
            CorreoVerificado = true,
            FechaCreacion = DateTime.UtcNow.AddSeconds(i),
        }).ToList();
        db.Usuarios.AddRange(usuarios);
        await db.SaveChangesAsync();
        return usuarios;
    }

    private static async Task BorrarAsync(IEnumerable<Usuario> usuarios)
    {
        var ids = usuarios.Select(u => u.IdUsuario).ToArray();
        await using var db = BaseDatosPruebas.CrearContexto();
        await db.Usuarios.Where(u => ids.Contains(u.IdUsuario)).ExecuteDeleteAsync();
    }

    private static string Marcador() => "Pg" + Guid.NewGuid().ToString("N")[..12];

    [PostgreSqlFact]
    public async Task Listar_BusquedaYFiltros_SeTraducenASql()
    {
        var marcador = Marcador();
        var usuarios = await CrearAsync(marcador, Roles.Usuario, Roles.Usuario, Roles.Administrador);
        try
        {
            await using var db = BaseDatosPruebas.CrearContexto();
            var servicio = Servicio(db);

            var porNombre = await servicio.ListarAsync(new FiltroUsuariosAdminRequest { Busqueda = marcador.ToUpperInvariant() }, default);
            var porCorreo = await servicio.ListarAsync(new FiltroUsuariosAdminRequest { Busqueda = usuarios[1].Correo.ToUpperInvariant() }, default);
            var administradores = await servicio.ListarAsync(new FiltroUsuariosAdminRequest { Busqueda = marcador, Rol = Roles.Administrador, Estado = "ACTIVO" }, default);

            Assert.Equal(3, porNombre.Valor!.TotalElementos);
            Assert.Equal(new[] { usuarios[2].IdUsuario, usuarios[1].IdUsuario, usuarios[0].IdUsuario }, porNombre.Valor.Elementos.Select(u => u.IdUsuario).ToArray());
            Assert.Equal(new[] { usuarios[1].IdUsuario }, porCorreo.Valor!.Elementos.Select(u => u.IdUsuario).ToArray());
            Assert.Equal(new[] { usuarios[2].IdUsuario }, administradores.Valor!.Elementos.Select(u => u.IdUsuario).ToArray());
            Assert.All(porNombre.Valor.Elementos, u => Assert.Equal(DateTimeKind.Utc, u.FechaCreacion.Kind));
        }
        finally
        {
            await BorrarAsync(usuarios);
        }
    }

    /// <summary>
    /// Los cambios de rol y estado se serializan con pg_advisory_xact_lock(14, 26): mientras otra transacción tiene el
    /// bloqueo, el cambio espera; al liberarse, continúa y vuelve a contar los administradores activos ya confirmados.
    /// Así dos administradores no pueden degradarse o desactivarse mutuamente a la vez (RN-BD-09).
    /// </summary>
    [PostgreSqlFact]
    public async Task CambiosDeRolYEstado_SeSerializanConElBloqueoDeAdministradores()
    {
        var marcador = Marcador();
        var usuarios = await CrearAsync(marcador, Roles.Administrador, Roles.Administrador);
        var (adminA, adminB) = (usuarios[0], usuarios[1]);
        try
        {
            await using var bloqueador = BaseDatosPruebas.CrearContexto();
            await using (var transaccion = await bloqueador.Database.BeginTransactionAsync())
            {
                await bloqueador.Database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_xact_lock({UsuarioAdminService.ClaveBloqueoModulo}, {UsuarioAdminService.ClaveBloqueoAdministradores})");

                await using var db = BaseDatosPruebas.CrearContexto();
                var cambio = Servicio(db).CambiarEstadoAsync(adminA.IdUsuario, adminB.IdUsuario, false, "127.0.0.1", default);

                await Task.Delay(TimeSpan.FromMilliseconds(800));
                Assert.False(cambio.IsCompleted);   // espera al bloqueo

                await transaccion.RollbackAsync();   // libera el bloqueo
                var resultado = await cambio.WaitAsync(TimeSpan.FromSeconds(15));

                Assert.True(resultado.Exitoso);
                Assert.False(resultado.Valor!.Activo);
            }

            await using var lectura = BaseDatosPruebas.CrearContexto();
            var guardado = await lectura.Usuarios.AsNoTracking().SingleAsync(u => u.IdUsuario == adminB.IdUsuario);
            Assert.False(guardado.Activo);
            Assert.NotEqual(adminB.SelloSeguridad, guardado.SelloSeguridad);
        }
        finally
        {
            await BorrarAsync(usuarios);
        }
    }

    /// <summary>Un rechazo dentro de la transacción (aquí, usuario inexistente) la revierte y libera el bloqueo para el siguiente cambio.</summary>
    [PostgreSqlFact]
    public async Task Rechazo_RevierteYLiberaElBloqueo()
    {
        var marcador = Marcador();
        var usuarios = await CrearAsync(marcador, Roles.Administrador, Roles.Usuario);
        try
        {
            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                var inexistente = await Servicio(db).CambiarRolAsync(usuarios[0].IdUsuario, long.MaxValue, Roles.Usuario, "127.0.0.1", default);
                Assert.False(inexistente.Exitoso);
            }

            await using var otro = BaseDatosPruebas.CrearContexto();
            var siguiente = await Servicio(otro)
                .CambiarRolAsync(usuarios[0].IdUsuario, usuarios[1].IdUsuario, Roles.Administrador, "127.0.0.1", default)
                .WaitAsync(TimeSpan.FromSeconds(15));

            Assert.True(siguiente.Exitoso);
            Assert.Equal(Roles.Administrador, siguiente.Valor!.Rol);
        }
        finally
        {
            await BorrarAsync(usuarios);
        }
    }
}
