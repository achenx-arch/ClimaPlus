namespace ClimaPlus.Api.Domain.Entities;

/// <summary>Tabla <c>dispositivo</c>: dispositivos ESP32 registrados por los usuarios.</summary>
public class Dispositivo
{
    public long IdDispositivo { get; set; }

    public long IdUsuario { get; set; }

    public long IdUbicacion { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>SHA-256 en hexadecimal (64 caracteres) de la API Key; nunca la clave (RN-BD-28).</summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    /// <summary>FALSE revoca el acceso del dispositivo (D-11).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Última lectura recibida; NULL si nunca se ha conectado (D-12).</summary>
    public DateTime? UltimaConexion { get; set; }

    public DateTime FechaCreacion { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Ubicacion Ubicacion { get; set; } = null!;

    public ICollection<LecturaSensor> Lecturas { get; set; } = new List<LecturaSensor>();
}
