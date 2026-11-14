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
/// Autenticación segura, fase 4: recuperación de contraseña (solicitar → verificar → restablecer), cambio desde el
/// perfil y "Establecer contraseña" para cuentas de Google. Las pruebas adelantan el reloj: fábrica propia.
/// </summary>
public sealed class RecuperacionPasswordTests : IClassFixture<RecuperacionPasswordTests.Fabrica>
{
    /// <summary>Fábrica propia: el reloj de esta clase avanza sin afectar a otras clases de pruebas.</summary>
    public sealed class Fabrica : ClimaPlusApiFactory
    {
    }

    private const string NuevaValida = "NuevaClave2026";

    private readonly Fabrica _fabrica;
    private readonly HttpClient _cliente;

    public RecuperacionPasswordTests(Fabrica fabrica)
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

    private async Task<List<DesafioAutenticacion>> LeerDesafiosAsync(string correo)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.Usuario.Correo == correo && d.Proposito == PropositosDesafio.RecuperarPassword)
            .OrderBy(d => d.CreadoEn)
            .ToListAsync();
    }

    private static string OtroCodigo(string codigo) => codigo == "000000" ? "000001" : "000000";

    private async Task<string> LeerPermisoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permiso").GetString()!;
    }

    private Task<HttpResponseMessage> MeAsync(string token) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", token));

    // ------------------------------------------------------------------ Solicitar

    [Fact]
    public async Task Solicitar_CuentaExistente_Responde202_YEnviaElCodigo()
    {
        var correo = await UsuarioVerificadoAsync();

        var respuesta = await Ayudas.SolicitarRecuperacionAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.RecuperarPassword, cuerpo.GetProperty("proposito").GetString());
        Assert.Equal(string.Concat(correo.AsSpan(0, 1), "***", correo.AsSpan(correo.IndexOf('@'))), cuerpo.GetProperty("destino").GetString());
        Assert.Equal(60, cuerpo.GetProperty("reenviarEn").GetInt32());
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia.TotalMinutes, 9.9, 10.1);

        var enviado = await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "codigo_recuperar_password");
        Assert.Matches("^[0-9]{6}$", EnviadorCorreoFalso.ExtraerCodigo(enviado));
        var desafio = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Pendiente, desafio.Estado);
        Assert.DoesNotContain(EnviadorCorreoFalso.ExtraerCodigo(enviado), desafio.CodigoHash!);
    }

    [Fact]
    public async Task Solicitar_CuentaInexistente_RespondeExactamenteIgual_SinEnviarNada()
    {
        var existente = await UsuarioVerificadoAsync();
        var inexistente = Ayudas.CorreoUnico();

        var real = await (await Ayudas.SolicitarRecuperacionAsync(_cliente, existente)).Content.ReadFromJsonAsync<JsonElement>();
        var respuesta = await Ayudas.SolicitarRecuperacionAsync(_cliente, inexistente);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var falso = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(real.EnumerateObject().Select(p => p.Name), falso.EnumerateObject().Select(p => p.Name));
        Assert.Equal(PropositosDesafio.RecuperarPassword, falso.GetProperty("proposito").GetString());
        Assert.Equal(string.Concat(inexistente.AsSpan(0, 1), "***", inexistente.AsSpan(inexistente.IndexOf('@'))), falso.GetProperty("destino").GetString());
        Assert.Empty(ColaCorreosObservada.CodigosPara(inexistente));

        // El señuelo se comporta igual: espera de reenvío y ningún código lo valida.
        var senuelo = falso.GetProperty("desafio").GetString()!;
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.ReenviarCodigoAsync(_cliente, senuelo)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, senuelo, "123456")).StatusCode);
    }

    [Fact]
    public async Task Solicitar_CuentaDesactivada_Responde202_SinEnviarCodigo()
    {
        var correo = await UsuarioVerificadoAsync();
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);
        var antes = ColaCorreosObservada.CodigosPara(correo).Count;

        Assert.Equal(HttpStatusCode.Accepted, (await Ayudas.SolicitarRecuperacionAsync(_cliente, correo)).StatusCode);
        Assert.Equal(antes, ColaCorreosObservada.CodigosPara(correo).Count);
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Fact]
    public async Task Solicitar_MasDeTresVecesPorHora_RespondeIgual_PeroNoEnvia_NiAnulaElDesafioVivo()
    {
        var correo = await UsuarioVerificadoAsync();
        string? ultimoCodigo = null;
        string? ultimoDesafio = null;
        for (var i = 0; i < 3; i++)
        {
            (ultimoDesafio, ultimoCodigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
            Assert.NotNull(ultimoCodigo);
        }

        var (_, cuarto) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        Assert.Null(cuarto);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("recuperacion_solicitada resultado=tope_por_cuenta", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarRecuperacionAsync(_cliente, ultimoDesafio!, ultimoCodigo!)).StatusCode);
    }

    // ------------------------------------------------------------------ Verificar

    [Fact]
    public async Task Verificar_CodigoCorrecto_DevuelvePermiso_SinSesion_YSinCambiarLaContrasena()
    {
        var correo = await UsuarioVerificadoAsync();
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        var respuesta = await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "permiso", "expiraEn" }, cuerpo.EnumerateObject().Select(p => p.Name));
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia.TotalMinutes, 9.9, 10.1);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Verificado, guardado.Estado);
        Assert.NotNull(guardado.VerificadoEn);
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
    }

    [Fact]
    public async Task Verificar_CodigoIncorrecto_Responde400_YCuentaElIntento()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        var respuesta = await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, OtroCodigo(codigo!));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("codigo", out _));
        Assert.Equal(1, Assert.Single(await LeerDesafiosAsync(correo)).Intentos);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)).StatusCode);
    }

    [Fact]
    public async Task Verificar_CodigoVencido_Responde400()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)).StatusCode);
    }

    [Fact]
    public async Task Verificar_CodigoReutilizado_NoEmiteUnSegundoPermiso()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)).StatusCode);
    }

    [Fact]
    public async Task Verificar_CincoFallos_AnulanElDesafio()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, OtroCodigo(codigo!))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)).StatusCode);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Anulado, guardado.Estado);
        Assert.Equal(5, guardado.Intentos);
    }

    [Fact]
    public async Task Verificar_DiezFallosPorHora_BloqueanLaRecuperacionDeEsaCuenta_PeroNoSuLogin()
    {
        var correo = await UsuarioVerificadoAsync();
        for (var ronda = 0; ronda < 2; ronda++)
        {
            var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, OtroCodigo(codigo!))).StatusCode);
            }
        }

        var (tercero, codigoTercero) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, tercero, codigoTercero!)).StatusCode);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("motivo=tope_fallos_cuenta proposito=RECUPERAR_PASSWORD", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task Reenvio_RespetaLaEspera_YElCodigoNuevoReemplazaAlAnterior()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, anterior) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.ReenviarCodigoAsync(_cliente, desafio)).StatusCode);
        _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(61));
        var reenvio = await Ayudas.ReenviarCodigoAsync(_cliente, desafio);

        Assert.Equal(HttpStatusCode.Accepted, reenvio.StatusCode);
        var nuevoDesafio = (await reenvio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("desafio").GetString()!;
        var nuevo = ColaCorreosObservada.Codigo(correo, previos);
        if (nuevo != anterior)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, nuevoDesafio, anterior!)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarRecuperacionAsync(_cliente, nuevoDesafio, nuevo)).StatusCode);
    }

    [Fact]
    public async Task DesafiosDeOtroProposito_NoSirvenEnLaRecuperacion_YAlReves()
    {
        // Registro (VERIFICAR_CORREO) e inicio de sesión (INICIO_SESION) no sirven en password/verificar.
        var correo = Ayudas.CorreoUnico();
        var registro = await Ayudas.LeerDesafioAsync(await Ayudas.RegistrarAsync(_cliente, correo));
        var codigoRegistro = ColaCorreosObservada.Codigo(correo, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, registro, codigoRegistro)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.VerificarCorreoAsync(_cliente, registro, codigoRegistro)).StatusCode);

        var (login, codigoLogin, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, login, codigoLogin)).StatusCode);

        // Y el de recuperación no sirve para verificar el correo ni para iniciar sesión.
        var (recuperacion, codigoRecuperacion) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, recuperacion, codigoRecuperacion!)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarLoginAsync(_cliente, recuperacion, codigoRecuperacion!)).StatusCode);
        Assert.Equal(0, Assert.Single(await LeerDesafiosAsync(correo)).Intentos);   // ninguno gastó intentos
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarRecuperacionAsync(_cliente, recuperacion, codigoRecuperacion!)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.VerificarLoginAsync(_cliente, login, codigoLogin)).StatusCode);
    }

    [Fact]
    public async Task DosVerificacionesSimultaneas_SoloUnaObtienePermiso()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
    }

    // ------------------------------------------------------------------ El permiso

    [Fact]
    public async Task ElPermiso_NoEsUnaSesion_NiUnDesafio_YElDesafioNoEsUnPermiso()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        var permiso = await LeerPermisoAsync(await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!));

        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(permiso)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarLoginAsync(_cliente, permiso, codigo!)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.ReenviarCodigoAsync(_cliente, permiso)).StatusCode);

        // El token del desafío (aunque ya esté verificado) no sirve como permiso; un JWT de sesión tampoco.
        var restablecerConDesafio = await Ayudas.RestablecerAsync(_cliente, desafio, NuevaValida);
        Assert.Equal(HttpStatusCode.BadRequest, restablecerConDesafio.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(restablecerConDesafio)).GetProperty("errors").TryGetProperty("permiso", out _));
        var sesion = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, sesion, NuevaValida)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);
    }

    [Fact]
    public async Task Restablecer_SinCodigoVerificado_NoCambiaNada()
    {
        var correo = await UsuarioVerificadoAsync();
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var partes = (await Ayudas.ObtenerPermisoAsync(_cliente, correo)).Split('.');
        var alterado = $"{partes[0]}.{partes[1]}.{(partes[2][0] == 'A' ? 'B' : 'A')}{partes[2][1..]}";

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, alterado, NuevaValida)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, "no-es-un-jwt", NuevaValida)).StatusCode);

        Assert.Equal(antes.PasswordHash, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    // ------------------------------------------------------------------ Restablecer

    [Fact]
    public async Task Restablecer_CambiaLaContrasena_RevocaLasSesiones_YAvisaPorCorreo()
    {
        var correo = await UsuarioVerificadoAsync();
        var sesionAnterior = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(sesionAnterior)).StatusCode);
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        var respuesta = await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.DoesNotContain("token", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);   // sin sesión
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(sesionAnterior)).StatusCode);                  // revocada
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo, Ayudas.PasswordValida)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo, NuevaValida)).StatusCode);

        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");
        Assert.Equal(EstadosDesafio.Consumido, Assert.Single(await LeerDesafiosAsync(correo)).Estado);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("Seguridad: password_restablecida", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Restablecer_EnUnaCuentaSinVerificar_LaDejaVerificada()
    {
        var correo = Ayudas.CorreoUnico();
        Assert.Equal(HttpStatusCode.Accepted, (await Ayudas.RegistrarAsync(_cliente, correo)).StatusCode);
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);

        Assert.True((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).CorreoVerificado);
        Assert.Equal(PropositosDesafio.InicioSesion, (await Ayudas.IniciarLoginAsync(_cliente, correo, NuevaValida)).Proposito);
    }

    [Fact]
    public async Task Restablecer_PermisoReutilizado_Responde400()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);

        var segunda = await Ayudas.RestablecerAsync(_cliente, permiso, "OtraNueva2026");

        Assert.Equal(HttpStatusCode.BadRequest, segunda.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo, NuevaValida)).StatusCode);
    }

    [Fact]
    public async Task Restablecer_PermisoVencido_Responde400()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);   // la anterior sigue
    }

    [Fact]
    public async Task UnaSolicitudNueva_AnulaElPermisoAnterior()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        await Ayudas.IniciarRecuperacionAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);
    }

    [Theory]
    [InlineData("NuevaClave2026", "NuevaClave2027", "confirmacion")]
    [InlineData("corta1", "corta1", "passwordNueva")]
    [InlineData("SoloLetrasLargas", "SoloLetrasLargas", "passwordNueva")]
    public async Task Restablecer_ContrasenaInvalida_Responde400_YElPermisoSigueSirviendo(string nueva, string confirmacion, string campo)
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        var respuesta = await Ayudas.RestablecerAsync(_cliente, permiso, nueva, confirmacion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);
    }

    [Fact]
    public async Task Restablecer_ConLaMismaContrasenaActual_Responde400_YElPermisoSigueSirviendo()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);

        var respuesta = await Ayudas.RestablecerAsync(_cliente, permiso, Ayudas.PasswordValida);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordNueva", out _));
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);
    }

    [Fact]
    public async Task DosRestablecimientosSimultaneos_ConElMismoPermiso_SoloUnoGana()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);
        var claves = Enumerable.Range(0, 6).Select(i => $"Simultanea{i}x2026").ToArray();

        var respuestas = await Task.WhenAll(claves.Select(c => Ayudas.RestablecerAsync(_cliente, permiso, c)));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.NoContent));
        Assert.All(respuestas.Where(r => r.StatusCode != HttpStatusCode.NoContent), r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        var ganadora = claves[Array.FindIndex(respuestas, r => r.StatusCode == HttpStatusCode.NoContent)];
        Assert.Equal(HttpStatusCode.Accepted, (await Ayudas.LoginAsync(_cliente, correo, ganadora)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo, claves.First(c => c != ganadora))).StatusCode);
    }

    [Fact]
    public async Task LosLogs_NoContienenCodigosNiPermisosNiContrasenas()
    {
        var correo = await UsuarioVerificadoAsync();
        var (desafio, codigo) = await Ayudas.IniciarRecuperacionAsync(_cliente, correo);
        await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, OtroCodigo(codigo!));
        var permiso = await LeerPermisoAsync(await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, codigo!));
        await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida);

        var logs = _fabrica.Logs.Mensajes.ToArray();
        var propios = logs.Where(m => !m.StartsWith("Microsoft.", StringComparison.Ordinal) && !m.StartsWith("System.", StringComparison.Ordinal)).ToArray();

        Assert.Contains(propios, m => m.Contains("Seguridad: recuperacion_verificada", StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(codigo!, StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(correo, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logs, m => m.Contains(permiso, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(desafio, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(NuevaValida, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(Ayudas.PasswordValida, StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------ Cambio desde el perfil

    [Fact]
    public async Task CambioDesdeElPerfil_RevocaLasSesiones_YAvisaPorCorreo()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await Ayudas.CambiarPasswordConCodigoAsync(_cliente, token, correo, Ayudas.PasswordValida, NuevaValida);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(token)).StatusCode);
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo, NuevaValida)).StatusCode);
    }

    [Fact]
    public async Task CambioDesdeElPerfil_ContrasenaActualIncorrecta_NoCambiaNiAvisa()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, "Incorrecta2026", NuevaValida);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordActual", out _));
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
        await Task.Delay(200);
        Assert.DoesNotContain(_fabrica.Correos.Enviados, m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task CambioDesdeElPerfil_SinSesion_Responde401()
    {
        var paso1 = await _cliente.PostAsJsonAsync("/api/usuarios/me/password/cambio",
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = NuevaValida, confirmacion = NuevaValida });
        var paso2 = await _cliente.PostAsJsonAsync("/api/usuarios/me/password/cambio/verificar",
            new { desafio = "x", codigo = "123456", passwordActual = Ayudas.PasswordValida, passwordNueva = NuevaValida, confirmacion = NuevaValida });

        Assert.Equal(HttpStatusCode.Unauthorized, paso1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, paso2.StatusCode);
    }

    // ------------------------------------------------------------------ Perfil: método de acceso

    [Fact]
    public async Task Perfil_InformaCorreoVerificado_MetodoDeAcceso_YSinDatosSensibles()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        foreach (var ruta in new[] { "/api/auth/me", "/api/usuarios/me" })
        {
            var cuerpo = await (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, ruta, token))).Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(cuerpo.GetProperty("correoVerificado").GetBoolean());
            Assert.True(cuerpo.GetProperty("tienePassword").GetBoolean());
            Assert.False(cuerpo.GetProperty("tieneGoogle").GetBoolean());
            Assert.False(cuerpo.TryGetProperty("passwordHash", out _));
            Assert.False(cuerpo.TryGetProperty("selloSeguridad", out _));
        }
    }

    // ------------------------------------------------------------------ Cuentas de Google: establecer contraseña

    [Fact]
    public async Task CuentaGoogle_EstableceSuContrasena_ConCodigo_YConservaLaIdentidadDeGoogle()
    {
        var (correo, token, sujeto) = await Ayudas.CrearCuentaSoloGoogleAsync(_fabrica);
        var perfil = await (await MeAsync(token)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(perfil.GetProperty("tienePassword").GetBoolean());
        Assert.True(perfil.GetProperty("tieneGoogle").GetBoolean());

        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var inicio = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/password/establecer", token));

        // El cuerpo se lee una sola vez: ReadFromJsonAsync cierra el flujo de la respuesta al terminar.
        Assert.Equal(HttpStatusCode.Accepted, inicio.StatusCode);
        var cuerpoInicio = await inicio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.RecuperarPassword, cuerpoInicio.GetProperty("proposito").GetString());
        var desafio = cuerpoInicio.GetProperty("desafio").GetString();
        Assert.False(string.IsNullOrWhiteSpace(desafio));
        var codigo = ColaCorreosObservada.Codigo(correo, previos);
        var permiso = await LeerPermisoAsync(await Ayudas.VerificarRecuperacionAsync(_cliente, desafio!, codigo));

        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);

        // Contraseña local creada; la sesión anterior queda revocada; la identidad de Google sigue igual.
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(token)).StatusCode);
        var nuevaSesion = await Ayudas.ObtenerTokenAsync(_cliente, correo, NuevaValida);
        perfil = await (await MeAsync(nuevaSesion)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(perfil.GetProperty("tienePassword").GetBoolean());
        Assert.True(perfil.GetProperty("tieneGoogle").GetBoolean());
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var identidad = Assert.Single(await db.IdentidadesExternas.AsNoTracking().Where(i => i.Usuario.Correo == correo).ToListAsync());
        Assert.Equal(sujeto, identidad.Sujeto);
        Assert.Equal(ProveedoresIdentidad.Google, identidad.Proveedor);
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");
    }

    [Fact]
    public async Task CuentaGoogle_UnJwtValidoNoBastaParaCrearLaContrasena()
    {
        var (correo, token, _) = await Ayudas.CrearCuentaSoloGoogleAsync(_fabrica);
        var desafio = await Ayudas.LeerDesafioAsync(
            await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/password/establecer", token)));

        // Sin el código del correo no hay permiso: ni el JWT ni el desafío sirven para restablecer.
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, token, NuevaValida)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, desafio, NuevaValida)).StatusCode);
        var codigo = ColaCorreosObservada.Codigo(correo, ColaCorreosObservada.CodigosPara(correo).Count - 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarRecuperacionAsync(_cliente, desafio, OtroCodigo(codigo))).StatusCode);
        Assert.Null((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task EstablecerPassword_EnUnaCuentaConContrasena_Responde409_SinEnviarCodigo()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = ColaCorreosObservada.CodigosPara(correo).Count;

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/password/establecer", token));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(antes, ColaCorreosObservada.CodigosPara(correo).Count);
    }

    [Fact]
    public async Task EstablecerPassword_SinSesion_Responde401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.PostAsync("/api/usuarios/me/password/establecer", null)).StatusCode);
    }

    // ------------------------------------------------------------------ Regresión del login de dos pasos

    [Fact]
    public async Task LaRecuperacionNoDebilitaElLogin_DespuesDeRestablecerSiguenLosDosPasos()
    {
        var correo = await UsuarioVerificadoAsync();
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.NoContent, (await Ayudas.RestablecerAsync(_cliente, permiso, NuevaValida)).StatusCode);

        var paso1 = await Ayudas.LoginAsync(_cliente, correo, NuevaValida);

        Assert.Equal(HttpStatusCode.Accepted, paso1.StatusCode);
        Assert.DoesNotContain("\"token\"", await paso1.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}

/// <summary>Rate limiting por IP de la recuperación con los valores de producción (5 cada 15 minutos).</summary>
public sealed class RecuperacionLimitesTests : IClassFixture<ClimaPlusApiFactoryLimitesCodigos>
{
    private readonly HttpClient _cliente;

    public RecuperacionLimitesTests(ClimaPlusApiFactoryLimitesCodigos fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task SolicitarRecuperacion_SuperaCincoPorIp_Responde429ConRetryAfter()
    {
        var codigos = new List<HttpResponseMessage>();
        for (var i = 0; i <= ClimaPlusApiFactoryLimitesCodigos.Recuperacion; i++)
        {
            codigos.Add(await Ayudas.SolicitarRecuperacionAsync(_cliente, Ayudas.CorreoUnico()));
        }

        Assert.Equal(ClimaPlusApiFactoryLimitesCodigos.Recuperacion, codigos.Count(r => r.StatusCode == HttpStatusCode.Accepted));
        Assert.Equal(HttpStatusCode.TooManyRequests, codigos[^1].StatusCode);
        Assert.True(codigos[^1].Headers.Contains("Retry-After"));
    }
}
