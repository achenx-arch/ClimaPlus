using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClimaPlus.Api.Tests.Infraestructura;

public static class Ayudas
{
    public const string PasswordValida = "ClimaPlus2026";

    public static string CorreoUnico() => $"prueba-{Guid.NewGuid():N}@ejemplo.com";

    public static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, string correo, string password = PasswordValida, string nombre = "Usuario Prueba") =>
        cliente.PostAsJsonAsync("/api/auth/register", new { nombre, correo, password });

    public static Task<HttpResponseMessage> LoginAsync(HttpClient cliente, string correo, string password = PasswordValida) =>
        cliente.PostAsJsonAsync("/api/auth/login", new { correo, password });

    /// <summary>
    /// Peticiones HTTP que hace <see cref="CrearUsuarioConSesionAsync"/>: registro, verificación del correo, login
    /// (contraseña) y login/verificar (código).
    /// </summary>
    public const int PeticionesCrearUsuarioConSesion = 4;

    public static Task<HttpResponseMessage> VerificarCorreoAsync(HttpClient cliente, string desafio, string codigo) =>
        cliente.PostAsJsonAsync("/api/auth/verificar-correo", new { desafio, codigo });

    public static Task<HttpResponseMessage> ReenviarCodigoAsync(HttpClient cliente, string desafio) =>
        cliente.PostAsJsonAsync("/api/auth/reenviar-codigo", new { desafio });

    /// <summary>Paso 2 del inicio de sesión.</summary>
    public static Task<HttpResponseMessage> VerificarLoginAsync(HttpClient cliente, string desafio, string codigo) =>
        cliente.PostAsJsonAsync("/api/auth/login/verificar", new { desafio, codigo });

    /// <summary>Token del desafío de una respuesta 202 (registro, login, reenvío).</summary>
    public static async Task<string> LeerDesafioAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var desafio = cuerpo.GetProperty("desafio").GetString();
        Assert.False(string.IsNullOrWhiteSpace(desafio));
        return desafio!;
    }

    /// <summary>Registra un usuario y verifica su correo con el código que la API encoló para él.</summary>
    public static async Task RegistrarYVerificarAsync(HttpClient cliente, string correo, string password = PasswordValida, string nombre = "Usuario Prueba")
    {
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var desafio = await LeerDesafioAsync(await RegistrarAsync(cliente, correo, password, nombre));
        var codigo = ColaCorreosObservada.Codigo(correo, previos);
        var verificacion = await VerificarCorreoAsync(cliente, desafio, codigo);
        Assert.Equal(HttpStatusCode.NoContent, verificacion.StatusCode);
    }

    /// <summary>Registra un usuario nuevo, verifica su correo, inicia sesión y devuelve (correo, token).</summary>
    public static async Task<(string Correo, string Token)> CrearUsuarioConSesionAsync(HttpClient cliente)
    {
        var correo = CorreoUnico();
        await RegistrarYVerificarAsync(cliente, correo);
        return (correo, await ObtenerTokenAsync(cliente, correo, PasswordValida));
    }

    /// <summary>
    /// Paso 1 del login (contraseña): 202 con el desafío. Devuelve el desafío y el código que la API encoló para él.
    /// </summary>
    public static async Task<(string Desafio, string Codigo, string Proposito)> IniciarLoginAsync(HttpClient cliente, string correo, string password = PasswordValida)
    {
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var respuesta = await LoginAsync(cliente, correo, password);
        Assert.Equal(HttpStatusCode.Accepted, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(cuerpo.TryGetProperty("token", out _));   // el paso 1 nunca entrega la sesión
        var desafio = cuerpo.GetProperty("desafio").GetString()!;
        return (desafio, ColaCorreosObservada.Codigo(correo, previos), cuerpo.GetProperty("proposito").GetString()!);
    }

    /// <summary>Inicio de sesión completo (contraseña + código): devuelve la respuesta 200 del paso 2.</summary>
    public static async Task<HttpResponseMessage> IniciarSesionCompletaAsync(HttpClient cliente, string correo, string password = PasswordValida)
    {
        var (desafio, codigo, _) = await IniciarLoginAsync(cliente, correo, password);
        return await VerificarLoginAsync(cliente, desafio, codigo);
    }

    /// <summary>Inicio de sesión completo en dos pasos; devuelve el JWT.</summary>
    public static async Task<string> ObtenerTokenAsync(HttpClient cliente, string correo, string password)
    {
        var respuesta = await IniciarSesionCompletaAsync(cliente, correo, password);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        return token!;
    }

    public static HttpRequestMessage ConToken(HttpMethod metodo, string ruta, string token, object? cuerpo = null)
    {
        var solicitud = new HttpRequestMessage(metodo, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null)
        {
            solicitud.Content = JsonContent.Create(cuerpo);
        }

        return solicitud;
    }

    /// <summary>Crea directamente en la base de datos un administrador activo.</summary>
    public static async Task<string> CrearAdministradorAsync(ClimaPlusApiFactory fabrica)
    {
        var correo = CorreoUnico();
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var hasher = alcance.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();

        var admin = new Usuario
        {
            Nombre = "Admin Prueba",
            Correo = correo,
            Rol = Roles.Administrador,
            Activo = true,
            CorreoVerificado = true,
            FechaCreacion = DateTime.UtcNow,
        };
        admin.PasswordHash = hasher.HashPassword(admin, PasswordValida);
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();
        return correo;
    }

    /// <summary>
    /// Cuenta creada con Google (sin contraseña local, con identidad externa) y un JWT de sesión para ella. El inicio
    /// con Google llega en la fase 5; aquí la cuenta se crea directamente en la base de datos.
    /// </summary>
    public static async Task<(string Correo, string Token, string Sujeto)> CrearCuentaSoloGoogleAsync(ClimaPlusApiFactory fabrica)
    {
        var correo = CorreoUnico();
        var sujeto = "google-sub-" + Guid.NewGuid().ToString("N");
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var usuario = new Usuario
        {
            Nombre = "Cuenta Google",
            Correo = correo,
            Rol = Roles.Usuario,
            Activo = true,
            CorreoVerificado = true,
            PasswordHash = null,
            FechaCreacion = DateTime.UtcNow,
        };
        usuario.IdentidadesExternas.Add(new IdentidadExterna
        {
            Proveedor = ProveedoresIdentidad.Google,
            Sujeto = sujeto,
            CorreoProveedor = correo,
            FechaVinculacion = DateTime.UtcNow,
        });
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        var token = alcance.ServiceProvider.GetRequiredService<ClimaPlus.Api.Security.JwtTokenService>().Crear(usuario).Token;
        return (correo, token, sujeto);
    }

    /// <summary>GET /api/auth/google/iniciar: devuelve el nonce (uso "login" sin sesión, "vincular" con sesión).</summary>
    public static async Task<string> NonceGoogleAsync(HttpClient cliente, string uso = "login", string? token = null)
    {
        var ruta = $"/api/auth/google/iniciar?uso={uso}";
        var respuesta = token is null ? await cliente.GetAsync(ruta) : await cliente.SendAsync(ConToken(HttpMethod.Get, ruta, token));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(GoogleFalso.ClientId, cuerpo.GetProperty("clientId").GetString());
        return cuerpo.GetProperty("nonce").GetString()!;
    }

    /// <summary>POST /api/auth/google con el ID token.</summary>
    public static Task<HttpResponseMessage> LoginGoogleAsync(HttpClient cliente, string credencial) =>
        cliente.PostAsJsonAsync("/api/auth/google", new { credencial });

    public static Task<HttpResponseMessage> SolicitarRecuperacionAsync(HttpClient cliente, string correo) =>
        cliente.PostAsJsonAsync("/api/auth/password/solicitar", new { correo });

    public static Task<HttpResponseMessage> VerificarRecuperacionAsync(HttpClient cliente, string desafio, string codigo) =>
        cliente.PostAsJsonAsync("/api/auth/password/verificar", new { desafio, codigo });

    public static Task<HttpResponseMessage> RestablecerAsync(HttpClient cliente, string permiso, string passwordNueva, string? confirmacion = null) =>
        cliente.PostAsJsonAsync("/api/auth/password/restablecer", new { permiso, passwordNueva, confirmacion = confirmacion ?? passwordNueva });

    /// <summary>Solicita la recuperación y devuelve el desafío y el código que la API encoló (o null si no encoló ninguno).</summary>
    public static async Task<(string Desafio, string? Codigo)> IniciarRecuperacionAsync(HttpClient cliente, string correo)
    {
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var desafio = await LeerDesafioAsync(await SolicitarRecuperacionAsync(cliente, correo));
        var codigos = ColaCorreosObservada.CodigosPara(correo);
        return (desafio, codigos.Count > previos ? EnviadorCorreoFalso.ExtraerCodigo(codigos[previos]) : null);
    }

    /// <summary>Recuperación hasta el permiso: solicitar + verificar. Devuelve el permiso.</summary>
    public static async Task<string> ObtenerPermisoAsync(HttpClient cliente, string correo)
    {
        var (desafio, codigo) = await IniciarRecuperacionAsync(cliente, correo);
        Assert.NotNull(codigo);
        var respuesta = await VerificarRecuperacionAsync(cliente, desafio, codigo!);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var permiso = (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("permiso").GetString();
        Assert.False(string.IsNullOrWhiteSpace(permiso));
        return permiso!;
    }

    /// <summary>Cambio de contraseña desde el perfil, paso 1 (POST /api/usuarios/me/password/cambio).</summary>
    public static Task<HttpResponseMessage> SolicitarCambioPasswordAsync(
        HttpClient cliente, string token, string passwordActual, string passwordNueva, string? confirmacion = null) =>
        cliente.SendAsync(ConToken(HttpMethod.Post, "/api/usuarios/me/password/cambio", token,
            new { passwordActual, passwordNueva, confirmacion = confirmacion ?? passwordNueva }));

    /// <summary>Cambio de contraseña desde el perfil, paso 2 (POST /api/usuarios/me/password/cambio/verificar).</summary>
    public static Task<HttpResponseMessage> VerificarCambioPasswordAsync(
        HttpClient cliente, string token, string desafio, string codigo, string passwordActual, string passwordNueva, string? confirmacion = null) =>
        cliente.SendAsync(ConToken(HttpMethod.Post, "/api/usuarios/me/password/cambio/verificar", token,
            new { desafio, codigo, passwordActual, passwordNueva, confirmacion = confirmacion ?? passwordNueva }));

    /// <summary>Paso 1 del cambio de contraseña: devuelve el desafío y el código CAMBIAR_PASSWORD que la API encoló.</summary>
    public static async Task<(string Desafio, string Codigo)> IniciarCambioPasswordAsync(
        HttpClient cliente, string token, string correo, string passwordActual, string passwordNueva)
    {
        var previos = ColaCorreosObservada.CodigosPara(correo).Count;
        var respuesta = await SolicitarCambioPasswordAsync(cliente, token, passwordActual, passwordNueva);
        var desafio = await LeerDesafioAsync(respuesta);
        return (desafio, ColaCorreosObservada.Codigo(correo, previos));
    }

    /// <summary>Cambio de contraseña completo (paso 1 + código del correo + paso 2). Devuelve la respuesta del paso 2.</summary>
    public static async Task<HttpResponseMessage> CambiarPasswordConCodigoAsync(
        HttpClient cliente, string token, string correo, string passwordActual, string passwordNueva)
    {
        var (desafio, codigo) = await IniciarCambioPasswordAsync(cliente, token, correo, passwordActual, passwordNueva);
        return await VerificarCambioPasswordAsync(cliente, token, desafio, codigo, passwordActual, passwordNueva);
    }

    /// <summary>Modifica un usuario directamente en la base de datos (simula una acción del administrador).</summary>
    public static async Task ModificarUsuarioAsync(ClimaPlusApiFactory fabrica, string correo, Action<Usuario> cambio)
    {
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        var usuario = await db.Usuarios.SingleAsync(u => u.Correo == correo);
        cambio(usuario);
        await db.SaveChangesAsync();
    }

    public static async Task<Usuario> LeerUsuarioAsync(ClimaPlusApiFactory fabrica, string correo)
    {
        using var alcance = fabrica.Services.CreateScope();
        var db = alcance.ServiceProvider.GetRequiredService<ClimaPlusDbContext>();
        return await db.Usuarios.AsNoTracking().SingleAsync(u => u.Correo == correo);
    }

    public static async Task<JsonElement> LeerProblemaAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
