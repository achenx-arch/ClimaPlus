-- =====================================================================
-- ClimaPlus – E-02 y E-03: comprobaciones DESPUÉS de aplicar 002 y 003
-- SOLO LECTURA. Comparar con la salida de E02_E03_verificacion_previa.sql.
-- =====================================================================

\echo '1. Migraciones (esperado: InitialCreate, ConsultaClimaUsuarioOpcional, ConsultaClimaDetalle)'
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

\echo '2. id_usuario y detalle (esperado: detalle | jsonb | YES, id_usuario | bigint | YES)'
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = current_schema() AND table_name = 'consulta_clima' AND column_name IN ('id_usuario', 'detalle')
ORDER BY column_name;

\echo '3. Columnas de consulta_clima (esperado: 12)'
SELECT count(*) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'consulta_clima';

\echo '4. CHECK de consulta_clima (esperado: 9, incluidas ck_consulta_clima_detalle_objeto y ck_consulta_clima_detalle_pronostico)'
SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
WHERE conrelid = 'consulta_clima'::regclass AND contype = 'c' ORDER BY conname;

\echo '5. Total de CHECK del esquema (esperado: 26)'
SELECT count(*) AS checks
FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
WHERE c.contype = 'c' AND n.nspname = current_schema();

\echo '6. FK de consulta_clima (sin cambios: RESTRICT)'
SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
WHERE conrelid = 'consulta_clima'::regclass AND contype = 'f' ORDER BY conname;

\echo '7. Tablas de negocio (esperado: las mismas 8)'
SELECT table_name
FROM information_schema.tables
WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
ORDER BY table_name;

\echo '8. Filas por tabla (deben coincidir con la verificación previa)'
SELECT 'usuario' AS tabla, count(*) FROM usuario
UNION ALL SELECT 'ubicacion', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima', count(*) FROM consulta_clima
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor
UNION ALL SELECT 'alerta', count(*) FROM alerta
ORDER BY tabla;
