using ClimaPlus.Api.Data;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClimaPlus.Api.Services;

/// <summary>Registro e inicio de sesión (RF-01, RF-02).</summary>
public sealed class AuthService
{
    // Hash de una contraseña ficticia: se verifica cuando el correo no existe, para que
    // la respuesta tarde lo mismo y no revele qué correos están registrados.
    private static readonly Lazy<string> HashFicticio =
        new(() => new PasswordHasher<Usuario>().HashPassword(new Usuario(), "climaplus-hash-ficticio-2026"));

    private readonly ClimaPlusDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly JwtTokenService _tokens;
    private readonly LimitadorIntentosLogin _intentos;
    private readonly ILogger _log;

    public AuthService(
        ClimaPlusDbContext db, IPasswordHasher<Usuario> hasher, JwtTokenService tokens, LimitadorIntentosLogin intentos, ILoggerFactory logs)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _intentos = intentos;
        _log = logs.CreateLogger(RegistroSeguridad.Categoria);
    }

    public async Task<Resultado<UsuarioResponse>> RegistrarAsync(RegistroRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var correo = Normalizacion.Correo(solicitud.Correo);

        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo, cancelacion))
        {
            _log.LogInformation("Seguridad: registro_rechazado motivo=correo_en_uso correo={Correo} ip={Ip}",
                RegistroSeguridad.EnmascararCorreo(correo), ip);
            return Resultado<UsuarioResponse>.Falla(CorreoEnUso());
        }

        var usuario = new Usuario
        {
            Nombre = Normalizacion.Nombre(solicitud.Nombre),
            Correo = correo,
            Rol = Roles.Usuario,          // RN-BD-05: el registro público siempre es USUARIO.
            Activo = true,
            FechaCreacion = DateTime.UtcNow,
        };
        usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.Password);

        _db.Usuarios.Add(usuario);
        try
        {
            await _db.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException ex) when (EsViolacionDeUnicidad(ex))
        {
            // Dos registros simultáneos con el mismo correo: lo resuelve uq_usuario_correo.
            return Resultado<UsuarioResponse>.Falla(CorreoEnUso());
        }

        _log.LogInformation("Seguridad: registro_exitoso idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
        return Resultado<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario));
    }

    public async Task<Resultado<LoginResponse>> IniciarSesionAsync(LoginRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var correo = Normalizacion.Correo(solicitud.Correo);

        // Fase 8 (A07): bloqueo temporal por cuenta tras varios fallos seguidos. Se comprueba antes de buscar al
        // usuario y vale igual para correos inexistentes, así no revela qué cuentas existen.
        if (_intentos.TiempoBloqueo(correo) is { } bloqueo)
        {
            var segundos = (int)Math.Ceiling(bloqueo.TotalSeconds);
            _log.LogWarning("Seguridad: login_bloqueado correo={Correo} ip={Ip} segundos={Segundos}",
                RegistroSeguridad.EnmascararCorreo(correo), ip, segundos);
            return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.DemasiadosIntentos,
                "Demasiados intentos fallidos para esta cuenta. Espera unos minutos e inténtalo de nuevo.",
                ReintentarSegundos: segundos));
        }

        var usuario = await _db.Usuarios.SingleOrDefaultAsync(u => u.Correo == correo, cancelacion);

        if (usuario is null)
        {
            _hasher.VerifyHashedPassword(new Usuario(), HashFicticio.Value, solicitud.Password);
            _intentos.RegistrarFallo(correo);
            _log.LogWarning("Seguridad: login_fallido motivo=credenciales correo={Correo} ip={Ip}",
                RegistroSeguridad.EnmascararCorreo(correo), ip);
            return Resultado<LoginResponse>.Falla(CredencialesInvalidas());
        }

        var verificacion = _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, solicitud.Password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            _intentos.RegistrarFallo(correo);
            _log.LogWarning("Seguridad: login_fallido motivo=credenciales idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<LoginResponse>.Falla(CredencialesInvalidas());
        }

        // La contraseña era correcta: se reinicia el contador de fallos.
        _intentos.Reiniciar(correo);

        // Solo después de comprobar la contraseña se informa que la cuenta está desactivada (RN-BD-07).
        if (!usuario.Activo)
        {
            _log.LogWarning("Seguridad: login_rechazado motivo=cuenta_desactivada idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<LoginResponse>.Falla(new ErrorServicio(TipoError.CuentaDesactivada,
                "Tu cuenta está desactivada. Contacta al administrador."));
        }

        if (verificacion == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.Password);
            await _db.SaveChangesAsync(cancelacion);
        }

        var token = _tokens.Crear(usuario);
        _log.LogInformation("Seguridad: login_exitoso idUsuario={IdUsuario} rol={Rol} ip={Ip}", usuario.IdUsuario, usuario.Rol, ip);

        return Resultado<LoginResponse>.Ok(new LoginResponse(token.Token, "Bearer", token.ExpiraEn, UsuarioResponse.Desde(usuario)));
    }

    internal static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ErrorServicio CorreoEnUso() =>
        new(TipoError.Conflicto, "Ya existe una cuenta con ese correo.", "correo");

    private static ErrorServicio CredencialesInvalidas() =>
        new(TipoError.CredencialesInvalidas, "Correo o contraseña incorrectos.");
}
