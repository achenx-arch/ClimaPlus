using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Perfil propio (M16, M17 – enmienda E-01). Siempre opera sobre el usuario del token:
/// no existe forma de consultar ni modificar el perfil de otra persona.
/// Nunca cambia rol ni estado (RN-BD-37, RN-BD-38).
/// </summary>
public sealed class UsuarioService
{
    private readonly ClimaPlusDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly ILogger _log;

    public UsuarioService(ClimaPlusDbContext db, IPasswordHasher<Usuario> hasher, ILoggerFactory logs)
    {
        _db = db;
        _hasher = hasher;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public async Task<Resultado<UsuarioResponse>> ObtenerAsync(long idUsuario, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);

        return usuario is null
            ? Resultado<UsuarioResponse>.Falla(NoEncontrado())
            : Resultado<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario));
    }

    public async Task<Resultado<UsuarioResponse>> ActualizarAsync(long idUsuario, ActualizarPerfilRequest solicitud, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<UsuarioResponse>.Falla(NoEncontrado());
        }

        var correo = Normalizacion.Correo(solicitud.Correo);
        var cambiaCorreo = !string.Equals(correo, usuario.Correo, StringComparison.Ordinal);

        if (cambiaCorreo && await _db.Usuarios.AnyAsync(u => u.Correo == correo && u.IdUsuario != idUsuario, cancelacion))
        {
            _log.LogInformation("Seguridad: perfil_rechazado motivo=correo_en_uso idUsuario={IdUsuario}", idUsuario);
            return Resultado<UsuarioResponse>.Falla(CorreoEnUso());
        }

        usuario.Nombre = Normalizacion.Nombre(solicitud.Nombre);
        usuario.Correo = correo;

        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (AuthService.EsViolacionDeUnicidad(ex))
        {
            return Resultado<UsuarioResponse>.Falla(CorreoEnUso());
        }

        _log.LogInformation("Seguridad: perfil_actualizado idUsuario={IdUsuario} correoCambiado={CorreoCambiado}", idUsuario, cambiaCorreo);
        return Resultado<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario));
    }

    public async Task<Resultado> CambiarPasswordAsync(long idUsuario, CambiarPasswordRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado.Falla(NoEncontrado());
        }

        if (_hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, solicitud.PasswordActual) == PasswordVerificationResult.Failed)
        {
            _log.LogWarning("Seguridad: cambio_password_fallido motivo=password_actual idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
            return Resultado.Falla(new ErrorServicio(TipoError.Validacion, "La contraseña actual es incorrecta.", "passwordActual"));
        }

        if (string.Equals(solicitud.PasswordActual, solicitud.PasswordNueva, StringComparison.Ordinal))
        {
            return Resultado.Falla(new ErrorServicio(TipoError.Validacion,
                "La nueva contraseña debe ser distinta de la actual.", "passwordNueva"));
        }

        usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.PasswordNueva);
        await _db.SaveChangesAsync(cancelacion);

        _log.LogInformation("Seguridad: password_cambiada idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
        return Resultado.Ok();
    }

    private static ErrorServicio NoEncontrado() =>
        new(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado.");

    private static ErrorServicio CorreoEnUso() =>
        new(TipoError.Conflicto, "Ya existe una cuenta con ese correo.", "correo");
}
