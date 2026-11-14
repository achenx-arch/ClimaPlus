using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>usuario</c> (diccionario de datos, sección 1).</summary>
public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuario", t =>
        {
            t.HasCheckConstraint("ck_usuario_nombre_no_vacio", "char_length(btrim(nombre)) > 0");
            t.HasCheckConstraint("ck_usuario_correo_minusculas", "correo = lower(correo)");
            t.HasCheckConstraint("ck_usuario_rol",
                $"rol IN ('{Roles.Usuario}', '{Roles.Administrador}')");
        });

        builder.HasKey(u => u.IdUsuario).HasName("pk_usuario");

        builder.Property(u => u.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .UseIdentityByDefaultColumn();

        builder.Property(u => u.Nombre)
            .HasColumnName("nombre")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Correo)
            .HasColumnName("correo")
            .HasMaxLength(150)
            .IsRequired();

        // Migración 004: NULL en cuentas creadas con Google que aún no tienen contraseña local.
        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255);

        builder.Property(u => u.Rol)
            .HasColumnName("rol")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(Roles.Usuario);

        builder.Property(u => u.Activo)
            .HasColumnName("activo")
            .IsRequired()
            .HasDefaultValue(true)
            .HasSentinel(true);

        builder.Property(u => u.FechaCreacion)
            .HasColumnName("fecha_creacion")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Migración 004: las cuentas existentes quedan sin verificar (no hay evidencia de que lo estén).
        builder.Property(u => u.CorreoVerificado)
            .HasColumnName("correo_verificado")
            .IsRequired()
            .HasDefaultValue(false);

        // Migración 004: origen del sello "sv" del JWT; gen_random_uuid() da un valor distinto a cada fila existente.
        builder.Property(u => u.SelloSeguridad)
            .HasColumnName("sello_seguridad")
            .HasColumnType("uuid")
            .IsRequired()
            .HasDefaultValueSql("gen_random_uuid()");

        // RN-BD-01: correo único.
        builder.HasIndex(u => u.Correo)
            .IsUnique()
            .HasDatabaseName("uq_usuario_correo");
    }
}
