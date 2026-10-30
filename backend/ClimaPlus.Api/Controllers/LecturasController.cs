using ClimaPlus.Api.DTOs.Lecturas;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Lecturas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Lecturas enviadas por el ESP32 (Fase 7, RF-24). No usa JWT: el dispositivo se autentica con la cabecera
/// X-Device-Key (07-seguridad-inicial.md, sección 5). El JWT de un usuario NO sirve para enviar lecturas.
/// </summary>
[ApiController]
[Route("api/lecturas")]
[AllowAnonymous]
[AutenticarDispositivo]
[EnableRateLimiting(PoliticasSeguridad.RateLimitDispositivos)]
public sealed class LecturasController : ClimaPlusControllerBase
{
    private readonly LecturaService _lecturas;

    public LecturasController(LecturaService lecturas)
    {
        _lecturas = lecturas;
    }

    /// <summary>
    /// Registra una lectura (temperatura, humedad, lluvia y luz; al menos una). La fecha la asigna el servidor.
    /// Responde las alertas generadas para que el ESP32 active el LED y el buzzer.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(LecturaRegistradaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Registrar([FromBody] LecturaRequest solicitud, CancellationToken cancelacion)
    {
        var dispositivo = (DispositivoAutenticado)HttpContext.Items[AutenticacionDispositivoFilter.ClaveItems]!;
        var resultado = await _lecturas.RegistrarAsync(dispositivo, solicitud, cancelacion);
        if (!resultado.Exitoso)
        {
            return RespuestaError(resultado.Error!);
        }

        // Las lecturas se consultan desde M12 (GET /api/dispositivos/{id}/lecturas).
        return Created($"/api/dispositivos/{dispositivo.IdDispositivo}/lecturas", resultado.Valor);
    }
}
