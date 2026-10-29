using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.DTOs.Historial;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Historial;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Historial de consultas propias (Fase 2G, M08). Roles USUARIO y ADMINISTRADOR (el administrador
/// ve su propia cuenta, 08-mockups.md). El usuario sale siempre del token; no hay ruta por idUsuario.
/// </summary>
[ApiController]
[Route("api/historial")]
[Authorize]
public sealed class HistorialController : ClimaPlusControllerBase
{
    private readonly HistorialService _historial;

    public HistorialController(HistorialService historial)
    {
        _historial = historial;
    }

    /// <summary>Consultas del usuario, de la más reciente a la más antigua, 20 por página.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResponse<ConsultaHistorialResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar([FromQuery] FiltroHistorialRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _historial.ListarAsync(User.ObtenerIdUsuario(), filtro, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Detalle de una consulta propia, con los datos extra (ACTUAL) o los días y bloques (PRONOSTICO).</summary>
    [HttpGet("{idConsulta:long}")]
    [ProducesResponseType(typeof(ConsultaHistorialDetalleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idConsulta, CancellationToken cancelacion)
    {
        var resultado = await _historial.ObtenerAsync(User.ObtenerIdUsuario(), idConsulta, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
