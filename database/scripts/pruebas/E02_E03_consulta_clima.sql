-- =====================================================================
-- ClimaPlus – Pruebas SQL de E-02 y E-03 (especificación, sección 4)
-- Todo ocurre dentro de una transacción con ROLLBACK final: NO deja datos.
-- Ejecutar DESPUÉS de aplicar 002 y 003:
--   psql -U <usuario> -d climaplus_db -f database/scripts/pruebas/E02_E03_consulta_clima.sql
-- Los casos 3 a 6 DEBEN mostrar el error de la restricción indicada.
-- =====================================================================

BEGIN;

-- Datos mínimos para las pruebas (se descartan con el ROLLBACK).
INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
VALUES ('Prueba E02', NULL, 'GT', 89.9999, 179.9999) RETURNING id_ubicacion \gset ubi_

-- 1. E-02: consulta ACTUAL anónima → DEBE ACEPTARSE
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, sensacion_termica,
  humedad, velocidad_viento, codigo_condicion, descripcion_condicion, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'ACTUAL', 22.5, 23.0, 74, 3.3, 802, 'nubes dispersas',
  '{"version":1,"presionHpa":1018}');

-- 2. E-03: PRONOSTICO con detalle → DEBE ACEPTARSE
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', '{"version":1,"dias":[]}');

-- 3. E-03: PRONOSTICO sin detalle → DEBE FALLAR (ck_consulta_clima_detalle_pronostico)
SAVEPOINT p3;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO');
ROLLBACK TO SAVEPOINT p3;

-- 4. E-03: detalle que no es objeto → DEBE FALLAR (ck_consulta_clima_detalle_objeto)
SAVEPOINT p4;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', '[1,2,3]');
ROLLBACK TO SAVEPOINT p4;

-- 5. Regresión: los CHECK de medición siguen activos (humedad 150) → DEBE FALLAR (ck_consulta_clima_humedad)
SAVEPOINT p5;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, sensacion_termica,
  humedad, velocidad_viento, codigo_condicion, descripcion_condicion)
VALUES (NULL, :ubi_id_ubicacion, 'ACTUAL', 22.5, 23.0, 150, 3.3, 802, 'nubes');
ROLLBACK TO SAVEPOINT p5;

-- 6. Regresión: PRONOSTICO con mediciones → DEBE FALLAR (ck_consulta_clima_mediciones_tipo)
SAVEPOINT p6;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', 20, '{"version":1}');
ROLLBACK TO SAVEPOINT p6;

-- 7. FK: usuario inexistente → DEBE FALLAR (fk_consulta_clima_usuario)
SAVEPOINT p7;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, detalle)
VALUES (9223372036854775807, :ubi_id_ubicacion, 'PRONOSTICO', '{"version":1}');
ROLLBACK TO SAVEPOINT p7;

ROLLBACK;
