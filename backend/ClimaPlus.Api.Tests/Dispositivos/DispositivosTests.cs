using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClimaPlus.Api.Tests.Dispositivos.AyudasDispositivos;

namespace ClimaPlus.Api.Tests.Dispositivos;

/// <summary>Fase 7: /api/dispositivos (RF-20 a RF-23). JWT obligatorio, aislamiento por usuario, RN-BD-28 y RN-BD-30.</summary>
public sealed class DispositivosTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public DispositivosTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string token, object? cuerpo = null) =>
        _cliente.SendAsync(Ayudas.ConToken(metodo, ruta, token, cuerpo));

    private async Task<(string Token, long IdUbicacion)> UsuarioConUbicacionAsync()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        return (token, await AyudasDatos.CrearUbicacionAsync(_fabrica));
    }

    // ------------------------------------------------------------------ Autenticación

    [Theory]
    [InlineData("GET", Ruta)]
    [InlineData("POST", Ruta)]
    [InlineData("GET", Ruta + "/1")]
    [InlineData("PUT", Ruta + "/1")]
    [InlineData("PATCH", Ruta + "/1/estado")]
    [InlineData("GET", Ruta + "/1/lecturas")]
    [InlineData("GET", Ruta + "/1/alertas")]
    public async Task SinToken_Responde401(string metodo, string ruta)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "POST" or "PUT" or "PATCH")
        {
            solicitud.Content = JsonContent.Create(new { nombre = "ESP32", idUbicacion = 1, activo = true });
        }

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ApiKeyDeDispositivo_NoSirveComoSesionDeUsuario()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (_, apiKey) = await CrearAsync(_cliente, token, idUbicacion);
        var solicitud = new HttpRequestMessage(HttpMethod.Get, Ruta);
        solicitud.Headers.TryAddWithoutValidation(ApiKeyDispositivo.Cabecera, apiKey);

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ RF-21 Registrar

    [Fact]
    public async Task Registrar_Devuelve201_ConApiKeyUnaSolaVez_YGuardaSoloElHash()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();

        var respuesta = await RegistrarAsync(_cliente, token, "  ESP32   Patio  ", idUbicacion);

        var cuerpo = await LeerAsync(respuesta, HttpStatusCode.Created);
        Assert.Equal("no-store", respuesta.Headers.CacheControl?.ToString());
        var apiKey = cuerpo.GetProperty("apiKey").GetString()!;
        Assert.StartsWith(ApiKeyDispositivo.Prefijo, apiKey);
        Assert.Equal(47, apiKey.Length);
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("aviso").GetString()));

        var dispositivo = cuerpo.GetProperty("dispositivo");
        var id = dispositivo.GetProperty("idDispositivo").GetInt64();
        Assert.Equal("ESP32 Patio", dispositivo.GetProperty("nombre").GetString());   // espacios normalizados
        Assert.True(dispositivo.GetProperty("activo").GetBoolean());
        Assert.False(dispositivo.GetProperty("enLinea").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dispositivo.GetProperty("ultimaConexion").ValueKind);
        Assert.Equal(JsonValueKind.Null, dispositivo.GetProperty("ultimaLectura").ValueKind);
        Assert.Equal(idUbicacion, dispositivo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.EndsWith($"{Ruta}/{id}", respuesta.Headers.Location!.ToString());

        // RN-BD-28: en la base solo está el SHA-256 de la clave.
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var guardado = await db.Dispositivos.AsNoTracking().SingleAsync(d => d.IdDispositivo == id);
        Assert.Equal(ApiKeyDispositivo.Hash(apiKey), guardado.ApiKeyHash);
        Assert.Equal(64, guardado.ApiKeyHash.Length);
        Assert.DoesNotContain(apiKey, guardado.ApiKeyHash);
    }

    [Fact]
    public async Task ApiKey_NoApareceEnListaNiDetalle_NiEnLogs()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);

        var lista = await (await EnviarAsync(HttpMethod.Get, Ruta, token)).Content.ReadAsStringAsync();
        var detalle = await (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token)).Content.ReadAsStringAsync();

        Assert.DoesNotContain(apiKey, lista);
        Assert.DoesNotContain(apiKey, detalle);
        Assert.DoesNotContain("apiKey", lista, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(apiKey, string.Join("\n", _fabrica.Logs.Mensajes));
    }

    [Fact]
    public async Task Registrar_ClavesDistintas_ParaCadaDispositivo()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();

        var (_, primera) = await CrearAsync(_cliente, token, idUbicacion);
        var (_, segunda) = await CrearAsync(_cliente, token, idUbicacion);

        Assert.NotEqual(primera, segunda);
    }

    [Fact]
    public async Task Registrar_NombreRepetidoDelMismoUsuario_Responde409_PeroOtroUsuarioPuedeUsarlo()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (otroToken, _) = await UsuarioConUbicacionAsync();
        await CrearAsync(_cliente, token, idUbicacion, "Sensor Jardín");

        var repetido = await RegistrarAsync(_cliente, token, "Sensor Jardín", idUbicacion);
        var otroUsuario = await RegistrarAsync(_cliente, otroToken, "Sensor Jardín", idUbicacion);

        var problema = await Ayudas.LeerProblemaAsync(repetido);
        Assert.Equal(409, problema.GetProperty("status").GetInt32());
        Assert.True(problema.GetProperty("errors").TryGetProperty("nombre", out _));
        Assert.Equal(HttpStatusCode.Created, otroUsuario.StatusCode);
    }

    [Fact]
    public async Task Registrar_UbicacionInexistente_Responde404()
    {
        var (token, _) = await UsuarioConUbicacionAsync();

        var respuesta = await RegistrarAsync(_cliente, token, NombreUnico(), 999_999_999);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    public static TheoryData<string> CuerposInvalidos => new()
    {
        "{}",
        "{\"nombre\":\"\",\"idUbicacion\":1}",
        "{\"nombre\":\"   \",\"idUbicacion\":1}",
        "{\"nombre\":\"<script>alert(1)</script>\",\"idUbicacion\":1}",
        "{\"nombre\":\"ESP32\",\"idUbicacion\":0}",
        "{\"nombre\":\"ESP32\"}",
        "{\"nombre\":\"" + new string('a', 101) + "\",\"idUbicacion\":1}",
        "{\"nombre\":\"ESP32\",\"idUbicacion\":1,\"apiKeyHash\":\"x\"}",
        "{\"nombre\":\"ESP32\",\"idUbicacion\":1,\"idUsuario\":1}",
    };

    [Theory]
    [MemberData(nameof(CuerposInvalidos))]
    public async Task Registrar_DatosInvalidos_Responde400(string json)
    {
        var (token, _) = await UsuarioConUbicacionAsync();
        var solicitud = Ayudas.ConToken(HttpMethod.Post, Ruta, token);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ RF-20 / RF-23 Listar y detalle

    [Fact]
    public async Task Listar_SoloDevuelveLosDelUsuario_MasRecientesPrimero()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (otroToken, _) = await UsuarioConUbicacionAsync();
        var (primero, _) = await CrearAsync(_cliente, token, idUbicacion);
        var (segundo, _) = await CrearAsync(_cliente, token, idUbicacion);
        await CrearAsync(_cliente, otroToken, idUbicacion);

        var lista = await LeerAsync(await EnviarAsync(HttpMethod.Get, Ruta, token), HttpStatusCode.OK);

        Assert.Equal(new[] { segundo, primero }, lista.EnumerateArray().Select(d => d.GetProperty("idDispositivo").GetInt64()).ToArray());
    }

    [Fact]
    public async Task Listar_SinDispositivos_DevuelveListaVacia()
    {
        var (token, _) = await UsuarioConUbicacionAsync();

        var lista = await LeerAsync(await EnviarAsync(HttpMethod.Get, Ruta, token), HttpStatusCode.OK);

        Assert.Equal(0, lista.GetArrayLength());
    }

    [Fact]
    public async Task DispositivoAjeno_SeTrataComoInexistente_404()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (intruso, _) = await UsuarioConUbicacionAsync();
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);

        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", intruso)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", intruso, new { nombre = "Robado", idUbicacion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", intruso, new { activo = false })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}/lecturas", intruso)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}/alertas", intruso)).StatusCode);

        // El intento ajeno no cambió nada: el dispositivo sigue activo y puede enviar lecturas.
        var detalle = await LeerAsync(await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token), HttpStatusCode.OK);
        Assert.True(detalle.GetProperty("activo").GetBoolean());
        Assert.NotEqual("Robado", detalle.GetProperty("nombre").GetString());
        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 20 })).StatusCode);
    }

    [Fact]
    public async Task Detalle_Inexistente_Responde404()
    {
        var (token, _) = await UsuarioConUbicacionAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/999999999", token)).StatusCode);
    }

    // ------------------------------------------------------------------ RF-22 Editar y estado

    [Fact]
    public async Task Actualizar_CambiaNombreYUbicacion()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var nueva = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Tactic");
        var (id, _) = await CrearAsync(_cliente, token, idUbicacion);

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token, new { nombre = "Estación Tactic", idUbicacion = nueva });

        var cuerpo = await LeerAsync(respuesta, HttpStatusCode.OK);
        Assert.Equal("Estación Tactic", cuerpo.GetProperty("nombre").GetString());
        Assert.Equal(nueva, cuerpo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Tactic", cuerpo.GetProperty("ubicacion").GetProperty("nombre").GetString());
    }

    [Fact]
    public async Task Actualizar_ConSuMismoNombre_EsValido_PeroConElDeOtroDispositivo_Responde409()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, _) = await CrearAsync(_cliente, token, idUbicacion, "Sala");
        await CrearAsync(_cliente, token, idUbicacion, "Cocina");

        var mismo = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token, new { nombre = "Sala", idUbicacion });
        var ajeno = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token, new { nombre = "Cocina", idUbicacion });

        Assert.Equal(HttpStatusCode.OK, mismo.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, ajeno.StatusCode);
    }

    [Fact]
    public async Task Actualizar_UbicacionInexistente_Responde404()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, _) = await CrearAsync(_cliente, token, idUbicacion);

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token, new { nombre = "Otro", idUbicacion = 999_999_999 });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Desactivar_RevocaLaApiKey_YActivarLaRestablece()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);

        var desactivado = await LeerAsync(await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, new { activo = false }), HttpStatusCode.OK);
        var rechazada = await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 20 });

        Assert.False(desactivado.GetProperty("activo").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, rechazada.StatusCode);

        // Idempotente: desactivar otra vez responde igual.
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, new { activo = false })).StatusCode);

        var activado = await LeerAsync(await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, new { activo = true }), HttpStatusCode.OK);
        Assert.True(activado.GetProperty("activo").GetBoolean());
        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 20 })).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"activo\":\"no\"}")]
    [InlineData("{\"activo\":true,\"nombre\":\"x\"}")]
    public async Task CambiarEstado_CuerpoInvalido_Responde400(string json)
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, _) = await CrearAsync(_cliente, token, idUbicacion);
        var solicitud = Ayudas.ConToken(HttpMethod.Patch, $"{Ruta}/{id}/estado", token);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.SendAsync(solicitud)).StatusCode);
    }

    // ------------------------------------------------------------------ RF-23 Lecturas y alertas del dispositivo

    [Fact]
    public async Task Lecturas_SePaginanDe20_MasRecientesPrimero()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);
        for (var i = 0; i < 21; i++)
        {
            _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(1));
            Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { luz = i })).StatusCode);
        }

        var pagina1 = await LeerAsync(await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}/lecturas", token), HttpStatusCode.OK);
        var pagina2 = await LeerAsync(await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}/lecturas?pagina=2", token), HttpStatusCode.OK);

        Assert.Equal(21, pagina1.GetProperty("totalElementos").GetInt32());
        Assert.Equal(2, pagina1.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(20, pagina1.GetProperty("elementos").GetArrayLength());
        Assert.Equal(20, pagina1.GetProperty("elementos")[0].GetProperty("luz").GetInt32());
        Assert.Equal(0, pagina2.GetProperty("elementos")[0].GetProperty("luz").GetInt32());
    }

    [Fact]
    public async Task Lecturas_PaginaInvalida_Responde400()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, _) = await CrearAsync(_cliente, token, idUbicacion);

        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}/lecturas?pagina=0", token)).StatusCode);
    }

    [Fact]
    public async Task Detalle_MuestraUltimaLecturaYEnLinea_YSeVuelveFueraDeLineaAlPasar5Minutos()
    {
        var (token, idUbicacion) = await UsuarioConUbicacionAsync();
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);
        await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 18.5m, humedad = 70, lluvia = false, luz = 40 });
        _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(1));
        var ultima = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 19.25m, lluvia = true }), HttpStatusCode.Created);

        var detalle = await LeerAsync(await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token), HttpStatusCode.OK);

        Assert.True(detalle.GetProperty("enLinea").GetBoolean());
        Assert.Equal(ultima.GetProperty("fechaLectura").GetDateTime(), detalle.GetProperty("ultimaConexion").GetDateTime());
        var lectura = detalle.GetProperty("ultimaLectura");
        Assert.Equal(ultima.GetProperty("idLectura").GetInt64(), lectura.GetProperty("idLectura").GetInt64());
        Assert.Equal(19.25m, lectura.GetProperty("temperatura").GetDecimal());
        Assert.True(lectura.GetProperty("lluvia").GetBoolean());
        Assert.Equal(JsonValueKind.Null, lectura.GetProperty("humedad").ValueKind);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var despues = await LeerAsync(await EnviarAsync(HttpMethod.Get, Ruta, token), HttpStatusCode.OK);
        Assert.False(despues.EnumerateArray().Single(d => d.GetProperty("idDispositivo").GetInt64() == id).GetProperty("enLinea").GetBoolean());
    }
}
