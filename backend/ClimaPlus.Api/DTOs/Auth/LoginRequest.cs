using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>POST /api/auth/login.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(ReglasValidacion.CorreoMax, ErrorMessage = "El correo admite como máximo 150 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronCorreo, ErrorMessage = ReglasValidacion.MensajeCorreo)]
    public string Correo { get; init; } = string.Empty;

    // Sin reglas de formato: una contraseña antigua podría no cumplir la política actual.
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, ErrorMessage = "La contraseña admite como máximo 128 caracteres.")]
    public string Password { get; init; } = string.Empty;
}
