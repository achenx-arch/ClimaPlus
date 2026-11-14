-- =====================================================================
-- ClimaPlus – Migración 004 (autenticación segura): comprobaciones DESPUÉS de aplicarla
-- SOLO LECTURA. Comparar con la salida de M004_verificacion_previa.sql: los puntos 3, 4 y 5
-- deben ser idénticos.
-- =====================================================================

\echo '1. Migraciones (esperado: las 3 anteriores + 20261109180000_AutenticacionSegura)'
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

\echo '2. Tablas (esperado: 10 = 8 de negocio + desafio_autenticacion + identidad_externa)'
SELECT table_name
FROM information_schema.tables
WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
ORDER BY table_name;

\echo '3. Filas por tabla (IGUALES a la verificación previa; las 2 tablas nuevas en 0)'
SELECT 'usuario' AS tabla, count(*) FROM usuario
UNION ALL SELECT 'ubicacion', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima', count(*) FROM consulta_clima
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor
UNION ALL SELECT 'alerta', count(*) FROM alerta
UNION ALL SELECT 'desafio_autenticacion', count(*) FROM desafio_autenticacion
UNION ALL SELECT 'identidad_externa', count(*) FROM identidad_externa
ORDER BY tabla;

\echo '4. Huella de los datos de usuario (IGUAL a la verificación previa: contraseñas, roles y estado intactos)'
SELECT count(*) AS usuarios,
       md5(string_agg(id_usuario || '|' || correo || '|' || password_hash || '|' || rol || '|' || activo, ',' ORDER BY id_usuario)) AS huella
FROM usuario;

\echo '5. Usuarios por rol (igual a la verificación previa)'
SELECT rol, count(*) FROM usuario GROUP BY rol ORDER BY rol;

\echo '6. Columnas nuevas (esperado: todos sin verificar, un sello distinto por usuario, ningún hash NULL)'
SELECT count(*) FILTER (WHERE correo_verificado) AS verificados,
       count(*) FILTER (WHERE NOT correo_verificado) AS sin_verificar,
       count(DISTINCT sello_seguridad) AS sellos_distintos,
       count(*) AS usuarios,
       count(*) FILTER (WHERE password_hash IS NULL) AS sin_password
FROM usuario;

\echo '7. Columnas de usuario afectadas (password_hash YES; correo_verificado y sello_seguridad NO)'
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = current_schema() AND table_name = 'usuario'
  AND column_name IN ('password_hash', 'correo_verificado', 'sello_seguridad')
ORDER BY column_name;

\echo '8. Total de CHECK del esquema (esperado: 38 = 26 + 9 de desafio_autenticacion + 3 de identidad_externa)'
SELECT count(*) AS checks
FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
WHERE c.contype = 'c' AND n.nspname = current_schema();

\echo '9. Índices de las tablas nuevas (esperado: 2 PK + 3 únicos, uno parcial)'
SELECT tablename, indexname, indexdef FROM pg_indexes
WHERE schemaname = current_schema() AND tablename IN ('desafio_autenticacion', 'identidad_externa')
ORDER BY tablename, indexname;
