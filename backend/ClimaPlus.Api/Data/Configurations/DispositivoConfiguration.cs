using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>dispositivo</c> (diccionario de datos, sección 6).</summary>
public class DispositivoConfiguration : IEntityTypeConfiguration<Dispositivo>
{
    public void Configure(EntityTypeBuilder<Dispositivo> builder)
    {
        builder.ToTable("dispositivo");

        builder.HasKey(d => d.IdDispositivo).HasName("pk_dispositivo");

        builder.Property(d => d.IdDispositivo)
            .HasColumnName("id_dispositivo")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(d => d.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(d => d.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(d => d.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(d => d.ApiKeyHash)
            .HasColumnName("api_key_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();

        builder.Property(d => d.Activo)
            .HasColumnName("activo")
            .IsRequired()
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(d => d.UltimaConexion)
            .HasColumnName("ultima_conexion")
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relaciones: primero se desactiva el dispositivo; nunca se borra en cascada.
        builder.HasOne(d => d.Usuario)
            .WithMany(u => u.Dispositivos)
            .HasForeignKey(d => d.IdUsuario)
            .HasConstraintName("fk_dispositivo_usuario")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Ubicacion)
            .WithMany(u => u.Dispositivos)
            .HasForeignKey(d => d.IdUbicacion)
            .HasConstraintName("fk_dispositivo_ubicacion")
            .OnDelete(DeleteBehavior.Restrict);

        // RN-BD-28: autenticación del ESP32 por hash único.
        builder.HasIndex(d => d.ApiKeyHash)
            .IsUnique()
            .HasDatabaseName("uq_dispositivo_api_key_hash");

        // RN-BD-30: nombre único por usuario; cubre también la búsqueda por id_usuario.
        builder.HasIndex(d => new { d.IdUsuario, d.Nombre })
            .IsUnique()
            .HasDatabaseName("uq_dispositivo_usuario_nombre");

        // Índice de la FK id_ubicacion (aceptado en 04-modelo-datos.md).
        builder.HasIndex(d => d.IdUbicacion)
            .HasDatabaseName("ix_dispositivo_id_ubicacion");
    }
}
