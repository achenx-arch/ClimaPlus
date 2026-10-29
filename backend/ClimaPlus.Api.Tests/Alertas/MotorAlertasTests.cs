using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Alertas;

/// <summary>
/// Motor de alertas (Fase 2H, RF-16) a través de GET /api/clima/actual con el proveedor simulado:
/// nunca se llama a OpenWeather real. Cada prueba usa una ubicación y usuarios nuevos.
/// </summary>
public sealed class MotorAlertasTests : IClassFixture<ClimaPlusApiFactory>
{
    private const short Despejado = 800;
    private const short LluviaLigera = 500;

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public MotorAlertasTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private ProveedorOpenWeatherSimulado Proveedor => _fabrica.Proveedor;

    private static string Ruta(long idUbicacion) => $"/api/clima/actual?idUbicacion={idUbicacion}";

    /// <summary>OpenWeather simulado con una temperatura y un código de condición concretos.</summary>
    private void ResponderClima(decimal temperatura, short codigo = Despejado)
    {
        var json = RespuestasOpenWeather.ClimaActual();
        json["main"]!["temp"] = temperatura;
        json["main"]!["feels_like"] = temperatura - 10m;   // la sensación térmica nunca se compara
        json["weather"]![0]!["id"] = codigo;
        Proveedor.ResponderJson(json.ToJsonString());
    }

    private async Task<(string Token, long IdUsuario)> UsuarioAsync()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        return (token, await AyudasDatos.IdUsuarioAsync(_fabrica, correo));
    }

    private async Task<(string Token, long IdUsuario, long IdUbicacion)> PrepararAsync(string ubicacion = "Cobán")
    {
        var (token, idUsuario) = await UsuarioAsync();
        return (token, idUsuario, await AyudasDatos.CrearUbicacionAsync(_fabrica, ubicacion));
    }

    /// <summary>Regla creada directamente en la base InMemory de pruebas.</summary>
    private async Task CrearReglaAsync(long idUsuario, string tipo, decimal? umbral = null, long? idUbicacion = null, bool activa = true)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        db.ConfiguracionesAlerta.Add(new ConfiguracionAlerta
        {
            IdUsuario = idUsuario,
            IdUbicacion = idUbicacion,
            Tipo = tipo,
            ValorUmbral = umbral,
            Activa = activa,
            FechaCreacion = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> ConsultarAsync(long idUbicacion, string? token)
    {
        var respuesta = token is null
            ? await _cliente.GetAsync(Ruta(idUbicacion))
            : await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement[] Generadas(JsonElement clima) => clima.GetProperty("alertasGeneradas").EnumerateArray().ToArray();

    private static string[] TiposGenerados(JsonElement clima) =>
        Generadas(clima).Select(a => a.GetProperty("tipo").GetString()!).ToArray();

    private async Task<List<Alerta>> AlertasDeUbicacionAsync(long idUbicacion)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.Alertas.AsNoTracking().Where(a => a.IdUbicacion == idUbicacion).OrderBy(a => a.IdAlerta).ToListAsync();
    }

    private async Task<int> ConsultasDeUbicacionAsync(long idUbicacion)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.ConsultasClima.CountAsync(c => c.IdUbicacion == idUbicacion);
    }

    // ================================================================== Quién genera alertas

    [Fact]
    public async Task Visitante_NoGeneraAlertas_YRecibeListaVacia()
    {
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 20m);
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(35m, LluviaLigera);

        var clima = await ConsultarAsync(idUbicacion, token: null);

        Assert.Empty(Generadas(clima));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
        Assert.Equal(1, await ConsultasDeUbicacionAsync(idUbicacion));   // la consulta anónima sí se guarda (E-02)
    }

    [Fact]
    public async Task UsuarioSinReglas_NoGeneraAlertas()
    {
        var (token, _, idUbicacion) = await PrepararAsync();
        ResponderClima(45m, LluviaLigera);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Empty(Generadas(clima));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    [Fact]
    public async Task ReglasDeOtroUsuario_NoAplican()
    {
        var (token, _, idUbicacion) = await PrepararAsync();
        var (_, idOtro) = await UsuarioAsync();
        await CrearReglaAsync(idOtro, TiposAlerta.TemperaturaAlta, 20m);
        await CrearReglaAsync(idOtro, TiposAlerta.Lluvia, idUbicacion: idUbicacion);
        ResponderClima(35m, LluviaLigera);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Empty(Generadas(clima));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    [Fact]
    public async Task ReglaInactiva_NoGenera()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 20m, activa: false);
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia, idUbicacion: idUbicacion, activa: false);
        ResponderClima(35m, LluviaLigera);

        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    // ================================================================== Lo que se guarda

    [Fact]
    public async Task TemperaturaAlta_GuardaAlertaConLaConsultaDeOrigen()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync("Cobán");
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 32m, idUbicacion);
        ResponderClima(33.4m);

        var clima = await ConsultarAsync(idUbicacion, token);

        var generada = Assert.Single(Generadas(clima));
        var alerta = Assert.Single(await AlertasDeUbicacionAsync(idUbicacion));
        Assert.Equal(alerta.IdAlerta, generada.GetProperty("idAlerta").GetInt64());
        Assert.Equal("TEMPERATURA_ALTA", generada.GetProperty("tipo").GetString());
        Assert.Equal("Cobán: 33.4 C supera tu umbral de 32 C", generada.GetProperty("mensaje").GetString());
        Assert.Equal(alerta.FechaGeneracion, generada.GetProperty("fechaGeneracion").GetDateTime().ToUniversalTime());

        Assert.Equal(idUsuario, alerta.IdUsuario);
        Assert.Equal(idUbicacion, alerta.IdUbicacion);
        Assert.Equal(clima.GetProperty("consulta").GetProperty("idConsulta").GetInt64(), alerta.IdConsulta);
        Assert.Null(alerta.IdLectura);
        Assert.Equal(TiposAlerta.TemperaturaAlta, alerta.Tipo);
        Assert.Equal(33.4m, alerta.ValorDetectado);
        Assert.Equal(clima.GetProperty("temperatura").GetDecimal(), alerta.ValorDetectado);
        Assert.Equal(32m, alerta.ValorUmbral);
        Assert.Equal("Cobán: 33.4 C supera tu umbral de 32 C", alerta.Mensaje);
        Assert.Equal(EstadosAlerta.Nueva, alerta.Estado);
        Assert.Equal(DateTimeKind.Utc, alerta.FechaGeneracion.Kind);
        Assert.InRange(alerta.FechaGeneracion, _fabrica.Reloj.GetUtcNow().UtcDateTime.AddMinutes(-1), _fabrica.Reloj.GetUtcNow().UtcDateTime);

        // La consulta de origen es ACTUAL y del mismo usuario (RN-BD-42).
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var origen = await db.ConsultasClima.AsNoTracking().SingleAsync(c => c.IdConsulta == alerta.IdConsulta);
        Assert.Equal(idUsuario, origen.IdUsuario);
        Assert.Equal(TiposConsulta.Actual, origen.TipoConsulta);
    }

    [Fact]
    public async Task TemperaturaBaja_GuardaValorYUmbral()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync("Zunil");
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaBaja, 5m);
        ResponderClima(-1.5m);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Equal(new[] { "TEMPERATURA_BAJA" }, TiposGenerados(clima));
        var alerta = Assert.Single(await AlertasDeUbicacionAsync(idUbicacion));
        Assert.Equal(-1.5m, alerta.ValorDetectado);
        Assert.Equal(5m, alerta.ValorUmbral);
        Assert.Equal("Zunil: -1.5 C está por debajo de tu umbral de 5 C", alerta.Mensaje);
    }

    [Fact]
    public async Task Lluvia_GuardaValoresNull()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync("Cobán");
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(18m, LluviaLigera);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Equal(new[] { "LLUVIA" }, TiposGenerados(clima));
        var alerta = Assert.Single(await AlertasDeUbicacionAsync(idUbicacion));
        Assert.Null(alerta.ValorDetectado);
        Assert.Null(alerta.ValorUmbral);
        Assert.Equal("Lluvia detectada en Cobán", alerta.Mensaje);
        Assert.NotNull(alerta.IdConsulta);
        Assert.Null(alerta.IdLectura);
    }

    [Theory]
    [InlineData(TiposAlerta.TemperaturaAlta)]
    [InlineData(TiposAlerta.TemperaturaBaja)]
    public async Task IgualdadExacta_NoGenera(string tipo)
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, tipo, 25m);
        ResponderClima(25m);

        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
    }

    [Fact]
    public async Task SeComparaTemperaturaReal_NoSensacionTermica()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaBaja, 20m);
        ResponderClima(25m);   // sensación térmica 15 °C: no debe disparar la baja de 20 °C

        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
    }

    // ================================================================== Prioridad de reglas

    [Fact]
    public async Task ReglaGlobal_AplicaACualquierUbicacionConsultada()
    {
        var (token, idUsuario, coban) = await PrepararAsync();
        var salama = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Salamá");
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        ResponderClima(31m);

        Assert.Single(Generadas(await ConsultarAsync(coban, token)));
        Assert.Single(Generadas(await ConsultarAsync(salama, token)));
    }

    [Fact]
    public async Task ReglaEspecifica_PrevaleceSobreGlobal()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 35m, idUbicacion);
        ResponderClima(33m);

        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    [Fact]
    public async Task ReglaEspecificaInactiva_NoBloqueaLaGlobal()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 35m, idUbicacion, activa: false);
        ResponderClima(33m);

        Assert.Single(Generadas(await ConsultarAsync(idUbicacion, token)));
        Assert.Equal(30m, Assert.Single(await AlertasDeUbicacionAsync(idUbicacion)).ValorUmbral);
    }

    [Fact]
    public async Task AltaYBajaSimultaneas_YLluvia_GeneranTresAlertasEnUnaConsulta()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 20m);
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaBaja, 25m, idUbicacion);
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(22m, 300);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Equal(new[] { "TEMPERATURA_ALTA", "TEMPERATURA_BAJA", "LLUVIA" }, TiposGenerados(clima));
        var alertas = await AlertasDeUbicacionAsync(idUbicacion);
        Assert.Equal(3, alertas.Count);
        Assert.Single(alertas.Select(a => a.IdConsulta).Distinct());
    }

    // ================================================================== Anti-duplicado (RN-BD-27, DP-8)

    [Fact]
    public async Task Duplicado_BloqueaA59Minutos_YPermiteA60()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        ResponderClima(31m);

        Assert.Single(Generadas(await ConsultarAsync(idUbicacion, token)));

        // Segunda consulta inmediata (desde la caché) y a los 59 minutos: sin alerta nueva.
        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(59));
        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));

        // 60 minutos después de la primera: se permite otra.
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(1));
        Assert.Single(Generadas(await ConsultarAsync(idUbicacion, token)));

        var alertas = await AlertasDeUbicacionAsync(idUbicacion);
        Assert.Equal(2, alertas.Count);
        Assert.True(alertas[1].FechaGeneracion - alertas[0].FechaGeneracion >= TimeSpan.FromMinutes(60));
        Assert.Equal(4, await ConsultasDeUbicacionAsync(idUbicacion));   // las 4 consultas se guardan
    }

    [Fact]
    public async Task Duplicado_AunqueLaAnteriorEsteLeida()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(18m, LluviaLigera);

        var idAlerta = Assert.Single(Generadas(await ConsultarAsync(idUbicacion, token))).GetProperty("idAlerta").GetInt64();
        var marcar = Ayudas.ConToken(HttpMethod.Patch, $"/api/alertas/{idAlerta}/estado", token, new { estado = "LEIDA" });
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(marcar)).StatusCode);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(30));
        Assert.Empty(Generadas(await ConsultarAsync(idUbicacion, token)));
        Assert.Single(await AlertasDeUbicacionAsync(idUbicacion));
    }

    [Fact]
    public async Task Duplicado_EsPorUsuarioUbicacionYTipo()
    {
        var (token, idUsuario, coban) = await PrepararAsync();
        var salama = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Salamá");
        var (tokenOtro, idOtro) = await UsuarioAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        await CrearReglaAsync(idOtro, TiposAlerta.TemperaturaAlta, 30m);
        ResponderClima(31m);

        Assert.Equal(new[] { "TEMPERATURA_ALTA" }, TiposGenerados(await ConsultarAsync(coban, token)));

        // Otra ubicación del mismo usuario: no está bloqueada.
        Assert.Equal(new[] { "TEMPERATURA_ALTA" }, TiposGenerados(await ConsultarAsync(salama, token)));

        // Otro usuario en la misma ubicación: no está bloqueado.
        Assert.Equal(new[] { "TEMPERATURA_ALTA" }, TiposGenerados(await ConsultarAsync(coban, tokenOtro)));

        // Otro tipo del mismo usuario y ubicación: no está bloqueado.
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(31m, LluviaLigera);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));   // fuera de la caché de 10 min, dentro de la ventana de 60
        Assert.Equal(new[] { "LLUVIA" }, TiposGenerados(await ConsultarAsync(coban, token)));
    }

    // ================================================================== Caché, pronóstico y errores

    [Fact]
    public async Task ConsultaDesdeCache_TambienEvalua()
    {
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var (tokenA, _) = await UsuarioAsync();
        var (tokenB, idB) = await UsuarioAsync();
        await CrearReglaAsync(idB, TiposAlerta.TemperaturaAlta, 30m, idUbicacion);
        ResponderClima(31m);

        Assert.False((await ConsultarAsync(idUbicacion, tokenA)).GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        var llamadas = Proveedor.Llamadas;

        var climaB = await ConsultarAsync(idUbicacion, tokenB);

        Assert.True(climaB.GetProperty("consulta").GetProperty("desdeCache").GetBoolean());
        Assert.Equal(llamadas, Proveedor.Llamadas);
        var alerta = Assert.Single(await AlertasDeUbicacionAsync(idUbicacion));
        Assert.Equal(idB, alerta.IdUsuario);
        Assert.Equal(climaB.GetProperty("consulta").GetProperty("idConsulta").GetInt64(), alerta.IdConsulta);
    }

    [Fact]
    public async Task Pronostico_NoGeneraAlertas()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, -50m);
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaBaja, 60m, idUbicacion);
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        Proveedor.ResponderJson(RespuestasOpenWeather.PronosticoDesde(_fabrica.Reloj.GetUtcNow().UtcDateTime).ToJsonString());

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, $"/api/clima/pronostico?idUbicacion={idUbicacion}", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.False((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("alertasGeneradas", out _));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    public static TheoryData<string, HttpStatusCode> ErroresProveedor => new()
    {
        { "estado500", HttpStatusCode.ServiceUnavailable },
        { "estado401", HttpStatusCode.ServiceUnavailable },
        { "timeout", HttpStatusCode.GatewayTimeout },
        { "sinRed", HttpStatusCode.ServiceUnavailable },
        { "jsonInvalido", HttpStatusCode.BadGateway },
    };

    [Theory]
    [MemberData(nameof(ErroresProveedor))]
    public async Task ErrorDeOpenWeather_NoGeneraConsultaNiAlerta(string caso, HttpStatusCode esperado)
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, -50m);
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaBaja, 60m);
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        switch (caso)
        {
            case "estado500": Proveedor.ResponderEstado(HttpStatusCode.InternalServerError); break;
            case "estado401": Proveedor.ResponderEstado(HttpStatusCode.Unauthorized); break;
            case "timeout": Proveedor.Lanzar(() => new TaskCanceledException("timeout", new TimeoutException())); break;
            case "sinRed": Proveedor.Lanzar(() => new HttpRequestException("sin red")); break;
            default: Proveedor.ResponderJson("{esto no es json"); break;
        }

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), token));

        Assert.Equal(esperado, respuesta.StatusCode);
        Assert.Equal(0, await ConsultasDeUbicacionAsync(idUbicacion));
        Assert.Empty(await AlertasDeUbicacionAsync(idUbicacion));
    }

    // ================================================================== Seguridad y lectura

    [Fact]
    public async Task AlertaGenerada_ApareceSoloParaSuDueno_YEnElContador()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var (tokenOtro, _) = await UsuarioAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.Lluvia);
        ResponderClima(18m, LluviaLigera);
        var contadorAntes = (await GetJsonAsync("/api/alertas/contador", token)).GetProperty("nuevas").GetInt32();

        var idAlerta = Assert.Single(Generadas(await ConsultarAsync(idUbicacion, token))).GetProperty("idAlerta").GetInt64();

        Assert.Equal(contadorAntes + 1, (await GetJsonAsync("/api/alertas/contador", token)).GetProperty("nuevas").GetInt32());
        var lista = await GetJsonAsync("/api/alertas", token);
        var item = Assert.Single(lista.GetProperty("elementos").EnumerateArray(), a => a.GetProperty("idAlerta").GetInt64() == idAlerta);
        Assert.Equal("NUEVA", item.GetProperty("estado").GetString());
        Assert.Equal("Lluvia detectada en Cobán", item.GetProperty("mensaje").GetString());

        // Otro usuario: no la ve, no cuenta y no puede marcarla.
        Assert.DoesNotContain(
            (await GetJsonAsync("/api/alertas", tokenOtro)).GetProperty("elementos").EnumerateArray(),
            a => a.GetProperty("idAlerta").GetInt64() == idAlerta);
        Assert.Equal(0, (await GetJsonAsync("/api/alertas/contador", tokenOtro)).GetProperty("nuevas").GetInt32());
        var marcar = Ayudas.ConToken(HttpMethod.Patch, $"/api/alertas/{idAlerta}/estado", tokenOtro, new { estado = "LEIDA" });
        Assert.Equal(HttpStatusCode.NotFound, (await _cliente.SendAsync(marcar)).StatusCode);

        // El administrador la ve en M15.
        var correoAdmin = await Ayudas.CrearAdministradorAsync(_fabrica);
        var tokenAdmin = await Ayudas.ObtenerTokenAsync(_cliente, correoAdmin, Ayudas.PasswordValida);
        var admin = await GetJsonAsync($"/api/admin/alertas/{idAlerta}", tokenAdmin);
        Assert.Equal(idAlerta, admin.GetProperty("idAlerta").GetInt64());
    }

    [Fact]
    public async Task RespuestaClimaActual_ConservaSusCampos_YAgregaAlertasGeneradas()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await CrearReglaAsync(idUsuario, TiposAlerta.TemperaturaAlta, 30m);
        ResponderClima(31m);

        var clima = await ConsultarAsync(idUbicacion, token);

        Assert.Equal(
            new[] { "alertasGeneradas", "codigoCondicion", "consulta", "descripcion", "detalle", "humedad", "sensacionTermica", "temperatura", "ubicacion", "velocidadViento" },
            clima.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(
            new[] { "fechaGeneracion", "idAlerta", "mensaje", "tipo" },
            Assert.Single(Generadas(clima)).EnumerateObject().Select(p => p.Name).Order().ToArray());

        var texto = await (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, Ruta(idUbicacion), token))).Content.ReadAsStringAsync();
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, texto);
        Assert.DoesNotContain("idUsuario", texto);
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, StringComparison.Ordinal));
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains("supera tu umbral", StringComparison.Ordinal));
    }

    private async Task<JsonElement> GetJsonAsync(string ruta, string token)
    {
        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, ruta, token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
