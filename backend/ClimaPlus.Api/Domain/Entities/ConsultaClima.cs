namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>consulta_clima</c>: registro de todas las consultas de clima (D-03, D-04 modificadas por E-02/E-03).
/// En consultas ACTUAL todas las mediciones son obligatorias; en PRONOSTICO todas son NULL
/// y el pronóstico se guarda en <see cref="Detalle"/>.
/// </summary>
public class ConsultaClima
{
    public long IdConsulta { get; set; }

    /// <summary>
    /// Usuario autenticado que hizo la consulta; NULL = visitante sin sesión (E-02).
    /// Las consultas con NULL nunca aparecen en un historial personal ni generan alertas (RN-BD-42).
    /// </summary>
    public long? IdUsuario { get; set; }

    public long IdUbicacion { get; set; }

    public string TipoConsulta { get; set; } = string.Empty;

    /// <summary>°C; solo en ACTUAL.</summary>
    public decimal? Temperatura { get; set; }

    /// <summary>°C; solo en ACTUAL.</summary>
    public decimal? SensacionTermica { get; set; }

    /// <summary>% (entero de OpenWeather); solo en ACTUAL.</summary>
    public short? Humedad { get; set; }

    /// <summary>m/s; solo en ACTUAL.</summary>
    public decimal? VelocidadViento { get; set; }

    /// <summary>Código de condición de OpenWeather (weather[0].id); solo en ACTUAL.</summary>
    public short? CodigoCondicion { get; set; }

    /// <summary>Descripción en español; solo en ACTUAL.</summary>
    public string? DescripcionCondicion { get; set; }

    /// <summary>
    /// Datos normalizados que no tienen columna propia (E-03), como JSON:
    /// en ACTUAL, los datos extra de M04; en PRONOSTICO, los días y bloques de M05.
    /// Formato definido en la sección 6 de E-02-E-03-especificacion-tecnica.md.
    /// </summary>
    public string? Detalle { get; set; }

    public DateTime FechaConsulta { get; set; }

    public Usuario? Usuario { get; set; }

    public Ubicacion Ubicacion { get; set; } = null!;

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
