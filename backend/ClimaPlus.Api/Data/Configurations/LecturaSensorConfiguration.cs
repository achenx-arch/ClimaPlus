using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>lectura_sensor</c> (diccionario de datos, sección 7).</summary>
public class LecturaSensorConfiguration : IEntityTypeConfiguration<LecturaSensor>
{
    public void Configure(EntityTypeBuilder<LecturaSensor> builder)
    {
        builder.ToTable("lectura_sensor", t =>
        {
            // RN-BD-31
            t.HasCheckConstraint("ck_lectura_sensor_temperatura", "temperatura BETWEEN -40 AND 85");
            t.HasCheckConstraint("ck_lectura_sensor_humedad", "humedad BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_lectura_sensor_luz", "luz BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_lectura_sensor_al_menos_una_medicion",
                "num_nonnulls(temperatura, humedad, lluvia, luz) >= 1");
        });

        builder.HasKey(l => l.IdLectura).HasName("pk_lectura_sensor");

        builder.Property(l => l.IdLectura)
            .HasColumnName("id_lectura")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(l => l.IdDispositivo)
            .HasColumnName("id_dispositivo")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(l => l.Temperatura)
            .HasColumnName("temperatura")
            .HasPrecision(5, 2);

        builder.Property(l => l.Humedad)
            .HasColumnName("humedad")
            .HasPrecision(5, 2);

        builder.Property(l => l.Lluvia)
            .HasColumnName("lluvia");

        builder.Property(l => l.Luz)
            .HasColumnName("luz")
            .HasColumnType("smallint");

        builder.Property(l => l.FechaLectura)
            .HasColumnName("fecha_lectura")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // RN-BD-03: toda lectura pertenece a un dispositivo existente.
        builder.HasOne(l => l.Dispositivo)
            .WithMany(d => d.Lecturas)
            .HasForeignKey(l => l.IdDispositivo)
            .HasConstraintName("fk_lectura_sensor_dispositivo")
            .OnDelete(DeleteBehavior.Restrict);

        // Últimas lecturas de un dispositivo.
        builder.HasIndex(l => new { l.IdDispositivo, l.FechaLectura })
            .IsDescending(false, true)
            .HasDatabaseName("ix_lectura_sensor_dispositivo_fecha");
    }
}
