using ClimaPlus.Api.DTOs.Clima;

namespace ClimaPlus.Api.DTOs.Favoritos;

/// <summary>Favorito del usuario autenticado (M06, M07).</summary>
public sealed record FavoritoResponse(
    long IdFavorito,
    DateTime FechaCreacion,
    UbicacionResponse Ubicacion,
    UltimaConsultaResponse? UltimaConsulta);

/// <summary>
/// Última consulta ACTUAL que el propio usuario registró para esa ubicación (M06/M07:
/// "última consulta registrada"). Es null si todavía no hay ninguna. Solo lectura: el
/// dashboard no consulta OpenWeather por cada favorito.
/// </summary>
public sealed record UltimaConsultaResponse
{
    public required long IdConsulta { get; init; }

    public required DateTime FechaConsulta { get; init; }

    public decimal? Temperatura { get; init; }

    public short? CodigoCondicion { get; init; }

    public string? Descripcion { get; init; }
}
