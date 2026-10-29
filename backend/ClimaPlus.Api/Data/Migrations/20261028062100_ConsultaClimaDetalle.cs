using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClimaPlus.Api.Data.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// E-03: columna consulta_clima.detalle JSONB NULL y sus dos CHECK
    /// (ck_consulta_clima_detalle_objeto y ck_consulta_clima_detalle_pronostico, RN-BD-43).
    /// </remarks>
    public partial class ConsultaClimaDetalle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detalle",
                table: "consulta_clima",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_consulta_clima_detalle_objeto",
                table: "consulta_clima",
                sql: "detalle IS NULL OR jsonb_typeof(detalle) = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consulta_clima_detalle_pronostico",
                table: "consulta_clima",
                sql: "tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_consulta_clima_detalle_objeto",
                table: "consulta_clima");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consulta_clima_detalle_pronostico",
                table: "consulta_clima");

            migrationBuilder.DropColumn(
                name: "detalle",
                table: "consulta_clima");
        }
    }
}
