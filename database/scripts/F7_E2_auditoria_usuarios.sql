-- =====================================================================
-- ClimaPlus – Fase 7, Etapa 2: auditoría de usuarios y de sus datos (SOLO LECTURA)
--
-- Lista cada cuenta con todo lo que depende de ella, para decidir cuáles son de prueba y cuáles se conservan.
-- La columna "indicios" solo ayuda a revisar: un correo de aspecto ficticio NO autoriza por sí solo a eliminar una
-- cuenta. La decisión la toma el responsable del proyecto, cuenta por cuenta.
--
-- Funciona antes y después de la migración 004. Contiene correos: guarda la salida fuera del repositorio.
--
-- Uso (PowerShell), con la sesión en solo lectura:
--   $env:PGOPTIONS = "-c default_transaction_read_only=on"
--   psql -X -U postgres -d climaplus_db -f database\scripts\F7_E2_auditoria_usuarios.sql -o auditoria-usuarios.txt
--   Remove-Item Env:PGOPTIONS
-- =====================================================================

\set QUIET 1
\pset pager off
SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL AS tiene_004 \gset

\qecho '== 1. Base auditada'
SELECT current_database() AS base, current_setting('server_version') AS version, now() AS fecha,
       current_setting('default_transaction_read_only') AS solo_lectura;

\qecho '== 2. Cuentas y datos dependientes (una fila por usuario)'
\if :tiene_004
SELECT u.id_usuario, u.nombre, u.correo, u.rol, u.activo, u.fecha_creacion::date AS creada,
       u.correo_verificado AS verificado, (u.password_hash IS NOT NULL) AS con_password,
       (SELECT count(*) FROM identidad_externa i WHERE i.id_usuario = u.id_usuario) AS google,
       (SELECT count(*) FROM favorito f WHERE f.id_usuario = u.id_usuario) AS favoritos,
       (SELECT count(*) FROM configuracion_alerta c WHERE c.id_usuario = u.id_usuario) AS reglas,
       (SELECT count(*) FROM consulta_clima q WHERE q.id_usuario = u.id_usuario) AS consultas,
       (SELECT count(*) FROM alerta a WHERE a.id_usuario = u.id_usuario) AS alertas,
       (SELECT count(*) FROM dispositivo d WHERE d.id_usuario = u.id_usuario) AS dispositivos,
       (SELECT count(*) FROM lectura_sensor l JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo WHERE d.id_usuario = u.id_usuario) AS lecturas,
       (SELECT count(*) FROM desafio_autenticacion x WHERE x.id_usuario = u.id_usuario) AS desafios,
       concat_ws(', ',
         CASE WHEN split_part(u.correo, '@', 2) ~ '(^|\.)(example\.(com|org|net)|ejemplo\.com|test|prueba|local|localhost|invalid|mailinator\.com)$' THEN 'dominio de prueba' END,
         CASE WHEN u.correo ~ '(test|prueba|demo|fake|ficticio|dummy|qa)' THEN 'correo con palabra de prueba' END,
         CASE WHEN u.nombre ~* '(test|prueba|demo|fake|ficticio|dummy)' THEN 'nombre de prueba' END) AS indicios
FROM usuario u
ORDER BY u.rol DESC, u.id_usuario;
\else
SELECT u.id_usuario, u.nombre, u.correo, u.rol, u.activo, u.fecha_creacion::date AS creada,
       (SELECT count(*) FROM favorito f WHERE f.id_usuario = u.id_usuario) AS favoritos,
       (SELECT count(*) FROM configuracion_alerta c WHERE c.id_usuario = u.id_usuario) AS reglas,
       (SELECT count(*) FROM consulta_clima q WHERE q.id_usuario = u.id_usuario) AS consultas,
       (SELECT count(*) FROM alerta a WHERE a.id_usuario = u.id_usuario) AS alertas,
       (SELECT count(*) FROM dispositivo d WHERE d.id_usuario = u.id_usuario) AS dispositivos,
       (SELECT count(*) FROM lectura_sensor l JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo WHERE d.id_usuario = u.id_usuario) AS lecturas,
       concat_ws(', ',
         CASE WHEN split_part(u.correo, '@', 2) ~ '(^|\.)(example\.(com|org|net)|ejemplo\.com|test|prueba|local|localhost|invalid|mailinator\.com)$' THEN 'dominio de prueba' END,
         CASE WHEN u.correo ~ '(test|prueba|demo|fake|ficticio|dummy|qa)' THEN 'correo con palabra de prueba' END,
         CASE WHEN u.nombre ~* '(test|prueba|demo|fake|ficticio|dummy)' THEN 'nombre de prueba' END) AS indicios
FROM usuario u
ORDER BY u.rol DESC, u.id_usuario;
\endif

\qecho '== 3. Dispositivos (las lecturas y alertas del ESP32 dependen de ellos)'
SELECT d.id_dispositivo, d.id_usuario, d.nombre, d.activo, d.ultima_conexion,
       (SELECT count(*) FROM lectura_sensor l WHERE l.id_dispositivo = d.id_dispositivo) AS lecturas
FROM dispositivo d ORDER BY d.id_usuario, d.id_dispositivo;

\qecho '== 4. Totales por tabla'
SELECT 'usuario' AS tabla, count(*) AS filas FROM usuario
UNION ALL SELECT 'ubicacion (catálogo compartido: no se borra)', count(*) FROM ubicacion
UNION ALL SELECT 'favorito', count(*) FROM favorito
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta
UNION ALL SELECT 'consulta_clima (con usuario)', count(*) FROM consulta_clima WHERE id_usuario IS NOT NULL
UNION ALL SELECT 'consulta_clima (anónimas: no se borran)', count(*) FROM consulta_clima WHERE id_usuario IS NULL
UNION ALL SELECT 'alerta', count(*) FROM alerta
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivo
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor;

\qecho '== 5. Referencias cruzadas entre cuentas (deben ser 0; si no, la limpieza se detendrá)'
SELECT 'alerta de un usuario que apunta a la consulta de otro' AS caso, count(*) AS filas
FROM alerta a JOIN consulta_clima q ON q.id_consulta = a.id_consulta
WHERE q.id_usuario IS NOT NULL AND q.id_usuario <> a.id_usuario
UNION ALL
SELECT 'alerta de un usuario que apunta a la lectura del dispositivo de otro', count(*)
FROM alerta a JOIN lectura_sensor l ON l.id_lectura = a.id_lectura JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo
WHERE d.id_usuario <> a.id_usuario
UNION ALL
SELECT 'alerta que apunta a una consulta anónima', count(*)
FROM alerta a JOIN consulta_clima q ON q.id_consulta = a.id_consulta WHERE q.id_usuario IS NULL;

\qecho '== 6. Administradores'
SELECT id_usuario, correo, activo, fecha_creacion::date AS creada FROM usuario WHERE rol = 'ADMINISTRADOR' ORDER BY id_usuario;
