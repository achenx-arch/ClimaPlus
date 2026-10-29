using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>consulta_clima</c> (diccionario de datos, sección 5).</summary>
public class ConsultaClimaConfiguration : IEntityTypeConfiguration<ConsultaClima>
{
    private const string Mediciones =
        "temperatura, sensacion_termica, humedad, velocidad_viento, codigo_condicion, descripcion_condicion";

    public void Configure(EntityTypeBuilder<ConsultaClima> builder)
    {
        builder.ToTable("consulta_clima", t =>
        {
            t.HasCheckConstraint("ck_consulta_clima_tipo_consulta",
                $"tipo_consulta IN ('{TiposConsulta.Actual}', '{TiposConsulta.Pronostico}')");

            // RN-BD-14: rangos (un CHECK con valor NULL se cumple, por eso aplican solo a ACTUAL).
            t.HasCheckConstraint("ck_consulta_clima_temperatura", "temperatura BETWEEN -90 AND 60");
            t.HasCheckConstraint("ck_consulta_clima_sensacion_termica", "sensacion_termica BETWEEN -90 AND 70");
            t.HasCheckConstraint("ck_consulta_clima_humedad", "humedad BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_consulta_clima_velocidad_viento", "velocidad_viento >= 0");
            t.HasCheckConstraint("ck_consulta_clima_codigo_condicion", "codigo_condicion BETWEEN 200 AND 899");

            // RN-BD-36 (D-03): ACTUAL exige las 6 mediciones; PRONOSTICO no guarda ninguna.
            t.HasCheckConstraint("ck_consulta_clima_mediciones_tipo",
                $"(tipo_consulta = '{TiposConsulta.Actual}' AND num_nonnulls({Mediciones}) = 6) " +
                $"OR (tipo_consulta = '{TiposConsulta.Pronostico}' AND num_nonnulls({Mediciones}) = 0)");

            // E-03: detalle es un objeto JSON cuando existe.
            t.HasCheckConstraint("ck_consulta_clima_detalle_objeto",
                "detalle IS NULL OR jsonb_typeof(detalle) = 'object'");

            // E-03 / RN-BD-43: toda consulta PRONOSTICO guarda su pronóstico en detalle.
            t.HasCheckConstraint("ck_consulta_clima_detalle_pronostico",
                $"tipo_consulta <> '{TiposConsulta.Pronostico}' OR detalle IS NOT NULL");
        });

        builder.HasKey(c => c.IdConsulta).HasName("pk_consulta_clima");

        builder.Property(c => c.IdConsulta)
            .HasColumnName("id_consulta")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        // E-02: NULL = visitante sin sesión.
        builder.Property(c => c.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint");

        builder.Property(c => c.IdUbicacion)
            .HasColumnName("id_ubicacion")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(c => c.TipoConsulta)
            .HasColumnName("tipo_consulta")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Temperatura)
            .HasColumnName("temperatura")
            .HasPrecision(5, 2);

        builder.Property(c => c.SensacionTermica)
            .HasColumnName("sensacion_termica")
            .HasPrecision(5, 2);

        builder.Property(c => c.Humedad)
            .HasColumnName("humedad")
            .HasColumnType("smallint");

        builder.Property(c => c.VelocidadViento)
            .HasColumnName("velocidad_viento")
            .HasPrecision(5, 2);

        builder.Property(c => c.CodigoCondicion)
            .HasColumnName("codigo_condicion")
            .HasColumnType("smallint");

        builder.Property(c => c.DescripcionCondicion)
            .HasColumnName("descripcion_condicion")
            .HasMaxLength(100);

        // E-03: datos normalizados en JSON (sección 6 de la especificación).
        builder.Property(c => c.Detalle)
            .HasColumnName("detalle")
            .HasColumnType("jsonb");

        builder.Property(c => c.FechaConsulta)
            .HasColumnName("fecha_consulta")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Relaciones: historial, nunca se borra en cascada.
        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.ConsultasClima)
            .HasForeignKey(c => c.IdUsuario)
            .IsRequired(false)
            .HasConstraintName("fk_consulta_clima_usuario")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Ubicacion)
            .WithMany(u => u.ConsultasClima)
            .HasForeignKey(c => c.IdUbicacion)
            .HasConstraintName("fk_consulta_clima_ubicacion")
            .OnDelete(DeleteBehavior.Restrict);

        // Historial paginado del usuario.
        builder.HasIndex(c => new { c.IdUsuario, c.FechaConsulta })
            .IsDescending(false, true)
            .HasDatabaseName("ix_consulta_clima_usuario_fecha");

        // Estadísticas por ubicación y periodo (reemplaza el índice simple de la FK).
        builder.HasIndex(c => new { c.IdUbicacion, c.FechaConsulta })
            .HasDatabaseName("ix_consulta_clima_ubicacion_fecha");
    }
}
