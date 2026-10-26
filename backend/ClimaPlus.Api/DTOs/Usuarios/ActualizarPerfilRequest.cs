using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Usuarios;

/// <summary>
/// PUT /api/usuarios/me. Solo nombre y correo. Enviar "rol", "activo" u otro campo
/// devuelve 400 (el rol y el estado no se modifican desde el perfil).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActualizarPerfilRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(ReglasValidacion.NombreMax, ErrorMessage = "El nombre admite como máximo 100 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronNombre, ErrorMessage = ReglasValidacion.MensajeNombre)]
    public string Nombre { get; init; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [StringLength(ReglasValidacion.CorreoMax, ErrorMessage = "El correo admite como máximo 150 caracteres.")]
    [RegularExpression(ReglasValidacion.PatronCorreo, ErrorMessage = ReglasValidacion.MensajeCorreo)]
    public string Correo { get; init; } = string.Empty;
}
