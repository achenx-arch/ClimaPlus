using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>identidad_externa</c> (migración 004).</summary>
public class IdentidadExternaConfiguration : IEntityTypeConfiguration<IdentidadExterna>
{
    public void Configure(EntityTypeBuilder<IdentidadExterna> builder)
    {
        builder.ToTable("identidad_externa", t =>
        {
            t.HasCheckConstraint("ck_identidad_externa_proveedor", $"proveedor IN ('{ProveedoresIdentidad.Google}')");
            t.HasCheckConstraint("ck_identidad_externa_sujeto_no_vacio", "char_length(btrim(sujeto)) > 0");
            t.HasCheckConstraint("ck_identidad_externa_correo_minusculas", "correo_proveedor = lower(correo_proveedor)");
        });

        builder.HasKey(i => i.IdIdentidad).HasName("pk_identidad_externa");

        builder.Property(i => i.IdIdentidad)
            .HasColumnName("id_identidad")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(i => i.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(i => i.Proveedor)
            .HasColumnName("proveedor")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.Sujeto)
            .HasColumnName("sujeto")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(i => i.CorreoProveedor)
            .HasColumnName("correo_proveedor")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(i => i.FechaVinculacion)
            .HasColumnName("fecha_vinculacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne(i => i.Usuario)
            .WithMany(u => u.IdentidadesExternas)
            .HasForeignKey(i => i.IdUsuario)
            .HasConstraintName("fk_identidad_externa_usuario")
            .OnDelete(DeleteBehavior.Cascade);

        // El "sub" identifica a la persona en el proveedor: no puede pertenecer a dos usuarios.
        builder.HasIndex(i => new { i.Proveedor, i.Sujeto })
            .IsUnique()
            .HasDatabaseName("uq_identidad_externa_proveedor_sujeto");

        // Un usuario tiene como máximo una identidad por proveedor; cubre las búsquedas por id_usuario.
        builder.HasIndex(i => new { i.IdUsuario, i.Proveedor })
            .IsUnique()
            .HasDatabaseName("uq_identidad_externa_usuario_proveedor");
    }
}
