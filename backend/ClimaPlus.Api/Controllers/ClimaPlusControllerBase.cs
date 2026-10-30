using ClimaPlus.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>Utilidades comunes: IP del cliente y traducción de errores de servicio a ProblemDetails.</summary>
public abstract class ClimaPlusControllerBase : ControllerBase
{
    protected string IpCliente => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    protected IActionResult RespuestaError(ErrorServicio error) => error.Tipo switch
    {
        TipoError.Validacion => ErrorDeCampo(error, StatusCodes.Status400BadRequest, "Datos inválidos"),
        TipoError.Conflicto => ErrorDeCampo(error, StatusCodes.Status409Conflict, "Conflicto"),
        TipoError.CredencialesInvalidas => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Credenciales inválidas", detail: error.Mensaje),
        TipoError.CuentaDesactivada => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cuenta desactivada", detail: error.Mensaje),
        TipoError.NoEncontrado => Problem(statusCode: StatusCodes.Status404NotFound, title: "No encontrado", detail: error.Mensaje),
        TipoError.ServicioNoDisponible => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Servicio no disponible", detail: error.Mensaje),
        TipoError.TiempoAgotado => Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "Tiempo de espera agotado", detail: error.Mensaje),
        TipoError.RespuestaProveedorInvalida => Problem(statusCode: StatusCodes.Status502BadGateway, title: "Respuesta inválida del proveedor", detail: error.Mensaje),
        TipoError.DispositivoNoAutenticado => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Dispositivo no autenticado", detail: error.Mensaje),
        TipoError.DispositivoDesactivado => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Dispositivo desactivado", detail: error.Mensaje),
        TipoError.DemasiadosIntentos => DemasiadosIntentos(error),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private IActionResult DemasiadosIntentos(ErrorServicio error)
    {
        if (error.ReintentarSegundos is { } segundos)
        {
            Response.Headers.RetryAfter = segundos.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Demasiados intentos", detail: error.Mensaje);
    }

    private IActionResult ErrorDeCampo(ErrorServicio error, int estado, string titulo)
    {
        ModelState.AddModelError(error.Campo ?? string.Empty, error.Mensaje);
        return ValidationProblem(detail: error.Mensaje, statusCode: estado, title: titulo, modelStateDictionary: ModelState);
    }
}
