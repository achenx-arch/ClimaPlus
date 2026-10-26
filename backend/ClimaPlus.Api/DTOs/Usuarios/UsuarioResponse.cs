using ClimaPlus.Api.Domain.Entities;

namespace ClimaPlus.Api.DTOs.Usuarios;

/// <summary>
/// Datos públicos de un usuario (M16, M17 y sesión). Nunca incluye password_hash.
/// FechaCreacion está en UTC; el frontend la muestra en hora local.
/// </summary>
public sealed record UsuarioResponse(long IdUsuario, string Nombre, string Correo, string Rol, bool Activo, DateTime FechaCreacion)
{
    public static UsuarioResponse Desde(Usuario usuario) => new(
        usuario.IdUsuario,
        usuario.Nombre,
        usuario.Correo,
        usuario.Rol,
        usuario.Activo,
        DateTime.SpecifyKind(usuario.FechaCreacion, DateTimeKind.Utc));
}
