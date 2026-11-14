using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Perfil propio (M16 usuario, M17 administrador). Las rutas solo existen con "me":
/// el id se toma del token, nunca de la URL ni del cuerpo.
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize]
public sealed class UsuariosController : ClimaPlusControllerBase
{
    private readonly UsuarioService _usuarios;
    private readonly AuthService _auth;

    public UsuariosController(UsuarioService usuarios, AuthService auth)
    {
        _usuarios = usuarios;
        _auth = auth;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObtenerPerfil(CancellationToken cancelacion)
    {
        var resultado = await _usuarios.ObtenerAsync(User.ObtenerIdUsuario(), cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarPerfil([FromBody] ActualizarPerfilRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.ActualizarAsync(User.ObtenerIdUsuario(), solicitud, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Cambiar la contraseña, paso 1: contraseña actual + nueva + confirmación. Si son correctas envía un código
    /// CAMBIAR_PASSWORD (5 minutos) al correo de la cuenta (202). Todavía no cambia nada.
    /// Sustituye al antiguo PUT /api/usuarios/me/password, que cambiaba la contraseña sin código.
    /// </summary>
    [HttpPost("me/password/cambio")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IniciarCambioPassword([FromBody] CambiarPasswordRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarCambioPasswordAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Cambiar la contraseña, paso 2: código del correo + los mismos datos del paso 1. Solo con un código válido cambia
    /// la contraseña, cierra todas las sesiones (también esta), anula los demás desafíos pendientes y envía un aviso
    /// por correo (204). El cliente debe volver a iniciar sesión.
    /// </summary>
    [HttpPost("me/password/cambio/verificar")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerificarCambioPassword([FromBody] VerificarCambioPasswordRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.VerificarCambioPasswordAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Vincular Google, paso 1: contraseña actual + ID token de Google (nonce de GET /api/auth/google/iniciar?uso=vincular).
    /// Envía un código VINCULAR_GOOGLE al correo de la cuenta (202).
    /// </summary>
    [HttpPost("me/google/vincular")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> VincularGoogle([FromBody] VincularGoogleRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarVinculacionGoogleAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Vincular Google, paso 2: valida el código y crea la vinculación. Cierra las demás sesiones y devuelve una
    /// sesión nueva para este dispositivo (200).
    /// </summary>
    [HttpPost("me/google/vincular/verificar")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerificarVinculacionGoogle([FromBody] VerificarCodigoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.VerificarVinculacionGoogleAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Desvincular Google con la contraseña actual. 409 si la cuenta no tiene contraseña local (primero "Establecer
    /// contraseña"). Cierra las demás sesiones y devuelve una sesión nueva (200).
    /// </summary>
    [HttpPost("me/google/desvincular")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DesvincularGoogle([FromBody] DesvincularGoogleRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.DesvincularGoogleAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Cuentas creadas con Google: envía un código RECUPERAR_PASSWORD al correo de la cuenta (202). Con ese código se
    /// usan POST /api/auth/password/verificar y /restablecer. La sesión sola no basta para crear la contraseña.
    /// 409 si la cuenta ya tiene contraseña local.
    /// </summary>
    [HttpPost("me/password/establecer")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitRecuperacion)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EstablecerPassword(CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarEstablecerPasswordAsync(User.ObtenerIdUsuario(), IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
