using ClimaPlus.Api.DTOs.Favoritos;
using ClimaPlus.Api.Security;
using ClimaPlus.Api.Services.Favoritos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.Controllers;

/// <summary>
/// Favoritos del usuario autenticado (Fase 2F, M07). Roles USUARIO y ADMINISTRADOR
/// (07-seguridad-inicial.md). El dueño siempre es el usuario del token.
/// </summary>
[ApiController]
[Route("api/favoritos")]
[Authorize]
public sealed class FavoritosController : ClimaPlusControllerBase
{
    private readonly FavoritoService _favoritos;

    public FavoritosController(FavoritoService favoritos)
    {
        _favoritos = favoritos;
    }

    /// <summary>Lista los favoritos del usuario, del más reciente al más antiguo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FavoritoResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var resultado = await _favoritos.ListarAsync(User.ObtenerIdUsuario(), cancelacion);
        return Ok(resultado.Valor);
    }

    [HttpGet("{idFavorito:long}", Name = "ObtenerFavorito")]
    [ProducesResponseType(typeof(FavoritoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idFavorito, CancellationToken cancelacion)
    {
        var resultado = await _favoritos.ObtenerAsync(User.ObtenerIdUsuario(), idFavorito, cancelacion);
        return resultado.Exitoso ? Ok(resultado.Valor) : RespuestaError(resultado.Error!);
    }

    /// <summary>Agrega una ubicación (obtenida de la búsqueda) a los favoritos del usuario.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FavoritoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Agregar([FromBody] CrearFavoritoRequest solicitud, CancellationToken cancelacion)
    {
        var resultado = await _favoritos.AgregarAsync(User.ObtenerIdUsuario(), solicitud, cancelacion);
        return resultado.Exitoso
            ? CreatedAtRoute("ObtenerFavorito", new { idFavorito = resultado.Valor!.IdFavorito }, resultado.Valor)
            : RespuestaError(resultado.Error!);
    }

    /// <summary>Quita una ubicación de los favoritos. La ubicación no se borra del catálogo.</summary>
    [HttpDelete("{idFavorito:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(long idFavorito, CancellationToken cancelacion)
    {
        var resultado = await _favoritos.EliminarAsync(User.ObtenerIdUsuario(), idFavorito, cancelacion);
        return resultado.Exitoso ? NoContent() : RespuestaError(resultado.Error!);
    }
}
