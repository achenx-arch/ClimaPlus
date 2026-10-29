using ClimaPlus.Api.DTOs.Clima;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Clima;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Rutas públicas de clima (D-20): búsqueda de ubicaciones (M01), clima actual (M04) y
/// pronóstico (M05). Cada consulta se guarda en consulta_clima antes de responder; con sesión,
/// con el usuario del token, y sin sesión con id_usuario NULL (E-02). D-21: token inválido → 401.
/// </summary>
[ApiController]
[Route("api/clima")]
[AllowAnonymous]
[RechazarTokenInvalido]
[EnableRateLimiting(PoliticasSeguridad.RateLimitClima)]
public sealed class ClimaController : ClimaPlusControllerBase
{
    private readonly UbicacionService _ubicaciones;
    private readonly ClimaService _clima;

    public ClimaController(UbicacionService ubicaciones, ClimaService clima)
    {
        _ubicaciones = ubicaciones;
        _clima = clima;
    }

    /// <summary>Usuario del token, o null para un visitante (E-02).</summary>
    private long? UsuarioOpcional => User.Identity?.IsAuthenticated == true ? User.ObtenerIdUsuario() : null;

    /// <summary>Busca ciudades por nombre. Devuelve ubicaciones ya guardadas en la base de datos.</summary>
    [HttpGet("ubicaciones")]
    [ProducesResponseType(typeof(IReadOnlyList<UbicacionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> BuscarUbicaciones([FromQuery] BusquedaUbicacionRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _ubicaciones.BuscarAsync(solicitud.Q!, solicitud.Limite, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Clima actual de una ubicación guardada. La consulta queda registrada antes de responder.</summary>
    [HttpGet("actual")]
    [ProducesResponseType(typeof(ClimaActualResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> ObtenerActual([FromQuery] ConsultaClimaRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _clima.ObtenerActualAsync(solicitud.IdUbicacion!.Value, UsuarioOpcional, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Pronóstico de 5 días (bloques de 3 h agrupados por fecha local, D-25). La consulta queda registrada.</summary>
    [HttpGet("pronostico")]
    [ProducesResponseType(typeof(PronosticoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> ObtenerPronostico([FromQuery] ConsultaClimaRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _clima.ObtenerPronosticoAsync(solicitud.IdUbicacion!.Value, UsuarioOpcional, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
