using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Alertas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Reglas de alerta del usuario autenticado (Fase 2H, M10). Roles USUARIO y ADMINISTRADOR
/// (el administrador las usa para su propia cuenta). El dueño siempre es el usuario del token.
/// </summary>
[ApiController]
[Route("api/configuraciones-alerta")]
[Authorize]
public sealed class ConfiguracionesAlertaController : ClimaPlusControllerBase
{
    private readonly ConfiguracionAlertaService _reglas;

    public ConfiguracionesAlertaController(ConfiguracionAlertaService reglas)
    {
        _reglas = reglas;
    }

    /// <summary>Lista las reglas del usuario, de la más reciente a la más antigua.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConfiguracionAlertaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var resultado = await _reglas.ListarAsync(User.ObtenerIdUsuario(), cancelacion);
        return Ok(resultado.Valor);
    }

    [HttpGet("{idConfiguracion:long}", Name = "ObtenerConfiguracionAlerta")]
    [ProducesResponseType(typeof(ConfiguracionAlertaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idConfiguracion, CancellationToken cancelacion)
    {
        var resultado = await _reglas.ObtenerAsync(User.ObtenerIdUsuario(), idConfiguracion, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfiguracionAlertaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CrearConfiguracionAlertaRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _reglas.CrearAsync(User.ObtenerIdUsuario(), solicitud, cancelacion);
        return resultado.Exitoso
            ? CreatedAtRoute("ObtenerConfiguracionAlerta", new { idConfiguracion = resultado.Valor!.IdConfiguracion }, resultado.Valor)
            : RespuestaError(resultado.Error!);
    }

    /// <summary>Edita la regla (reemplaza tipo, ubicación, umbral y estado).</summary>
    [HttpPut("{idConfiguracion:long}")]
    [ProducesResponseType(typeof(ConfiguracionAlertaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(long idConfiguracion, [FromBody] ActualizarConfiguracionAlertaRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _reglas.ActualizarAsync(User.ObtenerIdUsuario(), idConfiguracion, solicitud, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Activa o pausa la regla (switch de M10).</summary>
    [HttpPatch("{idConfiguracion:long}/estado")]
    [ProducesResponseType(typeof(ConfiguracionAlertaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(long idConfiguracion, [FromBody] CambiarEstadoConfiguracionRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _reglas.CambiarEstadoAsync(User.ObtenerIdUsuario(), idConfiguracion, solicitud.Activa!.Value, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Elimina la regla. Las alertas ya generadas se conservan.</summary>
    [HttpDelete("{idConfiguracion:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(long idConfiguracion, CancellationToken cancelacion)
    {
        var resultado = await _reglas.EliminarAsync(User.ObtenerIdUsuario(), idConfiguracion, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }
}
