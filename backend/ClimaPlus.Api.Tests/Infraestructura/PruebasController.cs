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
}
