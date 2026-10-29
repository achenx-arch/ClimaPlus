using ClimaPlus.Api.Services.Clima.Modelos;
using Microsoft.Extensions.Caching.Memory;

namespace ClimaPlus.Api.Services.Clima;

/// <summary>
/// Caché en memoria de búsquedas de Geocoding ya normalizadas (D-23: IMemoryCache solo para
/// repetir búsquedas). Usa una instancia propia con tamaño máximo para que muchas búsquedas
/// distintas no consuman memoria sin límite. Solo guarda respuestas exitosas.
/// Los datos del clima NO se guardan aquí: su caché es la base de datos (Categoría B).
/// </summary>
public sealed class CacheBusquedaUbicaciones : IDisposable
{
    public const int MaximoEntradas = 500;

    public static readonly TimeSpan Duracion = TimeSpan.FromHours(1);

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = MaximoEntradas });

    public bool IntentarObtener(string consulta, int limite, out IReadOnlyList<UbicacionNormalizada>? ubicaciones) =>
        _cache.TryGetValue(Clave(consulta, limite), out ubicaciones);

    public void Guardar(string consulta, int limite, IReadOnlyList<UbicacionNormalizada> ubicaciones) =>
        _cache.Set(Clave(consulta, limite), ubicaciones, new MemoryCacheEntryOptions
        {
            Size = 1,
            AbsoluteExpirationRelativeToNow = Duracion,
        });

    public void Dispose() => _cache.Dispose();

    private static string Clave(string consulta, int limite) => $"{limite}|{consulta.ToLowerInvariant()}";
}
