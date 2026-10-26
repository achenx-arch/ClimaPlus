using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Auth;

/// <summary>
/// POST /api/auth/register. No acepta "rol" ni "activo": cualquier campo extra se rechaza
/// con 400 (protección contra asignación masiva). Todo registro público es USUARIO.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class RegistroRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(ReglasValidacion.NombreMax, ErrorMessage = "El nombre admite como máximo 100 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronNombre, ErrorMessage = ReglasValidacion.MensajeNombre)]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(ReglasValidacion.CorreoMax, ErrorMessage = "El correo admite como máximo 150 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronCorreo, ErrorMessage = ReglasValidacion.MensajeCorreo)]
    public string Correo { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, MinimumLength = ReglasValidacion.PasswordMin, ErrorMessage = ReglasValidacion.MensajePassword)]
    [RegularExpression(ReglasValidacion.PatronPassword, ErrorMessage = ReglasValidacion.MensajePassword)]
    public string Password { get; init; } = string.Empty;
}
