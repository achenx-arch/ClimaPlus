-- =====================================================================
-- ClimaPlus – Fase 7, Etapa 2: verificación de SOLO LECTURA (para la copia climaplus_db_ensayo y, como contraste,
-- para climaplus_db y climaplus_test). No escribe nada. Los correos salen enmascarados (ab***@dominio): la salida se
-- puede compartir. Nunca muestra contraseñas, hashes, códigos, desafíos ni tokens.
--
-- Uso (PowerShell):
--   $env:PGOPTIONS = "-c default_transaction_read_only=on"
--   psql -X -U postgres -d climaplus_db_ensayo -v correo=tu.correo@dominio.com -f database\scripts\F7_E2_verificar_ensayo.sql
--   Remove-Item Env:PGOPTIONS
-- El parámetro correo es opcional (sin él, la sección 4 queda vacía).
-- =====================================================================

\set QUIET 1
\pset pager off
\if :{?correo}
\else
  \set correo ''
\endif
SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL AS tiene_004 \gset

\qecho '== 1. Base y sesión'
SELECT current_database() AS base, current_setting('default_transaction_read_only') AS solo_lectura, now()::timestamp(0) AS fecha;

\qecho '== 2. Migraciones registradas'
SELECT "MigrationId" AS migracion FROM "__EFMigrationsHistory" ORDER BY 1;

\qecho '== 3. Estructuras de la 004'
SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL AS desafio_autenticacion,
       to_regclass('public.identidad_externa') IS NOT NULL AS identidad_externa,
       EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'correo_verificado') AS usuario_correo_verificado,
       EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'sello_seguridad') AS usuario_sello_seguridad;

\qecho '== 4. Cuenta indicada (-v correo=...)'
\if :tiene_004
SELECT id_usuario, left(correo, 2) || '***@' || split_part(correo, '@', 2) AS correo, rol, activo,
       correo_verificado, (password_hash IS NOT NULL) AS con_password,
       (SELECT count(*) FROM identidad_externa i WHERE i.id_usuario = u.id_usuario) AS google,
       fecha_creacion::timestamp(0) AS creada
FROM usuario u WHERE correo = lower(btrim(:'correo'));
\else
SELECT id_usuario, left(correo, 2) || '***@' || split_part(correo, '@', 2) AS correo, rol, activo, fecha_creacion::timestamp(0) AS creada
FROM usuario WHERE correo = lower(btrim(:'correo'));
\endif

\qecho '== 5. Cuentas y datos dependientes (correos enmascarados)'
SELECT u.id_usuario, left(u.correo, 2) || '***@' || split_part(u.correo, '@', 2) AS correo, u.rol, u.activo,
       (SELECT count(*) FROM favorito f WHERE f.id_usuario = u.id_usuario) AS favoritos,
       (SELECT count(*) FROM configuracion_alerta c WHERE c.id_usuario = u.id_usuario) AS reglas,
       (SELECT count(*) FROM consulta_clima q WHERE q.id_usuario = u.id_usuario) AS consultas,
       (SELECT count(*) FROM alerta a WHERE a.id_usuario = u.id_usuario) AS alertas,
       (SELECT count(*) FROM dispositivo d WHERE d.id_usuario = u.id_usuario) AS dispositivos,
       (SELECT count(*) FROM lectura_sensor l JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo WHERE d.id_usuario = u.id_usuario) AS lecturas
FROM usuario u ORDER BY u.id_usuario;

\qecho '== 6. Totales por tabla'
SELECT 'usuario' AS tabla, count(*) AS filas FROM usuario
UNION ALL SELECT 'usuario ADMINISTRADOR activo', count(*) FROM usuario WHERE rol = 'ADMINISTRADOR' AND activo
UNION ALL SELECT 'ubicacion', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima (con usuario)', count(*) FROM consulta_clima WHERE id_usuario IS NOT NULL
UNION ALL SELECT 'consulta_clima (anónimas)', count(*) FROM consulta_clima WHERE id_usuario IS NULL
UNION ALL SELECT 'alerta', count(*) FROM alerta
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor;
