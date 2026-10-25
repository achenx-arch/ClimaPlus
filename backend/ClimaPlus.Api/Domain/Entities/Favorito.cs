namespace ClimaPlus.Api.Domain.Entities;

/// <summary>Tabla <c>favorito</c>: resuelve la relación N:M entre usuario y ubicación.</summary>
public class Favorito
{
    public long IdFavorito { get; set; }

    public long IdUsuario { get; set; }

    public long IdUbicacion { get; set; }

    public DateTime FechaCreacion { get; set; }

    public Usuario Usuario { get; set; } = null!;

    public Ubicacion Ubicacion { get; set; } = null!;
}
