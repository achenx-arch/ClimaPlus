using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>
/// Autenticación segura, fase 2: registro con verificación de correo (POST register, verificar-correo y
/// reenviar-codigo). Códigos de 6 dígitos, 10 minutos, 5 intentos, un solo uso, reenvío controlado y señuelos.
/// Las pruebas de esta clase adelantan el reloj, por eso tienen su propia fábrica.
/// </summary>
public sealed class RegistroVerificacionTests : IClassFixture<RegistroVerificacionTests.Fabrica>
{
    /// <summary>Fábrica propia: el reloj de esta clase avanza sin afectar a otras clases de pruebas.</summary>
    public sealed class Fabrica : ClimaPlusApiFactory
    {
    }

    private readonly Fabrica _fabrica;
    private readonly HttpClient _cliente;

    public RegistroVerificacionTests(Fabrica fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private async Task<(string Correo, string Desafio, string Codigo)> RegistrarAsync()
    {
        var correo = Ayudas.CorreoUnico();
        var desafio = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo));
        var codigo = await _fabrica.Correos.EsperarCodigoAsync(correo);
        return (correo, desafio, codigo);
    }

    private async Task<List<DesafioAutenticacion>> LeerDesafiosAsync(string correo)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.Usuario.Correo == correo)
            .OrderBy(d => d.CreadoEn)
            .ToListAsync();
    }

    private static string OtroCodigo(string codigo) => codigo == "000000" ? "000001" : "000000";

    private Task<HttpResponseMessage> VerificarAsync(string desafio, string codigo) => Ayudas.VerificarCorreoAsync(_cliente, desafio, codigo);

    private Task<HttpResponseMessage> ReenviarAsync(string desafio) => Ayudas.ReenviarCodigoAsync(_cliente, desafio);

    private void PasarEsperaDeReenvio() => _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(61));

    [Fact]
    public async Task CodigoCorrecto_VerificaElCorreo_ConsumeElDesafio_YNoAbreSesion()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();

        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(0, respuesta.Content.Headers.ContentLength ?? 0);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(PropositosDesafio.VerificarCorreo, guardado.Proposito);
        Assert.Equal(EstadosDesafio.Consumido, guardado.Estado);
        Assert.NotNull(guardado.FinalizadoEn);

        // El código nunca se guarda en claro: solo su HMAC-SHA256 (64 hex).
        Assert.NotNull(guardado.CodigoHash);
        Assert.Matches("^[0-9a-f]{64}$", guardado.CodigoHash!);
        Assert.DoesNotContain(codigo, guardado.CodigoHash!);
    }

    [Fact]
    public async Task CodigoIncorrecto_Responde400Generico_YCuentaUnIntento()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();

        var respuesta = await VerificarAsync(desafio, OtroCodigo(codigo));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("codigo", out _));
        Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        Assert.Equal(1, Assert.Single(await LeerDesafiosAsync(correo)).Intentos);

        // Sigue valiendo el código correcto (quedan intentos).
        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12a456")]
    [InlineData(" 123456")]
    [InlineData("")]
    public async Task CodigoMalFormado_Responde400SinTocarElDesafio(string codigo)
    {
        var (correo, desafio, _) = await RegistrarAsync();

        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(0, Assert.Single(await LeerDesafiosAsync(correo)).Intentos);
    }

    [Fact]
    public async Task CodigoVencido_DespuesDe10Minutos_NoVerifica()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));
        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
    }

    [Fact]
    public async Task CodigoUsado_NoSirveUnaSegundaVez()
    {
        var (_, desafio, codigo) = await RegistrarAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(desafio, codigo)).StatusCode);

        var segunda = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
    }

    [Fact]
    public async Task CincoIntentosFallidos_AnulanElDesafio_AunqueDespuesLleguePrimeroElCodigoCorrecto()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(desafio, OtroCodigo(codigo))).StatusCode);
        }

        var conCodigoCorrecto = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.BadRequest, conCodigoCorrecto.StatusCode);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Anulado, guardado.Estado);
        Assert.Equal(5, guardado.Intentos);
        Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);

        // Un desafío anulado no se puede reactivar pidiendo otro código: responde igual pero no envía nada.
        PasarEsperaDeReenvio();
        Assert.Equal(HttpStatusCode.Accepted, (await ReenviarAsync(desafio)).StatusCode);
        await Task.Delay(200);
        Assert.Single(_fabrica.Correos.CodigosPara(correo));
    }

    [Fact]
    public async Task ReenvioAntesDe60Segundos_Responde429ConRetryAfter_YNoEnvia()
    {
        var (correo, desafio, _) = await RegistrarAsync();

        var respuesta = await ReenviarAsync(desafio);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        var segundos = int.Parse(respuesta.Headers.GetValues("Retry-After").Single(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(segundos, 1, 60);
        await Task.Delay(200);
        Assert.Single(_fabrica.Correos.CodigosPara(correo));
    }

    [Fact]
    public async Task ReenvioPermitido_EnviaCodigoNuevo_YElAnteriorDejaDeValer()
    {
        var (correo, desafio, anterior) = await RegistrarAsync();

        PasarEsperaDeReenvio();
        var respuesta = await ReenviarAsync(desafio);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.VerificarCorreo, cuerpo.GetProperty("proposito").GetString());
        Assert.Equal(60, cuerpo.GetProperty("reenviarEn").GetInt32());
        Assert.False(cuerpo.TryGetProperty("destino", out _));       // el reenvío no repite el destino
        var nuevoDesafio = cuerpo.GetProperty("desafio").GetString()!;

        var nuevo = await _fabrica.Correos.EsperarCodigoAsync(correo, 2);
        Assert.Equal(1, Assert.Single(await LeerDesafiosAsync(correo)).Reenvios);

        if (nuevo != anterior)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(nuevoDesafio, anterior)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(nuevoDesafio, nuevo)).StatusCode);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
    }

    [Fact]
    public async Task ReenviosLimitados_CincoPorDesafio_YCincoCorreosConCodigoPorHora()
    {
        var (correo, desafio, _) = await RegistrarAsync();

        for (var i = 0; i < 5; i++)
        {
            PasarEsperaDeReenvio();
            Assert.Equal(HttpStatusCode.Accepted, (await ReenviarAsync(desafio)).StatusCode);
        }

        PasarEsperaDeReenvio();
        var sexto = await ReenviarAsync(desafio);

        Assert.Equal(HttpStatusCode.TooManyRequests, sexto.StatusCode);
        Assert.True(sexto.Headers.Contains("Retry-After"));

        // Tope por cuenta: 1 del registro + 4 reenvíos. El quinto reenvío respondió 202, pero no salió el correo.
        await _fabrica.Correos.EsperarCodigoAsync(correo, 5);
        await Task.Delay(200);
        Assert.Equal(5, _fabrica.Correos.CodigosPara(correo).Count);
        Assert.Equal(5, Assert.Single(await LeerDesafiosAsync(correo)).Reenvios);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("codigo_no_enviado motivo=tope_por_cuenta", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Senuelo_SeComportaIgualQueUnDesafioReal_YNoModificaNada()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var desafiosAntes = await LeerDesafiosAsync(correo);

        var senuelo = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo, "OtraClave2026", "Otro Nombre"));

        // Espera de reenvío idéntica a la de un desafío real.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await ReenviarAsync(senuelo)).StatusCode);
        PasarEsperaDeReenvio();
        var reenvio = await ReenviarAsync(senuelo);
        Assert.Equal(HttpStatusCode.Accepted, reenvio.StatusCode);
        var otroSenuelo = (await reenvio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("desafio").GetString()!;

        // Ningún código lo verifica, ni siquiera tras 5 intentos.
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(otroSenuelo, i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture))).StatusCode);
        }

        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.Nombre, despues.Nombre);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
        Assert.Equal(antes.CorreoVerificado, despues.CorreoVerificado);
        var desafiosDespues = await LeerDesafiosAsync(correo);
        Assert.Equal(desafiosAntes.Count, desafiosDespues.Count);
        Assert.All(desafiosDespues, d => Assert.Equal(EstadosDesafio.Consumido, d.Estado));
        Assert.Single(_fabrica.Correos.CodigosPara(correo));               // solo el código del registro original
    }

    [Fact]
    public async Task TokenDeDesafio_NoAbreEndpointsPrivados()
    {
        var (_, desafio, _) = await RegistrarAsync();

        var me = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", desafio));
        var favoritos = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/favoritos", desafio));

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, favoritos.StatusCode);
    }

    [Fact]
    public async Task TokenDeSesion_NoSirveComoDesafio()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var codigosAntes = ColaCorreosObservada.CodigosPara(correo).Count;   // registro + inicio de sesión

        var respuesta = await VerificarAsync(token, "123456");
        var reenvio = await ReenviarAsync(token);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reenvio.StatusCode);
        Assert.Equal(codigosAntes, ColaCorreosObservada.CodigosPara(correo).Count);
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("contenido")]
    [InlineData("basura")]
    public async Task TokenDeDesafioAlterado_SeRechaza(string alteracion)
    {
        var (correo, desafio, codigo) = await RegistrarAsync();
        var partes = desafio.Split('.');
        var alterado = alteracion switch
        {
            "firma" => $"{partes[0]}.{partes[1]}.{(partes[2][0] == 'A' ? 'B' : 'A')}{partes[2][1..]}",
            "contenido" => $"{partes[0]}.{Base64Url(JsonSerializer.Serialize(new { did = Guid.NewGuid().ToString("N"), prp = PropositosDesafio.VerificarCorreo }))}.{partes[2]}",
            _ => "no-es-un-jwt",
        };

        var respuesta = await VerificarAsync(alterado, codigo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReenviarAsync(alterado)).StatusCode);
    }

    [Fact]
    public async Task DosVerificacionesSimultaneas_ConElMismoCodigo_SoloUnaTieneExito()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => VerificarAsync(desafio, codigo)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.NoContent),
            r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Equal(EstadosDesafio.Consumido, Assert.Single(await LeerDesafiosAsync(correo)).Estado);
    }

    [Fact]
    public async Task FalloTemporalDelSmtp_SeReintenta_YElCodigoLlega()
    {
        _fabrica.Correos.FallarProximos(2);

        var (correo, desafio, codigo) = await RegistrarAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(desafio, codigo)).StatusCode);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
    }

    [Fact]
    public async Task CorreoPerdido_SeRecuperaPidiendoOtroCodigo()
    {
        _fabrica.Correos.FallarProximos(3);   // agota los 3 intentos de la cola: el primer código no llega
        var correo = Ayudas.CorreoUnico();
        var desafio = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo));
        await Task.Delay(300);
        Assert.Empty(_fabrica.Correos.CodigosPara(correo));

        PasarEsperaDeReenvio();
        var reenvio = await ReenviarAsync(desafio);
        var codigo = await _fabrica.Correos.EsperarCodigoAsync(correo);

        Assert.Equal(HttpStatusCode.Accepted, reenvio.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Fact]
    public async Task LosLogs_NoContienenCodigosNiHashesNiTokensDeDesafio()
    {
        var (correo, desafio, codigo) = await RegistrarAsync();
        await VerificarAsync(desafio, OtroCodigo(codigo));
        await VerificarAsync(desafio, codigo);
        var hash = Assert.Single(await LeerDesafiosAsync(correo)).CodigoHash!;

        var logs = _fabrica.Logs.Mensajes.ToArray();
        // Mensajes de la propia API (los de ASP.NET y EF incluyen tiempos y ids donde 6 dígitos pueden coincidir por azar).
        var propios = logs.Where(m => !m.StartsWith("Microsoft.", StringComparison.Ordinal) && !m.StartsWith("System.", StringComparison.Ordinal)).ToArray();

        Assert.Contains(logs, m => m.Contains("Seguridad: correo_verificado", StringComparison.Ordinal));
        Assert.Contains(logs, m => m.Contains("Seguridad: codigo_rechazado", StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(codigo, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(hash, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, m => m.Contains(desafio, StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(correo, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, m => m.Contains(ClimaPlusApiFactory.ClaveHmacPruebas, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CodigoDeOtroUsuario_NoVerificaMiCuenta()
    {
        var (correoA, desafioA, _) = await RegistrarAsync();
        var (_, _, codigoB) = await RegistrarAsync();

        var respuesta = await VerificarAsync(desafioA, codigoB);

        // Solo podría coincidir por azar (1 en un millón); en ese caso el código de B también sería el de A.
        if (respuesta.StatusCode == HttpStatusCode.BadRequest)
        {
            Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correoA)).CorreoVerificado);
        }
    }

    [Fact]
    public async Task RegistrarseDeNuevoSinVerificar_DevuelveSenuelo_YElDesafioOriginalSigueValiendo()
    {
        // Un usuario sin verificar que vuelve a registrarse recibe un señuelo (no hay pista de que existe):
        // su desafío original sigue vivo y es el que vale.
        var (correo, desafio, codigo) = await RegistrarAsync();

        var otra = await Ayudas.RegistrarAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.Accepted, otra.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await VerificarAsync(desafio, codigo)).StatusCode);
        Assert.Single(await LeerDesafiosAsync(correo));
    }

    private static string Base64Url(string texto) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Rate limiting por IP de los nuevos endpoints, con los valores de producción.</summary>
public sealed class RegistroVerificacionLimitesTests : IClassFixture<ClimaPlusApiFactoryLimitesCodigos>
{
    private readonly HttpClient _cliente;

    public RegistroVerificacionLimitesTests(ClimaPlusApiFactoryLimitesCodigos fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Registro_SuperaCincoPorIp_Responde429ConRetryAfter()
    {
        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i <= ClimaPlusApiFactoryLimitesCodigos.Registro; i++)
        {
            codigos.Add((await Ayudas.RegistrarAsync(_cliente, Ayudas.CorreoUnico())).StatusCode);
        }

        Assert.Equal(ClimaPlusApiFactoryLimitesCodigos.Registro, codigos.Count(c => c == HttpStatusCode.Accepted));
        Assert.Equal(HttpStatusCode.TooManyRequests, codigos[^1]);
    }

    [Fact]
    public async Task VerificarCorreo_SuperaVeintePorIp_Responde429()
    {
        var ultimo = HttpStatusCode.OK;
        for (var i = 0; i <= ClimaPlusApiFactoryLimitesCodigos.Codigos; i++)
        {
            var respuesta = await Ayudas.VerificarCorreoAsync(_cliente, "token-que-no-existe", "123456");
            ultimo = respuesta.StatusCode;
            if (i < ClimaPlusApiFactoryLimitesCodigos.Codigos)
            {
                Assert.Equal(HttpStatusCode.BadRequest, ultimo);
            }
            else
            {
                Assert.True(respuesta.Headers.Contains("Retry-After"));
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }

    [Fact]
    public async Task ReenviarCodigo_SuperaDiezPorIp_Responde429()
    {
        var ultimo = HttpStatusCode.OK;
        for (var i = 0; i <= ClimaPlusApiFactoryLimitesCodigos.Reenvio; i++)
        {
            ultimo = (await Ayudas.ReenviarCodigoAsync(_cliente, "token-que-no-existe")).StatusCode;
            if (i < ClimaPlusApiFactoryLimitesCodigos.Reenvio)
            {
                Assert.Equal(HttpStatusCode.BadRequest, ultimo);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }
}
