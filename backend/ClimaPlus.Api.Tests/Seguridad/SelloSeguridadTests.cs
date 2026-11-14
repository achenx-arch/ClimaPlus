using System.Net;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>
/// Migración 004: el sello "sv" del JWT sale de usuario.sello_seguridad. Renovarlo (al restablecer la contraseña o
/// vincular Google) revoca todas las sesiones. Las cuentas solo de Google no tienen contraseña local.
/// </summary>
public sealed class SelloSeguridadTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public SelloSeguridadTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private Task<HttpResponseMessage> PerfilAsync(string token) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token));

    [Fact]
    public async Task RenovarElSello_RevocaTodasLasSesiones()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        Assert.Equal(HttpStatusCode.OK, (await PerfilAsync(token)).StatusCode);

        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.SelloSeguridad = Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Unauthorized, (await PerfilAsync(token)).StatusCode);
        Assert.Contains("motivo=sesion_revocada", string.Join("\n", _fabrica.Logs.Mensajes));
    }

    [Fact]
    public async Task ElClaimSv_NoExponeElSelloNiElHash()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var usuario = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var sv = new JsonWebToken(token).GetClaim(ClaimsClimaPlus.Sello).Value;

        Assert.Matches("^[0-9a-f]{32}$", sv);
        Assert.DoesNotContain(usuario.SelloSeguridad.ToString("N"), token);
        Assert.DoesNotContain(usuario.PasswordHash!, token);
    }

    [Fact]
    public async Task CambiarLaContrasena_RenuevaElSello()
    {
        var (correo, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var antes = (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad;

        var cambio = await Ayudas.CambiarPasswordConCodigoAsync(_cliente, token, correo, Ayudas.PasswordValida, "OtraClave2026");

        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        Assert.NotEqual(antes, (await Ayudas.LeerUsuarioAsync(_fabrica, correo)).SelloSeguridad);
    }

    private async Task<(string Correo, string Token)> CrearCuentaSoloGoogleAsync()
    {
        var correo = Ayudas.CorreoUnico();
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var usuario = new Usuario
        {
            Nombre = "Cuenta Google",
            Correo = correo,
            PasswordHash = null,
            CorreoVerificado = true,
            FechaCreacion = DateTime.UtcNow,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var token = alcance.ServiceProvider.GetRequiredService<JwtTokenService>().Crear(usuario).Token;
        return (correo, token);
    }

    [Fact]
    public async Task CuentaSoloGoogle_ElLoginConContrasena_RespondeComoCredencialesInvalidas()
    {
        var (correo, _) = await CrearCuentaSoloGoogleAsync();
        var otroCorreo = Ayudas.CorreoUnico();

        var sinPassword = await Ayudas.LoginAsync(_cliente, correo);
        var inexistente = await Ayudas.LoginAsync(_cliente, otroCorreo);

        // Misma respuesta que un correo que no existe: no revela que la cuenta es de Google.
        Assert.Equal(HttpStatusCode.Unauthorized, sinPassword.StatusCode);
        Assert.Equal(
            (await Ayudas.LeerProblemaAsync(inexistente)).GetProperty("detail").GetString(),
            (await Ayudas.LeerProblemaAsync(sinPassword)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CuentaSoloGoogle_NoPuedeCambiarUnaContrasenaQueNoTiene()
    {
        var (_, token) = await CrearCuentaSoloGoogleAsync();
        Assert.Equal(HttpStatusCode.OK, (await PerfilAsync(token)).StatusCode);

        var cambio = await Ayudas.SolicitarCambioPasswordAsync(_cliente, token, "Cualquiera2026", "OtraClave2026");

        Assert.Equal(HttpStatusCode.Conflict, cambio.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(cambio);
        Assert.True(problema.GetProperty("errors").TryGetProperty("passwordActual", out _));
    }
}
