using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.DTOs.Historial;
using ClimaPlus.Api.Services.Clima.Modelos;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Historial;

/// <summary>
/// Historial de consultas del usuario autenticado (Fase 2G, M08, RF-11). Solo lectura.
/// Aislamiento: todas las consultas filtran por el id del token, así que las anónimas
/// (id_usuario NULL, E-02) y las de otros usuarios nunca aparecen (RN-BD-13).
/// Una consulta ajena responde igual que una inexistente (404).
/// </summary>
public sealed class HistorialService
{
    private readonly ClimaPlusDbContext _db;
    private readonly ILogger<HistorialService> _log;

    public HistorialService(ClimaPlusDbContext db, ILogger<HistorialService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<Resultado<PaginaResponse<ConsultaHistorialResponse>>> ListarAsync(
        long idUsuario, FiltroHistorialRequest filtro, CancellationToken cancelacion)
    {
        if (filtro.Desde is { } d && filtro.Hasta is { } h && d >= h)
        {
            return Resultado<PaginaResponse<ConsultaHistorialResponse>>.Falla(
                new ErrorServicio(TipoError.Validacion, "La fecha 'desde' debe ser anterior a 'hasta'.", "desde"));
        }

        var consulta = DelUsuario(idUsuario);

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(c => c.TipoConsulta == tipo);
        }

        if (filtro.IdUbicacion is { } idUbicacion)
        {
            consulta = consulta.Where(c => c.IdUbicacion == idUbicacion);
        }

        if (filtro.Desde is { } desde)
        {
            var desdeUtc = desde.UtcDateTime;
            consulta = consulta.Where(c => c.FechaConsulta >= desdeUtc);
        }

        if (filtro.Hasta is { } hasta)
        {
            var hastaUtc = hasta.UtcDateTime;
            consulta = consulta.Where(c => c.FechaConsulta < hastaUtc);
        }

        var total = await consulta.CountAsync(cancelacion);

        // Una sola consulta SQL con la ubicación (sin N+1); el JSONB no se lee en la lista.
        var elementos = await consulta
            .OrderByDescending(c => c.FechaConsulta)
            .ThenByDescending(c => c.IdConsulta)
            .Skip((filtro.Pagina - 1) * Paginacion.TamanoPagina)
            .Take(Paginacion.TamanoPagina)
            .Select(c => new ConsultaHistorialResponse(
                c.IdConsulta,
                c.TipoConsulta,
                c.FechaConsulta,
                new UbicacionResponse(c.Ubicacion.IdUbicacion, c.Ubicacion.Nombre, c.Ubicacion.Region, c.Ubicacion.Pais, c.Ubicacion.Latitud, c.Ubicacion.Longitud),
                c.Temperatura,
                c.Humedad,
                c.VelocidadViento,
                c.CodigoCondicion,
                c.DescripcionCondicion))
            .ToListAsync(cancelacion);

        return Resultado<PaginaResponse<ConsultaHistorialResponse>>.Ok(
            PaginaResponse<ConsultaHistorialResponse>.Crear(elementos, filtro.Pagina, total));
    }

    public async Task<Resultado<ConsultaHistorialDetalleResponse>> ObtenerAsync(long idUsuario, long idConsulta, CancellationToken cancelacion)
    {
        var fila = await DelUsuario(idUsuario)
            .Where(c => c.IdConsulta == idConsulta)
            .Select(c => new
            {
                Consulta = c,
                Ubicacion = new UbicacionResponse(c.Ubicacion.IdUbicacion, c.Ubicacion.Nombre, c.Ubicacion.Region, c.Ubicacion.Pais, c.Ubicacion.Latitud, c.Ubicacion.Longitud),
            })
            .SingleOrDefaultAsync(cancelacion);

        if (fila is null)
        {
            return Resultado<ConsultaHistorialDetalleResponse>.Falla(new ErrorServicio(TipoError.NoEncontrado, "La consulta no existe."));
        }

        var c = fila.Consulta;
        var esActual = c.TipoConsulta == TiposConsulta.Actual;

        ClimaActualHistorialResponse? actual = null;
        if (esActual && c.Temperatura is { } t && c.SensacionTermica is { } s && c.Humedad is { } hu
            && c.VelocidadViento is { } v && c.CodigoCondicion is { } co && c.DescripcionCondicion is { } de)
        {
            actual = new ClimaActualHistorialResponse(t, s, hu, v, co, de, Deserializar<DetalleClimaActual>(c));
        }

        PronosticoHistorialResponse? pronostico = null;
        if (!esActual)
        {
            var detalle = Deserializar<DetallePronostico>(c);
            pronostico = new PronosticoHistorialResponse(detalle?.ZonaHorariaSegundos, detalle?.Dias ?? Array.Empty<DiaPronostico>());
        }

        return Resultado<ConsultaHistorialDetalleResponse>.Ok(
            new ConsultaHistorialDetalleResponse(c.IdConsulta, c.TipoConsulta, c.FechaConsulta, fila.Ubicacion, actual, pronostico));
    }

    /// <summary>Punto único de aislamiento: solo consultas cuyo id_usuario es el del token (nunca NULL).</summary>
    private IQueryable<ConsultaClima> DelUsuario(long idUsuario) =>
        _db.ConsultasClima.AsNoTracking().Where(c => c.IdUsuario == idUsuario);

    /// <summary>
    /// Lee detalle con los modelos tipados del backend: solo salen los campos del formato versión 1
    /// (sección 6 de la especificación E-02/E-03); cualquier otro contenido se descarta.
    /// </summary>
    private T? Deserializar<T>(ConsultaClima consulta)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(consulta.Detalle))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(consulta.Detalle, DetalleSerializador.Opciones);
        }
        catch (JsonException)
        {
            _log.LogWarning("Historial: detalle_ilegible idConsulta={IdConsulta}", consulta.IdConsulta);
            return null;
        }
    }
}
