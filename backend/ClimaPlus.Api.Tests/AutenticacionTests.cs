using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Tests;

/// <summary>Registro, inicio de sesión, JWT y autorización por rol.</summary>
public sealed class AutenticacionTests : IClassFixture<ClimaPlusApiFactory>
{
    private readonly ClimaPlusApiFactory _fabrica;
    private readonly HttpClient _cliente;

    public AutenticacionTests(ClimaPlusApiFactory fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task Registro_valido_devuelve_202_con_desafio_y_crea_cuenta_USUARIO_sin_verificar()
    {
        var correo = Ayudas.CorreoUnico();

        var respuesta = await Ayudas.RegistrarAsync(_cliente, correo.ToUpperInvariant(), nombre: "  Ana   López  ");

        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(correo, texto, StringComparison.OrdinalIgnoreCase);      // el destino va enmascarado

        var cuerpo = JsonSerializer.Deserialize<JsonElement>(texto);
        Assert.False(cuerpo.TryGetProperty("token", out _));                           // no hay sesión
        Assert.Equal(PropositosDesafio.VerificarCorreo, cuerpo.GetProperty("proposito").GetString());
        Assert.Equal(string.Concat(correo.AsSpan(0, 1), "***", correo.AsSpan(correo.IndexOf('@'))),
            cuerpo.GetProperty("destino").GetString());
        Assert.Equal(60, cuerpo.GetProperty("reenviarEn").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(cuerpo.GetProperty("desafio").GetString()));
        var vigencia = cuerpo.GetProperty("expiraEn").GetDateTime().ToUniversalTime() - _fabrica.Reloj.GetUtcNow().UtcDateTime;
        Assert.InRange(vigencia.TotalMinutes, 9.9, 10.1);

        var guardado = await Ayudas.LeerUsuarioAsync(_fabrica, correo);                // normalizado a minúsculas
        Assert.Equal(Roles.Usuario, guardado.Rol);
        Assert.True(guardado.Activo);
        Assert.False(guardado.CorreoVerificado);
        Assert.Equal("Ana López", guardado.Nombre);                                    // espacios normalizados
        Assert.NotEqual(Ayudas.PasswordValida, guardado.PasswordHash);                 // nunca en texto plano
        Assert.False(string.IsNullOrWhiteSpace(guardado.PasswordHash));

        var correoEnviado = await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "codigo_verificar_correo");
        Assert.Matches("^[0-9]{6}$", EnviadorCorreoFalso.ExtraerCodigo(correoEnviado));
    }

    [Fact]
    public async Task Registro_con_correo_duplicado_en_otras_mayusculas_responde_igual_y_solo_avisa_al_dueno()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);
        var original = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var respuesta = await Ayudas.RegistrarAsync(_cliente, correo.ToUpperInvariant(), "OtraClave2026", "Intruso");

        // Misma forma que un registro nuevo: no revela que el correo existe.
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(PropositosDesafio.VerificarCorreo, cuerpo.GetProperty("proposito").GetString());
        Assert.True(cuerpo.TryGetProperty("destino", out _));
        Assert.True(cuerpo.TryGetProperty("expiraEn", out _));
        Assert.True(cuerpo.TryGetProperty("reenviarEn", out _));
        var desafio = cuerpo.GetProperty("desafio").GetString()!;

        // Al dueño le llega un aviso, nunca un código.
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_registro_existente");
        Assert.Single(_fabrica.Correos.CodigosPara(correo));

        // El señuelo no verifica nada ni cambia la cuenta.
        var codigoReal = await _fabrica.Correos.EsperarCodigoAsync(correo);
        Assert.Equal(HttpStatusCode.BadRequest, (await Ayudas.VerificarCorreoAsync(_cliente, desafio, codigoReal)).StatusCode);
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(original.PasswordHash, despues.PasswordHash);
        Assert.Equal(original.Nombre, despues.Nombre);
        Assert.Equal(original.SelloSeguridad, despues.SelloSeguridad);
        Assert.True(despues.CorreoVerificado);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo, "OtraClave2026")).StatusCode);
    }

    [Fact]
    public async Task Registro_no_permite_elegir_rol()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/auth/register", new
        {
            nombre = "Intruso",
            correo = Ayudas.CorreoUnico(),
            password = Ayudas.PasswordValida,
            rol = Roles.Administrador,
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("12345678")]        // sin letras
    [InlineData("abcdefgh")]        // sin números
    [InlineData("Ab1")]             // muy corta
    public async Task Registro_con_password_debil_devuelve_400(string password)
    {
        var respuesta = await Ayudas.RegistrarAsync(_cliente, Ayudas.CorreoUnico(), password);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.True(problema.GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_con_password_incorrecta_devuelve_401_generico()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);

        var conPasswordMala = await Ayudas.LoginAsync(_cliente, correo, "OtraClave2026");
        var conCorreoInexistente = await Ayudas.LoginAsync(_cliente, Ayudas.CorreoUnico());

        Assert.Equal(HttpStatusCode.Unauthorized, conPasswordMala.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, conCorreoInexistente.StatusCode);

        // Mismo mensaje en ambos casos: no revela si el correo existe.
        var detalle1 = (await Ayudas.LeerProblemaAsync(conPasswordMala)).GetProperty("detail").GetString();
        var detalle2 = (await Ayudas.LeerProblemaAsync(conCorreoInexistente)).GetProperty("detail").GetString();
        Assert.Equal(detalle1, detalle2);
    }

    [Fact]
    public async Task Login_exitoso_devuelve_token_que_abre_auth_me()
    {
        var correo = Ayudas.CorreoUnico();
        await Ayudas.RegistrarAsync(_cliente, correo);

        // Dos pasos: contraseña (202, sin token) y código del correo (200 con el JWT).
        var respuesta = await Ayudas.IniciarSesionCompletaAsync(_cliente, correo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", cuerpo.GetProperty("tipoToken").GetString());
        Assert.True(cuerpo.GetProperty("expiraEn").GetDateTime() > DateTime.UtcNow);
        Assert.False(cuerpo.GetProperty("usuario").TryGetProperty("passwordHash", out _));

        var token = cuerpo.GetProperty("token").GetString()!;
        var me = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", token));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var usuario = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(correo, usuario.GetProperty("correo").GetString());
    }

    [Fact]
    public async Task Endpoint_privado_sin_token_devuelve_401()
    {
        var respuesta = await _cliente.GetAsync("/api/usuarios/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var problema = await Ayudas.LeerProblemaAsync(respuesta);
        Assert.Equal(401, problema.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("no-es-un-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.firma-falsa")]
    public async Task Token_malformado_devuelve_401(string token)
    {
        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", token));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Token_firmado_con_otra_clave_devuelve_401()
    {
        var (correo, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        var usuario = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        // Mismo emisor, audiencia y claims, pero un rol ADMINISTRADOR forjado con otra clave.
        var forjado = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ClimaPlus.Api",
            Audience = "ClimaPlus.Frontend",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", usuario.IdUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim("role", Roles.Administrador),
            }),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("otra-clave-que-no-es-la-del-servidor-1234567890")),
                SecurityAlgorithms.HmacSha256),
        });

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", forjado));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Usuario_sin_rol_de_administrador_recibe_403()
    {
        var (_, token) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", token));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Administrador_accede_al_endpoint_de_administrador()
    {
        var correoAdmin = await Ayudas.CrearAdministradorAsync(_fabrica);
        var token = await Ayudas.ObtenerTokenAsync(_cliente, correoAdmin, Ayudas.PasswordValida);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", token));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Usuario_desactivado_no_inicia_sesion_y_su_token_previo_deja_de_valer()
    {
        var (correo, tokenPrevio) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        var login = await Ayudas.LoginAsync(_cliente, correo);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);

        var conTokenPrevio = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", tokenPrevio));
        Assert.Equal(HttpStatusCode.Unauthorized, conTokenPrevio.StatusCode);
    }

    [Fact]
    public async Task Cambio_de_rol_invalida_el_token_previo()
    {
        var (correo, tokenPrevio) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);

        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Rol = Roles.Administrador);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/usuarios/me", tokenPrevio));
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
