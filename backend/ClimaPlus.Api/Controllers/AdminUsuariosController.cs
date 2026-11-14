using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.DTOs.Admin;
using ClimaPlus.Api.DTOs.Comun;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Gestión de usuarios (M14). Solo ADMINISTRADOR: RF-26 listar/filtrar, RF-27 cambiar rol, RF-28 activar/desactivar.
/// Las respuestas no incluyen contraseñas, hashes, sellos, códigos ni tokens. No hay borrado (RN-BD-08).
/// </summary>
[ApiController]
[Route("api/admin/usuarios")]
[Authorize(Roles = Roles.Administrador)]
public sealed class AdminUsuariosController : ClimaPlusControllerBase
{
    private readonly UsuarioAdminService _usuarios;

    public AdminUsuariosController(UsuarioAdminService usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>RF-26: lista paginada (20 por página), más recientes primero. Filtros: búsqueda por nombre o correo, rol y estado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaResponse<UsuarioAdminResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Listar([FromQuery] FiltroUsuariosAdminRequest filtro, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.ListarAsync(filtro, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Datos administrativos de un usuario.</summary>
    [HttpGet("{idUsuario:long}")]
    [ProducesResponseType(typeof(UsuarioAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idUsuario, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.ObtenerAsync(idUsuario, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-27: cambia el rol (USUARIO / ADMINISTRADOR). Cierra las sesiones abiertas del usuario afectado.</summary>
    [HttpPatch("{idUsuario:long}/rol")]
    [ProducesResponseType(typeof(UsuarioAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarRol(long idUsuario, [FromBody] CambiarRolUsuarioRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.CambiarRolAsync(User.ObtenerIdUsuario(), idUsuario, solicitud.Rol!, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>RF-28: activa o desactiva la cuenta. Cierra las sesiones abiertas del usuario afectado.</summary>
    [HttpPatch("{idUsuario:long}/estado")]
    [ProducesResponseType(typeof(UsuarioAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(long idUsuario, [FromBody] CambiarEstadoUsuarioRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _usuarios.CambiarEstadoAsync(User.ObtenerIdUsuario(), idUsuario, solicitud.Activo!.Value, IpCliente, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }
}
