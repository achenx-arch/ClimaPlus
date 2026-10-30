using System.Net;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>Fase 9: GET /health para Docker, GitHub Actions y monitoreo.</summary>
public sealed class SaludTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly HttpClient _cliente;

    public SaludTests(ClimaPlusApiFactory fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Health_EsPublico_YRespondeHealthy()
    {
        var respuesta = await _cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(CabecerasSeguridad.CspApi, string.Join(",", respuesta.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task Health_NoExponeDetalles()
    {
        var cuerpo = await (await _cliente.GetAsync("/health")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("postgres", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("version", cuerpo, StringComparison.OrdinalIgnoreCase);
    }
}
