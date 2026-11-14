using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Perfil propio (M16, M17 – enmienda E-01). Siempre opera sobre el usuario del token:
/// no existe forma de consultar ni modificar el perfil de otra persona.
/// Nunca cambia rol ni estado (RN-BD-37, RN-BD-38). El cambio de contraseña, con código por correo, está en
/// AuthService.CambioPassword.cs (migración 005).
/// </summary>
public sealed class UsuarioService
{
    private readonly ClimaPlusDbContext _db;
    private readonly ILogger _log;

    public UsuarioService(ClimaPlusDbContext db, ILoggerFactory logs)
    {
        _db = db;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    private Task<bool> TieneGoogleAsync(long idUsuario, CancellationToken cancelacion) =>
        _db.IdentidadesExternas.AnyAsync(i => i.IdUsuario == idUsuario, cancelacion);

    public async Task<Resultado<UsuarioResponse>> ObtenerAsync(long idUsuario, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);

        return usuario is null
            ? Resultado<UsuarioResponse>.Falla(NoEncontrado())
            : Resultado<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario, await TieneGoogleAsync(idUsuario, cancelacion)));
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
        return Resultado<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario, await TieneGoogleAsync(idUsuario, cancelacion)));
    }

    private static ErrorServicio NoEncontrado() =>
        new(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado.");

    private static ErrorServicio CorreoEnUso() =>
        new(TipoError.Conflicto, "Ya existe una cuenta con ese correo.", "correo");
}
