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

    public UsuariosController(UsuarioService usuarios)
    {
        _usuarios = usuarios;
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

    [HttpPut("me/password")]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.CambiarPasswordAsync(User.ObtenerIdUsuario(), solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }
}
