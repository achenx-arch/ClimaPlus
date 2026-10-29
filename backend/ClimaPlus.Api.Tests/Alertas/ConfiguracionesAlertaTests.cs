using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Tests.Infraestructura;

namespace ClimaPlus.Api.Tests.Alertas;

/// <summary>Fase 2H: /api/configuraciones-alerta (JWT, aislamiento, RN-BD-16 a RN-BD-19 y RN-BD-21).</summary>
public sealed class ConfiguracionesAlertaTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string Ruta = "/api/configuraciones-alerta";

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public ConfiguracionesAlertaTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string token, object? cuerpo = null) =>
        _cliente.SendAsync(Ayudas.ConToken(metodo, ruta, token, cuerpo));

    private Task<HttpResponseMessage> EnviarJsonAsync(HttpMethod metodo, string ruta, string token, string json)
    {
        var solicitud = Ayudas.ConToken(metodo, ruta, token);
        solicitud.Content = new StringContent(json, Encoding.UTF8, "application/json");
        return _cliente.SendAsync(solicitud);
    }

    private Task<HttpResponseMessage> CrearAsync(string token, string tipo, long? idUbicacion = null, decimal? valorUmbral = null, bool? activa = null) =>
        EnviarAsync(HttpMethod.Post, Ruta, token, new { tipo, idUbicacion, valorUmbral, activa });

    private static async Task<JsonElement> CuerpoAsync(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<long> IdAsync(HttpResponseMessage respuesta) =>
        (await CuerpoAsync(respuesta, HttpStatusCode.Created)).GetProperty("idConfiguracion").GetInt64();

    private async Task<JsonElement[]> ListarAsync(string token) =>
        (await CuerpoAsync(await EnviarAsync(HttpMethod.Get, Ruta, token), HttpStatusCode.OK)).Deserialize<JsonElement[]>()!;

    private static async Task AssertErrorDeCampoAsync(HttpResponseMessage respuesta, HttpStatusCode estado, string campo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty(campo, out _), $"Se esperaba errors.{campo}: {problema}");
    }

    // ------------------------------------------------------------------ Autenticación y aislamiento

    [Theory]
    [InlineData("GET", Ruta)]
    [InlineData("GET", Ruta + "/1")]
    [InlineData("POST", Ruta)]
    [InlineData("PUT", Ruta + "/1")]
    [InlineData("PATCH", Ruta + "/1/estado")]
    [InlineData("DELETE", Ruta + "/1")]
    public async Task SinToken_Responde401(string metodo, string ruta)
    {
        var solicitud = new HttpRequestMessage(new HttpMethod(metodo), ruta);
        if (metodo is "POST" or "PUT" or "PATCH")
        {
            solicitud.Content = JsonContent.Create(new { tipo = "LLUVIA", activa = true });
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

        Assert.Equal(HttpStatusCode.Unauthorized, (await EnviarAsync(HttpMethod.Get, Ruta, token)).StatusCode);
    }

    [Fact]
    public async Task OtroUsuario_NoPuedeVerEditarPausarNiEliminarLaRegla()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, intruso) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", valorUmbral: 30m));

        var ver = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", intruso);
        var editar = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", intruso, new { tipo = "TEMPERATURA_ALTA", valorUmbral = 10m, activa = false });
        var pausar = await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", intruso, new { activa = false });
        var eliminar = await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", intruso);

        Assert.Equal(HttpStatusCode.NotFound, ver.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, editar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pausar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, eliminar.StatusCode);
        Assert.Empty(await ListarAsync(intruso));

        var regla = Assert.Single(await ListarAsync(token));
        Assert.Equal(30m, regla.GetProperty("valorUmbral").GetDecimal());
        Assert.True(regla.GetProperty("activa").GetBoolean());
    }

    [Fact]
    public async Task ElIdUsuarioNoSeAceptaEnElCuerpo()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarJsonAsync(HttpMethod.Post, Ruta, token, "{\"tipo\":\"LLUVIA\",\"idUsuario\":1}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task MismasReglasEnDosUsuarios_SonIndependientes()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (_, otro) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "LLUVIA")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(otro, "LLUVIA")).StatusCode);
    }

    // ------------------------------------------------------------------ Crear y consultar

    [Fact]
    public async Task Crear_ReglaGlobalDeLluvia_SinUmbralYActivaPorDefecto()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await CrearAsync(token, "LLUVIA");

        var cuerpo = await CuerpoAsync(respuesta, HttpStatusCode.Created);
        var id = cuerpo.GetProperty("idConfiguracion").GetInt64();
        Assert.Equal($"{Ruta}/{id}", respuesta.Headers.Location?.AbsolutePath);
        Assert.Equal("LLUVIA", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("ubicacion").ValueKind);
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("valorUmbral").ValueKind);
        Assert.True(cuerpo.GetProperty("activa").GetBoolean());
        Assert.EndsWith("Z", cuerpo.GetProperty("fechaCreacion").GetString());

        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token)).StatusCode);
    }

    [Fact]
    public async Task Crear_ReglaDeTemperaturaParaUnaUbicacion_DevuelveLaUbicacion()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Cobán");

        var cuerpo = await CuerpoAsync(await CrearAsync(token, "TEMPERATURA_BAJA", idUbicacion, 8.5m, activa: false), HttpStatusCode.Created);

        Assert.Equal("TEMPERATURA_BAJA", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(idUbicacion, cuerpo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.Equal("Cobán", cuerpo.GetProperty("ubicacion").GetProperty("nombre").GetString());
        Assert.Equal(8.5m, cuerpo.GetProperty("valorUmbral").GetDecimal());
        Assert.False(cuerpo.GetProperty("activa").GetBoolean());
    }

    [Theory]
    [InlineData(-50.0)]
    [InlineData(60.0)]
    [InlineData(0.0)]
    [InlineData(-12.75)]
    public async Task Crear_UmbralesEnElLimite_SonValidos(double umbral)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_ALTA", valorUmbral: (decimal)umbral)).StatusCode);
    }

    [Fact]
    public async Task Listar_SinReglas_RespondeListaVacia_YConReglasLasOrdenaDeLaMasReciente()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        Assert.Empty(await ListarAsync(token));

        await IdAsync(await CrearAsync(token, "LLUVIA"));
        await Task.Delay(20);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", valorUmbral: 30m));

        var reglas = await ListarAsync(token);
        Assert.Equal(new[] { "TEMPERATURA_ALTA", "LLUVIA" }, reglas.Select(r => r.GetProperty("tipo").GetString()));
    }

    // ------------------------------------------------------------------ Validación (400)

    [Theory]
    [InlineData("{}", "tipo")]
    [InlineData("{\"tipo\":\"\"}", "tipo")]
    [InlineData("{\"tipo\":\"VIENTO\"}", "tipo")]
    [InlineData("{\"tipo\":\"lluvia\"}", "tipo")]
    [InlineData("{\"tipo\":\"LLUVIA' OR 1=1 --\"}", "tipo")]
    [InlineData("{\"tipo\":\"<script>alert(1)</script>\"}", "tipo")]
    [InlineData("{\"tipo\":\"TEMPERATURA_ALTA\"}", "valorUmbral")]
    [InlineData("{\"tipo\":\"TEMPERATURA_BAJA\",\"valorUmbral\":null}", "valorUmbral")]
    [InlineData("{\"tipo\":\"TEMPERATURA_ALTA\",\"valorUmbral\":60.01}", "valorUmbral")]
    [InlineData("{\"tipo\":\"TEMPERATURA_BAJA\",\"valorUmbral\":-50.5}", "valorUmbral")]
    [InlineData("{\"tipo\":\"TEMPERATURA_ALTA\",\"valorUmbral\":25.123}", "valorUmbral")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"valorUmbral\":5}", "valorUmbral")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"idUbicacion\":0}", "idUbicacion")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"idUbicacion\":-3}", "idUbicacion")]
    public async Task Crear_DatosInvalidos_Responde400ConElCampo(string json, string campo)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarJsonAsync(HttpMethod.Post, Ruta, token, json);

        await AssertErrorDeCampoAsync(respuesta, HttpStatusCode.BadRequest, campo);
        Assert.Empty(await ListarAsync(token));
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"activa\":\"si\"}")]
    [InlineData("{\"tipo\":\"TEMPERATURA_ALTA\",\"valorUmbral\":\"treinta\"}")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"operador\":\">\"}")]
    public async Task Crear_JsonInvalidoOCamposDeMas_Responde400(string json)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarJsonAsync(HttpMethod.Post, Ruta, token, json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task Crear_UbicacionInexistente_Responde404()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await CrearAsync(token, "LLUVIA", idUbicacion: 987_654_321);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    // ------------------------------------------------------------------ RN-BD-19: duplicados (409)

    [Fact]
    public async Task Duplicado_MismoTipoYUbicacion_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", idUbicacion, 30m));

        var repetida = await CrearAsync(token, "TEMPERATURA_ALTA", idUbicacion, 35m, activa: false);

        await AssertErrorDeCampoAsync(repetida, HttpStatusCode.Conflict, "tipo");
    }

    [Fact]
    public async Task Duplicado_DosReglasGlobalesDelMismoTipo_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await IdAsync(await CrearAsync(token, "LLUVIA"));

        await AssertErrorDeCampoAsync(await CrearAsync(token, "LLUVIA"), HttpStatusCode.Conflict, "tipo");
    }

    [Fact]
    public async Task MismoTipo_GlobalYEspecifica_SePermiten()
    {
        // RN-BD-20: la específica prevalece sobre la global al evaluar; ambas pueden existir.
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var coban = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var guatemala = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Guatemala");

        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_ALTA", coban, 28m)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_ALTA", guatemala, 32m)).StatusCode);
        Assert.Equal(3, (await ListarAsync(token)).Length);
    }

    // ------------------------------------------------------------------ RN-BD-21: alta > baja (409)

    [Theory]
    [InlineData(20.0, 25.0)]   // alta menor que baja
    [InlineData(20.0, 20.0)]   // iguales
    public async Task AltaNoMayorQueBaja_EnLaMismaUbicacion_Responde409(double alta, double baja)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_BAJA", idUbicacion, (decimal)baja));

        var respuesta = await CrearAsync(token, "TEMPERATURA_ALTA", idUbicacion, (decimal)alta);

        await AssertErrorDeCampoAsync(respuesta, HttpStatusCode.Conflict, "valorUmbral");
    }

    [Fact]
    public async Task BajaNoMenorQueAlta_EntreReglasGlobales_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));

        await AssertErrorDeCampoAsync(await CrearAsync(token, "TEMPERATURA_BAJA", null, 31m), HttpStatusCode.Conflict, "valorUmbral");
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_BAJA", null, 10m)).StatusCode);
    }

    [Fact]
    public async Task AltaYBaja_EnUbicacionesDistintas_NoSeComparan()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var a = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var b = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_BAJA", a, 25m));

        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_ALTA", b, 20m)).StatusCode);
    }

    [Fact]
    public async Task ReglaPausada_TambienCuentaParaRNBD21()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await IdAsync(await CrearAsync(token, "TEMPERATURA_BAJA", null, 15m, activa: false));

        await AssertErrorDeCampoAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 10m), HttpStatusCode.Conflict, "valorUmbral");
    }

    // ------------------------------------------------------------------ Actualizar (PUT)

    [Fact]
    public async Task Actualizar_CambiaTodosLosCampos()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var idUbicacion = await AyudasDatos.CrearUbicacionAsync(_fabrica);
        var id = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token,
            new { tipo = "TEMPERATURA_BAJA", idUbicacion, valorUmbral = 5.25m, activa = false });

        var cuerpo = await CuerpoAsync(respuesta, HttpStatusCode.OK);
        Assert.Equal(id, cuerpo.GetProperty("idConfiguracion").GetInt64());
        Assert.Equal("TEMPERATURA_BAJA", cuerpo.GetProperty("tipo").GetString());
        Assert.Equal(idUbicacion, cuerpo.GetProperty("ubicacion").GetProperty("idUbicacion").GetInt64());
        Assert.Equal(5.25m, cuerpo.GetProperty("valorUmbral").GetDecimal());
        Assert.False(cuerpo.GetProperty("activa").GetBoolean());
    }

    [Fact]
    public async Task Actualizar_LaMismaReglaSinCambios_NoSeConsideraDuplicada()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));
        await IdAsync(await CrearAsync(token, "TEMPERATURA_BAJA", null, 10m));

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token,
            new { tipo = "TEMPERATURA_ALTA", idUbicacion = (long?)null, valorUmbral = 31m, activa = true });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_AUnTipoYUbicacionQueYaExisten_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await IdAsync(await CrearAsync(token, "LLUVIA"));
        var id = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", token,
            new { tipo = "LLUVIA", idUbicacion = (long?)null, valorUmbral = (decimal?)null, activa = true });

        await AssertErrorDeCampoAsync(respuesta, HttpStatusCode.Conflict, "tipo");
        var regla = await CuerpoAsync(await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token), HttpStatusCode.OK);
        Assert.Equal("TEMPERATURA_ALTA", regla.GetProperty("tipo").GetString());
    }

    [Fact]
    public async Task Actualizar_RompiendoRNBD21_Responde409()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var alta = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));
        await IdAsync(await CrearAsync(token, "TEMPERATURA_BAJA", null, 10m));

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{alta}", token,
            new { tipo = "TEMPERATURA_ALTA", idUbicacion = (long?)null, valorUmbral = 5m, activa = true });

        await AssertErrorDeCampoAsync(respuesta, HttpStatusCode.Conflict, "valorUmbral");
    }

    [Theory]
    [InlineData("{\"tipo\":\"LLUVIA\"}", "activa")]
    [InlineData("{\"tipo\":\"LLUVIA\",\"valorUmbral\":3,\"activa\":true}", "valorUmbral")]
    [InlineData("{\"tipo\":\"TEMPERATURA_ALTA\",\"activa\":true}", "valorUmbral")]
    public async Task Actualizar_DatosInvalidos_Responde400(string json, string campo)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "LLUVIA"));

        await AssertErrorDeCampoAsync(await EnviarJsonAsync(HttpMethod.Put, $"{Ruta}/{id}", token, json), HttpStatusCode.BadRequest, campo);
    }

    [Fact]
    public async Task Actualizar_ReglaInexistente_Responde404()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/987654321", token, new { tipo = "LLUVIA", activa = true });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ Activar / pausar (PATCH)

    [Fact]
    public async Task CambiarEstado_PausaYReactivaSinTocarLosDemasCampos()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "TEMPERATURA_ALTA", null, 30m));

        var pausada = await CuerpoAsync(await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, new { activa = false }), HttpStatusCode.OK);
        var reactivada = await CuerpoAsync(await EnviarAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, new { activa = true }), HttpStatusCode.OK);

        Assert.False(pausada.GetProperty("activa").GetBoolean());
        Assert.True(reactivada.GetProperty("activa").GetBoolean());
        Assert.Equal(30m, reactivada.GetProperty("valorUmbral").GetDecimal());
        Assert.Equal("TEMPERATURA_ALTA", reactivada.GetProperty("tipo").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"activa\":null}")]
    [InlineData("{\"activa\":\"no\"}")]
    [InlineData("{\"activa\":false,\"valorUmbral\":10}")]
    public async Task CambiarEstado_CuerpoInvalido_Responde400(string json)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "LLUVIA"));

        var respuesta = await EnviarJsonAsync(HttpMethod.Patch, $"{Ruta}/{id}/estado", token, json);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    // ------------------------------------------------------------------ Eliminar

    [Fact]
    public async Task Eliminar_Responde204_YLiberaElTipoParaUnaReglaNueva()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var id = await IdAsync(await CrearAsync(token, "LLUVIA"));

        Assert.Equal(HttpStatusCode.NoContent, (await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", token)).StatusCode);
        Assert.Empty(await ListarAsync(token));
        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "LLUVIA")).StatusCode);
    }

    [Theory]
    [InlineData("/api/configuraciones-alerta/abc")]
    [InlineData("/api/configuraciones-alerta/99999999999999999999")]
    public async Task IdNoNumerico_Responde404(string ruta)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        Assert.Equal(HttpStatusCode.NotFound, (await EnviarAsync(HttpMethod.Delete, ruta, token)).StatusCode);
    }

    [Fact]
    public async Task Administrador_TambienGestionaSusReglas()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);
        var token = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);

        Assert.Equal(HttpStatusCode.Created, (await CrearAsync(token, "TEMPERATURA_BAJA", valorUmbral: 5m)).StatusCode);
        Assert.Single(await ListarAsync(token));
    }
}
