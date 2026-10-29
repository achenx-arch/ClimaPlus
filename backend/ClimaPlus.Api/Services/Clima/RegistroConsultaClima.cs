using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Services.Clima.Modelos;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Implementación de <see cref="IRegistroConsultaClima"/> sobre consulta_clima (E-02 y E-03).
///
/// Caché en la base de datos (D-23): cuando una consulta se atiende con datos vigentes, se guarda
/// una fila nueva con los mismos valores (y el mismo detalle). La antigüedad de esos datos se mide
/// desde la PRIMERA fila con ese detalle, es decir, desde que se pidieron a OpenWeather; así las
/// copias no alargan la vigencia.
/// </summary>
public sealed class RegistroConsultaClima : IRegistroConsultaClima
{
    private readonly ClimaPlusDbContext _db;
    private readonly TimeProvider _reloj;
    private readonly ILogger<RegistroConsultaClima> _log;

    public RegistroConsultaClima(ClimaPlusDbContext db, TimeProvider reloj, ILogger<RegistroConsultaClima> log)
    {
        _db = db;
        _reloj = reloj;
        _log = log;
    }

    private DateTime AhoraUtc => _reloj.GetUtcNow().UtcDateTime;

    public async Task<ClimaActualNormalizado?> BuscarActualVigenteAsync(long idUbicacion, TimeSpan vigencia, CancellationToken cancelacion)
    {
        var fila = await BuscarVigenteAsync(idUbicacion, TiposConsulta.Actual, vigencia, cancelacion);
        return fila is null ? null : LeerActual(fila);
    }

    public async Task<DetallePronostico?> BuscarPronosticoVigenteAsync(long idUbicacion, TimeSpan vigencia, CancellationToken cancelacion)
    {
        var fila = await BuscarVigenteAsync(idUbicacion, TiposConsulta.Pronostico, vigencia, cancelacion);
        return fila is null ? null : LeerPronostico(fila);
    }

    public async Task<ClimaActualGuardado> RegistrarActualAsync(
        long idUbicacion, long? idUsuario, ClimaActualNormalizado clima, bool desdeCache, CancellationToken cancelacion)
    {
        var fila = new ConsultaClima
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            TipoConsulta = TiposConsulta.Actual,
            Temperatura = clima.Temperatura,
            SensacionTermica = clima.SensacionTermica,
            Humedad = clima.Humedad,
            VelocidadViento = clima.VelocidadViento,
            CodigoCondicion = clima.CodigoCondicion,
            DescripcionCondicion = clima.Descripcion,
            Detalle = DetalleSerializador.Serializar(clima.Detalle),
            FechaConsulta = AhoraUtc,
        };

        var guardada = await GuardarYLeerAsync(fila, cancelacion);
        var leido = LeerActual(guardada)
            ?? throw new InvalidOperationException($"La consulta {guardada.IdConsulta} no se pudo leer después de guardarla.");

        return new ClimaActualGuardado(guardada.IdConsulta, guardada.FechaConsulta, desdeCache, leido);
    }

    public async Task<PronosticoGuardado> RegistrarPronosticoAsync(
        long idUbicacion, long? idUsuario, DetallePronostico pronostico, bool desdeCache, CancellationToken cancelacion)
    {
        var fila = new ConsultaClima
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            TipoConsulta = TiposConsulta.Pronostico,
            Detalle = DetalleSerializador.Serializar(pronostico),   // RN-BD-43: obligatorio en PRONOSTICO
            FechaConsulta = AhoraUtc,
        };

        var guardada = await GuardarYLeerAsync(fila, cancelacion);
        var leido = LeerPronostico(guardada)
            ?? throw new InvalidOperationException($"La consulta {guardada.IdConsulta} no se pudo leer después de guardarla.");

        return new PronosticoGuardado(guardada.IdConsulta, guardada.FechaConsulta, desdeCache, leido);
    }

    // ------------------------------------------------------------------ Lectura y caché

    /// <summary>
    /// Fila más reciente del tipo y la ubicación dentro de la vigencia, cuyo detalle se obtuvo
    /// de OpenWeather (primera fila con ese mismo detalle) también dentro de la vigencia.
    /// </summary>
    private async Task<ConsultaClima?> BuscarVigenteAsync(long idUbicacion, string tipo, TimeSpan vigencia, CancellationToken cancelacion)
    {
        var limite = AhoraUtc - vigencia;

        var reciente = await _db.ConsultasClima
            .AsNoTracking()
            .Where(c => c.IdUbicacion == idUbicacion && c.TipoConsulta == tipo && c.Detalle != null && c.FechaConsulta >= limite)
            .OrderByDescending(c => c.FechaConsulta)
            .ThenByDescending(c => c.IdConsulta)
            .FirstOrDefaultAsync(cancelacion);

        if (reciente is null)
        {
            return null;
        }

        var obtenidaDelProveedor = await _db.ConsultasClima
            .Where(c => c.IdUbicacion == idUbicacion && c.TipoConsulta == tipo && c.Detalle == reciente.Detalle)
            .MinAsync(c => c.FechaConsulta, cancelacion);

        return obtenidaDelProveedor >= limite ? reciente : null;
    }

    private async Task<ConsultaClima> GuardarYLeerAsync(ConsultaClima fila, CancellationToken cancelacion)
    {
        _db.ConsultasClima.Add(fila);
        await _db.SaveChangesAsync(cancelacion);
        _db.Entry(fila).State = EntityState.Detached;

        // Se responde con lo que quedó en la base de datos, no con el objeto en memoria (guía, sección 4).
        return await _db.ConsultasClima.AsNoTracking().SingleAsync(c => c.IdConsulta == fila.IdConsulta, cancelacion);
    }

    /// <summary>Reconstruye el clima actual a partir de las columnas y el detalle. null si la fila no es utilizable.</summary>
    private ClimaActualNormalizado? LeerActual(ConsultaClima fila)
    {
        if (fila.Temperatura is not { } temperatura || fila.SensacionTermica is not { } sensacion || fila.Humedad is not { } humedad
            || fila.VelocidadViento is not { } viento || fila.CodigoCondicion is not { } codigo || fila.DescripcionCondicion is not { } descripcion)
        {
            return null;
        }

        var detalle = Deserializar<DetalleClimaActual>(fila);
        if (detalle is null)
        {
            return null;
        }

        return new ClimaActualNormalizado
        {
            Temperatura = temperatura,
            SensacionTermica = sensacion,
            Humedad = humedad,
            VelocidadViento = viento,
            CodigoCondicion = codigo,
            Descripcion = descripcion,
            Detalle = detalle,
        };
    }

    private DetallePronostico? LeerPronostico(ConsultaClima fila)
    {
        var pronostico = Deserializar<DetallePronostico>(fila);
        return pronostico is { Dias.Count: > 0 } ? pronostico : null;
    }

    private T? Deserializar<T>(ConsultaClima fila)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(fila.Detalle))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(fila.Detalle, DetalleSerializador.Opciones);
        }
        catch (JsonException)
        {
            _log.LogWarning("Clima: detalle_ilegible idConsulta={IdConsulta}", fila.IdConsulta);
            return null;
        }
    }
}
