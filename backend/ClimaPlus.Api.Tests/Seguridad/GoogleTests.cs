using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.Tests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ClimaPlus.Api.Tests.Seguridad;

/// <summary>
/// Autenticación segura, fase 5: "Continuar con Google" (Google Identity Services + validación del ID token en la
/// API), vinculación y desvinculación. Google se sustituye por <see cref="GoogleFalso"/>: tokens firmados con una
/// clave de prueba que la API valida con su validador real. Sin Internet ni Google Cloud.
/// </summary>
public sealed class GoogleTests : IClassFixture<GoogleTests.Fabrica>
{
    /// <summary>Fábrica propia: el reloj de esta clase avanza sin afectar a otras clases de pruebas.</summary>
    public sealed class Fabrica : ClimaPlusApiFactory
    {
    }

    private readonly Fabrica _fabrica;
    private readonly HttpClient _cliente;

    public GoogleTests(Fabrica fabrica)
    {
        _fabrica = fabrica;
        _cliente = fabrica.CreateClient();
    }

    private DateTime Ahora => _fabrica.Reloj.GetUtcNow().UtcDateTime;

    private string Token(string sujeto, string correo, string? nonce, bool verificado = true) =>
        GoogleFalso.Token(sujeto, correo, nonce, verificado, ahora: Ahora);

    private static string CorreoGmail() => $"g{Guid.NewGuid():N}@gmail.com";

    private async Task<HttpResponseMessage> EntrarConGoogleAsync(string sujeto, string correo, bool verificado = true) =>
        await Ayudas.LoginGoogleAsync(_cliente, Token(sujeto, correo, await Ayudas.NonceGoogleAsync(_cliente), verificado));

    private async Task<(long IdUsuario, string Token)> LeerSesionAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (cuerpo.GetProperty("usuario").GetProperty("idUsuario").GetInt64(), cuerpo.GetProperty("token").GetString()!);
    }

    private async Task<List<IdentidadExterna>> IdentidadesAsync(string? sujeto = null, long? idUsuario = null)
    {
        using var alcance = _fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.IdentidadesExternas.AsNoTracking()
            .Where(i => (sujeto == null || i.Sujeto == sujeto) && (idUsuario == null || i.IdUsuario == idUsuario))
            .ToListAsync();
    }

    private async Task<int> UsuariosConCorreoAsync(string correo)
    {
        using var alcance = _fabrica.Services.CreateScope();
        return await alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>().Usuarios.CountAsync(u => u.Correo == correo);
    }

    private Task<HttpResponseMessage> MeAsync(string token) => _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/auth/me", token));

    private async Task<string> UsuarioLocalConSesionAsync(string? correo = null)
    {
        correo ??= Ayudas.CorreoUnico();
        await Ayudas.RegistrarYVerificarAsync(_cliente, correo);
        return await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
    }

    private async Task<HttpResponseMessage> VincularPaso1Async(string token, string sujeto, string correoGoogle, string? password = null, string uso = "vincular")
    {
        var nonce = await Ayudas.NonceGoogleAsync(_cliente, uso, uso == "vincular" ? token : null);
        return await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/google/vincular", token,
            new { credencial = Token(sujeto, correoGoogle, nonce), passwordActual = password ?? Ayudas.PasswordValida }));
    }

    private Task<HttpResponseMessage> VincularPaso2Async(string token, string desafio, string codigo) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/google/vincular/verificar", token, new { desafio, codigo }));

    private Task<HttpResponseMessage> DesvincularAsync(string token, string password) =>
        _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/google/desvincular", token, new { passwordActual = password }));

    /// <summary>Vinculación completa; devuelve el token nuevo.</summary>
    private async Task<string> VincularAsync(string correo, string token, string sujeto, string correoGoogle)
    {
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var desafio = await Ayudas.LeerDesafioAsync(await VincularPaso1Async(token, sujeto, correoGoogle));
        var (_, nuevo) = await LeerSesionAsync(await VincularPaso2Async(token, desafio, ColaCorreosObservada.Codigo(correo, previos)));
        return nuevo;
    }

    // ------------------------------------------------------------------ Iniciar (nonce)

    [Fact]
    public async Task Iniciar_DevuelveClientIdYNonceDeUnSoloUso()
    {
        var respuesta = await _cliente.GetAsync("/api/auth/google/iniciar");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "clientId", "nonce", "expiraEn" }, cuerpo.EnumerateObject().Select(p => p.Name));
        Assert.Equal(GoogleFalso.ClientId, cuerpo.GetProperty("clientId").GetString());
        Assert.True(cuerpo.GetProperty("nonce").GetString()!.Length >= 43);   // 256 bits en base64url
        var otro = await Ayudas.NonceGoogleAsync(_cliente);
        Assert.NotEqual(cuerpo.GetProperty("nonce").GetString(), otro);
    }

    [Fact]
    public async Task Iniciar_UsoInvalido_Responde400_YVincularSinSesion401()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _cliente.GetAsync("/api/auth/google/iniciar?uso=admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.GetAsync("/api/auth/google/iniciar?uso=vincular")).StatusCode);
    }

    // ------------------------------------------------------------------ Primer inicio e inicio posterior

    [Fact]
    public async Task PrimerInicio_CreaCuentaUsuario_Verificada_SinContrasena_YEmiteSesionSinCodigo()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();

        var respuesta = await Ayudas.LoginGoogleAsync(_cliente, Token(sujeto, correo.ToUpperInvariant(), await Ayudas.NonceGoogleAsync(_cliente)));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", cuerpo.GetProperty("tipoToken").GetString());
        var usuario = cuerpo.GetProperty("usuario");
        Assert.Equal(correo, usuario.GetProperty("correo").GetString());           // en minúsculas
        Assert.Equal(Roles.Usuario, usuario.GetProperty("rol").GetString());       // nunca ADMINISTRADOR
        Assert.True(usuario.GetProperty("correoVerificado").GetBoolean());
        Assert.False(usuario.GetProperty("tienePassword").GetBoolean());
        Assert.True(usuario.GetProperty("tieneGoogle").GetBoolean());
        Assert.Equal("Ana Google", usuario.GetProperty("nombre").GetString());
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(cuerpo.GetProperty("token").GetString()!)).StatusCode);

        var identidad = Assert.Single(await IdentidadesAsync(sujeto));
        Assert.Equal(ProveedoresIdentidad.Google, identidad.Proveedor);
        Assert.Equal(correo, identidad.CorreoProveedor);
        Assert.Empty(ColaCorreosObservada.CodigosPara(correo));                    // Google no exige código extra
        Assert.Null((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task InicioPosterior_EntraALaMismaCuenta_PorSub_AunqueGoogleCambieElCorreo()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        var (id, _) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correo));

        var correoNuevo = CorreoGmail();
        var (idDespues, _) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correoNuevo));

        Assert.Equal(id, idDespues);
        Assert.Equal(1, await UsuariosConCorreoAsync(correo));
        Assert.Equal(0, await UsuariosConCorreoAsync(correoNuevo));                 // el correo de la cuenta no cambia
        Assert.Equal(correoNuevo, Assert.Single(await IdentidadesAsync(sujeto)).CorreoProveedor);   // solo la referencia
    }

    [Fact]
    public async Task MismoCorreoConOtroSub_NoEntraALaCuentaDelPrimero()
    {
        var correo = CorreoGmail();
        await LeerSesionAsync(await EntrarConGoogleAsync(GoogleFalso.NuevoSujeto(), correo));

        var otro = await EntrarConGoogleAsync(GoogleFalso.NuevoSujeto(), correo);

        Assert.Equal(HttpStatusCode.Conflict, otro.StatusCode);
        Assert.Equal(1, await UsuariosConCorreoAsync(correo));
    }

    [Fact]
    public async Task CuentaDesactivada_NoEntraConGoogle()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correo));
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.Activo = false);

        var respuesta = await EntrarConGoogleAsync(sujeto, correo);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.DoesNotContain("token", await respuesta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Administrador_ConGoogleVinculado_ConservaSuRol()
    {
        var correo = await Ayudas.CrearAdministradorAsync(_fabrica);
        var token = await Ayudas.ObtenerTokenAsync(_cliente, correo, Ayudas.PasswordValida);
        var sujeto = GoogleFalso.NuevoSujeto();
        await VincularAsync(correo, token, sujeto, CorreoGmail());

        var (_, sesion) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, CorreoGmail()));

        var me = await (await MeAsync(sesion)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Roles.Administrador, me.GetProperty("rol").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Get, "/api/pruebas/solo-admin", sesion))).StatusCode);
    }

    [Fact]
    public async Task DosPrimerosIniciosSimultaneos_ConElMismoSub_CreanUnaSolaCuenta()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        var tokens = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            tokens.Add(Token(sujeto, correo, await Ayudas.NonceGoogleAsync(_cliente)));
        }

        var respuestas = await Task.WhenAll(tokens.Select(t => Ayudas.LoginGoogleAsync(_cliente, t)));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var ids = new List<long>();
        foreach (var r in respuestas)
        {
            ids.Add((await LeerSesionAsync(r)).IdUsuario);
        }

        Assert.Single(ids.Distinct());
        Assert.Equal(1, await UsuariosConCorreoAsync(correo));
        Assert.Single(await IdentidadesAsync(sujeto));
    }

    // ------------------------------------------------------------------ Tokens inválidos

    public static TheoryData<string> TokensInvalidos => new()
    {
        "vencido", "audiencia", "emisor", "firma", "algoritmo", "azp", "sin_sub", "basura", "sin_nonce", "nonce_desconocido",
    };

    [Theory]
    [MemberData(nameof(TokensInvalidos))]
    public async Task TokenInvalido_Responde401_YNoCreaNada(string caso)
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        var nonce = await Ayudas.NonceGoogleAsync(_cliente);
        var token = caso switch
        {
            "vencido" => GoogleFalso.Token(sujeto, correo, nonce, ahora: Ahora.AddHours(-3), expira: Ahora.AddMinutes(-5)),
            "audiencia" => GoogleFalso.Token(sujeto, correo, nonce, audiencia: "otra-app.apps.googleusercontent.com", ahora: Ahora),
            "emisor" => GoogleFalso.Token(sujeto, correo, nonce, emisor: "https://accounts.google.evil.example", ahora: Ahora),
            "firma" => GoogleFalso.Token(sujeto, correo, nonce, firmaFalsa: true, ahora: Ahora),
            "algoritmo" => GoogleFalso.Token(sujeto, correo, nonce, algoritmo: SecurityAlgorithms.RsaSha512, ahora: Ahora),
            "azp" => GoogleFalso.Token(sujeto, correo, nonce, azp: "otra-app.apps.googleusercontent.com", ahora: Ahora),
            "sin_sub" => GoogleFalso.Token(" ", correo, nonce, ahora: Ahora),
            "sin_nonce" => GoogleFalso.Token(sujeto, correo, null, ahora: Ahora),
            "nonce_desconocido" => GoogleFalso.Token(sujeto, correo, "nonce-inventado-por-el-atacante", ahora: Ahora),
            _ => "esto.no.es-un-id-token",
        };

        var respuesta = await Ayudas.LoginGoogleAsync(_cliente, token);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.DoesNotContain("\"token\"", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, await UsuariosConCorreoAsync(correo));
        Assert.Empty(await IdentidadesAsync(sujeto));
    }

    [Fact]
    public async Task TokenReutilizado_ElNonceSoloSirveUnaVez()
    {
        var correo = CorreoGmail();
        var token = Token(GoogleFalso.NuevoSujeto(), correo, await Ayudas.NonceGoogleAsync(_cliente));
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.LoginGoogleAsync(_cliente, token)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginGoogleAsync(_cliente, token)).StatusCode);   // replay
    }

    [Fact]
    public async Task NonceVencido_Responde401()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var nonce = await Ayudas.NonceGoogleAsync(_cliente);

        _fabrica.Reloj.Adelantar(TimeSpan.FromMinutes(11));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginGoogleAsync(_cliente, Token(sujeto, CorreoGmail(), nonce))).StatusCode);
    }

    [Fact]
    public async Task NonceDeVinculacion_NoSirveParaIniciarSesion()
    {
        var token = await UsuarioLocalConSesionAsync();
        var nonce = await Ayudas.NonceGoogleAsync(_cliente, "vincular", token);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Ayudas.LoginGoogleAsync(_cliente, Token(GoogleFalso.NuevoSujeto(), CorreoGmail(), nonce))).StatusCode);
    }

    [Fact]
    public async Task CorreoNoVerificadoPorGoogle_NoCreaCuenta()
    {
        var correo = CorreoGmail();

        var respuesta = await EntrarConGoogleAsync(GoogleFalso.NuevoSujeto(), correo, verificado: false);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("credencial", out _));
        Assert.Equal(0, await UsuariosConCorreoAsync(correo));
    }

    [Fact]
    public async Task CredencialAusente_Responde400()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/auth/google", new { credencial = "" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ------------------------------------------------------------------ Correo de una cuenta local

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorreoDeCuentaLocal_NoSeVinculaAutomaticamente_NiSeCreaOtraCuenta(bool localVerificada)
    {
        var correo = Ayudas.CorreoUnico();
        if (localVerificada)
        {
            await Ayudas.RegistrarYVerificarAsync(_cliente, correo);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Accepted, (await Ayudas.RegistrarAsync(_cliente, correo)).StatusCode);
        }

        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        var sujeto = GoogleFalso.NuevoSujeto();

        var respuesta = await EntrarConGoogleAsync(sujeto, correo);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.DoesNotContain("\"token\"", await respuesta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await IdentidadesAsync(sujeto));
        Assert.Equal(1, await UsuariosConCorreoAsync(correo));
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.SelloSeguridad, despues.SelloSeguridad);
        Assert.Equal(antes.CorreoVerificado, despues.CorreoVerificado);

        // El dueño sigue entrando con su contraseña (en dos pasos).
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    // ------------------------------------------------------------------ Vincular

    [Fact]
    public async Task Vincular_ConContrasenaYCodigo_CreaLaIdentidad_CierraLasDemasSesiones_YPermiteEntrarConGoogle()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        var sujeto = GoogleFalso.NuevoSujeto();
        var correoGoogle = CorreoGmail();
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        var paso1 = await VincularPaso1Async(token, sujeto, correoGoogle);

        var desafio = await Ayudas.LeerDesafioAsync(paso1);
        var codigo = ColaCorreosObservada.Codigo(correo, previos);                  // el código va al correo de ClimaPlus
        Assert.Empty(await IdentidadesAsync(sujeto));                               // aún no se vincula
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);

        var paso2 = await VincularPaso2Async(token, desafio, codigo);

        var (id, nuevoToken) = await LeerSesionAsync(paso2);
        var identidad = Assert.Single(await IdentidadesAsync(sujeto));
        Assert.Equal(id, identidad.IdUsuario);
        Assert.Equal(correoGoogle, identidad.CorreoProveedor);
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(token)).StatusCode);      // sesiones anteriores cerradas
        var me = await (await MeAsync(nuevoToken)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(me.GetProperty("tieneGoogle").GetBoolean());
        Assert.True(me.GetProperty("tienePassword").GetBoolean());
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_google_vinculado");

        // Google entra a esta misma cuenta; la contraseña sigue funcionando.
        Assert.Equal(id, (await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correoGoogle))).IdUsuario);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task Vincular_SinSesion_Responde401()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/api/usuarios/me/google/vincular",
            new { credencial = "x", passwordActual = Ayudas.PasswordValida });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Vincular_ContrasenaIncorrecta_Responde400_SinEnviarCodigo()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;

        var respuesta = await VincularPaso1Async(token, GoogleFalso.NuevoSujeto(), CorreoGmail(), password: "Incorrecta2026");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(respuesta)).GetProperty("errors").TryGetProperty("passwordActual", out _));
        Assert.Equal(previos, ColaCorreosObservada.CodigosPara(correo).Count);
    }

    [Fact]
    public async Task Vincular_ConNonceDeLogin_OTokenInvalido_Responde400()
    {
        var token = await UsuarioLocalConSesionAsync();

        var conNonceDeLogin = await VincularPaso1Async(token, GoogleFalso.NuevoSujeto(), CorreoGmail(), uso: "login");

        Assert.Equal(HttpStatusCode.BadRequest, conNonceDeLogin.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(conNonceDeLogin)).GetProperty("errors").TryGetProperty("credencial", out _));
    }

    [Fact]
    public async Task Vincular_NonceDeOtroUsuario_NoSirve()
    {
        var tokenA = await UsuarioLocalConSesionAsync();
        var tokenB = await UsuarioLocalConSesionAsync();
        var nonceDeA = await Ayudas.NonceGoogleAsync(_cliente, "vincular", tokenA);

        var respuesta = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/google/vincular", tokenB,
            new { credencial = Token(GoogleFalso.NuevoSujeto(), CorreoGmail(), nonceDeA), passwordActual = Ayudas.PasswordValida }));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Vincular_GoogleYaVinculadoAOtraCuenta_Responde409()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, CorreoGmail()));   // ese Google ya tiene su cuenta
        var token = await UsuarioLocalConSesionAsync();

        var respuesta = await VincularPaso1Async(token, sujeto, CorreoGmail());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Single(await IdentidadesAsync(sujeto));
    }

    [Fact]
    public async Task Vincular_SubVinculadoPorOtraCuentaEntreLosDosPasos_Responde409()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        var sujeto = GoogleFalso.NuevoSujeto();
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var desafio = await Ayudas.LeerDesafioAsync(await VincularPaso1Async(token, sujeto, CorreoGmail()));

        // Mientras tanto, ese mismo Google crea su propia cuenta.
        await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, CorreoGmail()));
        var respuesta = await VincularPaso2Async(token, desafio, ColaCorreosObservada.Codigo(correo, previos));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var identidad = Assert.Single(await IdentidadesAsync(sujeto));
        Assert.NotEqual((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).IdUsuario, identidad.IdUsuario);
    }

    [Fact]
    public async Task Vincular_CuentaQueYaTieneGoogle_Responde409()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        token = await VincularAsync(correo, token, GoogleFalso.NuevoSujeto(), CorreoGmail());

        var respuesta = await VincularPaso1Async(token, GoogleFalso.NuevoSujeto(), CorreoGmail());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Vincular_CuentaSinVerificar_Responde409()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        await Ayudas.ModificarUsuarioAsync(_fabrica, correo, u => u.CorreoVerificado = false);

        var respuesta = await VincularPaso1Async(token, GoogleFalso.NuevoSujeto(), CorreoGmail());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task VincularPaso2_CodigoIncorrecto_DesafioDeOtroUsuario_OOtroProposito_NoVinculan()
    {
        var correoA = Ayudas.CorreoUnico();
        var tokenA = await UsuarioLocalConSesionAsync(correoA);
        var sujeto = GoogleFalso.NuevoSujeto();
        var previos = ColaCorreosObservada.CodigosPara(correoA).Count;
        var desafio = await Ayudas.LeerDesafioAsync(await VincularPaso1Async(tokenA, sujeto, CorreoGmail()));
        var codigo = ColaCorreosObservada.Codigo(correoA, previos);
        var tokenB = await UsuarioLocalConSesionAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await VincularPaso2Async(tokenA, desafio, codigo == "000000" ? "000001" : "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await VincularPaso2Async(tokenB, desafio, codigo)).StatusCode);   // de otra sesión
        var (login, codigoLogin, _) = await Ayudas.IniciarLoginAsync(_cliente, correoA);
        Assert.Equal(HttpStatusCode.BadRequest, (await VincularPaso2Async(tokenA, login, codigoLogin)).StatusCode); // otro propósito
        Assert.Empty(await IdentidadesAsync(sujeto));

        Assert.Equal(HttpStatusCode.OK, (await VincularPaso2Async(tokenA, desafio, codigo)).StatusCode);
    }

    // ------------------------------------------------------------------ Desvincular

    [Fact]
    public async Task Desvincular_ConContrasena_QuitaGoogle_ConservaLaCuenta_YCierraSesiones()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        var sujeto = GoogleFalso.NuevoSujeto();
        var correoGoogle = CorreoGmail();
        token = await VincularAsync(correo, token, sujeto, correoGoogle);
        var (_, sesionGoogle) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correoGoogle));
        var antes = await Ayudas.LeerUsuarioAsync(_fabrica, correo);

        var respuesta = await DesvincularAsync(token, Ayudas.PasswordValida);

        var (id, nuevo) = await LeerSesionAsync(respuesta);
        Assert.Equal(antes.IdUsuario, id);
        Assert.Empty(await IdentidadesAsync(sujeto));
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(sesionGoogle)).StatusCode);   // revocada
        Assert.Equal(HttpStatusCode.Unauthorized, (await MeAsync(token)).StatusCode);
        var me = await (await MeAsync(nuevo)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(me.GetProperty("tieneGoogle").GetBoolean());
        var despues = await Ayudas.LeerUsuarioAsync(_fabrica, correo);
        Assert.Equal(antes.PasswordHash, despues.PasswordHash);
        Assert.Equal(antes.Nombre, despues.Nombre);
        Assert.Equal(antes.Rol, despues.Rol);
        await _fabrica.Correos.EsperarAsync(m => m.Destinatario == correo && m.Tipo == "aviso_google_desvinculado");

        // Ese Google ya no entra; como su correo no es el de la cuenta, crearía una cuenta nueva aparte.
        var (otroId, _) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correoGoogle));
        Assert.NotEqual(id, otroId);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }

    [Fact]
    public async Task Desvincular_CuentaSoloGoogle_Responde409_YNoSeQuedaSinAcceso()
    {
        var (correo, token, sujeto) = await Ayudas.CrearCuentaSoloGoogleAsync(_fabrica);

        var respuesta = await DesvincularAsync(token, "CualquierCosa2026");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Single(await IdentidadesAsync(sujeto));
        Assert.Equal(HttpStatusCode.OK, (await MeAsync(token)).StatusCode);
        Assert.Null((await Ayudas.LeerUsuarioAsync(_fabrica, correo)).PasswordHash);
    }

    [Fact]
    public async Task Desvincular_ContrasenaIncorrecta_SinGoogle_OSinSesion()
    {
        var correo = Ayudas.CorreoUnico();
        var token = await UsuarioLocalConSesionAsync(correo);
        Assert.Equal(HttpStatusCode.NotFound, (await DesvincularAsync(token, Ayudas.PasswordValida)).StatusCode);

        var sujeto = GoogleFalso.NuevoSujeto();
        token = await VincularAsync(correo, token, sujeto, CorreoGmail());
        var incorrecta = await DesvincularAsync(token, "Incorrecta2026");
        Assert.Equal(HttpStatusCode.BadRequest, incorrecta.StatusCode);
        Assert.True((await Ayudas.LeerProblemaAsync(incorrecta)).GetProperty("errors").TryGetProperty("passwordActual", out _));
        Assert.Single(await IdentidadesAsync(sujeto));

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _cliente.PostAsJsonAsync("/api/usuarios/me/google/desvincular", new { passwordActual = Ayudas.PasswordValida })).StatusCode);
    }

    // ------------------------------------------------------------------ Regresión y logs

    [Fact]
    public async Task CuentaGoogle_NoEntraPorElLoginLocal_PeroPuedeCrearContrasenaConCodigo()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        var (_, token) = await LeerSesionAsync(await EntrarConGoogleAsync(sujeto, correo));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Ayudas.LoginAsync(_cliente, correo, Ayudas.PasswordValida)).StatusCode);
        var establecer = await _cliente.SendAsync(Ayudas.ConToken(HttpMethod.Post, "/api/usuarios/me/password/establecer", token));
        Assert.Equal(HttpStatusCode.Accepted, establecer.StatusCode);
    }

    [Fact]
    public async Task LosLogs_NoContienenIdTokensNiJwtNiNonces()
    {
        var sujeto = GoogleFalso.NuevoSujeto();
        var correo = CorreoGmail();
        var nonce = await Ayudas.NonceGoogleAsync(_cliente);
        var idToken = Token(sujeto, correo, nonce);
        var (_, jwt) = await LeerSesionAsync(await Ayudas.LoginGoogleAsync(_cliente, idToken));
        await Ayudas.LoginGoogleAsync(_cliente, GoogleFalso.Token(sujeto, correo, nonce, firmaFalsa: true, ahora: Ahora));

        var logs = _fabrica.Logs.Mensajes.ToArray();

        Assert.Contains(logs, m => m.Contains("Seguridad: google_cuenta_creada", StringComparison.Ordinal));
        Assert.Contains(logs, m => m.Contains("Seguridad: google_token_rechazado", StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(idToken, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(idToken.Split('.')[2], StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(jwt, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(nonce, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, m => m.Contains(sujeto, StringComparison.Ordinal));
    }
}

/// <summary>Sin Google:ClientId la API arranca y "Continuar con Google" responde 503.</summary>
public sealed class GoogleSinConfigurarTests : IClassFixture<ClimaPlusApiFactorySinGoogle>
{
    private readonly HttpClient _cliente;

    public GoogleSinConfigurarTests(ClimaPlusApiFactorySinGoogle fabrica)
    {
        _cliente = fabrica.CreateClient();
    }

    [Fact]
    public async Task SinClientId_IniciarYLogin_Responden503_YElLoginLocalSigueFuncionando()
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await _cliente.GetAsync("/api/auth/google/iniciar")).StatusCode);
        var token = GoogleFalso.Token(GoogleFalso.NuevoSujeto(), $"g{Guid.NewGuid():N}@gmail.com", "x");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Ayudas.LoginGoogleAsync(_cliente, token)).StatusCode);

        var (correo, _) = await Ayudas.CrearUsuarioConSesionAsync(_cliente);
        Assert.Equal(HttpStatusCode.OK, (await Ayudas.IniciarSesionCompletaAsync(_cliente, correo)).StatusCode);
    }
}
