using ClimaPlus.Api.Domain.Constantes;
using ClimaPlus.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClimaPlus.Api.Data.Configurations;

/// <summary>Tabla <c>desafio_autenticacion</c> (migración 004, docs/api/contrato-autenticacion.md).</summary>
public class DesafioAutenticacionConfiguration : IEntityTypeConfiguration<DesafioAutenticacion>
{
    /// <summary>Estados en los que un desafío sigue vivo (índice único parcial).</summary>
    public const string FiltroVivo = "estado IN ('" + EstadosDesafio.Pendiente + "', '" + EstadosDesafio.Verificado + "')";

    public void Configure(EntityTypeBuilder<DesafioAutenticacion> builder)
    {
        builder.ToTable("desafio_autenticacion", t =>
        {
            t.HasCheckConstraint("ck_desafio_autenticacion_proposito",
                "proposito IN ('" + string.Join("', '", PropositosDesafio.Todos) + "')");
            t.HasCheckConstraint("ck_desafio_autenticacion_estado",
                $"estado IN ('{EstadosDesafio.Pendiente}', '{EstadosDesafio.Verificado}', '{EstadosDesafio.Consumido}', '{EstadosDesafio.Anulado}')");
            t.HasCheckConstraint("ck_desafio_autenticacion_intentos", "intentos BETWEEN 0 AND 5");
            t.HasCheckConstraint("ck_desafio_autenticacion_reenvios", "reenvios BETWEEN 0 AND 5");
            // El código solo puede faltar en VINCULAR_GOOGLE antes de confirmar la contraseña local.
            t.HasCheckConstraint("ck_desafio_autenticacion_codigo",
                $"codigo_hash IS NOT NULL OR proposito = '{PropositosDesafio.VincularGoogle}'");
            t.HasCheckConstraint("ck_desafio_autenticacion_envio", "(codigo_hash IS NULL) = (enviado_en IS NULL)");
            // El sujeto de Google existe si y solo si el desafío es de vinculación.
            t.HasCheckConstraint("ck_desafio_autenticacion_sujeto_externo",
                $"(proposito = '{PropositosDesafio.VincularGoogle}') = (sujeto_externo IS NOT NULL)");
            // VERIFICADO solo existe en la recuperación de contraseña y siempre con su fecha.
            t.HasCheckConstraint("ck_desafio_autenticacion_verificado",
                $"estado <> '{EstadosDesafio.Verificado}' OR (proposito = '{PropositosDesafio.RecuperarPassword}' AND verificado_en IS NOT NULL)");
            t.HasCheckConstraint("ck_desafio_autenticacion_finalizado",
                $"estado NOT IN ('{EstadosDesafio.Consumido}', '{EstadosDesafio.Anulado}') OR finalizado_en IS NOT NULL");
        });

        builder.HasKey(d => d.IdDesafio).HasName("pk_desafio_autenticacion");

        builder.Property(d => d.IdDesafio)
            .HasColumnName("id_desafio")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property(d => d.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint")
            .IsRequired();

        builder.Property(d => d.Proposito)
            .HasColumnName("proposito")
            .HasMaxLength(24)
            .IsRequired();

        // Estado e intentos son tokens de concurrencia: dos peticiones simultáneas con el mismo código no pueden
        // consumir el desafío dos veces (el UPDATE incluye "AND estado = <original> AND intentos = <original>").
        builder.Property(d => d.Estado)
            .HasColumnName("estado")
            .HasMaxLength(12)
            .IsRequired()
            .IsConcurrencyToken();

        builder.Property(d => d.CodigoHash)
            .HasColumnName("codigo_hash")
            .HasMaxLength(64)
            .IsFixedLength();

        builder.Property(d => d.ExpiraEn)
            .HasColumnName("expira_en")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(d => d.Intentos)
            .HasColumnName("intentos")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue((short)0)
            .IsConcurrencyToken();

        builder.Property(d => d.Reenvios)
            .HasColumnName("reenvios")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue((short)0);

        builder.Property(d => d.EnviadoEn)
            .HasColumnName("enviado_en")
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.VerificadoEn)
            .HasColumnName("verificado_en")
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.FinalizadoEn)
            .HasColumnName("finalizado_en")
            .HasColumnType("timestamp with time zone");

        builder.Property(d => d.CreadoEn)
            .HasColumnName("creado_en")
            .HasColumnType("timestamp with time zone")
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(d => d.SujetoExterno)
            .HasColumnName("sujeto_externo")
            .HasMaxLength(255);

        builder.HasOne(d => d.Usuario)
            .WithMany(u => u.DesafiosAutenticacion)
            .HasForeignKey(d => d.IdUsuario)
            .HasConstraintName("fk_desafio_autenticacion_usuario")
            .OnDelete(DeleteBehavior.Cascade);

        // Un solo desafío vivo por usuario y propósito; propósitos distintos conviven.
        // También cubre las búsquedas por id_usuario.
        builder.HasIndex(d => new { d.IdUsuario, d.Proposito })
            .IsUnique()
            .HasFilter(FiltroVivo)
            .HasDatabaseName("uq_desafio_autenticacion_vivo");
    }
}
