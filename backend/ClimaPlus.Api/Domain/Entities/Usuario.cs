using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Domain.Entities;

/// <summary>Tabla <c>usuario</c>: personas registradas en el sistema, con su rol y estado.</summary>
public class Usuario
{
    public long IdUsuario { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Correo de inicio de sesión, guardado en minúsculas (RN-BD-04).</summary>
    public string Correo { get; set; } = string.Empty;

    /// <summary>Hash generado con PasswordHasher; nunca la contraseña (RN-BD-06).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Rol { get; set; } = Roles.Usuario;

    /// <summary>FALSE impide iniciar sesión (baja lógica).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Asignada por la base de datos (DEFAULT now()) si no se establece.</summary>
    public DateTime FechaCreacion { get; set; }

    public ICollection<Favorito> Favoritos { get; set; } = new List<Favorito>();

    public ICollection<ConfiguracionAlerta> ConfiguracionesAlerta { get; set; } = new List<ConfiguracionAlerta>();

    public ICollection<ConsultaClima> ConsultasClima { get; set; } = new List<ConsultaClima>();

    public ICollection<Dispositivo> Dispositivos { get; set; } = new List<Dispositivo>();

    public ICollection<Alerta> Alertas { get; set; } = new List<Alerta>();
}
