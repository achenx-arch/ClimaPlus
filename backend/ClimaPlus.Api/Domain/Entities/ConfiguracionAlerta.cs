namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>configuracion_alerta</c>: reglas de alerta de cada usuario.
/// El tipo determina la comparación; no existe columna "operador" (D-10).
/// </summary>
public class ConfiguracionAlerta
{
    public long IdConfiguracion { get; set; }

    public long IdUsuario { get; set; }

    /// <summary>NULL = la regla aplica a todas las ubicaciones del usuario.</summary>
    public long? IdUbicacion { get; set; }

    public string Tipo { get; set; } = string.Empty;

    /// <summary>Umbral en °C; NULL cuando el tipo es LLUVIA.</summary>
    public decimal? ValorUmbral { get; set; }

    public bool Activa { get; set; } = true;

    public DateTime FechaCreacion { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Ubicacion? Ubicacion { get; set; }
}
