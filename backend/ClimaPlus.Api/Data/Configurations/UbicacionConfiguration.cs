using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>ubicacion</c> (diccionario de datos, sección 2).</summary>
public class UbicacionConfiguration : IEntityTypeConfiguration<Ubicacion>
{
    public void Configure(EntityTypeBuilder<Ubicacion> builder)
    {
        builder.ToTable("ubicacion", t =>
        {
            t.HasCheckConstraint("ck_ubicacion_pais", "pais ~ '^[A-Z]{2}$'");
            t.HasCheckConstraint("ck_ubicacion_latitud", "latitud BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_ubicacion_longitud", "longitud BETWEEN -180 AND 180");
        });

        builder.HasKey(u => u.IdUbicacion).HasName("pk_ubicacion");

        builder.Property(u => u.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(u => u.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Region)
            .HasColumnName("region")
            .HasMaxLength(100);

        builder.Property(u => u.Pais)
            .HasColumnName("pais")
            .HasMaxLength(2)
            .IsFixedLength()
            .IsRequired();

        builder.Property(u => u.Latitud)
            .HasColumnName("latitud")
            .HasPrecision(7, 4)
            .IsRequired();

        builder.Property(u => u.Longitud)
            .HasColumnName("longitud")
            .HasPrecision(8, 4)
            .IsRequired();

        builder.Property(u => u.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // RN-BD-10: no se repiten coordenadas (D-07).
        builder.HasIndex(u => new { u.Latitud, u.Longitud })
            .IsUnique()
            .HasDatabaseName("uq_ubicacion_latitud_longitud");
    }
}
