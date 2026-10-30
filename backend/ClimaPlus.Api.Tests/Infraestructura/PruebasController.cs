using ClimaPlus.Api.Domain.Constantes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Tests.Infraestructura;

/// <summary>
/// Controlador que solo existe en las pruebas, para comprobar la autorización por rol
/// antes de que existan los endpoints de administración (RF-25 a RF-29).
/// </summary>
[ApiController]
[Route("api/pruebas")]
public sealed class PruebasController : ControllerBase
{
    [HttpGet("solo-admin")]
    [Authorize(Roles = Roles.Administrador)]
    public IActionResult SoloAdministrador() => Ok(new { mensaje = "ok" });

    /// <summary>Fase 8: simula un error inesperado para comprobar que la respuesta 500 no filtra detalles internos.</summary>
    [HttpGet("error")]
    [AllowAnonymous]
    public IActionResult ErrorInterno() => throw new InvalidOperationException("detalle-interno-secreto: cadena de conexion y pila");
}
