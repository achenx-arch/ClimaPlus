using System.ComponentModel.DataAnnotations;
using ClimaPlus.Api.DTOs.Comun;
using Microsoft.AspNetCore.Mvc;

namespace ClimaPlus.Api.DTOs.Historial;

/// <summary>
/// GET /api/historial (M08, RF-11). Filtros aprobados en 08-mockups.md: tipo (Todos / Actual /
/// Pronóstico), ubicación y rango de fechas; tabla paginada de 20 filas. El usuario sale del token.
/// </summary>
public sealed class FiltroHistorialRequest
{
    public const string PatronTipo = "^(ACTUAL|PRONOSTICO)$";

    /// <summary>ACTUAL o PRONOSTICO; omitido = todos.</summary>
    [FromQuery(Name = "tipo")]
    [RegularExpression(PatronTipo, ErrorMessage = "El tipo debe ser ACTUAL o PRONOSTICO.")]
    public string? Tipo { get; set; }

    [FromQuery(Name = "idUbicacion")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "La ubicación no es válida.")]
    public long? IdUbicacion { get; set; }

    /// <summary>Incluido. ISO 8601 con zona (por ejemplo 2026-10-01T00:00:00-06:00).</summary>
    [FromQuery(Name = "desde")]
    public DateTimeOffset? Desde { get; set; }

    /// <summary>Excluido.</summary>
    [FromQuery(Name = "hasta")]
    public DateTimeOffset? Hasta { get; set; }

    [FromQuery(Name = "pagina")]
    [Range(1, Paginacion.PaginaMaxima, ErrorMessage = "La página debe ser un número mayor o igual que 1.")]
    public int Pagina { get; set; } = 1;
}
