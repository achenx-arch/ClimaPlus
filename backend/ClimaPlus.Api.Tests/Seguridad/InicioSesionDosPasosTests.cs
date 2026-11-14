using System.Globalization;
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
/// Autenticación segura, fase 3: inicio de sesión en dos pasos. POST /api/auth/login comprueba la contraseña y
/// envía un código (202, sin JWT); POST /api/auth/login/verificar valida el código y solo entonces emite el JWT.
/// Las pruebas de esta clase adelantan el reloj, por eso tienen su propia fábrica.
/// </summary>
public sealed class InicioSesionDosPasosTests : IClassFixture<InicioSesionDosPasosTests.Fabrica>
{
    /// <summary>Fábrica propia: el reloj de esta clase avanza sin afectar a otras clases de pruebas.</summary>
    public sealed class Fabrica : ClimaPlusApiFactory
    {
    }

    private readonly Fabrica _fabrica;
    private readonly HttpClient _cliente;

    public InicioSesionDosPasosTests(Fabrica fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private async Task<string> UsuarioVerificadoAsync()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);
        return correo;
    }

    private async Task<List<DesafioAutenticacion>> LeerDesafiosAsync(string correo, string proposito)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.Usuario.Correo == correo && d.Proposito == proposito)
            .OrderBy(d => d.CreadoEn)
            .ToListAsync();
    }

    private static string OtroCodigo(string codigo) => codigo == "000000" ? "000001" : "000000";

    private Task<HttpResponseMessage> VerificarAsync(string desafio, string codigo) => Ayudas.VerificarLoginAsync(_cliente, desafio, codigo);

    // ------------------------------------------------------------------ Paso 1

    [Fact]
    public async Task Paso1_ContrasenaCorrecta_Responde202ConDesafio_SinJwt_YEnviaElCodigo()
    {
        var correo = await UsuarioVerificadoAsync();

        var respuesta = await Ayudas.LoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"token\"", texto, StringComparison.Ordinal);
        Assert.DoesNotContain(correo, texto, StringComparison.OrdinalIgnoreCase);
        var cuerpo = JsonSerializer.Deserialize<JsonElement>(texto);
        Assert.Equal(PropositosDesafio.InicioSesion, cuerpo.GetProperty("proposito").GetString());
        Assert.Equal(string.Concat(correo.AsSpan(0, 1), "***", correo.AsSpan(correo.IndexOf('@'))), cuerpo.GetProperty("destino").GetString());
        Assert.Equal(60, cuerpo.GetProperty("reenviarEn").GetInt32());
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia.TotalMinutes, 4.9, 5.1);    // el código de inicio de sesión dura 5 minutos

        var correoEnviado = await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "codigo_inicio_sesion");
        Assert.Matches("^[0-9]{6}$", EnviadorCorreoFalso.ExtraerCodigo(correoEnviado));
        var desafio = Assert.Single(await LeerDesafiosAsync(correo, PropositosDesafio.InicioSesion));
        Assert.Equal(EstadosDesafio.Pendiente, desafio.Estado);
    }

    [Fact]
    public async Task Paso1_ContrasenaIncorrecta_Responde401_YNoEnviaCodigo()
    {
        var correo = await UsuarioVerificadoAsync();
        var antes = ColaCorreosObservada.CodigosPara(correo).Count;

        var respuesta = await Ayudas.LoginAsync(_cliente, correo, "OtraClave2026");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal(antes, ColaCorreosObservada.CodigosPara(correo).Count);
        Assert.Empty(await LeerDesafiosAsync(correo, PropositosDesafio.InicioSesion));
    }

    [Fact]
    public async Task SinElSegundoPaso_NoHaySesion_ElTokenDelDesafioNoAbreEndpointsPrivados()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, _, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", desafio))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", desafio))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/favoritos", desafio))).StatusCode);
    }

    // ------------------------------------------------------------------ Paso 2

    [Fact]
    public async Task Paso2_CodigoCorrecto_EmiteElJwt_QueAbreLaSesion_YConsumeElDesafio()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, proposito) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        Assert.Equal(PropositosDesafio.InicioSesion, proposito);

        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", cuerpo.GetProperty("tipoToken").GetString());
        Assert.Equal(Roles.Usuario, cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        Assert.False(cuerpo.GetProperty("usuario").TryGetProperty("passwordHash", out _));
        var token = cuerpo.GetProperty("token").GetString()!;
        var me = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(correo, (await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("correo").GetString());

        Assert.Equal(EstadosDesafio.Consumido, Assert.Single(await LeerDesafiosAsync(correo, PropositosDesafio.InicioSesion)).Estado);
    }

    [Fact]
    public async Task Paso2_CodigoIncorrecto_Responde400Generico_YQuedanIntentos()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        var fallo = await VerificarAsync(desafio, OtroCodigo(codigo));

        Assert.Equal(HttpStatusCode.BadRequest, fallo.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(fallo)).GetProperty("errors").TryGetProperty("codigo", out _));
        Assert.Equal(1, Assert.Single(await LeerDesafiosAsync(correo, PropositosDesafio.InicioSesion)).Intentos);
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CodigoReutilizado_NoEmiteUnaSegundaSesion()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(desafio, codigo)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CodigoVencido_DespuesDe5Minutos_NoEmiteSesion()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(6));

        Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CincoCodigosIncorrectos_AnulanElDesafio_YBloqueanLaCuentaDesdeEsaIp()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(desafio, OtroCodigo(codigo))).StatusCode);
        }

        // Los fallos de código suman al bloqueo por cuenta + IP del login (condición 2).
        var conCodigoCorrecto = await VerificarAsync(desafio, codigo);
        Assert.Equal(HttpStatusCode.TooManyRequests, conCodigoCorrecto.StatusCode);
        Assert.True(conCodigoCorrecto.Headers.Contains("Retry-After"));
        var guardado = Assert.Single(await LeerDesafiosAsync(correo, PropositosDesafio.InicioSesion));
        Assert.Equal(EstadosDesafio.Anulado, guardado.Estado);
        Assert.Equal(5, guardado.Intentos);

        // Ni con la contraseña correcta se puede pedir otro código hasta que pase el bloqueo.
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(15));
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task ConocerLaContrasena_NoPermiteProbarCodigosSinLimite()
    {
        // Un atacante con la contraseña pide desafíos nuevos para seguir probando: el contador no se reinicia con
        // la contraseña correcta, solo con un inicio de sesión completo.
        var correo = await UsuarioVerificadoAsync();

        var (primero, codigo1, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(primero, OtroCodigo(codigo1))).StatusCode);
        }

        var (segundo, codigo2, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(segundo, OtroCodigo(codigo2))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await VerificarAsync(segundo, codigo2)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task DosVerificacionesSimultaneas_SoloUnaObtieneSesion()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => VerificarAsync(desafio, codigo)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    [Fact]
    public async Task UnDesafioNuevo_AnulaElAnterior()
    {
        var correo = await UsuarioVerificadoAsync();
        var (viejo, codigoViejo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        var (nuevo, codigoNuevo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(viejo, codigoViejo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(nuevo, codigoNuevo)).StatusCode);
    }

    // ------------------------------------------------------------------ Tokens que no sirven

    [Fact]
    public async Task DesafioDelRegistro_NoSirveParaIniciarSesion()
    {
        var correo = Ayudas.CorreoUnico();
        var desafio = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo));
        var codigo = ColaCorreosObservada.Codigo(correo, 0);

        Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(desafio, codigo)).StatusCode);

        // No se gastó ningún intento: el mismo código sigue verificando el correo (sin sesión).
        Assert.Equal(0, Assert.Single(await LeerDesafiosAsync(correo, PropositosDesafio.VerificarCorreo)).Intentos);
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.VerificarCorreoAsync(_cliente, desafio, codigo)).StatusCode);
    }

    [Fact]
    public async Task DesafioDeInicioDeSesion_NoSirveParaVerificarElCorreo()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, desafio, codigo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(desafio, codigo)).StatusCode);
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("basura")]
    [InlineData("sesion")]
    public async Task TokenAlterado_OTokenDeSesion_SeRechazaEnElPaso2(string tipo)
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        var partes = desafio.Split('.');
        var token = tipo switch
        {
            "firma" => $"{partes[0]}.{partes[1]}.{(partes[2][0] == 'A' ? 'B' : 'A')}{partes[2][1..]}",
            "sesion" => (await Ayudas.CrearUsuarioConSesionAsync(_cliente)).Token,
            _ => "no-es-un-jwt",
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(token, codigo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(desafio, codigo)).StatusCode);   // el real sigue intacto
    }

    // ------------------------------------------------------------------ Cuentas sin verificar, desactivadas y roles

    [Fact]
    public async Task CuentaSinVerificar_RecibeUnCodigoDeVerificacion_YAlValidarloQuedaVerificadaConSesion()
    {
        // Cuentas anteriores a la migración 004 o registros que no terminaron la verificación.
        var correo = Ayudas.CorreoUnico();
        Assert.Equal(HttpStatusCode.Accepted, (await Ayudas.RegistrarAsync(_cliente, correo)).StatusCode);

        var (desafio, codigo, proposito) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        Assert.Equal(PropositosDesafio.VerificarCorreo, proposito);
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "codigo_verificar_correo"
                                                 && EnviadorCorreoFalso.ExtraerCodigo(m) == codigo);
        var respuesta = await VerificarAsync(desafio, codigo);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        var token = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token))).StatusCode);

        // El siguiente inicio ya usa el código de inicio de sesión.
        Assert.Equal(PropositosDesafio.InicioSesion, (await Ayudas.IniciarLoginAsync(_cliente, correo)).Proposito);
    }

    [Fact]
    public async Task CuentaDesactivadaEntreLosDosPasos_NoRecibeSesion()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);
        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.DoesNotContain("token", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CuentaDesactivada_EnElPaso1_Responde403_SinEnviarCodigo()
    {
        var correo = await UsuarioVerificadoAsync();
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);
        var antes = ColaCorreosObservada.CodigosPara(correo).Count;

        Assert.Equal(HttpStatusCode.Forbidden, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
        Assert.Equal(antes, ColaCorreosObservada.CodigosPara(correo).Count);
    }

    [Fact]
    public async Task Administrador_TambienUsaDosPasos_YConservaSuRol()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);

        var (desafio, codigo, proposito) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        Assert.Equal(PropositosDesafio.InicioSesion, proposito);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", desafio))).StatusCode);

        var respuesta = await VerificarAsync(desafio, codigo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Roles.Administrador, cuerpo.GetProperty("usuario").GetProperty("rol").GetString());
        var token = cuerpo.GetProperty("token").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", token))).StatusCode);
    }

    // ------------------------------------------------------------------ Reenvío

    [Fact]
    public async Task ReenvioDelCodigoDeLogin_ElNuevoTokenSigueSirviendoParaLaSesion()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, anterior, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.ReenviarCodigoAsync(_cliente, desafio)).StatusCode);
        _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(61));
        var reenvio = await Ayudas.ReenviarCodigoAsync(_cliente, desafio);

        Assert.Equal(HttpStatusCode.Accepted, reenvio.StatusCode);
        var cuerpo = await reenvio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.InicioSesion, cuerpo.GetProperty("proposito").GetString());
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia.TotalMinutes, 4.9, 5.1);
        var nuevoDesafio = cuerpo.GetProperty("desafio").GetString()!;
        var nuevo = ColaCorreosObservada.Codigo(correo, previos);

        if (nuevo != anterior)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await VerificarAsync(nuevoDesafio, anterior)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(nuevoDesafio, nuevo)).StatusCode);
    }

    // ------------------------------------------------------------------ Tope de fallos de la verificación de correo

    [Fact]
    public async Task VerificacionDeCorreo_DiezFallosPorHora_BloqueanEsePropositoPeroNoElLogin()
    {
        var correo = Ayudas.CorreoUnico();
        var registro = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo));
        var codigoRegistro = ColaCorreosObservada.Codigo(correo, 0);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, registro, OtroCodigo(codigoRegistro))).StatusCode);
        }

        var (segundo, codigoSegundo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, segundo, OtroCodigo(codigoSegundo))).StatusCode);
        }

        // Diez fallos en la hora: el endpoint de verificación de correo ya no acepta ni el código correcto.
        var (tercero, codigoTercero, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, tercero, codigoTercero)).StatusCode);
        Assert.False((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("motivo=tope_fallos_cuenta", StringComparison.Ordinal));

        // Un desconocido que falla códigos no impide que el dueño inicie sesión (y así verifica su correo).
        Assert.Equal(HttpStatusCode.OK, (await VerificarAsync(tercero, codigoTercero)).StatusCode);
        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
    }

    // ------------------------------------------------------------------ Logs

    [Fact]
    public async Task LosLogs_NoContienenCodigosNiTokens_NiLaContrasena()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        await VerificarAsync(desafio, OtroCodigo(codigo));
        var respuesta = await VerificarAsync(desafio, codigo);
        var token = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

        var logs = _fabrica.Logs.Mensajes.ToArray();
        var propios = logs.Where(m => !m.StartsWith("Microsoft.", StringComparison.Ordinal) && !m.StartsWith("System.", StringComparison.Ordinal)).ToArray();

        Assert.Contains(propios, m => m.Contains("Seguridad: login_codigo_enviado", StringComparison.Ordinal));
        Assert.Contains(propios, m => m.Contains("Seguridad: login_exitoso", StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(codigo, StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(correo, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, m => m.Contains(desafio, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(Ayudas.PasswordValida, StringComparison.Ordinal));
    }
}

/// <summary>Bloqueo por cuenta + IP (5 fallos) y por cuenta desde todas las IP (20 fallos), condición 2.</summary>
public sealed class BloqueoPorCuentaEIpTests : IClassFixture<ClimaPlusApiFactoryVariasIp>
{
    private readonly HttpClient _cliente;

    public BloqueoPorCuentaEIpTests(ClimaPlusApiFactoryVariasIp fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> LoginDesdeAsync(string ip, string correo, string password)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { correo, password }) };
        solicitud.Headers.Add(FiltroIpConexion.Cabecera, ip);
        return _cliente.SendAsync(solicitud);
    }

    [Fact]
    public async Task CincoFallosDesdeUnaIp_BloqueanEsaIp_PeroNoAlDuenoDesdeOtra()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginDesdeAsync("203.0.113.10", correo, "Incorrecta2026")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginDesdeAsync("203.0.113.10", correo, Ayudas.PasswordValida)).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await LoginDesdeAsync("198.51.100.20", correo, Ayudas.PasswordValida)).StatusCode);
    }

    [Fact]
    public async Task VeinteFallosDesdeVariasIp_BloqueanLaCuentaDesdeCualquierIp()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);

        // Ataque distribuido: 5 IP con 4 fallos cada una (ninguna llega a su propio bloqueo de 5).
        for (var ip = 1; ip <= 5; ip++)
        {
            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await LoginDesdeAsync(string.Create(CultureInfo.InvariantCulture, $"203.0.113.{ip}"), correo, "Incorrecta2026")).StatusCode);
            }
        }

        var desdeIpNueva = await LoginDesdeAsync("192.0.2.99", correo, Ayudas.PasswordValida);

        Assert.Equal(HttpStatusCode.TooManyRequests, desdeIpNueva.StatusCode);
        Assert.True(desdeIpNueva.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task CorreoInexistente_SeBloqueaIgual_PorIpYPorCuenta()
    {
        var inexistente = Ayudas.CorreoUnico();

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginDesdeAsync("203.0.113.50", inexistente, "Incorrecta2026")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginDesdeAsync("203.0.113.50", inexistente, "Incorrecta2026")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginDesdeAsync("203.0.113.51", inexistente, "Incorrecta2026")).StatusCode);
    }
}
