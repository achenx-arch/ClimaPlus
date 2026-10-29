using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests.Alertas;

/// <summary>
/// Alertas generadas (M09 y M15). Las alertas se crean directamente en la base InMemory de
/// pruebas porque el motor de alertas todavía no existe (depende de E-02/E-03).
/// </summary>
public sealed class AlertasTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string Ruta = "/api/alertas";
    private const string RutaAdmin = "/api/admin/alertas";

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public AlertasTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string token, object? cuerpo = null) =>
        _cliente.SendAsync(Ayudas.ConToken(metodo, ruta, token, cuerpo));

    private async Task<JsonElement> GetOkAsync(string ruta, string token)
    {
        var respuesta = await EnviarAsync(HttpMethod.Get, ruta, token);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> MarcarAsync(string token, long idAlerta, string json = "{\"estado\":\"LEIDA\"}")
    {
        var solicitud = Ayudas.ConToken(HttpMethod.Patch, $"{Ruta}/{idAlerta}/estado", token);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _cliente.SendAsync(solicitud);
    }

    private async Task<int> ContadorAsync(string token) =>
        (await GetOkAsync($"{Ruta}/contador", token)).GetProperty("nuevas").GetInt32();

    /// <summary>Usuario con sesión, su id y una ubicación.</summary>
    private async Task<(string Token, long IdUsuario, long IdUbicacion)> PrepararAsync(string ubicacion = "Cobán")
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        return (token, await AyudasDatos.IdUsuarioAsync(_fabrica, correo), await AyudasDatos.CrearUbicacionAsync(_fabrica, ubicacion));
    }

    private async Task<string> TokenAdministradorAsync()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);
        return await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
    }

    private static long[] Ids(JsonElement pagina) =>
        pagina.GetProperty("elementos").EnumerateArray().Select(a => a.GetProperty("idAlerta").GetInt64()).ToArray();

    // ================================================================== Usuario (M09)

    [Theory]
    [InlineData("GET", Ruta)]
    [InlineData("GET", Ruta + "/contador")]
    [InlineData("PATCH", Ruta + "/1/estado")]
    [InlineData("GET", RutaAdmin)]
    [InlineData("GET", RutaAdmin + "/1")]
    public async Task SinToken_Responde401(string metodo, string ruta)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo == "PATCH")
        {
            solicitud.Content = JsonContent.Create(new { estado = "LEIDA" });
        }

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task UsuarioDesactivado_Responde401()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/contador", token)).StatusCode);
    }

    [Fact]
    public async Task SinAlertas_ListaVaciaYContadorCero()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var pagina = await GetOkAsync(Ruta, token);

        Assert.Empty(pagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(1, pagina.GetProperty("pagina").GetInt32());
        Assert.Equal(20, pagina.GetProperty("tamanoPagina").GetInt32());
        Assert.Equal(0, pagina.GetProperty("totalElementos").GetInt32());
        Assert.Equal(0, pagina.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(0, await ContadorAsync(token));
    }

    [Fact]
    public async Task Listar_DevuelveSoloLasPropias_DeLaMasReciente_ConTodosLosDatos()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync("Cobán");
        var (_, idOtro, idUbicacionOtro) = await PrepararAsync("Ajena");
        var ahora = DateTime.UtcNow;
        var antigua = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.Lluvia, fecha: ahora.AddHours(-2));
        var reciente = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.TemperaturaAlta, fecha: ahora.AddHours(-1));
        await AyudasDatos.CrearAlertaAsync(_fabrica, idOtro, idUbicacionOtro, fecha: ahora);

        var pagina = await GetOkAsync(Ruta, token);

        Assert.Equal(new[] { reciente, antigua }, Ids(pagina));
        Assert.Equal(2, pagina.GetProperty("totalElementos").GetInt32());

        var alta = pagina.GetProperty("elementos")[0];
        Assert.Equal("TEMPERATURA_ALTA", alta.GetProperty("tipo").GetString());
        Assert.Equal("NUEVA", alta.GetProperty("estado").GetString());
        Assert.Equal(33.4m, alta.GetProperty("valorDetectado").GetDecimal());
        Assert.Equal(32m, alta.GetProperty("valorUmbral").GetDecimal());
        Assert.False(string.IsNullOrWhiteSpace(alta.GetProperty("mensaje").GetString()));
        Assert.EndsWith("Z", alta.GetProperty("fechaGeneracion").GetString());
        Assert.Equal("Cobán", alta.GetProperty("ubicacion").GetProperty("nombre").GetString());
        var origen = alta.GetProperty("origen");
        Assert.Equal("OPENWEATHER", origen.GetProperty("tipo").GetString());
        Assert.True(origen.GetProperty("idConsulta").GetInt64() > 0);
        Assert.Equal(JsonValueKind.Null, origen.GetProperty("idLectura").ValueKind);
        Assert.Equal(JsonValueKind.Null, origen.GetProperty("idDispositivo").ValueKind);

        var lluvia = pagina.GetProperty("elementos")[1];
        Assert.Equal(JsonValueKind.Null, lluvia.GetProperty("valorDetectado").ValueKind);
        Assert.Equal(JsonValueKind.Null, lluvia.GetProperty("valorUmbral").ValueKind);

        // El usuario no recibe datos de otros usuarios.
        Assert.False(alta.TryGetProperty("usuario", out _));
    }

    [Fact]
    public async Task OrigenEsp32_IncluyeElDispositivo()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.Lluvia, desdeEsp32: true, nombreDispositivo: "Patio");

        var origen = (await GetOkAsync(Ruta, token)).GetProperty("elementos")[0].GetProperty("origen");

        Assert.Equal("ESP32", origen.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, origen.GetProperty("idConsulta").ValueKind);
        Assert.True(origen.GetProperty("idLectura").GetInt64() > 0);
        Assert.True(origen.GetProperty("idDispositivo").GetInt64() > 0);
        Assert.Equal("Patio", origen.GetProperty("nombreDispositivo").GetString());
    }

    [Fact]
    public async Task Filtros_PorEstadoYTipo()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var nuevaAlta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.TemperaturaAlta, EstadosAlerta.Nueva);
        var leidaAlta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.TemperaturaAlta, EstadosAlerta.Leida);
        var nuevaLluvia = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.Lluvia, EstadosAlerta.Nueva);

        Assert.Equal(new[] { nuevaLluvia, nuevaAlta }.Order().ToArray(), Ids(await GetOkAsync($"{Ruta}?estado=NUEVA", token)).Order().ToArray());
        Assert.Equal(new[] { leidaAlta }, Ids(await GetOkAsync($"{Ruta}?estado=LEIDA", token)));
        Assert.Equal(new[] { nuevaLluvia }, Ids(await GetOkAsync($"{Ruta}?tipo=LLUVIA", token)));
        Assert.Equal(new[] { nuevaAlta }, Ids(await GetOkAsync($"{Ruta}?estado=NUEVA&tipo=TEMPERATURA_ALTA", token)));
        Assert.Empty(Ids(await GetOkAsync($"{Ruta}?tipo=TEMPERATURA_BAJA", token)));
    }

    [Fact]
    public async Task Paginacion_DeVeintePorPagina()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var inicio = DateTime.UtcNow.AddDays(-1);
        for (var i = 0; i < 25; i++)
        {
            await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, fecha: inicio.AddMinutes(i));
        }

        var primera = await GetOkAsync(Ruta, token);
        var segunda = await GetOkAsync($"{Ruta}?pagina=2", token);
        var tercera = await GetOkAsync($"{Ruta}?pagina=3", token);

        Assert.Equal(20, Ids(primera).Length);
        Assert.Equal(5, Ids(segunda).Length);
        Assert.Empty(Ids(tercera));
        Assert.Equal(25, primera.GetProperty("totalElementos").GetInt32());
        Assert.Equal(2, primera.GetProperty("totalPaginas").GetInt32());
        Assert.Equal(2, segunda.GetProperty("pagina").GetInt32());
        Assert.Empty(Ids(primera).Intersect(Ids(segunda)));
    }

    [Theory]
    [InlineData("?estado=NUEVO", "estado")]
    [InlineData("?estado=leida", "estado")]
    [InlineData("?tipo=VIENTO", "tipo")]
    [InlineData("?tipo=LLUVIA'%20OR%201=1", "tipo")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?pagina=-1", "pagina")]
    [InlineData("?pagina=100001", "pagina")]
    [InlineData("?pagina=abc", "pagina")]
    public async Task Listar_FiltroInvalido_Responde400(string consulta, string campo)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarAsync(HttpMethod.Get, Ruta + consulta, token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty(campo, out _), problema.ToString());
    }

    [Fact]
    public async Task Contador_CuentaSoloLasNuevasPropias()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var (_, idOtro, idUbicacionOtro) = await PrepararAsync();
        await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, estado: EstadosAlerta.Nueva);
        await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.Lluvia, EstadosAlerta.Nueva);
        await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, estado: EstadosAlerta.Leida);
        await AyudasDatos.CrearAlertaAsync(_fabrica, idOtro, idUbicacionOtro, estado: EstadosAlerta.Nueva);

        Assert.Equal(2, await ContadorAsync(token));
    }

    [Fact]
    public async Task MarcarLeida_CambiaSoloElEstado_EsIdempotente_YBajaElContador()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var idAlerta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion);
        var antes = (await GetOkAsync(Ruta, token)).GetProperty("elementos")[0];
        Assert.Equal(1, await ContadorAsync(token));

        var primera = await MarcarAsync(token, idAlerta);
        var segunda = await MarcarAsync(token, idAlerta);

        Assert.Equal(HttpStatusCode.OK, primera.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var despues = await primera.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LEIDA", despues.GetProperty("estado").GetString());
        Assert.Equal(0, await ContadorAsync(token));

        // RN-BD-26: el resto de la alerta no cambia.
        foreach (var campo in new[] { "idAlerta", "tipo", "mensaje", "valorDetectado", "valorUmbral", "fechaGeneracion", "ubicacion", "origen" })
        {
            Assert.Equal(antes.GetProperty(campo).GetRawText(), despues.GetProperty(campo).GetRawText());
        }
    }

    [Theory]
    [InlineData("{\"estado\":\"NUEVA\"}")]
    [InlineData("{\"estado\":\"leida\"}")]
    [InlineData("{\"estado\":\"ARCHIVADA\"}")]
    [InlineData("{}")]
    [InlineData("{\"estado\":\"LEIDA\",\"mensaje\":\"cambiado\"}")]
    [InlineData("no es json")]
    public async Task MarcarLeida_CuerpoInvalido_Responde400SinCambiarNada(string json)
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var idAlerta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion);

        var respuesta = await MarcarAsync(token, idAlerta, json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(1, await ContadorAsync(token));
    }

    [Fact]
    public async Task MarcarLeida_AlertaAjenaOInexistente_Responde404()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var (intruso, _, _) = await PrepararAsync();
        var idAlerta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion);

        Assert.Equal(HttpStatusCode.NotFound, (await MarcarAsync(intruso, idAlerta)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MarcarAsync(token, 987_654_321)).StatusCode);
        Assert.Equal(1, await ContadorAsync(token));
    }

    // ================================================================== Administrador (M15)

    [Fact]
    public async Task Admin_UsuarioNormal_Responde403()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var lista = await EnviarAsync(HttpMethod.Get, RutaAdmin, token);
        var detalle = await EnviarAsync(HttpMethod.Get, $"{RutaAdmin}/1", token);

        Assert.Equal(HttpStatusCode.Forbidden, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, detalle.StatusCode);
        await Ayudas.LeerProblemaAsync(lista);
    }

    [Fact]
    public async Task Admin_VeAlertasDeTodosLosUsuarios_ConElDestinatario()
    {
        var admin = await TokenAdministradorAsync();
        var (_, idA, ubicacionA) = await PrepararAsync();
        var (_, idB, ubicacionB) = await PrepararAsync();
        var alertaA = await AyudasDatos.CrearAlertaAsync(_fabrica, idA, ubicacionA);
        var alertaB = await AyudasDatos.CrearAlertaAsync(_fabrica, idB, ubicacionB, TiposAlerta.Lluvia, desdeEsp32: true);

        var deA = await GetOkAsync($"{RutaAdmin}?idUsuario={idA}", admin);
        var deB = await GetOkAsync($"{RutaAdmin}?idUsuario={idB}", admin);
        var todas = await GetOkAsync(RutaAdmin, admin);

        Assert.Equal(new[] { alertaA }, Ids(deA));
        Assert.Equal(new[] { alertaB }, Ids(deB));
        Assert.True(todas.GetProperty("totalElementos").GetInt32() >= 2);
        var usuario = deA.GetProperty("elementos")[0].GetProperty("usuario");
        Assert.Equal(idA, usuario.GetProperty("idUsuario").GetInt64());
        Assert.Contains("@", usuario.GetProperty("correo").GetString());
        Assert.False(usuario.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Admin_FiltraPorOrigenTipoEstadoYFechas()
    {
        var admin = await TokenAdministradorAsync();
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        var dia = new DateTime(2001, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        var openWeather = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.TemperaturaAlta, EstadosAlerta.Nueva, dia);
        var esp32 = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.Lluvia, EstadosAlerta.Leida, dia.AddDays(1), desdeEsp32: true);
        var otroDia = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion, TiposAlerta.TemperaturaBaja, EstadosAlerta.Nueva, dia.AddDays(5));
        var baseRuta = $"{RutaAdmin}?idUsuario={idUsuario}";

        Assert.Equal(new[] { esp32 }, Ids(await GetOkAsync($"{baseRuta}&origen=ESP32", admin)));
        Assert.Equal(new[] { otroDia, openWeather }, Ids(await GetOkAsync($"{baseRuta}&origen=OPENWEATHER", admin)));
        Assert.Equal(new[] { otroDia }, Ids(await GetOkAsync($"{baseRuta}&tipo=TEMPERATURA_BAJA", admin)));
        Assert.Equal(new[] { esp32 }, Ids(await GetOkAsync($"{baseRuta}&estado=LEIDA", admin)));

        // desde incluido, hasta excluido; con zona horaria (−06:00 = Guatemala).
        var desde = Uri.EscapeDataString("2001-03-10T06:00:00-06:00");   // = 12:00Z
        var hasta = Uri.EscapeDataString("2001-03-11T06:00:00-06:00");   // = 12:00Z del 11
        Assert.Equal(new[] { openWeather }, Ids(await GetOkAsync($"{baseRuta}&desde={desde}&hasta={hasta}", admin)));
        Assert.Equal(new[] { otroDia, esp32 }, Ids(await GetOkAsync($"{baseRuta}&desde={hasta}", admin)));
    }

    [Theory]
    [InlineData("?origen=SMS", "origen")]
    [InlineData("?idUsuario=0", "idUsuario")]
    [InlineData("?desde=ayer", "desde")]
    [InlineData("?desde=2026-10-10T00:00:00Z&hasta=2026-10-01T00:00:00Z", "desde")]
    [InlineData("?desde=2026-10-10T00:00:00Z&hasta=2026-10-10T00:00:00Z", "desde")]
    [InlineData("?pagina=0", "pagina")]
    public async Task Admin_FiltroInvalido_Responde400(string consulta, string campo)
    {
        var admin = await TokenAdministradorAsync();

        var respuesta = await EnviarAsync(HttpMethod.Get, RutaAdmin + consulta, admin);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty(campo, out _), problema.ToString());
    }

    [Fact]
    public async Task Admin_Detalle_DevuelveLaAlertaO404()
    {
        var admin = await TokenAdministradorAsync();
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        var idAlerta = await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion);

        var detalle = await GetOkAsync($"{RutaAdmin}/{idAlerta}", admin);
        var inexistente = await EnviarAsync(HttpMethod.Get, $"{RutaAdmin}/987654321", admin);

        Assert.Equal(idAlerta, detalle.GetProperty("idAlerta").GetInt64());
        Assert.Equal(idUsuario, detalle.GetProperty("usuario").GetProperty("idUsuario").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);
    }

    [Fact]
    public async Task Admin_SusPropiasAlertasEnM09_SiguenAisladas()
    {
        // Las rutas /api/alertas del administrador solo devuelven las suyas, no las del sistema.
        var admin = await TokenAdministradorAsync();
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        await AyudasDatos.CrearAlertaAsync(_fabrica, idUsuario, idUbicacion);

        Assert.Empty(Ids(await GetOkAsync(Ruta, admin)));
        Assert.Equal(0, await ContadorAsync(admin));
    }
}
