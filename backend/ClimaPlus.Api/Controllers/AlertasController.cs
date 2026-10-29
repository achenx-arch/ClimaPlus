using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Alertas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Alertas generadas del usuario autenticado (M09, campana de la navbar y M06).
/// Roles USUARIO y ADMINISTRADOR (cada uno ve solo las suyas).
/// </summary>
[ApiController]
[Route("api/alertas")]
[Authorize]
public sealed class AlertasController : ClimaPlusControllerBase
{
    private readonly AlertaService _alertas;

    public AlertasController(AlertaService alertas)
    {
        _alertas = alertas;
    }

    /// <summary>Lista paginada (20 por página), de la más reciente a la más antigua. Filtros: estado y tipo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResponse<AlertaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar([FromQuery] FiltroAlertasRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _alertas.ListarAsync(User.ObtenerIdUsuario(), filtro, cancelacion);
        return Ok(resultado.Valor);
    }

    /// <summary>Cantidad de alertas NUEVA del usuario.</summary>
    [HttpGet("contador")]
    [ProducesResponseType(typeof(ContadorAlertasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Contador(CancellationToken cancelacion)
    {
        var resultado = await _alertas.ContarNuevasAsync(User.ObtenerIdUsuario(), cancelacion);
        return Ok(resultado.Valor);
    }

    /// <summary>Marca la alerta como LEIDA (idempotente).</summary>
    [HttpPatch("{idAlerta:long}/estado")]
    [ProducesResponseType(typeof(AlertaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(long idAlerta, [FromBody] CambiarEstadoAlertaRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _alertas.MarcarLeidaAsync(User.ObtenerIdUsuario(), idAlerta, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
