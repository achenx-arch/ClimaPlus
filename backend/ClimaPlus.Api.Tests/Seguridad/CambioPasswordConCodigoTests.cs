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
/// Cambio de contraseña desde el perfil con doble verificación (migración 005, propósito CAMBIAR_PASSWORD):
/// paso 1 (contraseña actual + nueva + confirmación → código por correo) y paso 2 (código → cambio). La contraseña no
/// cambia hasta validar el código; el cambio cierra todas las sesiones, anula los desafíos pendientes y avisa por correo.
/// Las pruebas adelantan el reloj: fábrica propia.
/// </summary>
public sealed class CambioPasswordConCodigoTests : IClassFixture<CambioPasswordConCodigoTests.Fabrica>
{
    /// <summary>Fábrica propia: el reloj de esta clase avanza sin afectar a otras clases de pruebas.</summary>
    public sealed class Fabrica : ClimaPlusApiFactory
    {
    }

    private const string Nueva = "CambioSeguro2027";

    private readonly Fabrica _fabrica;
    private readonly HttpClient _cliente;

    public CambioPasswordConCodigoTests(Fabrica fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private static string OtroCodigo(string codigo) => codigo == "000000" ? "000001" : "000000";

    private Task<HttpResponseMessage> MeAsync(string token) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token));

    private async Task<List<DesafioAutenticacion>> LeerDesafiosAsync(string correo, string? proposito = PropositosDesafio.CambiarPassword)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.Usuario.Correo == correo && (proposito == null || d.Proposito == proposito))
            .OrderBy(d => d.CreadoEn)
            .ToListAsync();
    }

    // ------------------------------------------------------------------ Paso 1

    [Fact]
    public async Task Paso1_ConDatosCorrectos_Responde202_EnviaElCodigo_YNoCambiaNada()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.CambiarPassword, cuerpo.GetProperty("proposito").GetString());
        Assert.DoesNotContain(correo, cuerpo.GetProperty("destino").GetString());   // correo enmascarado
        Assert.False(cuerpo.TryGetProperty("token", out _));

        // Vigencia de 5 minutos.
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia, TimeSpan.FromMinutes(4.5), TimeSpan.FromMinutes(5.1));

        var correoCodigo = ColaCorreosObservada.CodigosPara(correo)[previos];
        Assert.Equal("codigo_cambiar_password", correoCodigo.Tipo);
        Assert.Matches("^[0-9]{6}$", EnviadorCorreoFalso.ExtraerCodigo(correoCodigo));

        // Nada cambió: misma contraseña, mismo sello, la sesión sigue valiendo.
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);

        var desafio = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Pendiente, desafio.Estado);
        Assert.NotNull(desafio.CodigoHash);
        Assert.DoesNotContain(EnviadorCorreoFalso.ExtraerCodigo(correoCodigo), desafio.CodigoHash!);
    }

    [Fact]
    public async Task Paso1_ContrasenaActualIncorrecta_Responde400_YNoEnviaCodigo()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, "Incorrecta2026", Nueva);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordActual", out _));
        Assert.Equal(previos, ColaCorreosObservada.CodigosPara(correo).Count);
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Theory]
    [InlineData("CambioSeguro2027", "CambioSeguro2028", "confirmacion")]
    [InlineData("corta1", "corta1", "passwordNueva")]
    [InlineData("SoloLetrasLargas", "SoloLetrasLargas", "passwordNueva")]
    public async Task Paso1_NuevaInvalidaOConfirmacionDistinta_Responde400(string nueva, string confirmacion, string campo)
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, nueva, confirmacion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Fact]
    public async Task Paso1_IgualALaActual_Responde400()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, Ayudas.PasswordValida);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordNueva", out _));
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Fact]
    public async Task Paso1_CincoContrasenasActualesIncorrectas_BloqueanLaCuentaDesdeEsaIp()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, "Incorrecta2026", Nueva)).StatusCode);
        }

        // Aun con la contraseña correcta: bloqueo temporal (mismo limitador que el login, por cuenta + IP).
        var bloqueada = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueada.StatusCode);
        Assert.True(bloqueada.Headers.RetryAfter is not null);
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Fact]
    public async Task Paso1_CorreoSinVerificar_Responde409()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.CorreoVerificado = false);

        var respuesta = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Empty(await LeerDesafiosAsync(correo));
    }

    [Fact]
    public async Task RutaAntiguaPut_YaNoExiste_YNoCambiaLaContrasena()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me/password", token,
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = Nueva }));

        Assert.True(respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, respuesta.StatusCode.ToString());
        Assert.Equal(antes.PasswordHash, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    // ------------------------------------------------------------------ Paso 2

    [Fact]
    public async Task Paso2_CodigoCorrecto_CambiaLaContrasena_CierraTodasLasSesiones_YAvisa()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var otraSesion = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var respuesta = await Ayudas.CambiarPasswordConCodigoAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.NotEqual(antes.PasswordHash, despues.PasswordHash);
        Assert.NotEqual(antes.SelloSeguridad, despues.SelloSeguridad);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(otraSesion)).StatusCode);
        Assert.Equal(EstadosDesafio.Consumido, Assert.Single(await LeerDesafiosAsync(correo)).Estado);

        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo, Nueva)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CodigoIncorrecto_NoCambiaNada_YCuentaElIntento()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);

        var respuesta = await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, OtroCodigo(codigo), Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("codigo", out _));
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Pendiente, guardado.Estado);
        Assert.Equal(1, guardado.Intentos);
        await Task.Delay(200);
        Assert.DoesNotContain(_fabrica.Correos.Enviados, m => m.Destinatario == correo && m.Tipo == "aviso_password_cambiada");

        // El código correcto sigue sirviendo.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CincoFallos_AnulanElDesafio()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, OtroCodigo(codigo), Ayudas.PasswordValida, Nueva)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Anulado, guardado.Estado);
        Assert.Equal(5, guardado.Intentos);
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);
    }

    [Fact]
    public async Task Paso2_DiezFallosPorHora_BloqueanElCambioDeEsaCuenta_PeroNoSuLogin()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        for (var ronda = 0; ronda < 2; ronda++)
        {
            var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);
            for (var i = 0; i < 5; i++)
            {
                Assert.Equal(HttpStatusCode.BadRequest,
                    (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, OtroCodigo(codigo), Ayudas.PasswordValida, Nueva)).StatusCode);
            }
        }

        var bloqueado = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.Contains(_fabrica.Logs.Mensajes, m => m.Contains("cambio_password_rechazado motivo=tope_fallos_cuenta", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task Paso2_CodigoVencido_A_Los5Minutos_Responde400()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(5));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);
        Assert.Equal(antes.PasswordHash, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task Paso2_CodigoDeUnSoloUso()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);
        var sesionNueva = await Ayudas.ObtenerTokenAsync(_cliente, correo, Nueva);
        var hash = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash;

        // Mismo desafío y código con la sesión nueva: ya se consumió.
        var repetido = await Ayudas.VerificarCambioPasswordAsync(_cliente, sesionNueva, desafio, codigo, Nueva, "OtraClave2028");

        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);
        Assert.Equal(hash, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task Paso2_DesafioDeOtraCuenta_Responde400_YNoTocaNingunaCuenta()
    {
        var (correoA, tokenA) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (correoB, tokenB) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var hashA = (await Ayudas.LeerUsuarioAsync(_fabrica, correoA)).PasswordHash;
        var hashB = (await Ayudas.LeerUsuarioAsync(_fabrica, correoB)).PasswordHash;
        var (desafioA, codigoA) = await Ayudas.IniciarCambioPasswordAsync(_cliente, tokenA, correoA, Ayudas.PasswordValida, Nueva);

        // La cuenta B usa el desafío y el código (correctos) de A con su propia sesión y contraseña.
        var ajeno = await Ayudas.VerificarCambioPasswordAsync(_cliente, tokenB, desafioA, codigoA, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);
        Assert.Equal(hashA, (await Ayudas.LeerUsuarioAsync(_fabrica, correoA)).PasswordHash);
        Assert.Equal(hashB, (await Ayudas.LeerUsuarioAsync(_fabrica, correoB)).PasswordHash);
        var guardado = Assert.Single(await LeerDesafiosAsync(correoA));
        Assert.Equal(EstadosDesafio.Pendiente, guardado.Estado);
        Assert.Equal(0, guardado.Intentos);

        // El dueño todavía puede completar su cambio.
        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, tokenA, desafioA, codigoA, Ayudas.PasswordValida, Nueva)).StatusCode);
    }

    [Fact]
    public async Task Paso2_DesafioDeOtroProposito_Responde400()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var hash = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash;
        var (desafioLogin, codigoLogin, _) = await Ayudas.IniciarLoginAsync(_cliente, correo);

        var respuesta = await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafioLogin, codigoLogin, Ayudas.PasswordValida, Nueva);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(hash, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task Paso2_ContrasenaActualIncorrecta_Responde400_SinGastarElCodigo()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);

        var respuesta = await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, "Incorrecta2026", Nueva);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordActual", out _));
        var guardado = Assert.Single(await LeerDesafiosAsync(correo));
        Assert.Equal(EstadosDesafio.Pendiente, guardado.Estado);
        Assert.Equal(0, guardado.Intentos);

        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);
    }

    [Fact]
    public async Task Paso2_ConfirmacionDistinta_Responde400_SinGastarElCodigo()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);

        var respuesta = await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva, "Diferente2027");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("confirmacion", out _));
        Assert.Equal(0, Assert.Single(await LeerDesafiosAsync(correo)).Intentos);
    }

    [Fact]
    public async Task Paso2_AnulaLosDemasDesafiosPendientes_IncluidoUnPermisoDeRecuperacion()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var permiso = await Ayudas.ObtenerPermisoAsync(_cliente, correo);   // RECUPERAR_PASSWORD verificado
        Assert.Contains(await LeerDesafiosAsync(correo, proposito: null), d => d.Estado == EstadosDesafio.Verificado);

        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.CambiarPasswordConCodigoAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva)).StatusCode);

        Assert.DoesNotContain(await LeerDesafiosAsync(correo, proposito: null),
            d => d.Estado is EstadosDesafio.Pendiente or EstadosDesafio.Verificado);
        // El permiso obtenido antes del cambio ya no sirve para restablecer.
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.RestablecerAsync(_cliente, permiso, "OtraClave2029")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo, Nueva)).StatusCode);
    }

    [Fact]
    public async Task Reenvio_EmiteUnCodigoNuevo_QueReemplazaAlAnterior()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, anterior) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        _fabrica.Reloj.Adelantar(TimeSpan.FromSeconds(61));
        var reenvio = await Ayudas.ReenviarCodigoAsync(_cliente, desafio);

        Assert.Equal(HttpStatusCode.Accepted, reenvio.StatusCode);
        var cuerpo = await reenvio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.CambiarPassword, cuerpo.GetProperty("proposito").GetString());
        var nuevoDesafio = cuerpo.GetProperty("desafio").GetString()!;
        var nuevo = ColaCorreosObservada.Codigo(correo, previos);
        if (nuevo != anterior)
        {
            Assert.Equal(HttpStatusCode.BadRequest,
                (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, nuevoDesafio, anterior, Ayudas.PasswordValida, Nueva)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, nuevoDesafio, nuevo, Ayudas.PasswordValida, Nueva)).StatusCode);
    }

    [Fact]
    public async Task SinSesion_AmbosPasosResponden401()
    {
        var paso1 = await _cliente.PostAsJsonAsync("/api/usuarios/me/password/cambio",
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = Nueva, confirmacion = Nueva });
        var paso2 = await _cliente.PostAsJsonAsync("/api/usuarios/me/password/cambio/verificar",
            new { desafio = "x", codigo = "123456", passwordActual = Ayudas.PasswordValida, passwordNueva = Nueva, confirmacion = Nueva });

        Assert.Equal(HttpStatusCode.Unauthorized, paso1.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, paso2.StatusCode);
    }

    [Fact]
    public async Task LosLogs_NoContienenContrasenasNiCodigos()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var (desafio, codigo) = await Ayudas.IniciarCambioPasswordAsync(_cliente, token, correo, Ayudas.PasswordValida, Nueva);
        await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, OtroCodigo(codigo), Ayudas.PasswordValida, Nueva);
        Assert.Equal(HttpStatusCode.NoContent,
            (await Ayudas.VerificarCambioPasswordAsync(_cliente, token, desafio, codigo, Ayudas.PasswordValida, Nueva)).StatusCode);

        var logs = _fabrica.Logs.Mensajes.ToArray();
        var propios = logs.Where(m => !m.StartsWith("Microsoft.", StringComparison.Ordinal) && !m.StartsWith("System.", StringComparison.Ordinal)).ToArray();

        Assert.Contains(propios, m => m.Contains("Seguridad: cambio_password_codigo_enviado", StringComparison.Ordinal));
        Assert.Contains(propios, m => m.Contains("Seguridad: password_cambiada metodo=codigo", StringComparison.Ordinal));
        Assert.DoesNotContain(propios, m => m.Contains(codigo, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(desafio, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(Nueva, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(Ayudas.PasswordValida, StringComparison.Ordinal));
    }
}
