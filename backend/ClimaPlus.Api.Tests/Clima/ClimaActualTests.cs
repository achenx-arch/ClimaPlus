using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Clima;

/// <summary>
/// GET /api/clima/actual (Fase 2E): OpenWeather simulado → validar → guardar en consulta_clima
/// (usuario o NULL, E-02; detalle, E-03) → responder con lo guardado. Caché en la BD de 10 min (D-23).
/// Cada prueba usa una ubicación propia, porque la caché es por ubicación.
/// </summary>
public sealed class ClimaActualTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public ClimaActualTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private ProveedorOpenWeatherSimulado Proveedor => _fabrica.Proveedor;

    private static string Ruta(long idUbicacion) => $"/api/clima/actual?idUbicacion={idUbicacion}";

    private void ResponderClimaValido() => Proveedor.ResponderJson(RespuestasOpenWeather.ClimaActual().ToJsonString());

    private async Task<JsonElement> ObtenerOkAsync(long idUbicacion, string? token = null)
    {
        var respuesta = token is null
            ? await _cliente.GetAsync(Ruta(idUbicacion))
            : await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<List<ConsultaClima>> ConsultasDeAsync(long idUbicacion)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.ConsultasClima.AsNoTracking().Where(c => c.IdUbicacion == idUbicacion).OrderBy(c => c.IdConsulta).ToListAsync();
    }

    [Fact]
    public async Task Visitante_GuardaLaConsultaConIdUsuarioNull_YRespondeConLoGuardado()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Cobán");
        ResponderClimaValido();

        var cuerpo = await ObtenerOkAsync(idUbicacion);

        var fila = Assert.Single(await ConsultasDeAsync(idUbicacion));
        Assert.Null(fila.IdUsuario);
        Assert.Equal(TiposConsulta.Actual, fila.TipoConsulta);
        Assert.Equal(22.46m, fila.Temperatura);
        Assert.Equal((short)70, fila.Humedad);
        Assert.NotNull(fila.Detalle);

        Assert.Equal(idUbicacion, cuerpo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Cobán", cuerpo.GetProperty("ubicacion").GetProperty("nombre").GetString());
        var consulta = cuerpo.GetProperty("consulta");
        Assert.Equal(fila.IdConsulta, consulta.GetProperty("idConsulta").GetInt64());
        Assert.False(consulta.GetProperty("desdeCache").GetBoolean());
        Assert.EndsWith("Z", consulta.GetProperty("fechaConsultaUtc").GetString());
        Assert.Equal(22.46m, cuerpo.GetProperty("temperatura").GetDecimal());
        Assert.Equal(22.1m, cuerpo.GetProperty("sensacionTermica").GetDecimal());
        Assert.Equal(70, cuerpo.GetProperty("humedad").GetInt32());
        Assert.Equal(3.1m, cuerpo.GetProperty("velocidadViento").GetDecimal());
        Assert.Equal(802, cuerpo.GetProperty("codigoCondicion").GetInt32());
        Assert.Equal("nubes dispersas", cuerpo.GetProperty("descripcion").GetString());

        var detalle = cuerpo.GetProperty("detalle");
        Assert.Equal(1, detalle.GetProperty("version").GetInt32());
        Assert.Equal("2026-10-27T18:20:00Z", detalle.GetProperty("observadoEnUtc").GetString());
        Assert.Equal(1018, detalle.GetProperty("presionHpa").GetInt32());
        Assert.True(detalle.GetProperty("esDeDia").GetBoolean());

        // La petición al proveedor usa las coordenadas guardadas y la configuración de D-26.
        var solicitud = Assert.Single(Proveedor.Solicitudes);
        Assert.Equal("/data/2.5/weather", solicitud.AbsolutePath);
        Assert.Contains("units=metric", solicitud.Query);
        Assert.Contains("lang=es", solicitud.Query);
        Assert.Contains("lat=", solicitud.Query);
        Assert.Contains("lon=", solicitud.Query);
    }

    [Fact]
    public async Task ConSesion_GuardaElUsuarioDelToken()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        ResponderClimaValido();

        await ObtenerOkAsync(idUbicacion, token);

        var fila = Assert.Single(await ConsultasDeAsync(idUbicacion));
        Assert.Equal(await AyudasDatos.IdUsuarioAsync(_fabrica, correo), fila.IdUsuario);
    }

    [Fact]
    public async Task ConSesion_LaConsultaApareceComoUltimaConsultaDelFavorito()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        ResponderClimaValido();
        var clima = await ObtenerOkAsync(idUbicacion, token);

        var favorito = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/favoritos", token, new { idUbicacion }));

        var cuerpo = await favorito.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(
            clima.GetProperty("consulta").GetProperty("idConsulta").GetInt64(),
            cuerpo.GetProperty("ultimaConsulta").GetProperty("idConsulta").GetInt64());
    }

    [Fact]
    public async Task Cache_DentroDe10Minutos_NoLlamaAlProveedor_PeroGuardaOtraConsulta()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        ResponderClimaValido();

        var primera = await ObtenerOkAsync(idUbicacion);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(5));
        var segunda = await ObtenerOkAsync(idUbicacion, token);

        Assert.Equal(1, Proveedor.Llamadas);
        Assert.True(segunda.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.NotEqual(
            primera.GetProperty("consulta").GetProperty("idConsulta").GetInt64(),
            segunda.GetProperty("consulta").GetProperty("idConsulta").GetInt64());
        Assert.Equal(primera.GetProperty("detalle").GetRawText(), segunda.GetProperty("detalle").GetRawText());
        Assert.Equal(primera.GetProperty("temperatura").GetDecimal(), segunda.GetProperty("temperatura").GetDecimal());

        // Cada consulta queda registrada con su propio usuario.
        var filas = await ConsultasDeAsync(idUbicacion);
        Assert.Equal(2, filas.Count);
        Assert.Null(filas[0].IdUsuario);
        Assert.Equal(await AyudasDatos.IdUsuarioAsync(_fabrica, correo), filas[1].IdUsuario);
    }

    [Fact]
    public async Task Cache_LasCopiasNoAlarganLaVigencia_YDespuesDe10MinutosSeVuelveAConsultar()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        ResponderClimaValido();

        await ObtenerOkAsync(idUbicacion);                     // t = 0: OpenWeather
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(6));
        await ObtenerOkAsync(idUbicacion);                     // t = 6: caché (copia)
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(5));
        var tercera = await ObtenerOkAsync(idUbicacion);       // t = 11: datos de hace 11 min → OpenWeather

        Assert.Equal(2, Proveedor.Llamadas);
        Assert.False(tercera.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(3, (await ConsultasDeAsync(idUbicacion)).Count);
    }

    [Fact]
    public async Task Cache_UsaDatosDeOtroUsuario_SinExponerSuConsulta()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        ResponderClimaValido();

        var deUsuario = await ObtenerOkAsync(idUbicacion, token);
        var deVisitante = await ObtenerOkAsync(idUbicacion);

        Assert.True(deVisitante.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.NotEqual(
            deUsuario.GetProperty("consulta").GetProperty("idConsulta").GetInt64(),
            deVisitante.GetProperty("consulta").GetProperty("idConsulta").GetInt64());
        Assert.DoesNotContain("idUsuario", deVisitante.GetRawText());
        Assert.Null((await ConsultasDeAsync(idUbicacion))[1].IdUsuario);
    }

    [Fact]
    public async Task UbicacionInexistente_Responde404SinLlamarAlProveedor()
    {
        ResponderClimaValido();

        var respuesta = await _cliente.GetAsync(Ruta(987_654_321));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Theory]
    [InlineData("/api/clima/actual")]
    [InlineData("/api/clima/actual?idUbicacion=")]
    [InlineData("/api/clima/actual?idUbicacion=0")]
    [InlineData("/api/clima/actual?idUbicacion=-4")]
    [InlineData("/api/clima/actual?idUbicacion=abc")]
    [InlineData("/api/clima/actual?idUbicacion=1%3B%20DROP%20TABLE%20consulta_clima")]
    [InlineData("/api/clima/actual?lat=15.47&lon=-90.37")]
    public async Task ParametroInvalido_Responde400SinLlamarAlProveedor(string ruta)
    {
        ResponderClimaValido();

        var respuesta = await _cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("idUbicacion", out _), problema.ToString());
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.NotFound)]
    public async Task ErrorDelProveedor_SeTraduce_YNoGuardaNada(HttpStatusCode estadoProveedor, HttpStatusCode esperado)
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        Proveedor.ResponderEstado(estadoProveedor);

        var respuesta = await _cliente.GetAsync(Ruta(idUbicacion));

        Assert.Equal(esperado, respuesta.StatusCode);
        var texto = (await Ayudas.LeerProblemaAsync(respuesta)).GetRawText();
        Assert.DoesNotContain("mensaje interno del proveedor", texto);
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, texto);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
        Assert.Equal(1, Proveedor.Llamadas);   // sin reintentos (D-24)
    }

    [Fact]
    public async Task TiempoAgotado_Responde504_YFalloDeRed503()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);

        Proveedor.Lanzar(() => new TaskCanceledException("timeout", new TimeoutException()));
        var timeout = await _cliente.GetAsync(Ruta(idUbicacion));
        Proveedor.Lanzar(() => new HttpRequestException("sin red"));
        var red = await _cliente.GetAsync(Ruta(idUbicacion));

        Assert.Equal(HttpStatusCode.GatewayTimeout, timeout.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, red.StatusCode);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
    }

    [Theory]
    [InlineData("{esto no es json")]
    [InlineData("humedad_150")]
    [InlineData("sin_temperatura")]
    [InlineData("codigo_100")]
    public async Task RespuestaInvalidaOFueraDeRango_Responde502_YNoGuardaNada(string caso)
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var json = RespuestasOpenWeather.ClimaActual();
        switch (caso)
        {
            case "humedad_150": json["main"]!["humidity"] = 150; break;
            case "sin_temperatura": json["main"]!.AsObject().Remove("temp"); break;
            case "codigo_100": json["weather"]![0]!["id"] = 100; break;
        }

        Proveedor.ResponderJson(caso.StartsWith('{') ? caso : json.ToJsonString());

        var respuesta = await _cliente.GetAsync(Ruta(idUbicacion));

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
    }

    [Fact]
    public async Task TokenInvalido_Responde401_SinGuardarNiLlamarAlProveedor()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        ResponderClimaValido();

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), "token-invalido"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal(0, Proveedor.Llamadas);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
    }

    [Fact]
    public async Task LaApiKeyNoApareceEnLaRespuestaNiEnLaBaseDeDatos()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        ResponderClimaValido();

        var respuesta = await _cliente.GetAsync(Ruta(idUbicacion));

        Assert.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, Proveedor.Solicitudes.Single().Query);
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, await respuesta.Content.ReadAsStringAsync());
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, Assert.Single(await ConsultasDeAsync(idUbicacion)).Detalle);
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ElClimaActual_NoUsaUnPronosticoGuardadoComoCache()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, _fabrica.Reloj.GetUtcNow().UtcDateTime, TiposConsulta.Pronostico);
        ResponderClimaValido();

        var cuerpo = await ObtenerOkAsync(idUbicacion);

        Assert.False(cuerpo.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(1, Proveedor.Llamadas);
    }
}
