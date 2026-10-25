using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>alerta</c> (diccionario de datos, sección 8).</summary>
public class AlertaConfiguration : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> builder)
    {
        builder.ToTable("alerta", t =>
        {
            // RN-BD-16
            t.HasCheckConstraint("ck_alerta_tipo",
                $"tipo IN ('{TiposAlerta.Lluvia}', '{TiposAlerta.TemperaturaAlta}', '{TiposAlerta.TemperaturaBaja}')");

            // RN-BD-25
            t.HasCheckConstraint("ck_alerta_estado",
                $"estado IN ('{EstadosAlerta.Nueva}', '{EstadosAlerta.Leida}')");

            // RN-BD-23: exactamente un origen (consulta ACTUAL o lectura del ESP32).
            t.HasCheckConstraint("ck_alerta_origen_unico",
                "num_nonnulls(id_consulta, id_lectura) = 1");

            // RN-BD-24: lluvia sin valores; temperatura con valor detectado y umbral.
            t.HasCheckConstraint("ck_alerta_valores_tipo",
                $"(tipo = '{TiposAlerta.Lluvia}' AND valor_detectado IS NULL AND valor_umbral IS NULL) " +
                $"OR (tipo <> '{TiposAlerta.Lluvia}' AND valor_detectado IS NOT NULL AND valor_umbral IS NOT NULL)");
        });

        builder.HasKey(a => a.IdAlerta).HasName("pk_alerta");

        builder.Property(a => a.IdAlerta)
            .HasColumnName("id_alerta")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(a => a.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(a => a.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(a => a.IdConsulta)
            .HasColumnName("id_consulta")
            .HasColumnType("bigint");

        builder.Property(a => a.IdLectura)
            .HasColumnName("id_lectura")
            .HasColumnType("bigint");

        builder.Property(a => a.Tipo)
            .HasColumnName("tipo")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.ValorDetectado)
            .HasColumnName("valor_detectado")
            .HasPrecision(5, 2);

        builder.Property(a => a.ValorUmbral)
            .HasColumnName("valor_umbral")
            .HasPrecision(5, 2);

        builder.Property(a => a.Mensaje)
            .HasColumnName("mensaje")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.Estado)
            .HasColumnName("estado")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(EstadosAlerta.Nueva);

        builder.Property(a => a.FechaGeneracion)
            .HasColumnName("fecha_generacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relaciones: historial, todas RESTRICT.
        builder.HasOne(a => a.Usuario)
            .WithMany(u => u.Alertas)
            .HasForeignKey(a => a.IdUsuario)
            .HasConstraintName("fk_alerta_usuario")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Ubicacion)
            .WithMany(u => u.Alertas)
            .HasForeignKey(a => a.IdUbicacion)
            .HasConstraintName("fk_alerta_ubicacion")
            .OnDelete(DeleteBehavior.Restrict);

        // SET NULL violaría ck_alerta_origen_unico, por eso RESTRICT.
        builder.HasOne(a => a.Consulta)
            .WithMany(c => c.Alertas)
            .HasForeignKey(a => a.IdConsulta)
            .IsRequired(false)
            .HasConstraintName("fk_alerta_consulta_clima")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Lectura)
            .WithMany(l => l.Alertas)
            .HasForeignKey(a => a.IdLectura)
            .IsRequired(false)
            .HasConstraintName("fk_alerta_lectura_sensor")
            .OnDelete(DeleteBehavior.Restrict);

        // Historial de alertas y verificación del anti-duplicado de 60 minutos (D-09).
        builder.HasIndex(a => new { a.IdUsuario, a.FechaGeneracion })
            .IsDescending(false, true)
            .HasDatabaseName("ix_alerta_usuario_fecha");

        // Índices de las FK restantes (aceptados en 04-modelo-datos.md).
        builder.HasIndex(a => a.IdUbicacion).HasDatabaseName("ix_alerta_id_ubicacion");
        builder.HasIndex(a => a.IdConsulta).HasDatabaseName("ix_alerta_id_consulta");
        builder.HasIndex(a => a.IdLectura).HasDatabaseName("ix_alerta_id_lectura");
    }
}
