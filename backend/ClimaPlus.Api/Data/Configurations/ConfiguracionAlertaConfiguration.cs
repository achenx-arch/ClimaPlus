using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>configuracion_alerta</c> (diccionario de datos, sección 4).</summary>
public class ConfiguracionAlertaConfiguration : IEntityTypeConfiguration<ConfiguracionAlerta>
{
    public void Configure(EntityTypeBuilder<ConfiguracionAlerta> builder)
    {
        builder.ToTable("configuracion_alerta", t =>
        {
            // RN-BD-16
            t.HasCheckConstraint("ck_configuracion_alerta_tipo",
                $"tipo IN ('{TiposAlerta.Lluvia}', '{TiposAlerta.TemperaturaAlta}', '{TiposAlerta.TemperaturaBaja}')");

            // RN-BD-18
            t.HasCheckConstraint("ck_configuracion_alerta_umbral_rango",
                "valor_umbral BETWEEN -50 AND 60");

            // RN-BD-17: LLUVIA no admite umbral; las de temperatura lo requieren.
            t.HasCheckConstraint("ck_configuracion_alerta_umbral_tipo",
                $"(tipo = '{TiposAlerta.Lluvia}' AND valor_umbral IS NULL) " +
                $"OR (tipo <> '{TiposAlerta.Lluvia}' AND valor_umbral IS NOT NULL)");
        });

        builder.HasKey(c => c.IdConfiguracion).HasName("pk_configuracion_alerta");

        builder.Property(c => c.IdConfiguracion)
            .HasColumnName("id_configuracion")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(c => c.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(c => c.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint");

        builder.Property(c => c.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ValorUmbral)
            .HasColumnName("valor_umbral")
            .HasPrecision(5, 2);

        builder.Property(c => c.Activa)
            .HasColumnName("activa")
            .IsRequired()
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(c => c.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relaciones
        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.ConfiguracionesAlerta)
            .HasForeignKey(c => c.IdUsuario)
            .HasConstraintName("fk_configuracion_alerta_usuario")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Ubicacion)
            .WithMany(u => u.ConfiguracionesAlerta)
            .HasForeignKey(c => c.IdUbicacion)
            .IsRequired(false)
            .HasConstraintName("fk_configuracion_alerta_ubicacion")
            .OnDelete(DeleteBehavior.Restrict);

        // RN-BD-19: una regla por usuario + ubicación (o global) + tipo.
        // NULLS NOT DISTINCT hace que dos reglas globales (id_ubicacion NULL) del mismo tipo choquen.
        builder.HasIndex(c => new { c.IdUsuario, c.IdUbicacion, c.Tipo })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasDatabaseName("uq_configuracion_alerta_usuario_ubicacion_tipo");

        // Índice de la FK id_ubicacion (aceptado en 04-modelo-datos.md).
        builder.HasIndex(c => c.IdUbicacion)
            .HasDatabaseName("ix_configuracion_alerta_id_ubicacion");
    }
}
