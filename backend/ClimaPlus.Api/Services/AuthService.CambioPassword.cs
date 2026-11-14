using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Cambio de contraseña desde el perfil con doble verificación (migración 005, propósito CAMBIAR_PASSWORD).
///
/// Paso 1 (POST /api/usuarios/me/password/cambio): contraseña actual + nueva + confirmación. Si son correctas, envía un
/// código de 6 dígitos al correo de la cuenta (5 minutos, 5 intentos, un solo uso) y responde 202 con el desafío.
/// No cambia nada y no guarda la contraseña nueva en ningún sitio.
///
/// Paso 2 (POST /api/usuarios/me/password/cambio/verificar): el código más los mismos datos. Solo si el código es
/// válido, en un único SaveChanges (una transacción): se guarda el hash nuevo, se consume el desafío, se renueva el sello
/// de seguridad (todas las sesiones, incluida la actual, dejan de valer) y se anulan los demás desafíos pendientes de la
/// cuenta. Después se envía un aviso por correo.
///
/// Protecciones:
/// - La contraseña actual se comprueba con el mismo bloqueo del login: 5 fallos por cuenta + IP y 20 por cuenta en
///   15 minutos (LimitadorIntentosLogin), además del rate limiting por IP de cada endpoint.
/// - Códigos incorrectos: 5 por desafío y, por cuenta, 10 por hora para este propósito (ControlEnvios).
/// - El desafío debe ser de la cuenta de la sesión; el usuario sale siempre de la fila del desafío.
/// - Los logs nunca incluyen contraseñas ni códigos.
/// </summary>
public sealed partial class AuthService
{
    /// <summary>Paso 1: valida las contraseñas y envía el código CAMBIAR_PASSWORD (202).</summary>
    public async Task<Resultado<RespuestaDesafio>> IniciarCambioPasswordAsync(
        long idUsuario, CambiarPasswordRequest solicitud, string ip, CancellationToken cancelacion)
    {
        const string proposito = PropositosDesafio.CambiarPassword;
        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<RespuestaDesafio>.Falla(CuentaNoEncontrada());
        }

        if (ValidarCuentaParaCambio(usuario) is { } errorCuenta)
        {
            return Resultado<RespuestaDesafio>.Falla(errorCuenta);
        }

        if (VerificarPasswordActual(usuario, solicitud.PasswordActual, ip) is { } errorPassword)
        {
            return Resultado<RespuestaDesafio>.Falla(errorPassword);
        }

        if (ValidarPasswordNueva(solicitud.PasswordActual, solicitud.PasswordNueva, solicitud.Confirmacion) is { } errorNueva)
        {
            return Resultado<RespuestaDesafio>.Falla(errorNueva);
        }

        if (_envios.FallosAgotados(idUsuario, proposito))
        {
            _log.LogWarning("Seguridad: cambio_password_rechazado motivo=tope_fallos_cuenta idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(DemasiadosCodigosIncorrectos());
        }

        var desafio = await _desafios.CrearAsync(usuario, proposito, cancelacion);
        EnviarCodigo(usuario, desafio);

        _log.LogInformation("Seguridad: cambio_password_codigo_enviado idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, usuario.Correo));
    }

    /// <summary>
    /// Paso 2: valida el código y, solo entonces, cambia la contraseña, consume el desafío, cierra todas las sesiones
    /// y anula los demás desafíos pendientes, todo en la misma transacción. 204 si se cambió.
    /// </summary>
    public async Task<Resultado> VerificarCambioPasswordAsync(
        long idUsuario, VerificarCambioPasswordRequest solicitud, string ip, CancellationToken cancelacion)
    {
        const string proposito = PropositosDesafio.CambiarPassword;
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        var idDesafio = datos?.Proposito == proposito ? datos.IdDesafio : Guid.NewGuid();

        // El desafío tiene que existir y ser de la cuenta de esta sesión.
        var titular = await _desafios.BuscarTitularAsync(idDesafio, proposito, cancelacion);
        if (titular is null || titular.IdUsuario != idUsuario)
        {
            _log.LogWarning("Seguridad: codigo_rechazado motivo=desafio_invalido proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
                proposito, idUsuario, ip);
            return Resultado.Falla(CodigoInvalido());
        }

        if (_envios.FallosAgotados(idUsuario, proposito))
        {
            _log.LogWarning("Seguridad: codigo_rechazado motivo=tope_fallos_cuenta proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
                proposito, idUsuario, ip);
            return Resultado.Falla(DemasiadosCodigosIncorrectos());
        }

        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado.Falla(CuentaNoEncontrada());
        }

        if (ValidarCuentaParaCambio(usuario) is { } errorCuenta)
        {
            return Resultado.Falla(errorCuenta);
        }

        // Se vuelve a exigir la contraseña actual: si cambió entre los dos pasos (por ejemplo, con una recuperación),
        // este cambio ya no vale.
        if (VerificarPasswordActual(usuario, solicitud.PasswordActual, ip) is { } errorPassword)
        {
            return Resultado.Falla(errorPassword);
        }

        if (ValidarPasswordNueva(solicitud.PasswordActual, solicitud.PasswordNueva, solicitud.Confirmacion) is { } errorNueva)
        {
            return Resultado.Falla(errorNueva);
        }

        // El hash se calcula antes (es lento a propósito) y solo se asigna si el código es válido.
        var hashNuevo = _hasher.HashPassword(usuario, solicitud.PasswordNueva);
        var desafio = await _desafios.VerificarAsync(idDesafio, proposito, solicitud.Codigo, EstadosDesafio.Consumido, cancelacion,
            alAceptar: d =>
            {
                d.Usuario.PasswordHash = hashNuevo;
                d.Usuario.SelloSeguridad = Guid.NewGuid();   // revoca todas las sesiones, incluida la actual
            },
            alFallar: d => _envios.RegistrarFallo(d.IdUsuario, proposito),
            anularOtrosVivos: true);
        if (desafio is null)
        {
            _log.LogWarning("Seguridad: codigo_rechazado proposito={Proposito} idUsuario={IdUsuario} ip={Ip}", proposito, idUsuario, ip);
            return Resultado.Falla(CodigoInvalido());
        }

        var cuenta = desafio.Usuario;
        _cola.Encolar(PlantillasCorreo.AvisoPasswordCambiada(cuenta.Correo, cuenta.Nombre, desafio.FinalizadoEn ?? DateTime.UtcNow));
        _log.LogInformation("Seguridad: password_cambiada metodo=codigo idUsuario={IdUsuario} ip={Ip}", cuenta.IdUsuario, ip);
        return Resultado.Ok();
    }

    /// <summary>409 si la cuenta no tiene contraseña local (creada con Google) o no verificó su correo.</summary>
    private static ErrorServicio? ValidarCuentaParaCambio(Usuario usuario)
    {
        if (usuario.PasswordHash is null)
        {
            // Una cuenta creada con Google establece su contraseña con el flujo de recuperación, nunca aquí.
            return new ErrorServicio(TipoError.Conflicto,
                "Tu cuenta no tiene contraseña local. Usa \"Establecer contraseña\" para crear una.", "passwordActual");
        }

        if (!usuario.CorreoVerificado)
        {
            return new ErrorServicio(TipoError.Conflicto,
                "Antes de cambiar la contraseña, verifica tu correo con \"¿Olvidaste tu contraseña?\".");
        }

        return null;
    }

    /// <summary>La confirmación debe coincidir y la nueva contraseña debe ser distinta de la actual.</summary>
    private static ErrorServicio? ValidarPasswordNueva(string actual, string nueva, string confirmacion)
    {
        if (!string.Equals(nueva, confirmacion, StringComparison.Ordinal))
        {
            return new ErrorServicio(TipoError.Validacion, "Las contraseñas no coinciden.", "confirmacion");
        }

        if (string.Equals(actual, nueva, StringComparison.Ordinal))
        {
            return new ErrorServicio(TipoError.Validacion, "La nueva contraseña debe ser distinta de la actual.", "passwordNueva");
        }

        return null;
    }

    private static ErrorServicio CuentaNoEncontrada() =>
        new(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado.");

    private static ErrorServicio DemasiadosCodigosIncorrectos() =>
        new(TipoError.DemasiadosIntentos,
            "Se ingresaron demasiados códigos incorrectos para esta cuenta. Espera una hora antes de volver a intentarlo.",
            ReintentarSegundos: 3600);
}
