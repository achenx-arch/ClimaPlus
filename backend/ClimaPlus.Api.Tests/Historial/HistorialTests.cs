using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Tests.Historial;

/// <summary>
/// Historial de consultas (Fase 2G, M08, RF-11). Las consultas se crean en la base InMemory de
/// pruebas o con GET /api/clima/actual|pronostico contra el proveedor simulado (nunca OpenWeather real).
/// </summary>
public sealed class HistorialTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string Ruta = "/api/historial";

    private static readonly string[] CamposFila =
    {
        "idConsulta", "tipo", "fechaConsultaUtc", "ubicacion", "temperatura", "humedad", "velocidadViento", "codigoCondicion", "descripcion",
    };

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public HistorialTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private static DateTime Utc(int dia, int hora, int minuto = 0, int segundo = 0) =>
        new(2026, 10, dia, hora, minuto, segundo, DateTimeKind.Utc);

    private Task<HttpResponseMessage> GetAsync(string ruta, string token) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, ruta, token));

    private async Task<JsonElement> GetOkAsync(string ruta, string token)
    {
        var respuesta = await GetAsync(ruta, token);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(string Token, long IdUsuario, long IdUbicacion)> PrepararAsync(string ubicacion = "Cobán")
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        return (token, await AyudasDatos.IdUsuarioAsync(_fabrica, correo), await AyudasDatos.CrearUbicacionAsync(_fabrica, ubicacion));
    }

    private static long[] Ids(JsonElement pagina) =>
        pagina.GetProperty("elementos").EnumerateArray().Select(c => c.GetProperty("idConsulta").GetInt64()).ToArray();

    private static string TokenFirmado(long idUsuario, DateTime emitido, DateTime expira, string clave = ClimaPlusApiFactory.ClaveJwtPruebas) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ClimaPlus.Api",
            Audience = "ClimaPlus.Frontend",
            IssuedAt = emitido,
            NotBefore = emitido,
            Expires = expira,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", idUsuario.ToString()),
                new Claim("role", Roles.Usuario),
                new Claim("jti", Guid.NewGuid().ToString("N")),
            }),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256),
        });

    // ================================================================== Autenticación

    [Theory]
    [InlineData(Ruta)]
    [InlineData(Ruta + "/1")]
    public async Task SinToken_Responde401(string ruta)
    {
        var respuesta = await _cliente.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Theory]
    [InlineData(Ruta)]
    [InlineData(Ruta + "/1")]
    public async Task TokenInvalido_Responde401(string ruta)
    {
        var respuesta = await GetAsync(ruta, "token-invalido");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Fact]
    public async Task TokenExpirado_Responde401()
    {
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        var idConsulta = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(5, 12));
        // Firma, emisor, audiencia y usuario correctos; vencido hace 10 minutos (más que el margen de 30 s).
        var vencido = TokenFirmado(idUsuario, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddMinutes(-10));

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(Ruta, vencido)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync($"{Ruta}/{idConsulta}", vencido)).StatusCode);
    }

    [Fact]
    public async Task TokenFirmadoConOtraClave_Responde401()
    {
        var (_, idUsuario, _) = await PrepararAsync();
        var forjado = TokenFirmado(idUsuario, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), "otra-clave-que-no-es-la-del-servidor-0123456789");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(Ruta, forjado)).StatusCode);
    }

    [Fact]
    public async Task UsuarioDesactivado_Responde401()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync(Ruta, token)).StatusCode);
    }

    // ================================================================== Lista

    [Fact]
    public async Task SinHistorial_ListaVacia()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var pagina = await GetOkAsync(Ruta, token);

        Assert.Empty(pagina.GetProperty("elementos").EnumerateArray());
        Assert.Equal(1, pagina.GetProperty("pagina").GetInt32());
        Assert.Equal(20, pagina.GetProperty("tamanoPagina").GetInt32());
        Assert.Equal(0, pagina.GetProperty("totalElementos").GetInt32());
        Assert.Equal(0, pagina.GetProperty("totalPaginas").GetInt32());
    }

    [Fact]
    public async Task ConHistorial_SoloPropias_OrdenDescendente_SinAnonimasNiAjenas()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var (_, idOtro, _) = await PrepararAsync();

        var antigua = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(1, 10));
        var reciente = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(3, 10), TiposConsulta.Pronostico);
        var media = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(2, 10));
        // Misma fecha que "media": desempata el id descendente.
        var mediaPosterior = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(2, 10));
        await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, Utc(4, 10));
        await AyudasDatos.CrearConsultaAsync(_fabrica, idOtro, idUbicacion, Utc(4, 10));

        var pagina = await GetOkAsync(Ruta, token);

        Assert.Equal(new[] { reciente, mediaPosterior, media, antigua }, Ids(pagina));
        Assert.Equal(4, pagina.GetProperty("totalElementos").GetInt32());
        Assert.Equal(1, pagina.GetProperty("totalPaginas").GetInt32());
    }

    [Fact]
    public async Task Fila_TieneLosDatosDeM08_SinDetalleNiUsuario_YPronosticoSinMediciones()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync("Ubicación M08");
        var idActual = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(6, 10), temperatura: 18.5m);
        var idPronostico = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(6, 11), TiposConsulta.Pronostico);

        var filas = (await GetOkAsync(Ruta, token)).GetProperty("elementos").EnumerateArray().ToArray();

        foreach (var fila in filas)
        {
            Assert.Equal(CamposFila.Order().ToArray(), fila.EnumerateObject().Select(p => p.Name).Order().ToArray());
            var ubicacion = fila.GetProperty("ubicacion");
            Assert.Equal(idUbicacion, ubicacion.GetProperty("idUbicacion").GetInt64());
            Assert.Equal("Ubicación M08", ubicacion.GetProperty("nombre").GetString());
        }

        var pronostico = filas[0];
        Assert.Equal(idPronostico, pronostico.GetProperty("idConsulta").GetInt64());
        Assert.Equal("PRONOSTICO", pronostico.GetProperty("tipo").GetString());
        foreach (var campo in new[] { "temperatura", "humedad", "velocidadViento", "codigoCondicion", "descripcion" })
        {
            Assert.Equal(JsonValueKind.Null, pronostico.GetProperty(campo).ValueKind);
        }

        var actual = filas[1];
        Assert.Equal(idActual, actual.GetProperty("idConsulta").GetInt64());
        Assert.Equal("ACTUAL", actual.GetProperty("tipo").GetString());
        Assert.Equal(Utc(6, 10), actual.GetProperty("fechaConsultaUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(18.5m, actual.GetProperty("temperatura").GetDecimal());
        Assert.Equal(70, actual.GetProperty("humedad").GetInt16());
        Assert.Equal(3m, actual.GetProperty("velocidadViento").GetDecimal());
        Assert.Equal(500, actual.GetProperty("codigoCondicion").GetInt16());
        Assert.Equal("lluvia ligera", actual.GetProperty("descripcion").GetString());
    }

    [Fact]
    public async Task Paginacion_20PorPagina()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var ids = new List<long>();
        for (var i = 0; i < 21; i++)
        {
            ids.Add(await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(1, 0).AddMinutes(i)));
        }

        ids.Reverse();

        var pagina1 = await GetOkAsync($"{Ruta}?pagina=1", token);
        var pagina2 = await GetOkAsync($"{Ruta}?pagina=2", token);
        var pagina3 = await GetOkAsync($"{Ruta}?pagina=3", token);

        Assert.Equal(ids.Take(20).ToArray(), Ids(pagina1));
        Assert.Equal(ids.Skip(20).ToArray(), Ids(pagina2));
        Assert.Empty(Ids(pagina3));
        foreach (var (pagina, numero) in new[] { (pagina1, 1), (pagina2, 2), (pagina3, 3) })
        {
            Assert.Equal(numero, pagina.GetProperty("pagina").GetInt32());
            Assert.Equal(21, pagina.GetProperty("totalElementos").GetInt32());
            Assert.Equal(2, pagina.GetProperty("totalPaginas").GetInt32());
        }

        // Página máxima permitida: lista vacía, sin desbordar.
        Assert.Empty(Ids(await GetOkAsync($"{Ruta}?pagina=100000", token)));
    }

    [Theory]
    [InlineData("ACTUAL")]
    [InlineData("PRONOSTICO")]
    public async Task FiltroTipo(string tipo)
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var idActual = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(7, 10));
        var idPronostico = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(7, 11), TiposConsulta.Pronostico);

        var pagina = await GetOkAsync($"{Ruta}?tipo={tipo}", token);

        Assert.Equal(new[] { tipo == "ACTUAL" ? idActual : idPronostico }, Ids(pagina));
        Assert.Equal(1, pagina.GetProperty("totalElementos").GetInt32());
    }

    [Fact]
    public async Task FiltroUbicacion()
    {
        var (token, idUsuario, coban) = await PrepararAsync("Cobán");
        var salama = await AyudasDatos.CrearUbicacionAsync(_fabrica, "Salamá");
        var enCoban = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, coban, Utc(8, 10));
        await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, salama, Utc(8, 11));

        Assert.Equal(new[] { enCoban }, Ids(await GetOkAsync($"{Ruta}?idUbicacion={coban}", token)));

        // Una ubicación sin consultas del usuario (o inexistente) devuelve una lista vacía.
        Assert.Empty(Ids(await GetOkAsync($"{Ruta}?idUbicacion=9223372036854775807", token)));
    }

    [Fact]
    public async Task FiltroFechas_DesdeIncluido_HastaExcluido_RespetaLaZona_YSeCombina()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        // 1 de octubre en Guatemala (UTC-6) = [2026-10-01T06:00Z, 2026-10-02T06:00Z).
        await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(1, 5, 59, 59));
        var inicio = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(1, 6));
        var fin = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(2, 5, 59, 59), TiposConsulta.Pronostico);
        await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(2, 6));

        var desde = Uri.EscapeDataString("2026-10-01T00:00:00-06:00");
        var hasta = Uri.EscapeDataString("2026-10-02T00:00:00-06:00");

        Assert.Equal(new[] { fin, inicio }, Ids(await GetOkAsync($"{Ruta}?desde={desde}&hasta={hasta}", token)));
        Assert.Equal(new[] { inicio }, Ids(await GetOkAsync($"{Ruta}?desde={desde}&hasta={hasta}&tipo=ACTUAL", token)));

        // Solo uno de los dos extremos.
        Assert.Equal(3, Ids(await GetOkAsync($"{Ruta}?desde={desde}", token)).Length);
        Assert.Equal(3, Ids(await GetOkAsync($"{Ruta}?hasta={hasta}", token)).Length);
    }

    [Theory]
    [InlineData("tipo=actual", "tipo")]
    [InlineData("tipo=OTRO", "tipo")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("pagina=-1", "pagina")]
    [InlineData("pagina=100001", "pagina")]
    [InlineData("pagina=abc", "pagina")]
    [InlineData("idUbicacion=0", "idUbicacion")]
    [InlineData("idUbicacion=abc", "idUbicacion")]
    [InlineData("desde=no-es-fecha", "desde")]
    [InlineData("hasta=2026-13-45", "hasta")]
    public async Task ParametroInvalido_Responde400(string consulta, string campo)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await GetAsync($"{Ruta}?{consulta}", token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty(campo, out _), $"Falta errors.{campo}");
    }

    [Theory]
    [InlineData("2026-10-02T00:00:00Z", "2026-10-02T00:00:00Z")]
    [InlineData("2026-10-03T00:00:00Z", "2026-10-02T00:00:00Z")]
    public async Task DesdeNoAnteriorAHasta_Responde400(string desde, string hasta)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await GetAsync($"{Ruta}?desde={Uri.EscapeDataString(desde)}&hasta={Uri.EscapeDataString(hasta)}", token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("desde", out _));
    }

    [Fact]
    public async Task IdUsuarioEnLaUrl_SeIgnora_ElUsuarioSaleDelToken()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        var (_, idOtro, _) = await PrepararAsync();
        var propia = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(9, 10));
        await AyudasDatos.CrearConsultaAsync(_fabrica, idOtro, idUbicacion, Utc(9, 11));

        Assert.Equal(new[] { propia }, Ids(await GetOkAsync($"{Ruta}?idUsuario={idOtro}", token)));
    }

    [Fact]
    public async Task Administrador_VeSoloSuPropioHistorial()
    {
        var correoAdmin = await Ayudas.CrearAdministradorAsync(_fabrica);
        var tokenAdmin = await Ayudas.ObtenerTokenAsync(_cliente, correoAdmin, Ayudas.PasswordValida);
        var idAdmin = await AyudasDatos.IdUsuarioAsync(_fabrica, correoAdmin);
        var (_, idUsuario, idUbicacion) = await PrepararAsync();
        var delAdmin = await AyudasDatos.CrearConsultaAsync(_fabrica, idAdmin, idUbicacion, Utc(10, 10));
        var delUsuario = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(10, 11));

        Assert.Equal(new[] { delAdmin }, Ids(await GetOkAsync(Ruta, tokenAdmin)));
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"{Ruta}/{delUsuario}", tokenAdmin)).StatusCode);
    }

    // ================================================================== Detalle

    [Fact]
    public async Task DetalleActual_CoincideConLaRespuestaDeClimaActual()
    {
        var (token, _, idUbicacion) = await PrepararAsync("Ubicación detalle actual");
        _fabrica.Proveedor.ResponderJson(RespuestasOpenWeather.ClimaActual().ToJsonString());
        var respuestaClima = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, $"/api/clima/actual?idUbicacion={idUbicacion}", token));
        Assert.Equal(HttpStatusCode.OK, respuestaClima.StatusCode);
        var clima = await respuestaClima.Content.ReadFromJsonAsync<JsonElement>();
        var idConsulta = clima.GetProperty("consulta").GetProperty("idConsulta").GetInt64();

        // Una consulta anónima de la misma ubicación no aparece en el historial de nadie.
        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync($"/api/clima/actual?idUbicacion={idUbicacion}")).StatusCode);

        var lista = await GetOkAsync($"{Ruta}?idUbicacion={idUbicacion}", token);
        Assert.Equal(new[] { idConsulta }, Ids(lista));

        var detalle = await GetOkAsync($"{Ruta}/{idConsulta}", token);

        Assert.Equal(idConsulta, detalle.GetProperty("idConsulta").GetInt64());
        Assert.Equal("ACTUAL", detalle.GetProperty("tipo").GetString());
        Assert.Equal(clima.GetProperty("ubicacion").GetRawText(), detalle.GetProperty("ubicacion").GetRawText());
        Assert.Equal(JsonValueKind.Null, detalle.GetProperty("pronostico").ValueKind);
        var actual = detalle.GetProperty("actual");
        foreach (var campo in new[] { "temperatura", "sensacionTermica", "velocidadViento" })
        {
            Assert.Equal(clima.GetProperty(campo).GetDecimal(), actual.GetProperty(campo).GetDecimal());
        }

        Assert.Equal(clima.GetProperty("humedad").GetInt16(), actual.GetProperty("humedad").GetInt16());
        Assert.Equal(clima.GetProperty("codigoCondicion").GetInt16(), actual.GetProperty("codigoCondicion").GetInt16());
        Assert.Equal(clima.GetProperty("descripcion").GetString(), actual.GetProperty("descripcion").GetString());
        Assert.Equal(clima.GetProperty("detalle").GetRawText(), actual.GetProperty("detalle").GetRawText());
        Assert.DoesNotContain(ClimaPlusApiFactory.ClaveOpenWeatherPruebas, await (await GetAsync($"{Ruta}/{idConsulta}", token)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DetallePronostico_CoincideConLaRespuestaDePronostico()
    {
        var (token, _, idUbicacion) = await PrepararAsync("Ubicación detalle pronóstico");
        _fabrica.Proveedor.ResponderJson(RespuestasOpenWeather.PronosticoDesde(_fabrica.Reloj.GetUtcNow().UtcDateTime).ToJsonString());
        var respuestaPronostico = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, $"/api/clima/pronostico?idUbicacion={idUbicacion}", token));
        Assert.Equal(HttpStatusCode.OK, respuestaPronostico.StatusCode);
        var pronosticoApi = await respuestaPronostico.Content.ReadFromJsonAsync<JsonElement>();
        var idConsulta = pronosticoApi.GetProperty("consulta").GetProperty("idConsulta").GetInt64();

        var fila = (await GetOkAsync($"{Ruta}?idUbicacion={idUbicacion}", token)).GetProperty("elementos")[0];
        Assert.Equal(idConsulta, fila.GetProperty("idConsulta").GetInt64());
        Assert.Equal("PRONOSTICO", fila.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, fila.GetProperty("temperatura").ValueKind);

        var detalle = await GetOkAsync($"{Ruta}/{idConsulta}", token);

        Assert.Equal("PRONOSTICO", detalle.GetProperty("tipo").GetString());
        Assert.Equal(JsonValueKind.Null, detalle.GetProperty("actual").ValueKind);
        var pronostico = detalle.GetProperty("pronostico");
        Assert.Equal(pronosticoApi.GetProperty("zonaHorariaSegundos").GetInt32(), pronostico.GetProperty("zonaHorariaSegundos").GetInt32());
        Assert.NotEmpty(pronostico.GetProperty("dias").EnumerateArray());
        Assert.NotEmpty(pronostico.GetProperty("dias")[0].GetProperty("bloques").EnumerateArray());
        Assert.Equal(pronosticoApi.GetProperty("dias").GetRawText(), pronostico.GetProperty("dias").GetRawText());
    }

    [Fact]
    public async Task DetalleDeOtroUsuario_Responde404_IgualQueUnaInexistente()
    {
        var (token, _, idUbicacion) = await PrepararAsync();
        var (_, idOtro, _) = await PrepararAsync();
        var ajena = await AyudasDatos.CrearConsultaAsync(_fabrica, idOtro, idUbicacion, Utc(11, 10));

        var respuestaAjena = await GetAsync($"{Ruta}/{ajena}", token);
        var respuestaInexistente = await GetAsync($"{Ruta}/{long.MaxValue}", token);

        Assert.Equal(HttpStatusCode.NotFound, respuestaAjena.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, respuestaInexistente.StatusCode);
        var problemaAjena = await Ayudas.LeerProblemaAsync(respuestaAjena);
        var problemaInexistente = await Ayudas.LeerProblemaAsync(respuestaInexistente);
        Assert.Equal("La consulta no existe.", problemaAjena.GetProperty("detail").GetString());

        // Indistinguibles: mismos campos y mismos valores. Solo traceId cambia por solicitud (lo genera ASP.NET Core).
        static Dictionary<string, string> CamposPublicos(JsonElement problema) =>
            problema.EnumerateObject().Where(p => p.Name != "traceId").ToDictionary(p => p.Name, p => p.Value.GetRawText());

        var camposAjena = CamposPublicos(problemaAjena);
        Assert.Equal(CamposPublicos(problemaInexistente), camposAjena);
        Assert.Equal(new[] { "detail", "status", "title", "type" }, camposAjena.Keys.Order().ToArray());

        // Ningún campo expone el id consultado ni datos del dueño. Se comparan valores JSON completos,
        // no subcadenas del cuerpo (el "type" es una URL del RFC 9110 que contiene dígitos).
        var idTexto = ajena.ToString();
        foreach (var campo in problemaAjena.EnumerateObject())
        {
            var valor = campo.Value.ValueKind == JsonValueKind.String ? campo.Value.GetString()! : campo.Value.GetRawText();
            Assert.NotEqual(idTexto, valor);
            Assert.NotEqual(idOtro.ToString(), valor);
        }

        Assert.DoesNotMatch($@"\b{idTexto}\b", problemaAjena.GetProperty("title").GetString()!);
        Assert.DoesNotMatch($@"\b{idTexto}\b", problemaAjena.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task DetalleDeConsultaAnonima_Responde404()
    {
        var (token, _, idUbicacion) = await PrepararAsync();
        var anonima = await AyudasDatos.CrearConsultaAsync(_fabrica, null, idUbicacion, Utc(12, 10));

        var respuesta = await GetAsync($"{Ruta}/{anonima}", token);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        await Ayudas.LeerProblemaAsync(respuesta);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("99999999999999999999")]
    public async Task DetalleConIdNoValido_Responde404(string id)
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync($"{Ruta}/{id}", token)).StatusCode);
    }

    [Fact]
    public async Task DetalleConContenidoNoReconocido_SoloExponeCamposConocidos()
    {
        var (token, idUsuario, idUbicacion) = await PrepararAsync();
        // Contenido que no cumple el formato versión 1 (no debería existir; las CHECK de E-03 solo exigen un objeto).
        var actual = await AyudasDatos.CrearConsultaAsync(_fabrica, idUsuario, idUbicacion, Utc(13, 10), detalle: "{\"version\":1,\"secretoInterno\":\"no-exponer\"}");
        var pronostico = await AyudasDatos.CrearConsultaAsync(
            _fabrica, idUsuario, idUbicacion, Utc(13, 11), TiposConsulta.Pronostico,
            detalle: "{\"version\":1,\"zonaHorariaSegundos\":-21600,\"dias\":[],\"secretoInterno\":\"no-exponer\"}");

        var respuestaActual = await GetAsync($"{Ruta}/{actual}", token);
        var respuestaPronostico = await GetAsync($"{Ruta}/{pronostico}", token);

        Assert.Equal(HttpStatusCode.OK, respuestaActual.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respuestaPronostico.StatusCode);
        var textoActual = await respuestaActual.Content.ReadAsStringAsync();
        var textoPronostico = await respuestaPronostico.Content.ReadAsStringAsync();
        Assert.DoesNotContain("no-exponer", textoActual);
        Assert.DoesNotContain("no-exponer", textoPronostico);

        var detalleActual = JsonDocument.Parse(textoActual).RootElement.GetProperty("actual");
        Assert.Equal(JsonValueKind.Null, detalleActual.GetProperty("detalle").ValueKind);
        Assert.Equal(20m, detalleActual.GetProperty("temperatura").GetDecimal());
        var detallePronostico = JsonDocument.Parse(textoPronostico).RootElement.GetProperty("pronostico");
        Assert.Equal(-21600, detallePronostico.GetProperty("zonaHorariaSegundos").GetInt32());
        Assert.Empty(detallePronostico.GetProperty("dias").EnumerateArray());

        // El log registra solo el id, nunca el contenido.
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains($"detalle_ilegible idConsulta={actual}"));
        Assert.DoesNotContain(_fabrica.Logs.Mensajes, m => m.Contains("no-exponer"));
    }
}
