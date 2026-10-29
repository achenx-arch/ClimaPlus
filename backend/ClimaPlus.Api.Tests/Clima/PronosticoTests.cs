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
/// GET /api/clima/pronostico (Fase 2E): consulta PRONOSTICO guardada con las mediciones en NULL y
/// los días y bloques en detalle (E-03, RN-BD-43). Caché en la BD de 30 min (D-23).
/// </summary>
public sealed class PronosticoTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public PronosticoTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private ProveedorOpenWeatherSimulado Proveedor => _fabrica.Proveedor;

    private static string Ruta(long idUbicacion) => $"/api/clima/pronostico?idUbicacion={idUbicacion}";

    private void ResponderPronosticoValido() =>
        Proveedor.ResponderJson(RespuestasOpenWeather.PronosticoDesde(_fabrica.Reloj.GetUtcNow().UtcDateTime).ToJsonString());

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

    private async Task<int> AlertasDeAsync(long idUbicacion)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.Alertas.CountAsync(a => a.IdUbicacion == idUbicacion);
    }

    [Fact]
    public async Task Visitante_GuardaPronosticoSinMediciones_ConDiasYBloquesEnDetalle()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Cobán");
        ResponderPronosticoValido();

        var cuerpo = await ObtenerOkAsync(idUbicacion);

        var fila = Assert.Single(await ConsultasDeAsync(idUbicacion));
        Assert.Null(fila.IdUsuario);
        Assert.Equal(TiposConsulta.Pronostico, fila.TipoConsulta);
        Assert.Null(fila.Temperatura);
        Assert.Null(fila.Humedad);
        Assert.Null(fila.DescripcionCondicion);
        using (var detalle = JsonDocument.Parse(fila.Detalle!))
        {
            Assert.Equal(1, detalle.RootElement.GetProperty("version").GetInt32());
            Assert.True(detalle.RootElement.GetProperty("dias").GetArrayLength() is >= 1 and <= 5);
        }

        Assert.Equal(fila.IdConsulta, cuerpo.GetProperty("consulta").GetProperty("idConsulta").GetInt64());
        Assert.False(cuerpo.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(-21600, cuerpo.GetProperty("zonaHorariaSegundos").GetInt32());
        var dias = cuerpo.GetProperty("dias");
        Assert.True(dias.GetArrayLength() is >= 1 and <= 5);
        var dia = dias[0];
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", dia.GetProperty("fecha").GetString());
        Assert.True(dia.GetProperty("bloques").GetArrayLength() is >= 1 and <= 8);
        foreach (var campo in new[] { "completo", "temperaturaMax", "temperaturaMin", "codigoCondicion", "descripcion", "probabilidadLluvia" })
        {
            Assert.True(dia.TryGetProperty(campo, out _), campo);
        }

        var solicitud = Assert.Single(Proveedor.Solicitudes);
        Assert.Equal("/data/2.5/forecast", solicitud.AbsolutePath);
        Assert.Contains("units=metric", solicitud.Query);
        Assert.Contains("lang=es", solicitud.Query);
    }

    [Fact]
    public async Task ConSesion_GuardaElUsuario_YNuncaGeneraAlertas()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        ResponderPronosticoValido();

        await ObtenerOkAsync(idUbicacion, token);

        Assert.Equal(await AyudasDatos.IdUsuarioAsync(_fabrica, correo), Assert.Single(await ConsultasDeAsync(idUbicacion)).IdUsuario);
        Assert.Equal(0, await AlertasDeAsync(idUbicacion));   // RN-BD-34
    }

    [Fact]
    public async Task Cache_DentroDe30Minutos_ReutilizaElPronostico_YDespuesVuelveAConsultar()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        ResponderPronosticoValido();

        var primera = await ObtenerOkAsync(idUbicacion);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(20));
        var segunda = await ObtenerOkAsync(idUbicacion);

        Assert.Equal(1, Proveedor.Llamadas);
        Assert.True(segunda.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(primera.GetProperty("dias").GetRawText(), segunda.GetProperty("dias").GetRawText());

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));   // 31 min desde que se pidió a OpenWeather
        ResponderPronosticoValido();
        var tercera = await ObtenerOkAsync(idUbicacion);

        Assert.Equal(1, Proveedor.Llamadas);   // ResponderPronosticoValido reinicia el contador
        Assert.False(tercera.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(3, (await ConsultasDeAsync(idUbicacion)).Count);
    }

    [Fact]
    public async Task ElPronostico_NoUsaUnClimaActualGuardadoComoCache()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, _fabrica.Reloj.GetUtcNow().UtcDateTime, TiposConsulta.Actual,
            detalle: "{\"version\":1,\"observadoEnUtc\":\"2026-10-27T18:20:00Z\",\"zonaHorariaSegundos\":-21600,\"esDeDia\":true}");
        ResponderPronosticoValido();

        var cuerpo = await ObtenerOkAsync(idUbicacion);

        Assert.False(cuerpo.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(1, Proveedor.Llamadas);
    }

    [Theory]
    [InlineData("sin_bloques")]
    [InlineData("sin_zona_horaria")]
    [InlineData("humedad_150")]
    [InlineData("todo_en_el_pasado")]
    public async Task PronosticoInvalido_Responde502_YNoGuardaNada(string caso)
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var json = caso == "todo_en_el_pasado"
            ? RespuestasOpenWeather.Pronostico(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            : RespuestasOpenWeather.PronosticoDesde(_fabrica.Reloj.GetUtcNow().UtcDateTime);
        switch (caso)
        {
            case "sin_bloques": json["list"] = new System.Text.Json.Nodes.JsonArray(); break;
            case "sin_zona_horaria": json["city"]!.AsObject().Remove("timezone"); break;
            case "humedad_150": json["list"]![3]!["main"]!["humidity"] = 150; break;
        }

        Proveedor.ResponderJson(json.ToJsonString());

        var respuesta = await _cliente.GetAsync(Ruta(idUbicacion));

        Assert.Equal(HttpStatusCode.BadGateway, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
    }

    [Fact]
    public async Task ErrorDelProveedor_Responde503_YUbicacionInexistente404()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        Proveedor.ResponderEstado(HttpStatusCode.ServiceUnavailable);

        var caido = await _cliente.GetAsync(Ruta(idUbicacion));
        var inexistente = await _cliente.GetAsync(Ruta(987_654_321));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, caido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
        Assert.Equal(1, Proveedor.Llamadas);
    }

    [Theory]
    [InlineData("/api/clima/pronostico")]
    [InlineData("/api/clima/pronostico?idUbicacion=0")]
    [InlineData("/api/clima/pronostico?idUbicacion=x")]
    public async Task ParametroInvalido_Responde400(string ruta)
    {
        ResponderPronosticoValido();

        var respuesta = await _cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, Proveedor.Llamadas);
    }

    [Fact]
    public async Task UsuarioDesactivado_Responde401()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);
        ResponderPronosticoValido();

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), token));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(await ConsultasDeAsync(idUbicacion));
    }
}
