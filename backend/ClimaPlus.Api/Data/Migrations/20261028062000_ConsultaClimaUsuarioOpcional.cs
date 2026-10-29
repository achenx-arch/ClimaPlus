using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimaPlus.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// E-02: consulta_clima.id_usuario admite NULL (visitante sin sesión).
    /// SQL esperado: ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;
    /// </remarks>
    public partial class ConsultaClimaUsuarioOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "id_usuario",
                table: "consulta_clima",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Reversión solo para desarrollo. Sin valor por defecto a propósito: si ya existen consultas
        /// anónimas, SET NOT NULL falla y no se modifica ningún dato (especificación E-02/E-03, sección 3).
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "id_usuario",
                table: "consulta_clima",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
