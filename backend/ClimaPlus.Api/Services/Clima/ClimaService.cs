using ClimaPlus.Api.Data;
using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.Integraciones.OpenWeather;
using ClimaPlus.Api.Services.Alertas;
using ClimaPlus.Api.Services.Clima.Modelos;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Clima actual (M03/M04) y pronóstico de 5 días (M05) — Fase 2E, D-20, D-23, D-24.
/// Flujo: ubicación guardada → datos vigentes en consulta_clima o, si no hay, OpenWeather →
/// validar y normalizar → GUARDAR la consulta (usuario o NULL, E-02) → responder con lo guardado.
/// Cada petición deja su propia fila: así el historial y las estadísticas cuentan todas las consultas.
/// Fase 2H: con sesión, la consulta ACTUAL y sus alertas se guardan en una sola transacción (D-02, RF-16).
/// </summary>
public sealed class ClimaService
{
    /// <summary>D-23: vigencia de los datos del clima actual.</summary>
    public static readonly TimeSpan VigenciaActual = TimeSpan.FromMinutes(10);

    /// <summary>D-23: vigencia del pronóstico.</summary>
    public static readonly TimeSpan VigenciaPronostico = TimeSpan.FromMinutes(30);

    private readonly ClimaPlusDbContext _db;
    private readonly IOpenWeatherClient _openWeather;
    private readonly IRegistroConsultaClima _registro;
    private readonly IMotorAlertas _motorAlertas;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ClimaService> _log;

    public ClimaService(
        ClimaPlusDbContext db,
        IOpenWeatherClient openWeather,
        IRegistroConsultaClima registro,
        IMotorAlertas motorAlertas,
        TimeProvider reloj,
        ILogger<ClimaService> log)
    {
        _db = db;
        _openWeather = openWeather;
        _registro = registro;
        _motorAlertas = motorAlertas;
        _reloj = reloj;
        _log = log;
    }

    public async Task<Resultado<ClimaActualResponse>> ObtenerActualAsync(long idUbicacion, long? idUsuario, CancellationToken cancelacion)
    {
        var ubicacion = await _db.Ubicaciones.AsNoTracking().SingleOrDefaultAsync(u => u.IdUbicacion == idUbicacion, cancelacion);
        if (ubicacion is null)
        {
            return Resultado<ClimaActualResponse>.Falla(UbicacionInexistente());
        }

        var clima = await _registro.BuscarActualVigenteAsync(idUbicacion, VigenciaActual, cancelacion);
        var desdeCache = clima is not null;

        if (clima is null)
        {
            var respuesta = await _openWeather.ObtenerClimaActualAsync(ubicacion.Latitud, ubicacion.Longitud, cancelacion);
            if (!respuesta.Exitoso)
            {
                return Resultado<ClimaActualResponse>.Falla(ErroresClima.Desde(respuesta.Error!.Value));
            }

            if (!ClimaTransformador.IntentarTransformarActual(respuesta.Valor, out clima, out var motivo))
            {
                _log.LogWarning("Clima: actual_invalido idUbicacion={IdUbicacion} motivo={Motivo}", idUbicacion, motivo);
                return Resultado<ClimaActualResponse>.Falla(ErroresClima.RespuestaInvalida());
            }
        }

        ClimaActualGuardado guardado;
        IReadOnlyList<AlertaGeneradaResponse> alertas;

        if (idUsuario is not { } idAutenticado)
        {
            // Visitante: se guarda con id_usuario NULL y nunca se evalúan alertas (RN-BD-42).
            guardado = await _registro.RegistrarActualAsync(idUbicacion, null, clima!, desdeCache, cancelacion);
            alertas = Array.Empty<AlertaGeneradaResponse>();
        }
        else
        {
            // Atómico: consulta y alertas se confirman juntas o no se guarda ninguna (DP-6). Si algo falla,
            // la transacción se revierte al salir del using y la excepción llega al manejador global (500).
            await using var transaccion = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancelacion)
                : null;

            await _motorAlertas.BloquearUsuarioAsync(idAutenticado, cancelacion);
            guardado = await _registro.RegistrarActualAsync(idUbicacion, idAutenticado, clima!, desdeCache, cancelacion);

            // También con datos de la caché: cada petición es una consulta ACTUAL real del usuario (DP-5).
            alertas = await _motorAlertas.EvaluarConsultaActualAsync(
                new ConsultaActualEvaluable(
                    idAutenticado,
                    idUbicacion,
                    ubicacion.Nombre,
                    guardado.IdConsulta,
                    guardado.Clima.Temperatura,
                    guardado.Clima.CodigoCondicion),
                cancelacion);

            if (transaccion is not null)
            {
                await transaccion.CommitAsync(cancelacion);
            }
        }

        _log.LogInformation("Clima: consulta_actual idConsulta={IdConsulta} idUbicacion={IdUbicacion} conSesion={ConSesion} desdeCache={DesdeCache} alertas={Alertas}",
            guardado.IdConsulta, idUbicacion, idUsuario is not null, desdeCache, alertas.Count);

        return Resultado<ClimaActualResponse>.Ok(ClimaActualResponse.Desde(UbicacionResponse.Desde(ubicacion), guardado, alertas));
    }

    public async Task<Resultado<PronosticoResponse>> ObtenerPronosticoAsync(long idUbicacion, long? idUsuario, CancellationToken cancelacion)
    {
        var ubicacion = await _db.Ubicaciones.AsNoTracking().SingleOrDefaultAsync(u => u.IdUbicacion == idUbicacion, cancelacion);
        if (ubicacion is null)
        {
            return Resultado<PronosticoResponse>.Falla(UbicacionInexistente());
        }

        var pronostico = await _registro.BuscarPronosticoVigenteAsync(idUbicacion, VigenciaPronostico, cancelacion);
        var desdeCache = pronostico is not null;

        if (pronostico is null)
        {
            var respuesta = await _openWeather.ObtenerPronosticoAsync(ubicacion.Latitud, ubicacion.Longitud, cancelacion);
            if (!respuesta.Exitoso)
            {
                return Resultado<PronosticoResponse>.Falla(ErroresClima.Desde(respuesta.Error!.Value));
            }

            if (!ClimaTransformador.IntentarTransformarPronostico(respuesta.Valor, _reloj.GetUtcNow().UtcDateTime, out pronostico, out var motivo))
            {
                _log.LogWarning("Clima: pronostico_invalido idUbicacion={IdUbicacion} motivo={Motivo}", idUbicacion, motivo);
                return Resultado<PronosticoResponse>.Falla(ErroresClima.RespuestaInvalida());
            }
        }

        var guardado = await _registro.RegistrarPronosticoAsync(idUbicacion, idUsuario, pronostico!, desdeCache, cancelacion);

        _log.LogInformation("Clima: consulta_pronostico idConsulta={IdConsulta} idUbicacion={IdUbicacion} conSesion={ConSesion} desdeCache={DesdeCache}",
            guardado.IdConsulta, idUbicacion, idUsuario is not null, desdeCache);

        return Resultado<PronosticoResponse>.Ok(PronosticoResponse.Desde(UbicacionResponse.Desde(ubicacion), guardado));
    }

    private static ErrorServicio UbicacionInexistente() =>
        new(TipoError.NoEncontrado, "La ubicación no existe. Búscala de nuevo.");
}
