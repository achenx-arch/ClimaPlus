using ClimaPlus.Api.Domain.Entities;

namespace ClimaPlus.Api.DTOs.Usuarios;

/// <summary>
/// Datos públicos de un usuario (M16, M17 y sesión). Nunca incluye password_hash.
/// FechaCreacion está en UTC; el frontend la muestra en hora local.
/// Autenticación segura (fase 4): CorreoVerificado, TienePassword (la cuenta tiene contraseña local) y TieneGoogle
/// (tiene una identidad de Google vinculada) permiten al perfil mostrar el método de acceso y ofrecer "Cambiar
/// contraseña" o "Establecer contraseña". Son campos agregados: no se quitó ninguno.
/// </summary>
public sealed record UsuarioResponse(
    long IdUsuario, string Nombre, string Correo, string Rol, bool Activo, DateTime FechaCreacion,
    bool CorreoVerificado, bool TienePassword, bool TieneGoogle)
{
    public static UsuarioResponse Desde(Usuario usuario, bool tieneGoogle) => new(
        usuario.IdUsuario,
        usuario.Nombre,
        usuario.Correo,
        usuario.Rol,
        usuario.Activo,
        DateTime.SpecifyKind(usuario.FechaCreacion, DateTimeKind.Utc),
        usuario.CorreoVerificado,
        usuario.PasswordHash is not null,
        tieneGoogle);
}
