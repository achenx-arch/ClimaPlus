-- =====================================================================
-- ClimaPlus – Migración 005 (cambio de contraseña con código): verificación ANTES y DESPUÉS
-- SOLO LECTURA: no modifica datos ni esquema. Se ejecuta igual antes y después de aplicar la 005 y las dos salidas
-- se comparan (F7_aplicar_005.ps1 lo hace automáticamente):
--   - Secciones 2, 3, 4, 5 y 7 deben ser IDÉNTICAS (la 005 no toca filas, relaciones ni otras restricciones).
--   - Sección 1: después aparece 20261113150000_CambioPasswordConCodigo (5 migraciones).
--   - Sección 6: después la restricción incluye 'CAMBIAR_PASSWORD' y conserva los 5 propósitos anteriores.
-- No muestra correos, contraseñas ni códigos: solo conteos y huellas md5.
-- Uso manual:
--   psql -X -A -t -U postgres -d climaplus_test -v ON_ERROR_STOP=1 -f database/scripts/M005_verificacion.sql
-- =====================================================================

\echo '1. Migraciones'
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";

\echo '2. Filas por tabla'
SELECT 'alerta' || '=' || count(*) FROM alerta
UNION ALL SELECT 'configuracion_alerta=' || count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima=' || count(*) FROM consulta_clima
UNION ALL SELECT 'desafio_autenticacion=' || count(*) FROM desafio_autenticacion
UNION ALL SELECT 'dispositivo=' || count(*) FROM dispositivo
UNION ALL SELECT 'favorito=' || count(*) FROM favorito
UNION ALL SELECT 'identidad_externa=' || count(*) FROM identidad_externa
UNION ALL SELECT 'lectura_sensor=' || count(*) FROM lectura_sensor
UNION ALL SELECT 'ubicacion=' || count(*) FROM ubicacion
UNION ALL SELECT 'usuario=' || count(*) FROM usuario;

\echo '3. Huella completa de cada tabla (todas las columnas de todas las filas, incluidas las relaciones)'
SELECT 'alerta=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM alerta t
UNION ALL SELECT 'configuracion_alerta=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM configuracion_alerta t
UNION ALL SELECT 'consulta_clima=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM consulta_clima t
UNION ALL SELECT 'desafio_autenticacion=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM desafio_autenticacion t
UNION ALL SELECT 'dispositivo=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM dispositivo t
UNION ALL SELECT 'favorito=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM favorito t
UNION ALL SELECT 'identidad_externa=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM identidad_externa t
UNION ALL SELECT 'lectura_sensor=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM lectura_sensor t
UNION ALL SELECT 'ubicacion=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM ubicacion t
UNION ALL SELECT 'usuario=' || coalesce(md5(string_agg(t::text, E'\n' ORDER BY t::text)), 'vacia') FROM usuario t;

\echo '4. Usuarios por rol, estado y verificación'
SELECT rol || '|activo=' || activo || '|verificado=' || correo_verificado || '|' || count(*)
FROM usuario GROUP BY rol, activo, correo_verificado ORDER BY 1;

\echo '5. Restricciones e índices del esquema (por tipo)'
SELECT 'restriccion_' || c.contype::text || '=' || count(*)
FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
WHERE n.nspname = current_schema() GROUP BY c.contype
UNION ALL
SELECT 'indices=' || count(*) FROM pg_indexes WHERE schemaname = current_schema()
ORDER BY 1;

\echo '6. Restricción de propósitos de desafio_autenticacion'
SELECT pg_get_constraintdef(oid) FROM pg_constraint
WHERE conname = 'ck_desafio_autenticacion_proposito' AND conrelid = 'desafio_autenticacion'::regclass;

\echo '7. Desafíos por propósito y estado'
SELECT proposito || '|' || estado || '|' || count(*) FROM desafio_autenticacion GROUP BY proposito, estado ORDER BY 1;
