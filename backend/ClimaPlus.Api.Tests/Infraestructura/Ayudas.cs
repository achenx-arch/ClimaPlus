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

    /// <summary>Registra un usuario nuevo, inicia sesión y devuelve (correo, token).</summary>
    public static async Task<(string Correo, string Token)> CrearUsuarioConSesionAsync(HttpClient cliente)
    {
        var correo = CorreoUnico();
        var registro = await RegistrarAsync(cliente, correo);
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);
        return (correo, await ObtenerTokenAsync(cliente, correo, PasswordValida));
    }

    public static async Task<string> ObtenerTokenAsync(HttpClient cliente, string correo, string password)
    {
        var respuesta = await LoginAsync(cliente, correo, password);
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
            FechaCreacion = DateTime.UtcNow,
        };
        admin.PasswordHash = hasher.HashPassword(admin, PasswordValida);
        db.Usuarios.Add(admin);
        await db.SaveChangesAsync();
        return correo;
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
