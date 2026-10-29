using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Clima;

/// <summary>Parámetros de GET /api/clima/actual y GET /api/clima/pronostico (D-20).</summary>
public sealed class ConsultaClimaRequest
{
    /// <summary>Id devuelto por GET /api/clima/ubicaciones (o por un favorito).</summary>
    [FromQuery(Name = "idUbicacion")]
    [Required(ErrorMessage = "Indica la ubicación.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "La ubicación no es válida.")]
    public long? IdUbicacion { get; set; }
}
