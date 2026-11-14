using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Correo;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Tests.Correo;

/// <summary>
/// Fase 7: entrega real por SMTP (MailKit) contra Mailpit, sin Internet. Se omite si no existe la variable
/// MAILPIT_URL (por ejemplo http://localhost:8025). El SMTP se toma del mismo host, puerto MAILPIT_SMTP_PUERTO
/// (1025 por defecto). Solo envía a una dirección de prueba *.local; nunca a un correo real.
/// </summary>
[Trait("Categoria", "Mailpit")]
public sealed class EntregaMailpitTests
{
    private static string? UrlMailpit => Environment.GetEnvironmentVariable("MAILPIT_URL");

    [MailpitFact]
    public async Task SmtpEmailSender_EntregaElCorreo_ConHtmlTextoYRemitente()
    {
        var api = new Uri(UrlMailpit!);
        var puerto = int.TryParse(Environment.GetEnvironmentVariable("MAILPIT_SMTP_PUERTO"), out var p) ? p : 1025;
        var opciones = new CorreoOptions
        {
            Host = api.Host,
            Puerto = puerto,
            Remitente = "no-reply@climaplus.local",
            NombreRemitente = "ClimaPlus",
            Seguridad = SeguridadCorreo.Ninguna,
        };
        Assert.Empty(opciones.Validar());

        var marca = $"prueba-entrega-{Guid.NewGuid():N}";
        var destino = $"{marca}@climaplus.local";
        var enviador = new SmtpEmailSender(new MonitorFijo<CorreoOptions>(opciones));

        await enviador.EnviarAsync(
            new MensajeCorreo(destino, $"ClimaPlus {marca}", $"<p>Código <b>{marca}</b></p>", $"Código {marca}", "prueba_entrega"),
            CancellationToken.None);

        using var http = new HttpClient { BaseAddress = api, Timeout = TimeSpan.FromSeconds(10) };
        JsonElement? mensaje = null;
        for (var intento = 0; intento < 20 && mensaje is null; intento++)
        {
            var busqueda = await http.GetFromJsonAsync<JsonElement>($"/api/v1/search?query={Uri.EscapeDataString($"to:{destino}")}");
            var lista = busqueda.GetProperty("messages");
            if (lista.GetArrayLength() > 0)
            {
                mensaje = lista[0];
            }
            else
            {
                await Task.Delay(250);
            }
        }

        Assert.True(mensaje.HasValue, "Mailpit no recibió el correo.");
        var detalle = await http.GetFromJsonAsync<JsonElement>($"/api/v1/message/{mensaje.Value.GetProperty("ID").GetString()}");
        Assert.Equal($"ClimaPlus {marca}", detalle.GetProperty("Subject").GetString());
        Assert.Equal("ClimaPlus", detalle.GetProperty("From").GetProperty("Name").GetString());
        Assert.Equal("no-reply@climaplus.local", detalle.GetProperty("From").GetProperty("Address").GetString());
        Assert.Contains(marca, detalle.GetProperty("Text").GetString());
        Assert.Contains($"<b>{marca}</b>", detalle.GetProperty("HTML").GetString());
    }

    private sealed class MonitorFijo<T> : IOptionsMonitor<T>
    {
        public MonitorFijo(T valor) => CurrentValue = valor;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

/// <summary>[Fact] que se omite si no está definida MAILPIT_URL.</summary>
public sealed class MailpitFactAttribute : FactAttribute
{
    public MailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MAILPIT_URL")))
        {
            Skip = "Prueba de entrega omitida: define MAILPIT_URL (por ejemplo http://localhost:8025) con Mailpit levantado.";
        }
    }
}
