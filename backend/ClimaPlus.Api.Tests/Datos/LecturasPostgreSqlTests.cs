using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Dispositivos;
using ClimaPlus.Api.DTOs.Lecturas;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Alertas;
using ClimaPlus.Api.Services.Dispositivos;
using ClimaPlus.Api.Services.Lecturas;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Fase 7 contra PostgreSQL real, SOLO en la base aislada (*_test, variable CLIMAPLUS_TEST_DB): las lecturas y sus
/// alertas cumplen los CHECK, la proyección de dispositivos se traduce a SQL y los datos se borran al terminar.
/// </summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class LecturasPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    private static DispositivoService Dispositivos(ClimaPlusDbContext db) =>
        new(db, TimeProvider.System, NullLogger<DispositivoService>.Instance);

    private static LecturaService Lecturas(ClimaPlusDbContext db) =>
        new(db, new MotorAlertas(db, TimeProvider.System, NullLogger<MotorAlertas>.Instance), TimeProvider.System, NullLogger<LecturaService>.Instance);

    private static async Task<(Usuario Usuario, Ubicacion Ubicacion)> CrearDuenoAsync()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        var ubicacion = new Ubicacion
        {
            Nombre = "Cobán",
            Pais = "GT",
            Latitud = Math.Round((decimal)Random.Shared.NextDouble() * 80m, 4),
            Longitud = Math.Round((decimal)Random.Shared.NextDouble() * 170m, 4),
            FechaCreacion = DateTime.UtcNow,
        };
        var usuario = new Usuario
        {
            Nombre = "Prueba lecturas",
            Correo = $"lecturas-{Guid.NewGuid():N}@ejemplo.com",
            PasswordHash = "no-es-un-hash-real",
            Rol = Roles.Usuario,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        };
        db.AddRange(ubicacion, usuario);
        await db.SaveChangesAsync();
        db.ConfiguracionesAlerta.AddRange(
            new ConfiguracionAlerta { IdUsuario = usuario.IdUsuario, Tipo = TiposAlerta.TemperaturaAlta, ValorUmbral = 30m, Activa = true, FechaCreacion = DateTime.UtcNow },
            new ConfiguracionAlerta { IdUsuario = usuario.IdUsuario, IdUbicacion = ubicacion.IdUbicacion, Tipo = TiposAlerta.Lluvia, Activa = true, FechaCreacion = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return (usuario, ubicacion);
    }

    private static async Task LimpiarAsync(long idUsuario, long idUbicacion)
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await db.Alertas.Where(a => a.IdUsuario == idUsuario).ExecuteDeleteAsync();
        await db.LecturasSensor.Where(l => l.Dispositivo.IdUsuario == idUsuario).ExecuteDeleteAsync();
        await db.Dispositivos.Where(d => d.IdUsuario == idUsuario).ExecuteDeleteAsync();
        await db.ConfiguracionesAlerta.Where(c => c.IdUsuario == idUsuario).ExecuteDeleteAsync();
        await db.Usuarios.Where(u => u.IdUsuario == idUsuario).ExecuteDeleteAsync();
        await db.Ubicaciones.Where(u => u.IdUbicacion == idUbicacion).ExecuteDeleteAsync();
    }

    [PostgreSqlFact]
    public async Task RegistrarDispositivo_EnviarLectura_GuardaTodoYCumpleLosCheck()
    {
        var (usuario, ubicacion) = await CrearDuenoAsync();
        try
        {
            DispositivoRegistradoResponse registrado;
            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                var resultado = await Dispositivos(db).RegistrarAsync(usuario.IdUsuario,
                    new CrearDispositivoRequest { Nombre = "ESP32 Patio", IdUbicacion = ubicacion.IdUbicacion }, default);
                Assert.True(resultado.Exitoso);
                registrado = resultado.Valor!;
            }

            var autenticado = new DispositivoAutenticado(registrado.Dispositivo.IdDispositivo, usuario.IdUsuario, ubicacion.IdUbicacion, ubicacion.Nombre);
            LecturaRegistradaResponse respuesta;
            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                var resultado = await Lecturas(db).RegistrarAsync(autenticado,
                    new LecturaRequest { Temperatura = 31.256m, Humedad = 77.5m, Lluvia = true, Luz = 12 }, default);
                Assert.True(resultado.Exitoso);
                respuesta = resultado.Valor!;
            }

            Assert.Equal(new[] { TiposAlerta.TemperaturaAlta, TiposAlerta.Lluvia }, respuesta.AlertasGeneradas.Select(a => a.Tipo).ToArray());

            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                // Lo guardado en PostgreSQL: hash de 64 caracteres, lectura redondeada, última conexión y alertas con origen ESP32.
                var dispositivo = await db.Dispositivos.AsNoTracking().SingleAsync(d => d.IdDispositivo == autenticado.IdDispositivo);
                Assert.Equal(ApiKeyDispositivo.Hash(registrado.ApiKey), dispositivo.ApiKeyHash);
                var lectura = await db.LecturasSensor.AsNoTracking().SingleAsync(l => l.IdLectura == respuesta.IdLectura);
                Assert.Equal(31.26m, lectura.Temperatura);
                Assert.Equal(lectura.FechaLectura, dispositivo.UltimaConexion);
                Assert.Equal(respuesta.FechaLectura, lectura.FechaLectura);
                var alertas = await db.Alertas.AsNoTracking().Where(a => a.IdUsuario == usuario.IdUsuario).ToListAsync();
                Assert.Equal(2, alertas.Count);
                Assert.All(alertas, a =>
                {
                    Assert.Equal(respuesta.IdLectura, a.IdLectura);
                    Assert.Null(a.IdConsulta);
                });

                // La proyección con la última lectura (subconsulta) se traduce a SQL.
                var lista = await Dispositivos(db).ListarAsync(usuario.IdUsuario, default);
                var listado = Assert.Single(lista.Valor!);
                Assert.True(listado.EnLinea);
                Assert.Equal(respuesta.IdLectura, listado.UltimaLectura!.IdLectura);
                Assert.Equal(77.5m, listado.UltimaLectura.Humedad);

                var paginaLecturas = await Dispositivos(db).ListarLecturasAsync(usuario.IdUsuario, autenticado.IdDispositivo, 1, default);
                Assert.Equal(1, paginaLecturas.Valor!.TotalElementos);
                var paginaAlertas = await new AlertaService(db, NullLogger<AlertaService>.Instance)
                    .ListarPorDispositivoAsync(usuario.IdUsuario, autenticado.IdDispositivo, 1, default);
                Assert.Equal(2, paginaAlertas.Valor!.TotalElementos);
            }
        }
        finally
        {
            await LimpiarAsync(usuario.IdUsuario, ubicacion.IdUbicacion);
        }
    }

    [PostgreSqlFact]
    public async Task NombreDuplicado_LoRechazaElServicio_YLaRestriccionUnica()
    {
        var (usuario, ubicacion) = await CrearDuenoAsync();
        try
        {
            await using var db = BaseDatosPruebas.CrearContexto();
            var servicio = Dispositivos(db);
            var solicitud = new CrearDispositivoRequest { Nombre = "Repetido", IdUbicacion = ubicacion.IdUbicacion };
            Assert.True((await servicio.RegistrarAsync(usuario.IdUsuario, solicitud, default)).Exitoso);

            var repetido = await servicio.RegistrarAsync(usuario.IdUsuario, solicitud, default);

            Assert.False(repetido.Exitoso);
            Assert.Equal("nombre", repetido.Error!.Campo);

            // uq_dispositivo_usuario_nombre también lo impide si se salta la validación previa.
            db.Dispositivos.Add(new Dispositivo
            {
                IdUsuario = usuario.IdUsuario,
                IdUbicacion = ubicacion.IdUbicacion,
                Nombre = "Repetido",
                ApiKeyHash = ApiKeyDispositivo.Hash(ApiKeyDispositivo.Generar()),
                FechaCreacion = DateTime.UtcNow,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await LimpiarAsync(usuario.IdUsuario, ubicacion.IdUbicacion);
        }
    }

    [PostgreSqlFact]
    public async Task DispositivoDesactivadoDentroDeLaTransaccion_NoGuardaLectura()
    {
        var (usuario, ubicacion) = await CrearDuenoAsync();
        try
        {
            long idDispositivo;
            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                var registrado = await Dispositivos(db).RegistrarAsync(usuario.IdUsuario,
                    new CrearDispositivoRequest { Nombre = "Revocado", IdUbicacion = ubicacion.IdUbicacion }, default);
                idDispositivo = registrado.Valor!.Dispositivo.IdDispositivo;
                await Dispositivos(db).CambiarEstadoAsync(usuario.IdUsuario, idDispositivo, new CambiarEstadoDispositivoRequest { Activo = false }, default);
            }

            // Simula que el filtro lo autenticó justo antes de que el dueño lo desactivara.
            await using (var db = BaseDatosPruebas.CrearContexto())
            {
                var resultado = await Lecturas(db).RegistrarAsync(
                    new DispositivoAutenticado(idDispositivo, usuario.IdUsuario, ubicacion.IdUbicacion, ubicacion.Nombre),
                    new LecturaRequest { Temperatura = 40m }, default);

                Assert.False(resultado.Exitoso);
            }

            await using var verificacion = BaseDatosPruebas.CrearContexto();
            Assert.False(await verificacion.LecturasSensor.AnyAsync(l => l.IdDispositivo == idDispositivo));
            Assert.False(await verificacion.Alertas.AnyAsync(a => a.IdUsuario == usuario.IdUsuario));
        }
        finally
        {
            await LimpiarAsync(usuario.IdUsuario, ubicacion.IdUbicacion);
        }
    }
}
