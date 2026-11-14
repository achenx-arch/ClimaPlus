using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimaPlus.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Migración 005: el cambio de contraseña desde el perfil exige un código por correo (propósito CAMBIAR_PASSWORD).
    /// Solo reemplaza ck_desafio_autenticacion_proposito para admitir el nuevo propósito, conservando los cinco
    /// anteriores. No toca tablas, columnas, índices ni filas. La columna proposito (varchar(24)) ya admite el valor.
    /// </remarks>
    public partial class CambioPasswordConCodigo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_desafio_autenticacion_proposito",
                table: "desafio_autenticacion");

            migrationBuilder.AddCheckConstraint(
                name: "ck_desafio_autenticacion_proposito",
                table: "desafio_autenticacion",
                sql: "proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD', 'CAMBIAR_PASSWORD')");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Revertir falla (a propósito) si ya existen filas CAMBIAR_PASSWORD: hay que eliminarlas o anularlas antes,
        /// como indica database/scripts/F7_revertir_005.sql.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_desafio_autenticacion_proposito",
                table: "desafio_autenticacion");

            migrationBuilder.AddCheckConstraint(
                name: "ck_desafio_autenticacion_proposito",
                table: "desafio_autenticacion",
                sql: "proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD')");
        }
    }
}
