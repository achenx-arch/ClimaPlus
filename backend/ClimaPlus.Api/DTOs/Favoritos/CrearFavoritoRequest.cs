using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClimaPlus.Api.DTOs.Favoritos;

/// <summary>
/// POST /api/favoritos. Solo se recibe el id de una ubicación ya guardada (devuelta por
/// GET /api/clima/ubicaciones); nunca nombre ni coordenadas escritos por el cliente (D-22).
/// El usuario se toma del token.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearFavoritoRequest
{
    [Required(ErrorMessage = "Selecciona una ubicación.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "La ubicación no es válida.")]
    public long? IdUbicacion { get; init; }
}
