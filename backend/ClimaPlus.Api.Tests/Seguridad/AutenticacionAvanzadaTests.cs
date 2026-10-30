using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>
/// Fase 8 – OWASP A07 (Identification and Authentication Failures): bloqueo temporal por cuenta y revocación de
/// sesiones al cambiar la contraseña.
/// </summary>
public sealed class AutenticacionAvanzadaTests : IClassFixture<ClimaPlusApiFactory>
{
    private const string NuevaPassword = "OtraClave2026";

    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public AutenticacionAvanzadaTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private async Task FallarAsync(string correo, int veces)
    {
        for (var i = 0; i < veces; i++)
        {
            var respuesta = await Ayudas.LoginAsync(_cliente, correo, "Incorrecta2026");
            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }

    // ------------------------------------------------------------------ Bloqueo por cuenta

    [Fact]
    public async Task CincoFallos_BloqueanLaCuenta_AunqueLaContrasenaSeaCorrecta()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);
        await FallarAsync(correo, 5);

        var respuesta = await Ayudas.LoginAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal("Demasiados intentos", problema.GetProperty("title").GetString());
        Assert.True(int.Parse(respuesta.Headers.GetValues("Retry-After").Single()) > 0);
    }

    [Fact]
    public async Task ElBloqueo_EsPorCuenta_NoAfectaAOtroCorreo()
    {
        var bloqueado = Ayudas.CorreoUnico();
        var otro = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, bloqueado);
        await Ayudas.RegistrarAsync(_cliente, otro);
        await FallarAsync(bloqueado, 5);

        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginAsync(_cliente, otro)).StatusCode);
    }

    [Fact]
    public async Task CorreoInexistente_TambienSeBloquea_ParaNoRevelarQueCuentasExisten()
    {
        var inexistente = Ayudas.CorreoUnico();
        await FallarAsync(inexistente, 5);

        var respuesta = await Ayudas.LoginAsync(_cliente, inexistente, "Incorrecta2026");

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnInicioCorrecto_ReiniciaElContador()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);
        await FallarAsync(correo, 4);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
        await FallarAsync(correo, 4);

        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task ElBloqueo_SeLiberaSolo_PasadosLos15Minutos()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);
        await FallarAsync(correo, 5);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(15));

        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task ElBloqueo_SeRegistraEnLogs_SinLaContrasena()
    {
        var correo = Ayudas.CorreoUnico();
        await FallarAsync(correo, 5);
        await Ayudas.LoginAsync(_cliente, correo, "ClaveQueNoDebeVerse2026");

        var logs = string.Join("\n", _fabrica.Logs.Mensajes);
        Assert.Contains("login_bloqueado", logs);
        Assert.DoesNotContain("ClaveQueNoDebeVerse2026", logs);
        Assert.DoesNotContain(correo, logs);   // el correo se enmascara
    }

    // ------------------------------------------------------------------ Revocación de sesiones

    [Fact]
    public async Task CambiarContrasena_RevocaTodasLasSesionesAbiertas()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);
        var sesionA = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        var sesionB = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", sesionB))).StatusCode);

        var cambio = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Put, "/api/usuarios/me/password", sesionA,
            new { passwordActual = Ayudas.PasswordValida, passwordNueva = NuevaPassword }));
        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", sesionA))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", sesionB))).StatusCode);
        Assert.Contains("motivo=sesion_revocada", string.Join("\n", _fabrica.Logs.Mensajes));

        // La contraseña nueva funciona y su token es válido; la anterior ya no.
        var nueva = await Ayudas.ObtenerTokenAsync(_cliente, correo, NuevaPassword);
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", nueva))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task TokenFirmadoConLaClaveCorrecta_PeroSinSello_SeRechaza()
    {
        // Aunque alguien obtuviera la clave del JWT, sin el sello derivado de la contraseña el token no sirve.
        var (correo, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var usuario = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var sinSello = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ClimaPlus.Api",
            Audience = "ClimaPlus.Frontend",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", usuario.IdUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("role", Roles.Usuario),
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ClimaPlusApiFactory.ClaveJwtPruebas)), SecurityAlgorithms.HmacSha256),
        });

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", sinSello));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElToken_NoContieneDatosPersonales()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(token);

        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains(correo, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(new[] { "aud", "exp", "iat", "iss", "jti", "nbf", "role", "sub", "sv" }, jwt.Claims.Select(c => c.Type).Distinct().OrderBy(t => t).ToArray());
    }
}

/// <summary>Fase 8 – OWASP A04: límite general por IP para toda la API (también las rutas con sesión).</summary>
public sealed class LimiteGeneralTests : IClassFixture<ClimaPlusApiFactoryLimiteGeneral>
{
    private readonly HttpClient _cliente;

    public LimiteGeneralTests(ClimaPlusApiFactoryLimiteGeneral fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task RutaConSesion_SinLimitePropio_RespondeTambien429AlSuperarElLimiteGeneral()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);   // 2 peticiones

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < ClimaPlusApiFactoryLimiteGeneral.Limite; i++)
        {
            codigos.Add((await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/favoritos", token))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, codigos[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, codigos);
        Assert.Equal(ClimaPlusApiFactoryLimiteGeneral.Limite - 2, codigos.Count(c => c == HttpStatusCode.OK));
    }
}
