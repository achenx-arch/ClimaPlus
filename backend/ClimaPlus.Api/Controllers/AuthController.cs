using ClimaPlus.Api.DTOs.Auth;
using ClimaPlus.Api.DTOs.Usuarios;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClimaPlus.Api.Controllers;

/// <summary>Registro con verificación de correo, inicio de sesión en dos pasos y sesión actual (M02, M03).</summary>
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

    /// <summary>
    /// Crea una cuenta con rol USUARIO, sin verificar, y envía un código al correo. Responde 202 con el desafío.
    /// Si el correo ya existe responde exactamente igual (no revela qué cuentas existen) y avisa a su dueño.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitRegistro)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegistroRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.RegistrarAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Confirma el correo con el código de 6 dígitos. No inicia sesión: 204 y el usuario va al login.</summary>
    [HttpPost("verificar-correo")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerificarCorreo([FromBody] VerificarCodigoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.VerificarCorreoAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }

    /// <summary>Envía un código nuevo para el desafío (uno cada 60 s, máximo 5). El anterior deja de valer.</summary>
    [HttpPost("reenviar-codigo")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitReenvio)]
    [ProducesResponseType(typeof(RespuestaReenvio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ReenviarCodigo([FromBody] ReenviarCodigoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.ReenviarCodigoAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Paso 1 del inicio de sesión: comprueba correo y contraseña y envía un código al correo. Responde 202 con el
    /// desafío (INICIO_SESION, o VERIFICAR_CORREO si la cuenta aún no verificó su correo). Nunca devuelve el JWT.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarSesionAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Paso 2 del inicio de sesión: valida el código de 6 dígitos y solo entonces devuelve el JWT.</summary>
    [HttpPost("login/verificar")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginVerificar([FromBody] VerificarCodigoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.VerificarInicioSesionAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Recuperación, paso 1: envía un código RECUPERAR_PASSWORD si el correo es de una cuenta activa. Responde siempre
    /// 202 con la misma forma, exista o no la cuenta.
    /// </summary>
    [HttpPost("password/solicitar")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitRecuperacion)]
    [ProducesResponseType(typeof(RespuestaDesafio), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SolicitarRecuperacion([FromBody] SolicitarRecuperacionRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.SolicitarRecuperacionAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? StatusCode(StatusCodes.Status202Accepted, resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Recuperación, paso 2: valida el código y devuelve el permiso de restablecer (10 minutos, un solo uso).</summary>
    [HttpPost("password/verificar")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(typeof(RespuestaPermiso), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerificarRecuperacion([FromBody] VerificarCodigoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.VerificarRecuperacionAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// Recuperación, paso 3: cambia la contraseña con el permiso. Cierra todas las sesiones y avisa por correo.
    /// No inicia sesión.
    /// </summary>
    [HttpPost("password/restablecer")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.RestablecerPasswordAsync(solicitud, IpCliente, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// "Continuar con Google", paso 1: Client ID y nonce de un solo uso para el botón de Google Identity Services.
    /// uso=login (por defecto, sin sesión) o uso=vincular (exige sesión; el nonce queda atado a ese usuario).
    /// 503 si Google no está configurado en el servidor.
    /// </summary>
    [HttpGet("google/iniciar")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitCodigos)]
    [ProducesResponseType(typeof(RespuestaInicioGoogle), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public IActionResult IniciarGoogle([FromQuery] string? uso)
    {
        UsoNonceGoogle tipo;
        long? idUsuario = null;
        switch (uso)
        {
            case null or "" or "login":
                tipo = UsoNonceGoogle.Login;
                break;
            case "vincular":
                if (User.Identity?.IsAuthenticated != true)
                {
                    return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "No autenticado",
                        detail: "Inicia sesión para vincular tu cuenta de Google.");
                }

                tipo = UsoNonceGoogle.Vincular;
                idUsuario = User.ObtenerIdUsuario();
                break;
            default:
                return RespuestaError(new ErrorServicio(TipoError.Validacion, "El parámetro 'uso' debe ser 'login' o 'vincular'.", "uso"));
        }

        var resultado = _auth.IniciarGoogle(tipo, idUsuario);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>
    /// "Continuar con Google", paso 2: valida el ID token de Google y emite el JWT de ClimaPlus (200). Entra a la
    /// cuenta vinculada a ese "sub" o crea una cuenta USUARIO nueva. 409 si el correo ya es de una cuenta de ClimaPlus
    /// sin Google vinculado (no se vincula por correo).
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(PoliticasSeguridad.RateLimitAutenticacion)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IniciarSesionGoogle([FromBody] GoogleLoginRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _auth.IniciarSesionGoogleAsync(solicitud, IpCliente, cancelacion);
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
