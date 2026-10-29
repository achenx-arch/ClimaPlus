using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClimaPlus.Api.Tests.Infraestructura.RespuestasOpenWeather;

namespace ClimaPlus.Api.Tests.Clima;

/// <summary>
/// GET /api/clima/ubicaciones (Fase 2E): Geocoding simulado → normalizar → guardar o reutilizar → responder desde la BD.
/// Cada prueba usa un texto de búsqueda distinto porque las búsquedas exitosas se guardan en caché.
/// </summary>
public sealed class UbicacionesTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public UbicacionesTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private ProveedorOpenWeatherSimulado Proveedor => _fabrica.Proveedor;

    private static string BusquedaUnica() => "Ciudad " + Guid.NewGuid().ToString("N")[..12];

    private static string Ruta(string q, int? limite = null) =>
        $"/api/clima/ubicaciones?q={Uri.EscapeDataString(q)}" + (limite is null ? string.Empty : $"&limite={limite}");

    private async Task<JsonElement[]> BuscarOkAsync(string q, int? limite = null)
    {
        var respuesta = await _cliente.GetAsync(Ruta(q, limite));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    [Fact]
    public async Task Busqueda_GuardaUbicacionesYRespondeConLosRegistrosPersistidos()
    {
        var q = BusquedaUnica();
        Proveedor.ResponderJson(Geocoding(Coban, Guatemala));

        var ubicaciones = await BuscarOkAsync(q, 5);

        Assert.Equal(2, ubicaciones.Length);
        var coban = ubicaciones[0];
        Assert.True(coban.GetProperty("idUbicacion").GetInt64() > 0);
        Assert.Equal("Cobán", coban.GetProperty("nombre").GetString());          // local_names.es
        Assert.Equal("Alta Verapaz", coban.GetProperty("region").GetString());
        Assert.Equal("GT", coban.GetProperty("pais").GetString());
        Assert.Equal(15.4708m, coban.GetProperty("latitud").GetDecimal());        // 4 decimales (D-07)
        Assert.Equal(-90.3708m, coban.GetProperty("longitud").GetDecimal());
        Assert.Equal("Ciudad de Guatemala", ubicaciones[1].GetProperty("nombre").GetString());

        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var guardada = await db.Ubicaciones.AsNoTracking().SingleAsync(u => u.IdUbicacion == coban.GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Cobán", guardada.Nombre);
        Assert.Equal(15.4708m, guardada.Latitud);
        Assert.Equal(-90.3708m, guardada.Longitud);

        // La petición al proveedor: ruta de Geocoding, texto escapado, límite y clave.
        var solicitud = Assert.Single(Proveedor.Solicitudes);
        Assert.Equal("/geo/1.0/direct", solicitud.AbsolutePath);
        Assert.Contains("q=" + Uri.EscapeDataString(q), solicitud.Query);
        Assert.Contains("limit=5", solicitud.Query);
        Assert.Contains("appid=", solicitud.Query);
    }

    [Fact]
    public async Task BusquedaRepetida_ReutilizaLosMismosIdsSinDuplicarFilas()
    {
        var q = BusquedaUnica();
        Proveedor.ResponderJson(Geocoding(new Lugar("Lugar Repetido", 10.12345m, -80.54321m, "pa")));

        var primera = await BuscarOkAsync(q);
        var segunda = await BuscarOkAsync(q);

        Assert.Equal(primera[0].GetProperty("idUbicacion").GetInt64(), segunda[0].GetProperty("idUbicacion").GetInt64());
        Assert.Equal("PA", primera[0].GetProperty("pais").GetString());
        Assert.Equal(1, Proveedor.Llamadas);   // la segunda búsqueda sale de la caché

        // Otra búsqueda distinta que devuelve las mismas coordenadas reutiliza la fila existente.
        Proveedor.ResponderJson(Geocoding(new Lugar("Otro Nombre", 10.12349m, -80.54315m, "PA")));
        var tercera = await BuscarOkAsync(BusquedaUnica());
        Assert.Equal(primera[0].GetProperty("idUbicacion").GetInt64(), tercera[0].GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Lugar Repetido", tercera[0].GetProperty("nombre").GetString());

        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        Assert.Equal(1, await db.Ubicaciones.CountAsync(u => u.Latitud == 10.1235m && u.Longitud == -80.5432m));
    }

    [Fact]
    public async Task ResultadosConLasMismasCoordenadasRedondeadas_SeDevuelvenUnaSolaVez()
    {
        Proveedor.ResponderJson(Geocoding(
            new Lugar("Duplicado Uno", 20.00001m, 30.00004m, "MX"),
            new Lugar("Duplicado Dos", 19.99999m, 29.99996m, "MX")));

        var ubicaciones = await BuscarOkAsync(BusquedaUnica());

        var unica = Assert.Single(ubicaciones);
        Assert.Equal("Duplicado Uno", unica.GetProperty("nombre").GetString());
        Assert.Equal(20.0000m, unica.GetProperty("latitud").GetDecimal());
        Assert.Equal(30.0000m, unica.GetProperty("longitud").GetDecimal());
    }

    [Fact]
    public async Task ResultadosInvalidosDelProveedor_SeDescartan()
    {
        Proveedor.ResponderJson(Geocoding(
            new Lugar("Pais de tres letras", 1m, 1m, "GTM"),
            new Lugar("Latitud fuera de rango", 95m, 1m, "GT"),
            new Lugar("Longitud fuera de rango", 1m, -181m, "GT"),
            new Lugar("Sin longitud", 1m, null, "GT"),
            new Lugar("   ", 2m, 2m, "GT"),
            new Lugar("Region muy larga", 3.5m, 3.5m, "GT", new string('x', 101)),
            new Lugar("Valida", 4.25m, 4.25m, "SV")));

        var ubicaciones = await BuscarOkAsync(BusquedaUnica());

        Assert.Equal(2, ubicaciones.Length);
        Assert.Equal("Region muy larga", ubicaciones[0].GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, ubicaciones[0].GetProperty("region").ValueKind);
        Assert.Equal("Valida", ubicaciones[1].GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task SinResultados_RespondeListaVacia()
    {
        Proveedor.ResponderJson("[]");

        var ubicaciones = await BuscarOkAsync(BusquedaUnica());

        Assert.Empty(ubicaciones);
    }

    [Theory]
    [InlineData("/api/clima/ubicaciones")]
    [InlineData("/api/clima/ubicaciones?q=")]
    [InlineData("/api/clima/ubicaciones?q=a")]
    [InlineData("/api/clima/ubicaciones?q=%3Cscript%3Ealert(1)%3C%2Fscript%3E")]
    [InlineData("/api/clima/ubicaciones?q=Coban%3B%20DROP%20TABLE%20ubicacion")]
    [InlineData("/api/clima/ubicaciones?q=Coban&limite=0")]
    [InlineData("/api/clima/ubicaciones?q=Coban&limite=6")]
    [InlineData("/api/clima/ubicaciones?q=Coban&limite=abc")]
    public async Task EntradaInvalida_Responde400SinLlamarAlProveedor(string ruta)
    {
        Proveedor.ResponderJson(Geocoding(Coban));

        var respuesta = await _cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Fact]
    public async Task BusquedaDe101Caracteres_Responde400()
    {
        Proveedor.ResponderJson(Geocoding(Coban));

        var respuesta = await _cliente.GetAsync(Ruta(new string('a', 101)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("q", out _));
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Forbidden, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.NotFound)]
    public async Task ErrorDelProveedor_SeTraduceSinExponerDetalles(HttpStatusCode estadoProveedor, HttpStatusCode esperado)
    {
        Proveedor.ResponderEstado(estadoProveedor);

        var respuesta = await _cliente.GetAsync(Ruta(BusquedaUnica()));

        Assert.Equal(esperado, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        var texto = problema.GetRawText();
        Assert.DoesNotContain("mensaje interno del proveedor", texto);
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, texto);
        Assert.DoesNotContain("openweather.pruebas", texto);
        Assert.Equal(1, Proveedor.Llamadas);   // sin reintentos (D-24)
    }

    [Fact]
    public async Task FalloDeRed_Responde503()
    {
        Proveedor.Lanzar(() => new HttpRequestException("No se pudo conectar"));

        var respuesta = await _cliente.GetAsync(Ruta(BusquedaUnica()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task TiempoAgotado_Responde504()
    {
        // HttpClient señala el timeout con TaskCanceledException sin cancelar la petición del cliente.
        Proveedor.Lanzar(() => new TaskCanceledException("timeout", new TimeoutException()));

        var respuesta = await _cliente.GetAsync(Ruta(BusquedaUnica()));

        Assert.Equal(HttpStatusCode.GatewayTimeout, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Theory]
    [InlineData("{esto no es json")]
    [InlineData("{\"name\":\"objeto en vez de lista\"}")]
    [InlineData("null")]
    public async Task RespuestaInvalidaDelProveedor_Responde502(string cuerpo)
    {
        Proveedor.ResponderJson(cuerpo);

        var respuesta = await _cliente.GetAsync(Ruta(BusquedaUnica()));

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task LaApiKeyNoApareceEnLogsNiRespuestas()
    {
        Proveedor.ResponderJson(Geocoding(Coban));
        var exitosa = await _cliente.GetAsync(Ruta(BusquedaUnica()));
        Proveedor.ResponderEstado(HttpStatusCode.Unauthorized);
        var fallida = await _cliente.GetAsync(Ruta(BusquedaUnica()));
        Proveedor.Lanzar(() => new HttpRequestException("fallo"));
        var red = await _cliente.GetAsync(Ruta(BusquedaUnica()));

        // El cliente sí envía la clave al proveedor...
        Assert.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, Proveedor.Solicitudes.Single().Query);

        // ...pero nunca la devuelve ni la escribe en los logs.
        foreach (var respuesta in new[] { exitosa, fallida, red })
        {
            Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, await respuesta.Content.ReadAsStringAsync());
            Assert.DoesNotContain(respuesta.Headers, h => h.Value.Any(v => v.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas)));
        }

        Assert.NotEmpty(_fabrica.Logs.Mensajes);
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, StringComparison.Ordinal));
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains("appid=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RutaPublica_SinTokenYConTokenValido_Responde200()
    {
        Proveedor.ResponderJson(Geocoding(Coban));
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var sinToken = await _cliente.GetAsync(Ruta(BusquedaUnica()));
        var conToken = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(BusquedaUnica()), token));

        Assert.Equal(HttpStatusCode.OK, sinToken.StatusCode);
        Assert.Equal(HttpStatusCode.OK, conToken.StatusCode);
    }

    [Fact]
    public async Task RutaPublica_ConTokenInvalido_Responde401()
    {
        Proveedor.ResponderJson(Geocoding(Coban));

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(BusquedaUnica()), "token-invalido"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Contains("Bearer", respuesta.Headers.WwwAuthenticate.ToString());
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Fact]
    public async Task RutaPublica_ConTokenDeUsuarioDesactivado_Responde401()
    {
        Proveedor.ResponderJson(Geocoding(Coban));
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(BusquedaUnica()), token));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal(0, Proveedor.Llamadas);
    }
}

/// <summary>D-23: /api/clima/* limitado por IP (en pruebas, 3 por minuto).</summary>
public sealed class ClimaRateLimitingTests : IClassFixture<ClimaPlusApiFactoryLimiteBajo>
{
    private readonly ClimaPlusApiFactoryLimiteBajo _fabrica;

    public ClimaRateLimitingTests(ClimaPlusApiFactoryLimiteBajo fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public async Task SuperarElLimite_Responde429ConRetryAfter()
    {
        _fabrica.Proveedor.ResponderJson("[]");
        var cliente = _fabrica.CreateClient();

        for (var i = 0; i < ClimaPlusApiFactoryLimiteBajo.Limite; i++)
        {
            var permitida = await cliente.GetAsync($"/api/clima/ubicaciones?q=Limite{i}");
            Assert.Equal(HttpStatusCode.OK, permitida.StatusCode);
        }

        var rechazada = await cliente.GetAsync("/api/clima/ubicaciones?q=LimiteExtra");

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.True(rechazada.Headers.Contains("Retry-After"));
        await Ayudas.LeerProblemaAsync(rechazada);
        Assert.Equal(ClimaPlusApiFactoryLimiteBajo.Limite, _fabrica.Proveedor.Llamadas);
    }
}

/// <summary>Sin OpenWeather:ApiKey la API arranca, pero las rutas de clima responden 503.</summary>
public sealed class ClimaSinClaveTests : IClassFixture<ClimaPlusApiFactorySinClaveOpenWeather>
{
    private readonly ClimaPlusApiFactorySinClaveOpenWeather _fabrica;

    public ClimaSinClaveTests(ClimaPlusApiFactorySinClaveOpenWeather fabrica)
    {
        _fabrica = fabrica;
    }

    [Fact]
    public async Task SinApiKey_Responde503SinLlamarAlProveedor()
    {
        _fabrica.Proveedor.ResponderJson(Geocoding(Coban));
        var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/clima/ubicaciones?q=Coban");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(0, _fabrica.Proveedor.Llamadas);
    }
}
