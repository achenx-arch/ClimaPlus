using System.Text.RegularExpressions;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.Integraciones.OpenWeather;
using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;
using ClimaPlus.Api.Services.Clima.Modelos;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Búsqueda de ubicaciones (Fase 2E, M02). Flujo conforme a la guía académica (sección 4):
/// Geocoding → normalizar → guardar o reutilizar en ubicacion → leer de la base de datos → responder.
/// D-22: las ubicaciones solo se crean con datos de Geocoding, nunca con texto del cliente.
/// </summary>
public sealed partial class UbicacionService
{
    public const int NombreMax = 100;   // ubicacion.nombre VARCHAR(100)
    public const int RegionMax = 100;   // ubicacion.region VARCHAR(100)

    private readonly ClimaPlusDbContext _db;
    private readonly IOpenWeatherClient _openWeather;
    private readonly CacheBusquedaUbicaciones _cache;
    private readonly ILogger<UbicacionService> _log;

    public UbicacionService(ClimaPlusDbContext db, IOpenWeatherClient openWeather, CacheBusquedaUbicaciones cache, ILogger<UbicacionService> log)
    {
        _db = db;
        _openWeather = openWeather;
        _cache = cache;
        _log = log;
    }

    public async Task<Resultado<IReadOnlyList<UbicacionResponse>>> BuscarAsync(string consulta, int limite, CancellationToken cancelacion)
    {
        var texto = Normalizacion.Nombre(consulta);

        if (!_cache.IntentarObtener(texto, limite, out var candidatas))
        {
            var respuesta = await _openWeather.BuscarUbicacionesAsync(texto, limite, cancelacion);
            if (!respuesta.Exitoso)
            {
                return Resultado<IReadOnlyList<UbicacionResponse>>.Falla(ErroresClima.Desde(respuesta.Error!.Value));
            }

            var recibidas = respuesta.Valor!;
            candidatas = Normalizar(recibidas);
            if (candidatas.Count < recibidas.Count)
            {
                _log.LogInformation("Clima: geocoding_descartados cantidad={Cantidad}", recibidas.Count - candidatas.Count);
            }

            _cache.Guardar(texto, limite, candidatas);
        }

        var respuestas = new List<UbicacionResponse>(candidatas!.Count);
        foreach (var candidata in candidatas.Take(limite))
        {
            var ubicacion = await ObtenerOCrearAsync(candidata, cancelacion);
            respuestas.Add(UbicacionResponse.Desde(ubicacion));
        }

        return Resultado<IReadOnlyList<UbicacionResponse>>.Ok(respuestas);
    }

    /// <summary>
    /// Valida cada resultado contra las restricciones de ubicacion y descarta los inválidos.
    /// Coordenadas redondeadas a 4 decimales (D-07); sin coordenadas repetidas (RN-BD-10).
    /// </summary>
    public static IReadOnlyList<UbicacionNormalizada> Normalizar(IEnumerable<GeocodingUbicacion?> resultados)
    {
        var lista = new List<UbicacionNormalizada>();

        foreach (var r in resultados)
        {
            if (r is null)
            {
                continue;
            }

            var nombreLocal = r.NombresLocales is not null && r.NombresLocales.TryGetValue("es", out var es) ? es : null;
            var nombre = Normalizacion.Nombre(string.IsNullOrWhiteSpace(nombreLocal) ? r.Nombre ?? string.Empty : nombreLocal);
            if (nombre.Length == 0)
            {
                continue;
            }

            if (nombre.Length > NombreMax)
            {
                nombre = nombre[..NombreMax].TrimEnd();
            }

            var pais = (r.Pais ?? string.Empty).Trim().ToUpperInvariant();
            if (!PatronPais().IsMatch(pais))
            {
                continue;
            }

            if (r.Latitud is not { } lat || lat < -90m || lat > 90m || r.Longitud is not { } lon || lon < -180m || lon > 180m)
            {
                continue;
            }

            var region = Normalizacion.Nombre(r.Estado ?? string.Empty);

            lista.Add(new UbicacionNormalizada(
                nombre,
                region.Length is > 0 and <= RegionMax ? region : null,
                pais,
                Math.Round(lat, 4, MidpointRounding.AwayFromZero),
                Math.Round(lon, 4, MidpointRounding.AwayFromZero)));
        }

        return lista.DistinctBy(u => (u.Latitud, u.Longitud)).ToList();
    }

    /// <summary>
    /// Reutiliza la ubicación si ya existen esas coordenadas; si no, la crea y la vuelve a leer.
    /// Si otra petición la crea al mismo tiempo, uq_ubicacion_latitud_longitud rechaza el
    /// segundo INSERT y se lee la fila que ganó.
    /// </summary>
    private async Task<Ubicacion> ObtenerOCrearAsync(UbicacionNormalizada candidata, CancellationToken cancelacion)
    {
        var existente = await BuscarPorCoordenadasAsync(candidata, cancelacion);
        if (existente is not null)
        {
            return existente;
        }

        var nueva = new Ubicacion
        {
            Nombre = candidata.Nombre,
            Region = candidata.Region,
            Pais = candidata.Pais,
            Latitud = candidata.Latitud,
            Longitud = candidata.Longitud,
            FechaCreacion = DateTime.UtcNow,
        };
        _db.Ubicaciones.Add(nueva);

        try
        {
            await _db.SaveChangesAsync(cancelacion);
            _log.LogInformation("Clima: ubicacion_creada idUbicacion={IdUbicacion}", nueva.IdUbicacion);
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            _db.Entry(nueva).State = EntityState.Detached;
        }

        // Se responde con la fila persistida, no con el objeto en memoria.
        return await BuscarPorCoordenadasAsync(candidata, cancelacion)
            ?? throw new InvalidOperationException("No se pudo leer la ubicación después de guardarla.");
    }

    private Task<Ubicacion?> BuscarPorCoordenadasAsync(UbicacionNormalizada candidata, CancellationToken cancelacion) =>
        _db.Ubicaciones
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Latitud == candidata.Latitud && u.Longitud == candidata.Longitud, cancelacion);

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex PatronPais();
}
