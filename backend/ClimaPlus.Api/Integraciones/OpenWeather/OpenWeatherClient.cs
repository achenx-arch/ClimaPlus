using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Integraciones.OpenWeather.Modelos;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Integraciones.OpenWeather;

/// <summary>
/// Cliente HTTP tipado de OpenWeather.
/// Seguridad: la API key va en el parámetro appid de la URL, así que la URL completa NUNCA
/// se registra en los logs (el logging de HttpClient está desactivado para este cliente) y
/// los mensajes del proveedor nunca se devuelven al frontend. Sin reintentos automáticos.
/// </summary>
public sealed class OpenWeatherClient : IOpenWeatherClient
{
    public const string NombreCliente = "OpenWeather";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IOptions<OpenWeatherOptions> _opciones;
    private readonly ILogger<OpenWeatherClient> _log;

    public OpenWeatherClient(HttpClient http, IOptions<OpenWeatherOptions> opciones, ILogger<OpenWeatherClient> log)
    {
        _http = http;
        _opciones = opciones;
        _log = log;
    }

    public Task<ResultadoProveedor<List<GeocodingUbicacion>>> BuscarUbicacionesAsync(string consulta, int limite, CancellationToken cancelacion) =>
        EnviarAsync<List<GeocodingUbicacion>>("geocoding", "geo/1.0/direct", new[]
        {
            KeyValuePair.Create("q", consulta),
            KeyValuePair.Create("limit", limite.ToString(CultureInfo.InvariantCulture)),
        }, cancelacion);

    public Task<ResultadoProveedor<ClimaActualProveedor>> ObtenerClimaActualAsync(decimal latitud, decimal longitud, CancellationToken cancelacion) =>
        EnviarAsync<ClimaActualProveedor>("clima_actual", "data/2.5/weather", ParametrosMeteorologicos(latitud, longitud), cancelacion);

    public Task<ResultadoProveedor<PronosticoProveedor>> ObtenerPronosticoAsync(decimal latitud, decimal longitud, CancellationToken cancelacion) =>
        EnviarAsync<PronosticoProveedor>("pronostico", "data/2.5/forecast", ParametrosMeteorologicos(latitud, longitud), cancelacion);

    private KeyValuePair<string, string>[] ParametrosMeteorologicos(decimal latitud, decimal longitud)
    {
        var opciones = _opciones.Value;
        return new[]
        {
            KeyValuePair.Create("lat", latitud.ToString(CultureInfo.InvariantCulture)),
            KeyValuePair.Create("lon", longitud.ToString(CultureInfo.InvariantCulture)),
            KeyValuePair.Create("units", opciones.Unidades),
            KeyValuePair.Create("lang", opciones.Idioma),
        };
    }

    private async Task<ResultadoProveedor<T>> EnviarAsync<T>(
        string operacion, string ruta, IEnumerable<KeyValuePair<string, string>> parametros, CancellationToken cancelacion)
        where T : class
    {
        var apiKey = _opciones.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogError("OpenWeather: operacion={Operacion} error=no_configurado (falta OpenWeather:ApiKey)", operacion);
            return ResultadoProveedor<T>.Falla(ErrorProveedor.NoConfigurado);
        }

        var consulta = string.Join("&", parametros
            .Append(KeyValuePair.Create("appid", apiKey))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, $"{ruta}?{consulta}");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, cancelacion);
        }
        catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            _log.LogWarning("OpenWeather: operacion={Operacion} error=tiempo_agotado", operacion);
            return ResultadoProveedor<T>.Falla(ErrorProveedor.TiempoAgotado);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning("OpenWeather: operacion={Operacion} error=red tipo={Tipo}", operacion, ex.GetType().Name);
            return ResultadoProveedor<T>.Falla(ErrorProveedor.NoDisponible);
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                var error = respuesta.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => ErrorProveedor.Autenticacion,
                    HttpStatusCode.NotFound => ErrorProveedor.NoEncontrado,
                    HttpStatusCode.TooManyRequests => ErrorProveedor.LimiteExcedido,
                    _ => ErrorProveedor.NoDisponible,
                };
                _log.LogWarning("OpenWeather: operacion={Operacion} error={Error} estado={Estado}", operacion, error, (int)respuesta.StatusCode);
                return ResultadoProveedor<T>.Falla(error);
            }

            try
            {
                var valor = await respuesta.Content.ReadFromJsonAsync<T>(OpcionesJson, cancelacion);
                if (valor is null)
                {
                    _log.LogWarning("OpenWeather: operacion={Operacion} error=respuesta_vacia", operacion);
                    return ResultadoProveedor<T>.Falla(ErrorProveedor.RespuestaInvalida);
                }

                return ResultadoProveedor<T>.Ok(valor);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _log.LogWarning("OpenWeather: operacion={Operacion} error=json_invalido", operacion);
                return ResultadoProveedor<T>.Falla(ErrorProveedor.RespuestaInvalida);
            }
            catch (TaskCanceledException) when (!cancelacion.IsCancellationRequested)
            {
                _log.LogWarning("OpenWeather: operacion={Operacion} error=tiempo_agotado", operacion);
                return ResultadoProveedor<T>.Falla(ErrorProveedor.TiempoAgotado);
            }
            catch (HttpRequestException ex)
            {
                _log.LogWarning("OpenWeather: operacion={Operacion} error=red tipo={Tipo}", operacion, ex.GetType().Name);
                return ResultadoProveedor<T>.Falla(ErrorProveedor.NoDisponible);
            }
        }
    }
}
