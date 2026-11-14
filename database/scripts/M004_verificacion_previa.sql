-- =====================================================================
-- ClimaPlus – Migración 004 (autenticación segura): comprobaciones ANTES de aplicarla
-- SOLO LECTURA: no modifica datos ni esquema. Guarda la salida para compararla con
-- M004_verificacion_posterior.sql.
-- Ejecutar primero contra la base de pruebas:
--   psql -U <usuario> -d climaplus_test -f database/scripts/M004_verificacion_previa.sql > previa.txt
-- =====================================================================

\echo '1. Migraciones aplicadas (esperado: InitialCreate, ConsultaClimaUsuarioOpcional, ConsultaClimaDetalle)'
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

\echo '2. Tablas (esperado: 8 de negocio; sin desafio_autenticacion ni identidad_externa)'
SELECT table_name
FROM information_schema.tables
WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
ORDER BY table_name;

\echo '3. Filas por tabla (deben ser las mismas después de aplicar la 004)'
SELECT 'usuario' AS tabla, count(*) FROM usuario
UNION ALL SELECT 'ubicacion', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima', count(*) FROM consulta_clima
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor
UNION ALL SELECT 'alerta', count(*) FROM alerta
ORDER BY tabla;

\echo '4. Huella de los datos de usuario que NO deben cambiar (id, correo, hash, rol, activo)'
SELECT count(*) AS usuarios,
       md5(string_agg(id_usuario || '|' || correo || '|' || password_hash || '|' || rol || '|' || activo, ',' ORDER BY id_usuario)) AS huella
FROM usuario;

\echo '5. Usuarios por rol'
SELECT rol, count(*) FROM usuario GROUP BY rol ORDER BY rol;

\echo '6. Total de CHECK del esquema (esperado antes: 26)'
SELECT count(*) AS checks
FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
WHERE c.contype = 'c' AND n.nspname = current_schema();

\echo '7. gen_random_uuid() disponible (PostgreSQL 13 o superior)'
SELECT gen_random_uuid() IS NOT NULL AS disponible, current_setting('server_version') AS version;
