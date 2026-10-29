namespace ClimaPlus.Api.DTOs.Comun;

/// <summary>
/// Paginación aprobada en 08-mockups.md ("Tablas administrativas"): 20 filas por página.
/// </summary>
public static class Paginacion
{
    public const int TamanoPagina = 20;

    /// <summary>Límite de la página solicitada, para que (pagina - 1) * 20 nunca desborde.</summary>
    public const int PaginaMaxima = 100_000;
}

/// <summary>Página de resultados.</summary>
public sealed record PaginaResponse<T>(
    IReadOnlyList<T> Elementos,
    int Pagina,
    int TamanoPagina,
    int TotalElementos,
    int TotalPaginas)
{
    public static PaginaResponse<T> Crear(IReadOnlyList<T> elementos, int pagina, int total) =>
        new(elementos, pagina, Paginacion.TamanoPagina, total, (int)Math.Ceiling(total / (double)Paginacion.TamanoPagina));
}
