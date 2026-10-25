using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ClimaPlus.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ubicacion",
                columns: table => new
                {
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pais = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: false),
                    latitud = table.Column<decimal>(type: "numeric(7,4)", precision: 7, scale: 4, nullable: false),
                    longitud = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ubicacion", x => x.id_ubicacion);
                    table.CheckConstraint("ck_ubicacion_latitud", "latitud BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_ubicacion_longitud", "longitud BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_ubicacion_pais", "pais ~ '^[A-Z]{2}$'");
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                columns: table => new
                {
                    id_usuario = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    rol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "USUARIO"),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario", x => x.id_usuario);
                    table.CheckConstraint("ck_usuario_correo_minusculas", "correo = lower(correo)");
                    table.CheckConstraint("ck_usuario_nombre_no_vacio", "char_length(btrim(nombre)) > 0");
                    table.CheckConstraint("ck_usuario_rol", "rol IN ('USUARIO', 'ADMINISTRADOR')");
                });

            migrationBuilder.CreateTable(
                name: "configuracion_alerta",
                columns: table => new
                {
                    id_configuracion = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor_umbral = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    activa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracion_alerta", x => x.id_configuracion);
                    table.CheckConstraint("ck_configuracion_alerta_tipo", "tipo IN ('LLUVIA', 'TEMPERATURA_ALTA', 'TEMPERATURA_BAJA')");
                    table.CheckConstraint("ck_configuracion_alerta_umbral_rango", "valor_umbral BETWEEN -50 AND 60");
                    table.CheckConstraint("ck_configuracion_alerta_umbral_tipo", "(tipo = 'LLUVIA' AND valor_umbral IS NULL) OR (tipo <> 'LLUVIA' AND valor_umbral IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_configuracion_alerta_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "ubicacion",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_configuracion_alerta_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consulta_clima",
                columns: table => new
                {
                    id_consulta = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: false),
                    tipo_consulta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    temperatura = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    sensacion_termica = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    humedad = table.Column<short>(type: "smallint", nullable: true),
                    velocidad_viento = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    codigo_condicion = table.Column<short>(type: "smallint", nullable: true),
                    descripcion_condicion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecha_consulta = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consulta_clima", x => x.id_consulta);
                    table.CheckConstraint("ck_consulta_clima_codigo_condicion", "codigo_condicion BETWEEN 200 AND 899");
                    table.CheckConstraint("ck_consulta_clima_humedad", "humedad BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_consulta_clima_mediciones_tipo", "(tipo_consulta = 'ACTUAL' AND num_nonnulls(temperatura, sensacion_termica, humedad, velocidad_viento, codigo_condicion, descripcion_condicion) = 6) OR (tipo_consulta = 'PRONOSTICO' AND num_nonnulls(temperatura, sensacion_termica, humedad, velocidad_viento, codigo_condicion, descripcion_condicion) = 0)");
                    table.CheckConstraint("ck_consulta_clima_sensacion_termica", "sensacion_termica BETWEEN -90 AND 70");
                    table.CheckConstraint("ck_consulta_clima_temperatura", "temperatura BETWEEN -90 AND 60");
                    table.CheckConstraint("ck_consulta_clima_tipo_consulta", "tipo_consulta IN ('ACTUAL', 'PRONOSTICO')");
                    table.CheckConstraint("ck_consulta_clima_velocidad_viento", "velocidad_viento >= 0");
                    table.ForeignKey(
                        name: "fk_consulta_clima_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "ubicacion",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_consulta_clima_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispositivo",
                columns: table => new
                {
                    id_dispositivo = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    api_key_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ultima_conexion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispositivo", x => x.id_dispositivo);
                    table.ForeignKey(
                        name: "fk_dispositivo_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "ubicacion",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispositivo_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "favorito",
                columns: table => new
                {
                    id_favorito = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_favorito", x => x.id_favorito);
                    table.ForeignKey(
                        name: "fk_favorito_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "ubicacion",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_favorito_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lectura_sensor",
                columns: table => new
                {
                    id_lectura = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_dispositivo = table.Column<long>(type: "bigint", nullable: false),
                    temperatura = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    humedad = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    lluvia = table.Column<bool>(type: "boolean", nullable: true),
                    luz = table.Column<short>(type: "smallint", nullable: true),
                    fecha_lectura = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lectura_sensor", x => x.id_lectura);
                    table.CheckConstraint("ck_lectura_sensor_al_menos_una_medicion", "num_nonnulls(temperatura, humedad, lluvia, luz) >= 1");
                    table.CheckConstraint("ck_lectura_sensor_humedad", "humedad BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_lectura_sensor_luz", "luz BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_lectura_sensor_temperatura", "temperatura BETWEEN -40 AND 85");
                    table.ForeignKey(
                        name: "fk_lectura_sensor_dispositivo",
                        column: x => x.id_dispositivo,
                        principalTable: "dispositivo",
                        principalColumn: "id_dispositivo",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alerta",
                columns: table => new
                {
                    id_alerta = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_usuario = table.Column<long>(type: "bigint", nullable: false),
                    id_ubicacion = table.Column<long>(type: "bigint", nullable: false),
                    id_consulta = table.Column<long>(type: "bigint", nullable: true),
                    id_lectura = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor_detectado = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    valor_umbral = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    mensaje = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "NUEVA"),
                    fecha_generacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerta", x => x.id_alerta);
                    table.CheckConstraint("ck_alerta_estado", "estado IN ('NUEVA', 'LEIDA')");
                    table.CheckConstraint("ck_alerta_origen_unico", "num_nonnulls(id_consulta, id_lectura) = 1");
                    table.CheckConstraint("ck_alerta_tipo", "tipo IN ('LLUVIA', 'TEMPERATURA_ALTA', 'TEMPERATURA_BAJA')");
                    table.CheckConstraint("ck_alerta_valores_tipo", "(tipo = 'LLUVIA' AND valor_detectado IS NULL AND valor_umbral IS NULL) OR (tipo <> 'LLUVIA' AND valor_detectado IS NOT NULL AND valor_umbral IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_alerta_consulta_clima",
                        column: x => x.id_consulta,
                        principalTable: "consulta_clima",
                        principalColumn: "id_consulta",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alerta_lectura_sensor",
                        column: x => x.id_lectura,
                        principalTable: "lectura_sensor",
                        principalColumn: "id_lectura",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alerta_ubicacion",
                        column: x => x.id_ubicacion,
                        principalTable: "ubicacion",
                        principalColumn: "id_ubicacion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alerta_usuario",
                        column: x => x.id_usuario,
                        principalTable: "usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alerta_id_consulta",
                table: "alerta",
                column: "id_consulta");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_id_lectura",
                table: "alerta",
                column: "id_lectura");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_id_ubicacion",
                table: "alerta",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_usuario_fecha",
                table: "alerta",
                columns: new[] { "id_usuario", "fecha_generacion" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_alerta_id_ubicacion",
                table: "configuracion_alerta",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "uq_configuracion_alerta_usuario_ubicacion_tipo",
                table: "configuracion_alerta",
                columns: new[] { "id_usuario", "id_ubicacion", "tipo" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_consulta_clima_ubicacion_fecha",
                table: "consulta_clima",
                columns: new[] { "id_ubicacion", "fecha_consulta" });

            migrationBuilder.CreateIndex(
                name: "ix_consulta_clima_usuario_fecha",
                table: "consulta_clima",
                columns: new[] { "id_usuario", "fecha_consulta" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_dispositivo_id_ubicacion",
                table: "dispositivo",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "uq_dispositivo_api_key_hash",
                table: "dispositivo",
                column: "api_key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_dispositivo_usuario_nombre",
                table: "dispositivo",
                columns: new[] { "id_usuario", "nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_favorito_id_ubicacion",
                table: "favorito",
                column: "id_ubicacion");

            migrationBuilder.CreateIndex(
                name: "uq_favorito_usuario_ubicacion",
                table: "favorito",
                columns: new[] { "id_usuario", "id_ubicacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lectura_sensor_dispositivo_fecha",
                table: "lectura_sensor",
                columns: new[] { "id_dispositivo", "fecha_lectura" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "uq_ubicacion_latitud_longitud",
                table: "ubicacion",
                columns: new[] { "latitud", "longitud" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_usuario_correo",
                table: "usuario",
                column: "correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta");

            migrationBuilder.DropTable(
                name: "configuracion_alerta");

            migrationBuilder.DropTable(
                name: "favorito");

            migrationBuilder.DropTable(
                name: "consulta_clima");

            migrationBuilder.DropTable(
                name: "lectura_sensor");

            migrationBuilder.DropTable(
                name: "dispositivo");

            migrationBuilder.DropTable(
                name: "ubicacion");

            migrationBuilder.DropTable(
                name: "usuario");
        }
    }
}
