using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>
/// Respuesta de GET /api/auth/google/iniciar: lo que necesita el botón de Google Identity Services. ClientId no es
/// secreto. Nonce: un solo uso, vence en ExpiraEn (UTC); se pasa a google.accounts.id.initialize({ nonce }).
/// </summary>
public sealed record RespuestaInicioGoogle(string ClientId, string Nonce, DateTime ExpiraEn);

/// <summary>POST /api/auth/google: el ID token ("credential") que Google entregó al navegador.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class GoogleLoginRequest
{
    [Required(ErrorMessage = "Falta la respuesta de Google.")]
    [StringLength(ValidadorTokenGoogle.LongitudMaxima, ErrorMessage = "La respuesta de Google no es válida.")]
    public string Credencial { get; init; } = string.Empty;
}

/// <summary>POST /api/usuarios/me/google/vincular: ID token (nonce de uso "vincular") y contraseña actual.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VincularGoogleRequest
{
    [Required(ErrorMessage = "Falta la respuesta de Google.")]
    [StringLength(ValidadorTokenGoogle.LongitudMaxima, ErrorMessage = "La respuesta de Google no es válida.")]
    public string Credencial { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    [StringLength(ReglasValidacion.PasswordMax, ErrorMessage = "La contraseña actual no es correcta.")]
    public string PasswordActual { get; init; } = string.Empty;
}

/// <summary>POST /api/usuarios/me/google/desvincular: contraseña actual (autenticación reciente).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DesvincularGoogleRequest
{
    [Required(ErrorMessage = "Escribe tu contraseña actual.")]
    [StringLength(ReglasValidacion.PasswordMax, ErrorMessage = "La contraseña actual no es correcta.")]
    public string PasswordActual { get; init; } = string.Empty;
}
