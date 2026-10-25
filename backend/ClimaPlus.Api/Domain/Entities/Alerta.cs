using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>alerta</c>: avisos generados cuando una medición cumple una regla activa (D-02).
/// Tiene exactamente un origen: una consulta ACTUAL (IdConsulta) o una lectura del ESP32 (IdLectura).
/// </summary>
public class Alerta
{
    public long IdAlerta { get; set; }

    public long IdUsuario { get; set; }

    public long IdUbicacion { get; set; }

    public long? IdConsulta { get; set; }

    public long? IdLectura { get; set; }

    public string Tipo { get; set; } = string.Empty;

    /// <summary>Temperatura medida en °C; NULL en alertas de lluvia.</summary>
    public decimal? ValorDetectado { get; set; }

    /// <summary>Umbral vigente al generarse; NULL en alertas de lluvia.</summary>
    public decimal? ValorUmbral { get; set; }

    /// <summary>Texto mostrado al usuario, generado por el backend (D-13).</summary>
    public string Mensaje { get; set; } = string.Empty;

    public string Estado { get; set; } = EstadosAlerta.Nueva;

    public DateTime FechaGeneracion { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Ubicacion Ubicacion { get; set; } = null!;

    public ConsultaClima? Consulta { get; set; }

    public LecturaSensor? Lectura { get; set; }
}
