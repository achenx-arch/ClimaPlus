using System.Net;
using System.Text;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>Fase 8 – OWASP A05 (Security Misconfiguration): cabeceras de seguridad, errores sin detalles y superficie mínima.</summary>
public sealed class CabecerasSeguridadTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public CabecerasSeguridadTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private static string Cabecera(HttpResponseMessage respuesta, string nombre) =>
        respuesta.Headers.TryGetValues(nombre, out var valores) ? string.Join(",", valores)
        : respuesta.Content.Headers.TryGetValues(nombre, out var deContenido) ? string.Join(",", deContenido)
        : string.Empty;

    private static void AssertCabecerasBasicas(HttpResponseMessage respuesta)
    {
        Assert.Equal("nosniff", Cabecera(respuesta, "X-Content-Type-Options"));
        Assert.Equal("DENY", Cabecera(respuesta, "X-Frame-Options"));
        Assert.Equal("no-referrer", Cabecera(respuesta, "Referrer-Policy"));
        Assert.Equal(CabecerasSeguridad.PermissionsPolicy, Cabecera(respuesta, "Permissions-Policy"));
        Assert.Equal("same-origin", Cabecera(respuesta, "Cross-Origin-Opener-Policy"));
        Assert.Equal(CabecerasSeguridad.CspApi, Cabecera(respuesta, "Content-Security-Policy"));
        Assert.False(respuesta.Headers.Contains("Server"));
        Assert.False(respuesta.Headers.Contains("X-Powered-By"));
    }

    [Fact]
    public async Task RespuestaExitosa_TieneCabecerasDeSeguridad_YNoSeGuardaEnCache()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);

        var respuesta = await Ayudas.LoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        AssertCabecerasBasicas(respuesta);
        Assert.True(respuesta.Headers.CacheControl?.NoStore);   // la respuesta lleva el token
    }

    [Theory]
    [InlineData("/api/usuarios/me")]        // 401
    [InlineData("/api/no-existe")]          // 401 por la política por defecto
    [InlineData("/api/pruebas/error")]      // 500
    public async Task RespuestasDeError_TambienTienenCabecerasDeSeguridad(string ruta)
    {
        var respuesta = await _cliente.GetAsync(ruta);

        Assert.False(respuesta.IsSuccessStatusCode);
        AssertCabecerasBasicas(respuesta);
    }

    [Fact]
    public async Task ErrorInterno_Responde500_SinFiltrarDetallesInternos()
    {
        var respuesta = await _cliente.GetAsync("/api/pruebas/error");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detalle-interno-secreto", cuerpo);
        Assert.DoesNotContain("InvalidOperationException", cuerpo);
        Assert.DoesNotContain("   at ", cuerpo);   // sin pila de llamadas
    }

    [Fact]
    public async Task Swagger_TieneSuPropiaCsp()
    {
        var respuesta = await _cliente.GetAsync("/swagger/index.html");

        Assert.Equal(CabecerasSeguridad.CspSwagger, Cabecera(respuesta, "Content-Security-Policy"));
        Assert.Equal("DENY", Cabecera(respuesta, "X-Frame-Options"));
    }

    [Fact]
    public async Task CuerpoDemasiadoGrande_Responde413_SinProcesarlo()
    {
        var enorme = "{\"correo\":\"" + new string('a', 1_100_000) + "\",\"password\":\"x\"}";

        var respuesta = await _cliente.PostAsync("/api/auth/login", new StringContent(enorme, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        AssertCabecerasBasicas(respuesta);
    }

    [Fact]
    public async Task ControladorDePlantilla_WeatherForecast_YaNoExiste()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/WeatherForecast", token));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public void Hsts_DeUnAnio_ConSubdominios()
    {
        var hsts = _fabrica.Services.GetRequiredService<IOptions<HstsOptions>>().Value;

        Assert.Equal(TimeSpan.FromDays(365), hsts.MaxAge);
        Assert.True(hsts.IncludeSubDomains);
    }
}
