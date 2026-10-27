using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClimaPlus.Api.Controllers;

/// <summary>Registro, inicio de sesión y sesión actual (M02, M03).</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ClimaPlusControllerBase
{
    private readonly AuthService _auth;
    private readonly UsuarioService _usuarios;

    public AuthController(AuthService auth, UsuarioService usuarios)
    {
        _auth = auth;
        _usuarios = usuarios;
    }

    /// <summary>Crea una cuenta con rol USUARIO.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegistroRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.RegistrarAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso
            ? Created("/api/usuarios/me", resultado.Valor)
            : RespuestaError(resultado.Error!);
    }

    /// <summary>Inicia sesión y devuelve un JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarSesionAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Usuario de la sesión actual (para la navbar y para restaurar la sesión).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancelacion)
    {
        var resultado = await _usuarios.ObtenerAsync(User.ObtenerIdUsuario(), cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
