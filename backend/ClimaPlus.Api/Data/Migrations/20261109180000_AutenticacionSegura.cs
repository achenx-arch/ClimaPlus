using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClimaPlus.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Migración 004 – Autenticación segura (diseño aprobado el 2026-11-09):
    /// - usuario: correo_verificado (false en las cuentas existentes), sello_seguridad (nuevo origen del sello "sv"
    ///   del JWT; cierra una vez todas las sesiones abiertas) y password_hash NULL para cuentas solo de Google.
    /// - desafio_autenticacion: códigos de 6 dígitos por usuario y propósito (solo su HMAC).
    /// - identidad_externa: vínculo con Google por su "sub".
    /// No borra ni modifica datos existentes. El Down no puede volver a poner password_hash NOT NULL si ya hay
    /// cuentas sin contraseña local: revertir requiere un respaldo previo (docs/despliegue/notas-version-autenticacion.md).
    /// </remarks>
    public partial class AutenticacionSegura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "usuario",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<bool>(
                name: "correo_verificado",
                table: "usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "sello_seguridad",
                table: "usuario",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateTable(
                name: "desafio_autenticacion",
                columns: table => new
                {
                    id_desafio = table.Column<Guid>(type: "uuid", nullable: false),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    proposito = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    estado = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    codigo_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    expira_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    intentos = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    reenvios = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    enviado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    verificado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finalizado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    creado_en = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    sujeto_externo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_desafio_autenticacion", x => x.id_desafio);
                    table.CheckConstraint("ck_desafio_autenticacion_codigo", "codigo_hash IS NOT NULL OR proposito = 'VINCULAR_GOOGLE'");
                    table.CheckConstraint("ck_desafio_autenticacion_envio", "(codigo_hash IS NULL) = (enviado_en IS NULL)");
                    table.CheckConstraint("ck_desafio_autenticacion_estado", "estado IN ('PENDIENTE', 'VERIFICADO', 'CONSUMIDO', 'ANULADO')");
                    table.CheckConstraint("ck_desafio_autenticacion_finalizado", "estado NOT IN ('CONSUMIDO', 'ANULADO') OR finalizado_en IS NOT NULL");
                    table.CheckConstraint("ck_desafio_autenticacion_intentos", "intentos BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_desafio_autenticacion_proposito", "proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD')");
                    table.CheckConstraint("ck_desafio_autenticacion_reenvios", "reenvios BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_desafio_autenticacion_sujeto_externo", "(proposito = 'VINCULAR_GOOGLE') = (sujeto_externo IS NOT NULL)");
                    table.CheckConstraint("ck_desafio_autenticacion_verificado", "estado <> 'VERIFICADO' OR (proposito = 'RECUPERAR_PASSWORD' AND verificado_en IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_desafio_autenticacion_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identidad_externa",
                columns: table => new
                {
                    id_identidad = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    proveedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sujeto = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    correo_proveedor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    fecha_vinculacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identidad_externa", x => x.id_identidad);
                    table.CheckConstraint("ck_identidad_externa_correo_minusculas", "correo_proveedor = lower(correo_proveedor)");
                    table.CheckConstraint("ck_identidad_externa_proveedor", "proveedor IN ('GOOGLE')");
                    table.CheckConstraint("ck_identidad_externa_sujeto_no_vacio", "char_length(btrim(sujeto)) > 0");
                    table.ForeignKey(
                        name: "fk_identidad_externa_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_desafio_autenticacion_vivo",
                table: "desafio_autenticacion",
                columns: new[] { "id_usuario", "proposito" },
                unique: true,
                filter: "estado IN ('PENDIENTE', 'VERIFICADO')");

            migrationBuilder.CreateIndex(
                name: "uq_identidad_externa_proveedor_sujeto",
                table: "identidad_externa",
                columns: new[] { "proveedor", "sujeto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_identidad_externa_usuario_proveedor",
                table: "identidad_externa",
                columns: new[] { "id_usuario", "proveedor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "desafio_autenticacion");

            migrationBuilder.DropTable(
                name: "identidad_externa");

            migrationBuilder.DropColumn(
                name: "correo_verificado",
                table: "usuario");

            migrationBuilder.DropColumn(
                name: "sello_seguridad",
                table: "usuario");

            // Sin defaultValue a propósito: EF rellenaría con '' las cuentas solo de Google. Así, si existen cuentas
            // sin contraseña local, el SET NOT NULL falla y nada se revierte a medias (requiere respaldo previo).
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "usuario",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
