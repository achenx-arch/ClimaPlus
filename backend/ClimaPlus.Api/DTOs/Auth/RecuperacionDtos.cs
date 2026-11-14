using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>POST /api/auth/password/solicitar.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SolicitarRecuperacionRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(ReglasValidacion.CorreoMax, ErrorMessage = "El correo admite como máximo 150 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronCorreo, ErrorMessage = ReglasValidacion.MensajeCorreo)]
    public string Correo { get; init; } = string.Empty;
}

/// <summary>
/// Respuesta 200 de POST /api/auth/password/verificar: permiso de un solo uso para restablecer la contraseña
/// (audiencia "ClimaPlus.Restablecer", 10 minutos). No es una sesión. ExpiraEn en UTC.
/// </summary>
public sealed record RespuestaPermiso(string Permiso, DateTime ExpiraEn);

/// <summary>POST /api/auth/password/restablecer.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RestablecerPasswordRequest
{
    [Required(ErrorMessage = "Falta el permiso. Vuelve a pedir un código.")]
    [StringLength(TokenDesafio.LongitudMaxima, ErrorMessage = "El permiso no es válido.")]
    public string Permiso { get; init; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, MinimumLength = ReglasValidacion.PasswordMin, ErrorMessage = ReglasValidacion.MensajePassword)]
    [RegularExpression(ReglasValidacion.PatronPassword, ErrorMessage = ReglasValidacion.MensajePassword)]
    public string PasswordNueva { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [Compare(nameof(PasswordNueva), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; init; } = string.Empty;
}
