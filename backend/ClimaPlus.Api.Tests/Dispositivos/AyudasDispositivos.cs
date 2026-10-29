using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests.Dispositivos;

/// <summary>Ayudas de la Fase 7: registrar dispositivos por la API y enviar lecturas como lo haría el ESP32.</summary>
public static class AyudasDispositivos
{
    public const string Ruta = "/api/dispositivos";
    public const string RutaLecturas = "/api/lecturas";

    private static int _secuencia;

    public static string NombreUnico() => $"ESP32 Patio {Interlocked.Increment(ref _secuencia)}";

    public static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, string token, string nombre, long idUbicacion) =>
        cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, Ruta, token, new { nombre, idUbicacion }));

    /// <summary>Registra un dispositivo y devuelve su id y la API Key (mostrada una sola vez).</summary>
    public static async Task<(long IdDispositivo, string ApiKey)> CrearAsync(HttpClient cliente, string token, long idUbicacion, string? nombre = null)
    {
        var respuesta = await RegistrarAsync(cliente, token, nombre ?? NombreUnico(), idUbicacion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (cuerpo.GetProperty("dispositivo").GetProperty("idDispositivo").GetInt64(), cuerpo.GetProperty("apiKey").GetString()!);
    }

    /// <summary>POST /api/lecturas con la cabecera X-Device-Key (null = sin cabecera).</summary>
    public static Task<HttpResponseMessage> EnviarLecturaAsync(HttpClient cliente, string? apiKey, object cuerpo)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, RutaLecturas) { Content = JsonContent.Create(cuerpo) };
        if (apiKey is not null)
        {
            solicitud.Headers.TryAddWithoutValidation(ApiKeyDispositivo.Cabecera, apiKey);
        }

        return cliente.SendAsync(solicitud);
    }

    public static async Task<JsonElement> LeerAsync(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Crea una regla de alerta por la API (Fase 2H). idUbicacion null = regla global.</summary>
    public static async Task CrearReglaAsync(HttpClient cliente, string token, string tipo, long? idUbicacion, decimal? valorUmbral)
    {
        var respuesta = await cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/configuraciones-alerta", token,
            new { tipo, idUbicacion, valorUmbral, activa = true }));
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }
}
