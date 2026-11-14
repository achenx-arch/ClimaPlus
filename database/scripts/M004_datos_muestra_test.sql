-- =====================================================================
-- ClimaPlus – Migración 004: datos de muestra para probar que la migración conserva todo.
-- SOLO para bases de PRUEBAS cuyo nombre termine en "_test" (se detiene en cualquier otra).
-- Uso (con las migraciones 001 a 003 ya aplicadas):
--   psql -d climaplus_test -f database/scripts/M004_datos_muestra_test.sql
--   psql -d climaplus_test -f database/scripts/M004_verificacion_previa.sql > previa.txt
--   psql -d climaplus_test -v ON_ERROR_STOP=1 -f database/migrations/004_AutenticacionSegura.sql
--   psql -d climaplus_test -f database/scripts/M004_verificacion_posterior.sql > posterior.txt
-- =====================================================================

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF current_database() NOT LIKE '%\_test' THEN
        RAISE EXCEPTION 'Este script solo se ejecuta en bases *_test (actual: %)', current_database();
    END IF;
END $$;

BEGIN;

INSERT INTO usuario (nombre, correo, password_hash, rol, activo) VALUES
    ('Muestra Admin', 'm004.admin@example.com', 'AQAAAAIAAYagAAAAEMuestraHashAdmin==', 'ADMINISTRADOR', TRUE),
    ('Muestra Usuario', 'm004.usuario@example.com', 'AQAAAAIAAYagAAAAEMuestraHashUsuario==', 'USUARIO', TRUE),
    ('Muestra Inactivo', 'm004.inactivo@example.com', 'AQAAAAIAAYagAAAAEMuestraHashInactivo==', 'USUARIO', FALSE);

INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
VALUES ('Muestra Cobán', 'Alta Verapaz', 'GT', 15.4708, -90.3708);

INSERT INTO favorito (id_usuario, id_ubicacion)
SELECT u.id_usuario, b.id_ubicacion FROM usuario u, ubicacion b
WHERE u.correo = 'm004.usuario@example.com' AND b.nombre = 'Muestra Cobán';

INSERT INTO configuracion_alerta (id_usuario, id_ubicacion, tipo, valor_umbral)
SELECT u.id_usuario, b.id_ubicacion, 'TEMPERATURA_ALTA', 30 FROM usuario u, ubicacion b
WHERE u.correo = 'm004.usuario@example.com' AND b.nombre = 'Muestra Cobán';

INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, sensacion_termica, humedad, velocidad_viento, codigo_condicion, descripcion_condicion)
SELECT u.id_usuario, b.id_ubicacion, 'ACTUAL', 31.5, 33.0, 70, 2.5, 800, 'cielo claro' FROM usuario u, ubicacion b
WHERE u.correo = 'm004.usuario@example.com' AND b.nombre = 'Muestra Cobán';

INSERT INTO dispositivo (id_usuario, id_ubicacion, nombre, api_key_hash)
SELECT u.id_usuario, b.id_ubicacion, 'Muestra ESP32', repeat('d', 64) FROM usuario u, ubicacion b
WHERE u.correo = 'm004.usuario@example.com' AND b.nombre = 'Muestra Cobán';

INSERT INTO lectura_sensor (id_dispositivo, temperatura, humedad, lluvia, luz)
SELECT id_dispositivo, 25.3, 83, FALSE, 40 FROM dispositivo WHERE nombre = 'Muestra ESP32';

INSERT INTO alerta (id_usuario, id_ubicacion, id_consulta, tipo, valor_detectado, valor_umbral, mensaje)
SELECT c.id_usuario, c.id_ubicacion, c.id_consulta, 'TEMPERATURA_ALTA', 31.5, 30, 'Muestra: 31.5 C supera 30 C'
FROM consulta_clima c JOIN usuario u ON u.id_usuario = c.id_usuario
WHERE u.correo = 'm004.usuario@example.com';

INSERT INTO alerta (id_usuario, id_ubicacion, id_lectura, tipo, mensaje)
SELECT d.id_usuario, d.id_ubicacion, l.id_lectura, 'LLUVIA', 'Muestra: lluvia detectada'
FROM lectura_sensor l JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo
WHERE d.nombre = 'Muestra ESP32';

COMMIT;
