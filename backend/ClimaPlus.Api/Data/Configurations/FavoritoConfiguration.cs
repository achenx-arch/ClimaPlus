using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>favorito</c> (diccionario de datos, sección 3).</summary>
public class FavoritoConfiguration : IEntityTypeConfiguration<Favorito>
{
    public void Configure(EntityTypeBuilder<Favorito> builder)
    {
        builder.ToTable("favorito");

        builder.HasKey(f => f.IdFavorito).HasName("pk_favorito");

        builder.Property(f => f.IdFavorito)
            .HasColumnName("id_favorito")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(f => f.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(f => f.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(f => f.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relaciones
        builder.HasOne(f => f.Usuario)
            .WithMany(u => u.Favoritos)
            .HasForeignKey(f => f.IdUsuario)
            .HasConstraintName("fk_favorito_usuario")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Ubicacion)
            .WithMany(u => u.Favoritos)
            .HasForeignKey(f => f.IdUbicacion)
            .HasConstraintName("fk_favorito_ubicacion")
            .OnDelete(DeleteBehavior.Restrict);

        // RN-BD-02: un usuario no guarda dos veces la misma ubicación.
        // También cubre la búsqueda por id_usuario (columna líder).
        builder.HasIndex(f => new { f.IdUsuario, f.IdUbicacion })
            .IsUnique()
            .HasDatabaseName("uq_favorito_usuario_ubicacion");

        // Índice de la FK id_ubicacion (aceptado en 04-modelo-datos.md).
        builder.HasIndex(f => f.IdUbicacion)
            .HasDatabaseName("ix_favorito_id_ubicacion");
    }
}
