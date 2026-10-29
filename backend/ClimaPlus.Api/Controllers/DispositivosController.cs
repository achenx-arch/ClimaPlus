using ClimaPlus.Api.DTOs.Alertas;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.DTOs.Dispositivos;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Alertas;
using ClimaPlus.Api.Services.Dispositivos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Dispositivos ESP32 del usuario autenticado (Fase 7, RF-20 a RF-23, M11 y M12). Roles USUARIO y ADMINISTRADOR
/// (07-seguridad-inicial.md: "Dispositivos propios"). El dueño siempre es el usuario del token.
/// </summary>
[ApiController]
[Route("api/dispositivos")]
[Authorize]
public sealed class DispositivosController : ClimaPlusControllerBase
{
    private readonly DispositivoService _dispositivos;
    private readonly AlertaService _alertas;

    public DispositivosController(DispositivoService dispositivos, AlertaService alertas)
    {
        _dispositivos = dispositivos;
        _alertas = alertas;
    }

    /// <summary>RF-20: lista los dispositivos del usuario con su estado y última lectura.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DispositivoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.ListarAsync(User.ObtenerIdUsuario(), cancelacion);
        return Ok(resultado.Valor);
    }

    /// <summary>RF-23: detalle de un dispositivo propio.</summary>
    [HttpGet("{idDispositivo:long}", Name = "ObtenerDispositivo")]
    [ProducesResponseType(typeof(DispositivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idDispositivo, CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.ObtenerAsync(User.ObtenerIdUsuario(), idDispositivo, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-21: registra un dispositivo. La API Key se devuelve UNA sola vez (RN-BD-28).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(DispositivoRegistradoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar([FromBody] CrearDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.RegistrarAsync(User.ObtenerIdUsuario(), solicitud, cancelacion);
        if (!resultado.Exitoso)
        {
            return RespuestaError(resultado.Error!);
        }

        // La respuesta contiene un secreto: que ningún intermediario la guarde.
        Response.Headers.CacheControl = "no-store";
        return CreatedAtRoute("ObtenerDispositivo", new { idDispositivo = resultado.Valor!.Dispositivo.IdDispositivo }, resultado.Valor);
    }

    /// <summary>RF-22: edita nombre y ubicación.</summary>
    [HttpPut("{idDispositivo:long}")]
    [ProducesResponseType(typeof(DispositivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(long idDispositivo, [FromBody] ActualizarDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.ActualizarAsync(User.ObtenerIdUsuario(), idDispositivo, solicitud, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-22: activa o desactiva. Desactivar revoca de inmediato la API Key (D-11).</summary>
    [HttpPatch("{idDispositivo:long}/estado")]
    [ProducesResponseType(typeof(DispositivoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(long idDispositivo, [FromBody] CambiarEstadoDispositivoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.CambiarEstadoAsync(User.ObtenerIdUsuario(), idDispositivo, solicitud, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-23: lecturas del dispositivo, las más recientes primero (20 por página).</summary>
    [HttpGet("{idDispositivo:long}/lecturas")]
    [ProducesResponseType(typeof(PaginaResponse<LecturaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarLecturas(long idDispositivo, [FromQuery] PaginaDispositivoRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _dispositivos.ListarLecturasAsync(User.ObtenerIdUsuario(), idDispositivo, filtro.Pagina, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-23: alertas originadas por este dispositivo (M12), las más recientes primero.</summary>
    [HttpGet("{idDispositivo:long}/alertas")]
    [ProducesResponseType(typeof(PaginaResponse<AlertaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarAlertas(long idDispositivo, [FromQuery] PaginaDispositivoRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _alertas.ListarPorDispositivoAsync(User.ObtenerIdUsuario(), idDispositivo, filtro.Pagina, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
