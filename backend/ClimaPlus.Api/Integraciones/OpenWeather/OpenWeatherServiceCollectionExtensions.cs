using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Integraciones.OpenWeather;

public static class OpenWeatherServiceCollectionExtensions
{
    /// <summary>
    /// Registra el cliente tipado de OpenWeather con IHttpClientFactory (D-26):
    /// HTTPS obligatorio, timeout configurable (8 s por defecto) y sin logging de URLs.
    /// La ApiKey no se valida al arrancar para que los compañeros sin clave puedan ejecutar la API;
    /// si falta, las rutas de clima responden 503.
    /// </summary>
    public static IServiceCollection AddOpenWeather(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OpenWeatherOptions>()
            .Bind(configuration.GetSection(OpenWeatherOptions.Seccion))
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps,
                "'OpenWeather:BaseUrl' debe ser una URL absoluta con https.")
            .Validate(o => o.TimeoutSegundos is > 0 and <= 30,
                "'OpenWeather:TimeoutSegundos' debe estar entre 1 y 30.")
            .ValidateOnStart();

        services
            .AddHttpClient<IOpenWeatherClient, OpenWeatherClient>(OpenWeatherClient.NombreCliente, (servicios, http) =>
            {
                var opciones = servicios.GetRequiredService<IOptions<OpenWeatherOptions>>().Value;
                http.BaseAddress = new Uri(opciones.BaseUrl.TrimEnd('/') + "/");
                http.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos);
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            // El logging por defecto de HttpClient escribe la URL completa, que incluye appid.
            .RemoveAllLoggers();

        return services;
    }
}
