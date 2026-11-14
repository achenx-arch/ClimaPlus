using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Correo;

/// <summary>
/// Cola de correos en segundo plano: entrega, reintentos y fallos del SMTP sin afectar a la API ni filtrar el
/// contenido en los logs.
/// </summary>
public sealed class ColaCorreosTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly IColaCorreos _cola;

    public ColaCorreosTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cola = fabrica.Services.GetRequiredService<IColaCorreos>();
    }

    private static MensajeCorreo Codigo(string destino, string codigo) =>
        PlantillasCorreo.Codigo(destino, "Prueba", PropositosDesafio.InicioSesion, codigo, 5);

    [Fact]
    public async Task UnCorreoEncolado_LlegaAlEnviador()
    {
        var destino = Ayudas.CorreoUnico();

        Assert.True(_cola.Encolar(Codigo(destino, "111222")));

        var enviado = await _fabrica.Correos.EsperarAsync(c => c.Destinatario == destino);
        Assert.Contains("111222", enviado.Texto);
    }

    [Fact]
    public async Task SiElSmtpFallaDosVeces_SeEntregaAlTercerIntento_SinFiltrarElContenidoEnLosLogs()
    {
        var destino = Ayudas.CorreoUnico();
        _fabrica.Correos.FallarProximos(2);

        _cola.Encolar(Codigo(destino, "333444"));

        await _fabrica.Correos.EsperarAsync(c => c.Destinatario == destino);
        var logs = string.Join("\n", _fabrica.Logs.Mensajes);
        Assert.Contains("envio_fallido tipo=codigo_inicio_sesion", logs);
        Assert.Contains("error=IOException", logs);
        Assert.Contains("intento=3", logs);
        Assert.DoesNotContain("333444", logs);
        Assert.DoesNotContain(destino, logs);   // el destinatario se enmascara
    }

    [Fact]
    public async Task SiElSmtpNoResponde_SeDescartaTrasTresIntentos_YLosSiguientesSiguenSaliendo()
    {
        var perdido = Ayudas.CorreoUnico();
        var siguiente = Ayudas.CorreoUnico();
        _fabrica.Correos.FallarProximos(3);

        _cola.Encolar(Codigo(perdido, "555666"));
        _cola.Encolar(Codigo(siguiente, "777888"));

        await _fabrica.Correos.EsperarAsync(c => c.Destinatario == siguiente);
        Assert.DoesNotContain(_fabrica.Correos.Enviados, c => c.Destinatario == perdido);
        Assert.Contains("reintenta=False", string.Join("\n", _fabrica.Logs.Mensajes));

        // La API sigue respondiendo con normalidad.
        var respuesta = await _fabrica.CreateClient().GetAsync("/api/auth/me");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
