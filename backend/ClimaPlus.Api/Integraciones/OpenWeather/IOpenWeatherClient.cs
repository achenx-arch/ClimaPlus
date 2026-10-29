using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;

namespace ClimaPlus.Api.Integraciones.OpenWeather;

/// <summary>
/// Acceso a OpenWeather (plan gratuito: Geocoding, Current Weather y 5 day / 3 hour Forecast).
/// Devuelve los datos tal como llegan; la validación y la normalización están en los servicios.
/// </summary>
public interface IOpenWeatherClient
{
    Task<ResultadoProveedor<List<GeocodingUbicacion>>> BuscarUbicacionesAsync(string consulta, int limite, CancellationToken cancelacion);

    Task<ResultadoProveedor<ClimaActualProveedor>> ObtenerClimaActualAsync(decimal latitud, decimal longitud, CancellationToken cancelacion);

    Task<ResultadoProveedor<PronosticoProveedor>> ObtenerPronosticoAsync(decimal latitud, decimal longitud, CancellationToken cancelacion);
}
