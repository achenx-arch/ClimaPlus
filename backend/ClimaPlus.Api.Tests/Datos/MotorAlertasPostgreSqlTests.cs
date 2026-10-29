using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Services.Alertas;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClimaPlus.Api.Tests.Datos;

/// <summary>
/// Motor de alertas contra PostgreSQL real, SOLO en la base aislada (*_test, variable CLIMAPLUS_TEST_DB).
/// Se omiten si la variable no existe. Nunca se usa climaplus_db.
/// </summary>
[Trait("Categoria", BaseDatosPruebas.Categoria)]
public sealed class MotorAlertasPostgreSqlTests : IClassFixture<BaseDatosPruebasFixture>
{
    private static MotorAlertas Motor(ClimaPlusDbContext db) => new(db, TimeProvider.System, NullLogger<MotorAlertas>.Instance);

    private static (Ubicacion Ubicacion, Usuario Usuario) Datos(string prefijo) => (
        new Ubicacion
        {
            Nombre = "Cobán",
            Pais = "GT",
            Latitud = Math.Round((decimal)Random.Shared.NextDouble() * 80m, 4),
            Longitud = Math.Round((decimal)Random.Shared.NextDouble() * 170m, 4),
            FechaCreacion = DateTime.UtcNow,
        },
        new Usuario
        {
            Nombre = "Prueba motor",
            Correo = $"{prefijo}-{Guid.NewGuid():N}@ejemplo.com",
            PasswordHash = "no-es-un-hash-real",
            Rol = Roles.Usuario,
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        });

    private static ConsultaClima ConsultaActual(long idUsuario, long idUbicacion, decimal temperatura, short codigo) => new()
    {
        IdUsuario = idUsuario,
        IdUbicacion = idUbicacion,
        TipoConsulta = TiposConsulta.Actual,
        Temperatura = temperatura,
        SensacionTermica = temperatura,
        Humedad = 80,
        VelocidadViento = 2m,
        CodigoCondicion = codigo,
        DescripcionCondicion = "lluvia ligera",
        FechaConsulta = DateTime.UtcNow,
    };

    private static ConsultaActualEvaluable Evaluable(ConsultaClima consulta) =>
        new(consulta.IdUsuario!.Value, consulta.IdUbicacion, "Cobán", consulta.IdConsulta, consulta.Temperatura!.Value, consulta.CodigoCondicion!.Value);

    [PostgreSqlFact]
    public async Task AlertasGeneradas_CumplenLosCheck_YElAntiDuplicadoSeTraduceASql()
    {
        await using var db = BaseDatosPruebas.CrearContexto();
        await using var tx = await db.Database.BeginTransactionAsync();   // se revierte: no deja datos
        var (ubicacion, usuario) = Datos("motor-check");
        db.AddRange(ubicacion, usuario);
        await db.SaveChangesAsync();
        db.ConfiguracionesAlerta.AddRange(
            new ConfiguracionAlerta { IdUsuario = usuario.IdUsuario, Tipo = TiposAlerta.TemperaturaAlta, ValorUmbral = 30m, Activa = true, FechaCreacion = DateTime.UtcNow },
            new ConfiguracionAlerta { IdUsuario = usuario.IdUsuario, IdUbicacion = ubicacion.IdUbicacion, Tipo = TiposAlerta.Lluvia, Activa = true, FechaCreacion = DateTime.UtcNow });
        var consulta = ConsultaActual(usuario.IdUsuario, ubicacion.IdUbicacion, 31.25m, 501);
        db.ConsultasClima.Add(consulta);
        await db.SaveChangesAsync();
        var motor = Motor(db);

        await motor.BloquearUsuarioAsync(usuario.IdUsuario, default);
        var generadas = await motor.EvaluarConsultaActualAsync(Evaluable(consulta), default);

        Assert.Equal(new[] { TiposAlerta.TemperaturaAlta, TiposAlerta.Lluvia }, generadas.Select(a => a.Tipo).ToArray());
        db.ChangeTracker.Clear();
        var guardadas = await db.Alertas.AsNoTracking().Where(a => a.IdUsuario == usuario.IdUsuario).OrderBy(a => a.IdAlerta).ToListAsync();
        Assert.Equal(2, guardadas.Count);
        Assert.All(guardadas, a =>
        {
            Assert.Equal(consulta.IdConsulta, a.IdConsulta);
            Assert.Null(a.IdLectura);
            Assert.Equal(EstadosAlerta.Nueva, a.Estado);
        });
        Assert.Equal(31.25m, guardadas[0].ValorDetectado);
        Assert.Equal(30m, guardadas[0].ValorUmbral);
        Assert.Null(guardadas[1].ValorDetectado);
        Assert.Null(guardadas[1].ValorUmbral);
        Assert.Equal(generadas.Select(g => g.FechaGeneracion).ToArray(), guardadas.Select(a => a.FechaGeneracion).ToArray());

        // Una segunda consulta dentro de la ventana no genera nada (consulta SQL real del anti-duplicado).
        var segunda = ConsultaActual(usuario.IdUsuario, ubicacion.IdUbicacion, 32m, 500);
        db.ConsultasClima.Add(segunda);
        await db.SaveChangesAsync();
        Assert.Empty(await motor.EvaluarConsultaActualAsync(Evaluable(segunda), default));

        await tx.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task BloqueoSinTransaccion_EsUnErrorDeProgramacion()
    {
        await using var db = BaseDatosPruebas.CrearContexto();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Motor(db).BloquearUsuarioAsync(1, default));
    }

    [PostgreSqlFact]
    public async Task PeticionesSimultaneas_DelMismoUsuario_GeneranUnaSolaAlerta()
    {
        // Datos confirmados (las transacciones concurrentes deben verlos); se borran al final, solo en la base *_test.
        var (ubicacion, usuario) = Datos("motor-concurrencia");
        await using (var db = BaseDatosPruebas.CrearContexto())
        {
            db.AddRange(ubicacion, usuario);
            await db.SaveChangesAsync();
            db.ConfiguracionesAlerta.Add(new ConfiguracionAlerta
            {
                IdUsuario = usuario.IdUsuario, Tipo = TiposAlerta.Lluvia, Activa = true, FechaCreacion = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        try
        {
            const int peticiones = 5;
            var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task<int> PeticionAsync()
            {
                await salida.Task;
                await using var db = BaseDatosPruebas.CrearContexto();
                await using var tx = await db.Database.BeginTransactionAsync();
                var motor = Motor(db);
                await motor.BloquearUsuarioAsync(usuario.IdUsuario, default);
                var consulta = ConsultaActual(usuario.IdUsuario, ubicacion.IdUbicacion, 20m, 500);
                db.ConsultasClima.Add(consulta);
                await db.SaveChangesAsync();
                var generadas = await motor.EvaluarConsultaActualAsync(Evaluable(consulta), default);
                await Task.Delay(300);   // mantiene la transacción abierta para forzar la coincidencia
                await tx.CommitAsync();
                return generadas.Count;
            }

            var tareas = Enumerable.Range(0, peticiones).Select(_ => Task.Run(PeticionAsync)).ToArray();
            salida.SetResult();
            var resultados = await Task.WhenAll(tareas);

            Assert.Equal(1, resultados.Sum());
            await using var lectura = BaseDatosPruebas.CrearContexto();
            Assert.Equal(1, await lectura.Alertas.CountAsync(a => a.IdUsuario == usuario.IdUsuario));
            Assert.Equal(peticiones, await lectura.ConsultasClima.CountAsync(c => c.IdUsuario == usuario.IdUsuario));
        }
        finally
        {
            await using var limpieza = BaseDatosPruebas.CrearContexto();
            await limpieza.Alertas.Where(a => a.IdUsuario == usuario.IdUsuario).ExecuteDeleteAsync();
            await limpieza.ConsultasClima.Where(c => c.IdUsuario == usuario.IdUsuario).ExecuteDeleteAsync();
            await limpieza.ConfiguracionesAlerta.Where(c => c.IdUsuario == usuario.IdUsuario).ExecuteDeleteAsync();
            await limpieza.Usuarios.Where(u => u.IdUsuario == usuario.IdUsuario).ExecuteDeleteAsync();
            await limpieza.Ubicaciones.Where(u => u.IdUbicacion == ubicacion.IdUbicacion).ExecuteDeleteAsync();
        }
    }
}
