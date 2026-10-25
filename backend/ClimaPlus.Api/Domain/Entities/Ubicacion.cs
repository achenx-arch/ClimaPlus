namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>ubicacion</c>: catálogo compartido de ubicaciones meteorológicas.
/// No pertenece a ningún usuario; se reutiliza por coordenadas (RN-BD-10, RN-BD-12).
/// </summary>
public class Ubicacion
{
    public long IdUbicacion { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Departamento o estado; distingue ciudades homónimas.</summary>
    public string? Region { get; set; }

    /// <summary>Código ISO 3166-1 alfa-2 en mayúsculas.</summary>
    public string Pais { get; set; } = string.Empty;

    /// <summary>Latitud redondeada a 4 decimales (D-07).</summary>
    public decimal Latitud { get; set; }

    /// <summary>Longitud redondeada a 4 decimales (D-07).</summary>
    public decimal Longitud { get; set; }

    public DateTime FechaCreacion { get; set; }

    public ICollection<Favorito> Favoritos { get; set; } = new List<Favorito>();

    public ICollection<ConfiguracionAlerta> ConfiguracionesAlerta { get; set; } = new List<ConfiguracionAlerta>();

    public ICollection<ConsultaClima> ConsultasClima { get; set; } = new List<ConsultaClima>();

    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
