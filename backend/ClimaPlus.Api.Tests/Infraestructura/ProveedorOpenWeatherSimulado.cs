using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Sustituye la red en el cliente de OpenWeather (D-27). Ninguna prueba llama a OpenWeather real:
/// toda petición termina aquí y responde lo que la prueba haya configurado.
/// </summary>
public sealed class ProveedorOpenWeatherSimulado : HttpMessageHandler
{
    private Func<HttpRequestMessage, HttpResponseMessage> _responder = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

    public ConcurrentQueue<Uri> Solicitudes { get; } = new();

    public int Llamadas => Solicitudes.Count;

    public void ResponderJson(string json, HttpStatusCode estado = HttpStatusCode.OK)
    {
        Reiniciar();
        _responder = _ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public void ResponderEstado(HttpStatusCode estado, string cuerpo = "{\"cod\":0,\"message\":\"mensaje interno del proveedor\"}")
    {
        Reiniciar();
        _responder = _ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json"),
        };
    }

    public void Lanzar(Func<Exception> excepcion)
    {
        Reiniciar();
        _responder = _ => throw excepcion();
    }

    public void Reiniciar() => Solicitudes.Clear();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Solicitudes.Enqueue(request.RequestUri!);
        return Task.FromResult(_responder(request));
    }
}
