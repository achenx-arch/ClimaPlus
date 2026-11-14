using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.Security;

namespace ClimaPlus.Api.DTOs.Usuarios;

/// <summary>
/// POST /api/usuarios/me/password/cambio (paso 1). Comprueba la contraseña actual y la nueva y envía un código
/// CAMBIAR_PASSWORD al correo de la cuenta. No cambia nada ni guarda la contraseña nueva.
/// </summary>
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

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [Compare(nameof(PasswordNueva), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; init; } = string.Empty;
}

/// <summary>
/// POST /api/usuarios/me/password/cambio/verificar (paso 2). El código del correo más los mismos datos del paso 1:
/// la contraseña nueva viaja de nuevo para no guardarla en ningún sitio antes de validar el código.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class VerificarCambioPasswordRequest
{
    [Required(ErrorMessage = "Falta el desafío. Vuelve a empezar.")]
    [StringLength(TokenDesafio.LongitudMaxima, ErrorMessage = "El desafío no es válido.")]
    public string Desafio { get; init; } = string.Empty;

    [Required(ErrorMessage = "Escribe el código de 6 dígitos.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "El código tiene 6 dígitos.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, ErrorMessage = "La contraseña admite como máximo 128 caracteres.")]
    public string PasswordActual { get; init; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(ReglasValidacion.PasswordMax, MinimumLength = ReglasValidacion.PasswordMin, ErrorMessage = ReglasValidacion.MensajePassword)]
    [RegularExpression(ReglasValidacion.PatronPassword, ErrorMessage = ReglasValidacion.MensajePassword)]
    public string PasswordNueva { get; init; } = string.Empty;

    [Required(ErrorMessage = "Confirma la nueva contraseña.")]
    [Compare(nameof(PasswordNueva), ErrorMessage = "Las contraseñas no coinciden.")]
    public string Confirmacion { get; init; } = string.Empty;
}
