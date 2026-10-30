using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ClimaPlus.Api.Tests.Dispositivos.AyudasDispositivos;

namespace ClimaPlus.Api.Tests.Dispositivos;

/// <summary>
/// Fase 7: POST /api/lecturas (RF-24). Autenticación por X-Device-Key (RN-BD-28, RN-BD-29), validación de rangos
/// (RN-BD-31), fecha del servidor y última conexión (RN-BD-32) y alertas con origen ESP32 (RN-BD-23, 27, 33).
/// </summary>
public sealed class LecturasTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public LecturasTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private async Task<(string Correo, string Token, long IdUbicacion, long IdDispositivo, string ApiKey)> DispositivoAsync(string nombreUbicacion = "Cobán")
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica, nombreUbicacion);
        var (id, apiKey) = await CrearAsync(_cliente, token, idUbicacion);
        return (correo, token, idUbicacion, id, apiKey);
    }

    private async Task<JsonElement> AlertasDelUsuarioAsync(string token)
    {
        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/alertas", token));
        return (await LeerAsync(respuesta, HttpStatusCode.OK)).GetProperty("elementos");
    }

    // ------------------------------------------------------------------ Autenticación del dispositivo

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("clave-sin-prefijo")]
    [InlineData("cpk_esta-clave-no-existe-en-la-base-de-datos-0000")]
    public async Task SinApiKeyValida_Responde401(string? apiKey)
    {
        var respuesta = await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 20 });

        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(401, problema.GetProperty("status").GetInt32());
        Assert.Equal("Dispositivo no autenticado", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ApiKeyDemasiadoLarga_Responde401()
    {
        var respuesta = await EnviarLecturaAsync(_cliente, ApiKeyDispositivo.Prefijo + new string('a', 200), new { temperatura = 20 });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task SinApiKey_Responde401_AunqueElCuerpoSeaInvalido()
    {
        // El filtro de autenticación corre antes de validar el cuerpo: no se revela qué campos espera la API.
        var respuesta = await EnviarLecturaAsync(_cliente, null, new { temperatura = 999, campoRaro = 1 });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task TokenDeUsuario_NoSirveParaEnviarLecturas()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, RutaLecturas, token, new { temperatura = 20 }));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task DispositivoDeCuentaDesactivada_Responde403()
    {
        var (correo, _, _, _, apiKey) = await DispositivoAsync();
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        var respuesta = await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 20 });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechazos_NoRegistranLaApiKeyEnLogs()
    {
        const string claveFalsa = "cpk_clave-que-nunca-debe-aparecer-en-los-logs-123";

        await EnviarLecturaAsync(_cliente, claveFalsa, new { temperatura = 20 });

        var logs = string.Join("\n", _fabrica.Logs.Mensajes);
        Assert.Contains("dispositivo_rechazado", logs);
        Assert.DoesNotContain(claveFalsa, logs);
    }

    // ------------------------------------------------------------------ Registro de la lectura

    [Fact]
    public async Task LecturaValida_Responde201_GuardaConFechaDelServidor_YActualizaUltimaConexion()
    {
        var (_, _, _, idDispositivo, apiKey) = await DispositivoAsync();
        var antes = _fabrica.Reloj.GetUtcNow().UtcDateTime;

        var respuesta = await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 24.567m, humedad = 68, lluvia = false, luz = 72 });

        var cuerpo = await LeerAsync(respuesta, HttpStatusCode.Created);
        var idLectura = cuerpo.GetProperty("idLectura").GetInt64();
        var fecha = cuerpo.GetProperty("fechaLectura").GetDateTime().ToUniversalTime();
        Assert.Equal(idDispositivo, cuerpo.GetProperty("idDispositivo").GetInt64());
        Assert.Equal(0, cuerpo.GetProperty("alertasGeneradas").GetArrayLength());
        Assert.InRange(fecha, antes.AddSeconds(-1), _fabrica.Reloj.GetUtcNow().UtcDateTime.AddSeconds(1));
        Assert.EndsWith($"/api/dispositivos/{idDispositivo}/lecturas", respuesta.Headers.Location!.ToString());

        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var lectura = await db.LecturasSensor.AsNoTracking().SingleAsync(l => l.IdLectura == idLectura);
        var dispositivo = await db.Dispositivos.AsNoTracking().SingleAsync(d => d.IdDispositivo == idDispositivo);
        Assert.Equal(24.57m, lectura.Temperatura);   // numeric(5,2)
        Assert.Equal(68m, lectura.Humedad);
        Assert.False(lectura.Lluvia);
        Assert.Equal((short)72, lectura.Luz);
        Assert.Equal(lectura.FechaLectura, dispositivo.UltimaConexion);   // RN-BD-32 / D-12
    }

    [Fact]
    public async Task LecturaConUnaSolaMedicion_EsValida()
    {
        var (_, _, _, _, apiKey) = await DispositivoAsync();

        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { lluvia = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { luz = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = -40 })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { humedad = 100 })).StatusCode);
    }

    public static TheoryData<string> LecturasInvalidas => new()
    {
        "{}",                                            // RN-BD-31: al menos una medición
        "{\"temperatura\":null,\"humedad\":null}",
        "{\"temperatura\":85.01}",
        "{\"temperatura\":-40.5}",
        "{\"humedad\":-1}",
        "{\"humedad\":100.5}",
        "{\"luz\":101}",
        "{\"luz\":-1}",
        "{\"luz\":50.5}",                                // la luz es un entero (D-08)
        "{\"lluvia\":\"si\"}",
        "{\"temperatura\":\"veinte\"}",
        "{\"temperatura\":20,\"fechaLectura\":\"2020-01-01T00:00:00Z\"}",   // RN-BD-32: la fecha es del servidor
        "{\"temperatura\":20,\"idDispositivo\":1}",
        "no-es-json",
    };

    [Theory]
    [MemberData(nameof(LecturasInvalidas))]
    public async Task LecturaInvalida_Responde400_YNoGuardaNada(string json)
    {
        var (_, _, _, idDispositivo, apiKey) = await DispositivoAsync();
        var solicitud = new HttpRequestMessage(HttpMethod.Post, RutaLecturas)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        solicitud.Headers.TryAddWithoutValidation(ApiKeyDispositivo.Cabecera, apiKey);

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        Assert.False(await db.LecturasSensor.AnyAsync(l => l.IdDispositivo == idDispositivo));
        Assert.Null((await db.Dispositivos.AsNoTracking().SingleAsync(d => d.IdDispositivo == idDispositivo)).UltimaConexion);
    }

    // ------------------------------------------------------------------ Alertas con origen ESP32

    [Fact]
    public async Task TemperaturaSobreElUmbral_GeneraAlertaConOrigenEsp32()
    {
        var (_, token, idUbicacion, idDispositivo, apiKey) = await DispositivoAsync("Cobán");
        await CrearReglaAsync(_cliente, token, TiposAlerta.TemperaturaAlta, idUbicacion, 30m);

        var cuerpo = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 33.4m, humedad = 50 }), HttpStatusCode.Created);

        var generada = Assert.Single(cuerpo.GetProperty("alertasGeneradas").EnumerateArray());
        Assert.Equal(TiposAlerta.TemperaturaAlta, generada.GetProperty("tipo").GetString());
        Assert.Equal("Cobán: 33.4 C supera tu umbral de 30 C", generada.GetProperty("mensaje").GetString());

        var alerta = Assert.Single((await AlertasDelUsuarioAsync(token)).EnumerateArray());
        var origen = alerta.GetProperty("origen");
        Assert.Equal("ESP32", origen.GetProperty("tipo").GetString());
        Assert.Equal(cuerpo.GetProperty("idLectura").GetInt64(), origen.GetProperty("idLectura").GetInt64());
        Assert.Equal(idDispositivo, origen.GetProperty("idDispositivo").GetInt64());
        Assert.Equal(JsonValueKind.Null, origen.GetProperty("idConsulta").ValueKind);
        Assert.Equal(33.4m, alerta.GetProperty("valorDetectado").GetDecimal());
        Assert.Equal(30m, alerta.GetProperty("valorUmbral").GetDecimal());

        // M12: la misma alerta aparece en las alertas del dispositivo.
        var delDispositivo = await LeerAsync(
            await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, $"{Ruta}/{idDispositivo}/alertas", token)), HttpStatusCode.OK);
        Assert.Equal(alerta.GetProperty("idAlerta").GetInt64(),
            Assert.Single(delDispositivo.GetProperty("elementos").EnumerateArray()).GetProperty("idAlerta").GetInt64());
    }

    [Fact]
    public async Task LluviaDetectada_GeneraAlertaLluviaSinValores_YSinLluviaNoGenera()
    {
        var (_, token, _, _, apiKey) = await DispositivoAsync("Tactic");
        await CrearReglaAsync(_cliente, token, TiposAlerta.Lluvia, null, null);   // regla global

        var seca = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { lluvia = false }), HttpStatusCode.Created);
        var mojada = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { lluvia = true }), HttpStatusCode.Created);

        Assert.Equal(0, seca.GetProperty("alertasGeneradas").GetArrayLength());
        var generada = Assert.Single(mojada.GetProperty("alertasGeneradas").EnumerateArray());
        Assert.Equal("Lluvia detectada en Tactic", generada.GetProperty("mensaje").GetString());
        var alerta = Assert.Single((await AlertasDelUsuarioAsync(token)).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, alerta.GetProperty("valorDetectado").ValueKind);   // RN-BD-24
    }

    [Fact]
    public async Task SinTemperatura_NoSeEvaluanReglasDeTemperatura()
    {
        var (_, token, idUbicacion, _, apiKey) = await DispositivoAsync();
        await CrearReglaAsync(_cliente, token, TiposAlerta.TemperaturaBaja, idUbicacion, 10m);

        var cuerpo = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { humedad = 90, luz = 5 }), HttpStatusCode.Created);

        Assert.Equal(0, cuerpo.GetProperty("alertasGeneradas").GetArrayLength());
    }

    [Fact]
    public async Task ReglaDeOtraUbicacion_NoAplica_AlDispositivo()
    {
        var (_, token, _, _, apiKey) = await DispositivoAsync();
        var otra = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Salamá");
        await CrearReglaAsync(_cliente, token, TiposAlerta.TemperaturaAlta, otra, 20m);

        var cuerpo = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 35 }), HttpStatusCode.Created);

        Assert.Equal(0, cuerpo.GetProperty("alertasGeneradas").GetArrayLength());
    }

    [Fact]
    public async Task AntiDuplicado_60Minutos_SeAplicaTambienALasLecturas()
    {
        var (_, token, idUbicacion, _, apiKey) = await DispositivoAsync();
        await CrearReglaAsync(_cliente, token, TiposAlerta.TemperaturaAlta, idUbicacion, 30m);

        var primera = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 31 }), HttpStatusCode.Created);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(59));
        var segunda = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 32 }), HttpStatusCode.Created);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(1));
        var tercera = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 33 }), HttpStatusCode.Created);

        Assert.Equal(1, primera.GetProperty("alertasGeneradas").GetArrayLength());
        Assert.Equal(0, segunda.GetProperty("alertasGeneradas").GetArrayLength());   // RN-BD-27
        Assert.Equal(1, tercera.GetProperty("alertasGeneradas").GetArrayLength());   // a los 60 minutos exactos ya se permite
        Assert.Equal(2, (await AlertasDelUsuarioAsync(token)).GetArrayLength());
    }

    [Fact]
    public async Task UsaLasReglasDelDueno_NoLasDeOtroUsuario()
    {
        var (_, _, idUbicacion, _, apiKey) = await DispositivoAsync();
        var (_, otroToken) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await CrearReglaAsync(_cliente, otroToken, TiposAlerta.TemperaturaAlta, idUbicacion, 10m);

        var cuerpo = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 30 }), HttpStatusCode.Created);

        Assert.Equal(0, cuerpo.GetProperty("alertasGeneradas").GetArrayLength());
        Assert.Equal(0, (await AlertasDelUsuarioAsync(otroToken)).GetArrayLength());
    }

    [Fact]
    public async Task CambiarUbicacion_LasLecturasSiguientesUsanLaNueva()
    {
        var (_, token, _, idDispositivo, apiKey) = await DispositivoAsync("Cobán");
        var nueva = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Chisec");
        await CrearReglaAsync(_cliente, token, TiposAlerta.TemperaturaAlta, nueva, 25m);
        var mover = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, $"{Ruta}/{idDispositivo}", token, new { nombre = "Movido", idUbicacion = nueva }));
        Assert.Equal(HttpStatusCode.OK, mover.StatusCode);

        var cuerpo = await LeerAsync(await EnviarLecturaAsync(_cliente, apiKey, new { temperatura = 28 }), HttpStatusCode.Created);

        var generada = Assert.Single(cuerpo.GetProperty("alertasGeneradas").EnumerateArray());
        Assert.StartsWith("Chisec:", generada.GetProperty("mensaje").GetString());
    }
}

/// <summary>Rate limiting de POST /api/lecturas, por dispositivo.</summary>
public sealed class LecturasRateLimitingTests : IClassFixture<ClimaPlusApiFactoryLimiteBajo>
{
    private readonly ClimaPlusApiFactoryLimiteBajo _fabrica;
    private readonly HttpClient _cliente;

    public LecturasRateLimitingTests(ClimaPlusApiFactoryLimiteBajo fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task SuperarElLimite_Responde429_SoloParaEseDispositivo()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (_, apiKey) = await CrearAsync(_cliente, token, idUbicacion);
        var (_, otraApiKey) = await CrearAsync(_cliente, token, idUbicacion);

        for (var i = 0; i < ClimaPlusApiFactoryLimiteBajo.Limite; i++)
        {
            Assert.Equal(HttpStatusCode.Created, (await EnviarLecturaAsync(_cliente, apiKey, new { luz = i })).StatusCode);
        }

        var excedida = await EnviarLecturaAsync(_cliente, apiKey, new { luz = 99 });
        var otroDispositivo = await EnviarLecturaAsync(_cliente, otraApiKey, new { luz = 1 });

        Assert.Equal(HttpStatusCode.TooManyRequests, excedida.StatusCode);
        Assert.True(excedida.Headers.Contains("Retry-After"));
        Assert.Equal(HttpStatusCode.Created, otroDispositivo.StatusCode);
    }
}
