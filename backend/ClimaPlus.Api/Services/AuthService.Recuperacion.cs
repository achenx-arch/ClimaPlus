using ClimaPlus.Api.Correo;
using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClimaPlus.Api.Services;

/// <summary>
/// Autenticación segura, fase 4: recuperación de contraseña y "Establecer contraseña" para cuentas de Google.
/// Flujo: solicitar (202, código RECUPERAR_PASSWORD) → verificar (200, permiso de 10 minutos) → restablecer (204).
/// - Nunca revela si la cuenta existe: un correo desconocido o una cuenta desactivada reciben un señuelo.
/// - El usuario sale siempre de la fila del desafío, nunca del token (condición 3).
/// - Restablecer consume el desafío VERIFICADO y cambia la contraseña en un solo SaveChanges (una transacción), con
///   el estado como token de concurrencia: el permiso sirve una sola vez aunque llegue dos veces a la vez.
/// - Renueva el sello de seguridad (revoca todas las sesiones), marca el correo como verificado y avisa por correo.
/// - Nunca entrega una sesión: después de restablecer, el usuario inicia sesión en dos pasos.
/// </summary>
public sealed partial class AuthService
{
    /// <summary>
    /// POST /api/auth/password/solicitar. Siempre 202 con la misma forma. Solo una cuenta real y activa recibe el
    /// código, y como máximo 3 veces por hora (tope silencioso, además de los 5 códigos por hora de la cuenta).
    /// </summary>
    public async Task<Resultado<RespuestaDesafio>> SolicitarRecuperacionAsync(
        SolicitarRecuperacionRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var correo = Normalizacion.Correo(solicitud.Correo);
        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.Correo == correo, cancelacion);

        if (usuario is null || !usuario.Activo)
        {
            // Mismo trabajo que una solicitud real (calcular el HMAC del código) y la misma respuesta.
            var senuelo = _desafios.CrearSenuelo(PropositosDesafio.RecuperarPassword);
            _desafios.Hash(senuelo.IdDesafio, senuelo.Proposito, senuelo.Codigo);
            _log.LogInformation("Seguridad: recuperacion_solicitada resultado=sin_envio correo={Correo} ip={Ip}",
                RegistroSeguridad.EnmascararCorreo(correo), ip);
            return Resultado<RespuestaDesafio>.Ok(Respuesta(senuelo, correo));
        }

        if (!_envios.PermitirRecuperacion(usuario.IdUsuario))
        {
            // Tope por cuenta: misma respuesta, sin crear desafío (el que ya está vivo sigue valiendo) y sin correo.
            _log.LogWarning("Seguridad: recuperacion_solicitada resultado=tope_por_cuenta idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
            return Resultado<RespuestaDesafio>.Ok(Respuesta(_desafios.CrearSenuelo(PropositosDesafio.RecuperarPassword), correo));
        }

        var desafio = await _desafios.CrearAsync(usuario, PropositosDesafio.RecuperarPassword, cancelacion);
        EnviarCodigo(usuario, desafio);

        _log.LogInformation("Seguridad: recuperacion_solicitada resultado=codigo_enviado idUsuario={IdUsuario} ip={Ip}", usuario.IdUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, correo));
    }

    /// <summary>
    /// POST /api/usuarios/me/password/establecer. Para cuentas creadas con Google (sin contraseña local): exige la
    /// sesión y además el código RECUPERAR_PASSWORD enviado al correo de la cuenta; luego se usan los mismos
    /// password/verificar y password/restablecer. Un JWT válido no basta por sí solo. La identidad de Google no se toca.
    /// </summary>
    public async Task<Resultado<RespuestaDesafio>> IniciarEstablecerPasswordAsync(long idUsuario, string ip, CancellationToken cancelacion)
    {
        var usuario = await _db.Usuarios.AsNoTracking().SingleOrDefaultAsync(u => u.IdUsuario == idUsuario, cancelacion);
        if (usuario is null)
        {
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.NoEncontrado, "No se encontró la cuenta del usuario autenticado."));
        }

        if (usuario.PasswordHash is not null)
        {
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.Conflicto,
                "Tu cuenta ya tiene contraseña. Usa \"Cambiar contraseña\" o, si la olvidaste, \"¿Olvidaste tu contraseña?\"."));
        }

        // La sesión ya identifica al usuario: aquí el tope por cuenta puede responder 429 sin revelar nada.
        if (!_envios.PermitirRecuperacion(usuario.IdUsuario))
        {
            _log.LogWarning("Seguridad: establecer_password_rechazado motivo=tope_por_cuenta idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
            return Resultado<RespuestaDesafio>.Falla(new ErrorServicio(TipoError.DemasiadosIntentos,
                "Ya pediste demasiados códigos. Inténtalo de nuevo en una hora.", ReintentarSegundos: 3600));
        }

        var desafio = await _desafios.CrearAsync(usuario, PropositosDesafio.RecuperarPassword, cancelacion);
        EnviarCodigo(usuario, desafio);

        _log.LogInformation("Seguridad: establecer_password_codigo_enviado idUsuario={IdUsuario} ip={Ip}", idUsuario, ip);
        return Resultado<RespuestaDesafio>.Ok(Respuesta(desafio, usuario.Correo));
    }

    /// <summary>
    /// POST /api/auth/password/verificar. Valida el código RECUPERAR_PASSWORD y deja el desafío VERIFICADO. Devuelve
    /// el permiso de restablecer (10 minutos, audiencia ClimaPlus.Restablecer). No cambia la contraseña ni abre sesión.
    /// </summary>
    public async Task<Resultado<RespuestaPermiso>> VerificarRecuperacionAsync(VerificarCodigoRequest solicitud, string ip, CancellationToken cancelacion)
    {
        const string proposito = PropositosDesafio.RecuperarPassword;
        var datos = await _tokensDesafio.LeerAsync(solicitud.Desafio);
        var idDesafio = datos?.Proposito == proposito ? datos.IdDesafio : Guid.NewGuid();

        // Tope por cuenta (condición 2): 10 códigos incorrectos por hora en la recuperación.
        var titular = await _desafios.BuscarTitularAsync(idDesafio, proposito, cancelacion);
        if (titular is not null && _envios.FallosAgotados(titular.IdUsuario, proposito))
        {
            _log.LogWarning("Seguridad: codigo_rechazado motivo=tope_fallos_cuenta proposito={Proposito} idUsuario={IdUsuario} ip={Ip}",
                proposito, titular.IdUsuario, ip);
            return Resultado<RespuestaPermiso>.Falla(CodigoInvalido());
        }

        var desafio = await _desafios.VerificarAsync(idDesafio, proposito, solicitud.Codigo, EstadosDesafio.Verificado, cancelacion,
            alFallar: d => _envios.RegistrarFallo(d.IdUsuario, proposito));
        if (desafio?.VerificadoEn is not { } verificado)
        {
            _log.LogWarning("Seguridad: codigo_rechazado proposito={Proposito} ip={Ip}", proposito, ip);
            return Resultado<RespuestaPermiso>.Falla(CodigoInvalido());
        }

        var expira = verificado.AddMinutes(_opcionesCodigos.Value.MinutosPermisoRestablecer);
        _log.LogInformation("Seguridad: recuperacion_verificada idUsuario={IdUsuario} ip={Ip}", desafio.IdUsuario, ip);
        return Resultado<RespuestaPermiso>.Ok(new RespuestaPermiso(_tokensDesafio.CrearPermiso(desafio.IdDesafio, expira), expira));
    }

    /// <summary>
    /// POST /api/auth/password/restablecer. Con un permiso válido, cambia la contraseña de forma atómica y de un solo
    /// uso, renueva el sello (cierra todas las sesiones), marca el correo como verificado y envía un aviso de seguridad.
    /// </summary>
    public async Task<Resultado> RestablecerPasswordAsync(RestablecerPasswordRequest solicitud, string ip, CancellationToken cancelacion)
    {
        var idDesafio = await _tokensDesafio.LeerPermisoAsync(solicitud.Permiso);
        if (idDesafio is not { } id)
        {
            _log.LogWarning("Seguridad: restablecer_rechazado motivo=permiso_invalido ip={Ip}", ip);
            return Resultado.Falla(PermisoInvalido());
        }

        // No se permite volver a poner la contraseña actual. Se comprueba antes de consumir el permiso, para que el
        // usuario pueda corregir y reintentar con el mismo permiso.
        var hashActual = await _db.DesafiosAutenticacion.AsNoTracking()
            .Where(d => d.IdDesafio == id && d.Proposito == PropositosDesafio.RecuperarPassword && d.Estado == EstadosDesafio.Verificado)
            .Select(d => new { d.Usuario, d.Usuario.PasswordHash })
            .SingleOrDefaultAsync(cancelacion);
        if (hashActual?.PasswordHash is { } actual
            && _hasher.VerifyHashedPassword(hashActual.Usuario, actual, solicitud.PasswordNueva) != PasswordVerificationResult.Failed)
        {
            return Resultado.Falla(new ErrorServicio(TipoError.Validacion,
                "La nueva contraseña debe ser distinta de la actual.", "passwordNueva"));
        }

        var ventana = TimeSpan.FromMinutes(_opcionesCodigos.Value.MinutosPermisoRestablecer);
        var desafio = await _desafios.ConsumirVerificadoAsync(id, ventana, d =>
        {
            var usuario = d.Usuario;
            usuario.PasswordHash = _hasher.HashPassword(usuario, solicitud.PasswordNueva);
            usuario.SelloSeguridad = Guid.NewGuid();   // revoca todas las sesiones abiertas
            usuario.CorreoVerificado = true;           // el código llegó a este buzón
        }, cancelacion);
        if (desafio is null)
        {
            _log.LogWarning("Seguridad: restablecer_rechazado motivo=permiso_usado_o_vencido ip={Ip}", ip);
            return Resultado.Falla(PermisoInvalido());
        }

        var cuenta = desafio.Usuario;
        _intentos.Reiniciar(cuenta.Correo, ip);
        _cola.Encolar(PlantillasCorreo.AvisoPasswordCambiada(cuenta.Correo, cuenta.Nombre, desafio.FinalizadoEn ?? DateTime.UtcNow));

        _log.LogInformation("Seguridad: password_restablecida idUsuario={IdUsuario} ip={Ip}", cuenta.IdUsuario, ip);
        return Resultado.Ok();
    }

    private static ErrorServicio PermisoInvalido() =>
        new(TipoError.Validacion, "El permiso para cambiar la contraseña ya se usó o venció. Pide un código nuevo.", "permiso");
}
