using ClimaPlus.Api.Domain.Constantes;

namespace ClimaPlus.Api.Domain.Entities;

/// <summary>
/// Tabla <c>identidad_externa</c> (migración 004): vínculo de un usuario con un proveedor externo (Google).
/// El identificador permanente es el "sub" del proveedor, nunca el correo. No se guardan tokens del proveedor.
/// </summary>
public class IdentidadExterna
{
    public long IdIdentidad { get; set; }

    public long IdUsuario { get; set; }

    public string Proveedor { get; set; } = ProveedoresIdentidad.Google;

    /// <summary>Identificador estable del proveedor ("sub" del ID token de Google).</summary>
    public string Sujeto { get; set; } = string.Empty;

    /// <summary>Correo que informó el proveedor al vincular; solo como referencia.</summary>
    public string CorreoProveedor { get; set; } = string.Empty;

    public DateTime FechaVinculacion { get; set; }

    public Usuario Usuario { get; set; } = null!;
}
