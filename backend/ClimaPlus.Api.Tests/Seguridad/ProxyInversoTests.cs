using System.Net;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>H-05: cabeceras X-Forwarded-* solo desde el proxy declarado.</summary>
public sealed class ProxyInversoOpcionesTests
{
    [Fact]
    public void SinRedesNiProxies_NoSeLeenLasCabeceras()
    {
        Assert.Null(ProxyInverso.CrearOpciones(new ProxyInversoOptions()));
        Assert.Null(ProxyInverso.CrearOpciones(new ProxyInversoOptions { RedesConocidas = { "", "  " } }));
    }

    [Fact]
    public void ConUnaRed_SoloConfiaEnEsaRedYEnUnSaltoDeProxy()
    {
        var opciones = ProxyInverso.CrearOpciones(new ProxyInversoOptions { RedesConocidas = { "172.16.0.0/12" } })!;

        var red = Assert.Single(opciones.KnownNetworks);
        Assert.Equal(IPAddress.Parse("172.16.0.0"), red.Prefix);
        Assert.Equal(12, red.PrefixLength);
        Assert.Empty(opciones.KnownProxies);   // se quita el 127.0.0.1 que ASP.NET trae por defecto
        Assert.Equal(1, opciones.ForwardLimit);
    }

    [Fact]
    public void AceptaIPv6_YProxiesExactos()
    {
        var opciones = ProxyInverso.CrearOpciones(new ProxyInversoOptions
        {
            RedesConocidas = { "fd00::/8" },
            ProxiesConocidos = { "10.0.0.5" },
        })!;

        Assert.Single(opciones.KnownNetworks);
        Assert.Equal(IPAddress.Parse("10.0.0.5"), Assert.Single(opciones.KnownProxies));
    }

    [Theory]
    [InlineData("172.16.0.0")]
    [InlineData("172.16.0.0/33")]
    [InlineData("no-es-una-red/8")]
    public void UnaRedInvalida_DetieneElArranqueConUnMensajeClaro(string red)
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProxyInverso.CrearOpciones(new ProxyInversoOptions { RedesConocidas = { red } }));
        Assert.Contains("ProxyInverso:RedesConocidas", error.Message);
    }

    [Fact]
    public void UnProxyInvalido_DetieneElArranque() =>
        Assert.Throws<InvalidOperationException>(() =>
            ProxyInverso.CrearOpciones(new ProxyInversoOptions { ProxiesConocidos = { "proxy.local" } }));
}

/// <summary>API detrás de un proxy en 10.0.0.0/8, con límite general de 5 peticiones por minuto.</summary>
public sealed class ClimaPlusApiFactoryProxy : ClimaPlusApiFactory
{
    public const int Limite = 5;

    /// <summary>Cabecera solo de pruebas: simula la IP de quien abre la conexión TCP con la API.</summary>
    public const string CabeceraIpConexion = FiltroIpConexion.Cabecera;

    protected override int LimiteGeneral => Limite;

    protected override void AgregarConfiguracion(IDictionary<string, string?> valores) =>
        valores["ProxyInverso:RedesConocidas:0"] = "10.0.0.0/8";

    protected override void AgregarServicios(IServiceCollection servicios) =>
        servicios.AddSingleton<IStartupFilter, FiltroIpConexion>();
}

public sealed class ProxyInversoTests : IClassFixture<ClimaPlusApiFactoryProxy>
{
    private readonly HttpClient _cliente;

    public ProxyInversoTests(ClimaPlusApiFactoryProxy fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> PedirAsync(string ipConexion, string? xForwardedFor)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        solicitud.Headers.Add(ClimaPlusApiFactoryProxy.CabeceraIpConexion, ipConexion);
        if (xForwardedFor is not null)
        {
            solicitud.Headers.Add("X-Forwarded-For", xForwardedFor);
        }

        return _cliente.SendAsync(solicitud);
    }

    [Fact]
    public async Task DetrasDelProxy_CadaClienteTieneSuPropioLimite()
    {
        for (var i = 0; i < ClimaPlusApiFactoryProxy.Limite; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PedirAsync("10.0.0.2", "203.0.113.10")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await PedirAsync("10.0.0.2", "203.0.113.10")).StatusCode);

        // Otro usuario detrás del mismo proxy no queda bloqueado por el primero.
        Assert.Equal(HttpStatusCode.Unauthorized, (await PedirAsync("10.0.0.2", "203.0.113.20")).StatusCode);
    }

    [Fact]
    public async Task FueraDelProxy_XForwardedForSeIgnora_NoSirveParaEsquivarElLimite()
    {
        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i <= ClimaPlusApiFactoryProxy.Limite; i++)
        {
            // Cada petición inventa otra IP, pero llega directamente desde 198.51.100.7 (no es el proxy).
            codigos.Add((await PedirAsync("198.51.100.7", $"192.0.2.{i + 1}")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, codigos[^1]);
    }
}
