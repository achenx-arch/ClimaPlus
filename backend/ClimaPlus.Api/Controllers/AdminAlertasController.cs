using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.Services.Alertas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Consulta de las alertas de todo el sistema (M15). Solo ADMINISTRADOR y solo lectura:
/// M15 no ofrece acciones sobre las alertas.
/// </summary>
[ApiController]
[Route("api/admin/alertas")]
[Authorize(Roles = Roles.Administrador)]
public sealed class AdminAlertasController : ClimaPlusControllerBase
{
    private readonly AlertaService _alertas;

    public AdminAlertasController(AlertaService alertas)
    {
        _alertas = alertas;
    }

    /// <summary>Lista paginada (20 por página). Filtros: tipo, estado, origen, rango de fechas y usuario.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResponse<AlertaAdminResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar([FromQuery] FiltroAlertasAdminRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _alertas.ListarAdminAsync(filtro, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Detalle de una alerta (modal de M15, con el mensaje completo).</summary>
    [HttpGet("{idAlerta:long}")]
    [ProducesResponseType(typeof(AlertaAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idAlerta, CancellationToken cancelacion)
    {
        var resultado = await _alertas.ObtenerAdminAsync(idAlerta, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
