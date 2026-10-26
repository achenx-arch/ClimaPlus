using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Usuarios;

/// <summary>PUT /api/usuarios/me/password.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarPasswordRequest
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, ErrorMessage = "La contraseña admite como máximo 128 caracteres.")]
    public string PasswordActual { get; init; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, MinimumLength = ReglasValidacion.PasswordMin, ErrorMessage = ReglasValidacion.MensajePassword)]
    [RegularExpression(ReglasValidacion.PatronPassword, ErrorMessage = ReglasValidacion.MensajePassword)]
    public string PasswordNueva { get; init; } = string.Empty;
}
