using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Lecturas;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Alertas;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Lecturas;

/// <summary>
/// Recepción de lecturas del ESP32 (Fase 7, RF-24). El dispositivo ya viene autenticado por su API Key
/// (<see cref="AutenticacionDispositivoFilter"/>). En una sola transacción: guarda la lectura con la fecha del
/// servidor, actualiza <c>ultima_conexion</c> (RN-BD-32, D-12) y evalúa las reglas del dueño para la ubicación del
/// dispositivo (RN-BD-33) con el mismo motor y anti-duplicado que las consultas de OpenWeather.
/// </summary>
public sealed class LecturaService
{
    private readonly ClimaPlusDbContext _db;
    private readonly IMotorAlertas _motorAlertas;
    private readonly TimeProvider _reloj;
    private readonly ILogger<LecturaService> _log;

    public LecturaService(ClimaPlusDbContext db, IMotorAlertas motorAlertas, TimeProvider reloj, ILogger<LecturaService> log)
    {
        _db = db;
        _motorAlertas = motorAlertas;
        _reloj = reloj;
        _log = log;
    }

    public async Task<Resultado<LecturaRegistradaResponse>> RegistrarAsync(
        DispositivoAutenticado dispositivo, LecturaRequest solicitud, CancellationToken cancelacion)
    {
        // RN-BD-31: al menos una medición (también lo exige ck_lectura_sensor_al_menos_una_medicion).
        if (!solicitud.TieneMediciones())
        {
            return Resultado<LecturaRegistradaResponse>.Falla(new ErrorServicio(TipoError.Validacion,
                "La lectura debe incluir al menos una medición: temperatura, humedad, lluvia o luz."));
        }

        // Atómico: lectura, última conexión y alertas se confirman juntas o no se guarda nada (DP-6).
        await using var transaccion = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancelacion)
            : null;

        // Mismo bloqueo por usuario que GET /api/clima/actual: el anti-duplicado es común a ambos orígenes (RN-BD-27).
        await _motorAlertas.BloquearUsuarioAsync(dispositivo.IdUsuario, cancelacion);

        // Se vuelve a leer dentro de la transacción: si lo desactivaron después de autenticarlo, se rechaza (D-11).
        var registro = await _db.Dispositivos.SingleOrDefaultAsync(d => d.IdDispositivo == dispositivo.IdDispositivo, cancelacion);
        if (registro is null || !registro.Activo)
        {
            return Resultado<LecturaRegistradaResponse>.Falla(new ErrorServicio(TipoError.DispositivoDesactivado,
                "Este dispositivo está desactivado. Actívalo desde Mis dispositivos para volver a enviar lecturas."));
        }

        var fecha = Ahora();
        var lectura = new LecturaSensor
        {
            IdDispositivo = dispositivo.IdDispositivo,
            // numeric(5,2): se redondea aquí para que la respuesta y lo guardado coincidan.
            Temperatura = solicitud.Temperatura is { } t ? Math.Round(t, 2, MidpointRounding.AwayFromZero) : null,
            Humedad = solicitud.Humedad is { } h ? Math.Round(h, 2, MidpointRounding.AwayFromZero) : null,
            Lluvia = solicitud.Lluvia,
            Luz = solicitud.Luz,
            FechaLectura = fecha,                   // RN-BD-32: la asigna el servidor
        };
        _db.LecturasSensor.Add(lectura);
        registro.UltimaConexion = fecha;           // D-12: misma transacción
        await _db.SaveChangesAsync(cancelacion);

        var alertas = await _motorAlertas.EvaluarLecturaAsync(
            new LecturaEvaluable(
                dispositivo.IdUsuario,
                registro.IdUbicacion,
                dispositivo.IdUbicacion == registro.IdUbicacion ? dispositivo.NombreUbicacion : await NombreUbicacionAsync(registro.IdUbicacion, cancelacion),
                lectura.IdLectura,
                lectura.Temperatura,
                lectura.Lluvia),
            cancelacion);

        if (transaccion is not null)
        {
            await transaccion.CommitAsync(cancelacion);
        }

        _log.LogInformation("Lecturas: lectura_registrada idLectura={IdLectura} idDispositivo={IdDispositivo} alertas={Alertas}",
            lectura.IdLectura, dispositivo.IdDispositivo, alertas.Count);

        return Resultado<LecturaRegistradaResponse>.Ok(
            new LecturaRegistradaResponse(lectura.IdLectura, dispositivo.IdDispositivo, fecha, alertas));
    }

    private Task<string> NombreUbicacionAsync(long idUbicacion, CancellationToken cancelacion) =>
        _db.Ubicaciones.Where(u => u.IdUbicacion == idUbicacion).Select(u => u.Nombre).SingleAsync(cancelacion);

    /// <summary>Microsegundos, igual que timestamptz.</summary>
    private DateTime Ahora()
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        return new DateTime(ahora.Ticks - (ahora.Ticks % 10), DateTimeKind.Utc);
    }
}
