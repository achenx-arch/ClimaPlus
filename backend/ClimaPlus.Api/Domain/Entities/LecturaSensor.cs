namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>lectura_sensor</c>: mediciones enviadas por un ESP32.
/// Cada medición es opcional, pero una lectura debe contener al menos una (RN-BD-31).
/// </summary>
public class LecturaSensor
{
    public long IdLectura { get; set; }

    public long IdDispositivo { get; set; }

    /// <summary>°C.</summary>
    public decimal? Temperatura { get; set; }

    /// <summary>%.</summary>
    public decimal? Humedad { get; set; }

    /// <summary>TRUE si el sensor detecta lluvia (D-08).</summary>
    public bool? Lluvia { get; set; }

    /// <summary>Nivel de luz normalizado 0–100 % (D-08).</summary>
    public short? Luz { get; set; }

    /// <summary>Asignada por el servidor al recibir la lectura (RN-BD-32).</summary>
    public DateTime FechaLectura { get; set; }

    public Dispositivo Dispositivo { get; set; } = null!;

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
