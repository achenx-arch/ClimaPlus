using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClimaPlus.Api.DTOs.Comun;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Admin;

/// <summary>Valores aceptados por los filtros y cambios de M14 (RF-26 a RF-28).</summary>
public static class FiltrosUsuarioAdmin
{
    public const string PatronRol = "^(USUARIO|ADMINISTRADOR)$";
    public const string PatronEstado = "^(ACTIVO|INACTIVO)$";
    public const string EstadoActivo = "ACTIVO";
    public const string EstadoInactivo = "INACTIVO";
}

/// <summary>GET /api/admin/usuarios (RF-26): búsqueda por nombre o correo, filtros por rol y estado, 20 por página.</summary>
public sealed class FiltroUsuariosAdminRequest
{
    /// <summary>Texto contenido en el nombre o en el correo (sin distinguir mayúsculas).</summary>
    [FromQuery(Name = "busqueda")]
    [StringLength(ReglasValidacion.CorreoMax, ErrorMessage = "La búsqueda admite como máximo 150 caracteres.")]
    public string? Busqueda { get; set; }

    [FromQuery(Name = "rol")]
    [RegularExpression(FiltrosUsuarioAdmin.PatronRol, ErrorMessage = "El rol debe ser USUARIO o ADMINISTRADOR.")]
    public string? Rol { get; set; }

    [FromQuery(Name = "estado")]
    [RegularExpression(FiltrosUsuarioAdmin.PatronEstado, ErrorMessage = "El estado debe ser ACTIVO o INACTIVO.")]
    public string? Estado { get; set; }

    [FromQuery(Name = "pagina")]
    [Range(1, Paginacion.PaginaMaxima, ErrorMessage = "La página debe ser un número mayor o igual que 1.")]
    public int Pagina { get; set; } = 1;
}

/// <summary>PATCH /api/admin/usuarios/{id}/rol (RF-27).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarRolUsuarioRequest
{
    [Required(ErrorMessage = "Indica el rol.")]
    [RegularExpression(FiltrosUsuarioAdmin.PatronRol, ErrorMessage = "El rol debe ser USUARIO o ADMINISTRADOR.")]
    public string? Rol { get; init; }
}

/// <summary>PATCH /api/admin/usuarios/{id}/estado (RF-28). No hay borrado físico (RN-BD-08).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CambiarEstadoUsuarioRequest
{
    [Required(ErrorMessage = "Indica si la cuenta queda activa (true) o desactivada (false).")]
    public bool? Activo { get; init; }
}

/// <summary>
/// Datos administrativos de una cuenta. Solo información no sensible: nunca el hash de la contraseña, el sello de
/// seguridad, desafíos, códigos ni identificadores de Google (solo si hay uno vinculado).
/// </summary>
public sealed record UsuarioAdminResponse(
    long IdUsuario,
    string Nombre,
    string Correo,
    string Rol,
    bool Activo,
    bool CorreoVerificado,
    bool TienePassword,
    bool TieneGoogle,
    DateTime FechaCreacion);
