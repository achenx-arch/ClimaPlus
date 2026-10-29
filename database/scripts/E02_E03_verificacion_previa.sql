-- =====================================================================
-- ClimaPlus – E-02 y E-03: comprobaciones ANTES de aplicar 002 y 003
-- SOLO LECTURA: no modifica datos ni esquema.
-- Ejecutar: psql -U <usuario> -d climaplus_db -f database/scripts/E02_E03_verificacion_previa.sql
-- =====================================================================

\echo '1. Migraciones aplicadas (esperado: solo 20261016052608_InitialCreate)'
SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

\echo '2. Tablas de negocio (esperado: 8)'
SELECT table_name
FROM information_schema.tables
WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name <> '__EFMigrationsHistory'
ORDER BY table_name;

\echo '3. Filas por tabla (para comparar después de aplicar: deben ser las mismas)'
SELECT 'usuario' AS tabla, count(*) FROM usuario
UNION ALL SELECT 'ubicacion', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima', count(*) FROM consulta_clima
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor
UNION ALL SELECT 'alerta', count(*) FROM alerta
ORDER BY tabla;

\echo '4. Consultas PRONOSTICO existentes (esperado: 0; si hay alguna, la CHECK de 003 no se podría aplicar)'
SELECT count(*) AS pronosticos FROM consulta_clima WHERE tipo_consulta = 'PRONOSTICO';

\echo '5. Columnas actuales de consulta_clima (esperado: 11; id_usuario NOT NULL; sin detalle)'
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = current_schema() AND table_name = 'consulta_clima'
ORDER BY ordinal_position;

\echo '6. CHECK actuales de consulta_clima (esperado: 7)'
SELECT conname FROM pg_constraint WHERE conrelid = 'consulta_clima'::regclass AND contype = 'c' ORDER BY conname;

\echo '7. Total de CHECK del esquema (esperado antes: 24)'
SELECT count(*) AS checks
FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
WHERE c.contype = 'c' AND n.nspname = current_schema();

\echo '8. Versión del servidor'
SELECT version();
