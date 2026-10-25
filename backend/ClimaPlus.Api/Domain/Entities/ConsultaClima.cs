namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>consulta_clima</c>: historial de consultas de usuarios autenticados (D-03, D-04).
/// En consultas ACTUAL todas las mediciones son obligatorias; en PRONOSTICO todas son NULL.
/// </summary>
public class ConsultaClima
{
    public long IdConsulta { get; set; }

    public long IdUsuario { get; set; }

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

    public DateTime FechaConsulta { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Ubicacion Ubicacion { get; set; } = null!;

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
