-- =====================================================================
-- ClimaPlus – Fase 7, Etapa 2: limpieza transaccional de cuentas de prueba
--
-- Elimina SOLO las cuentas cuyos id se indican y los datos que dependen de ellas. Nunca toca ubicaciones (catálogo
-- compartido), consultas anónimas, cuentas no indicadas, tablas ni migraciones.
--
-- POR DEFECTO NO CAMBIA NADA: muestra el informe de lo que se eliminaría y termina con ROLLBACK.
-- Solo confirma (COMMIT) con -v confirmar=SI. Sobre climaplus_db exige además -v autorizado_oficial=SI.
--
-- Parámetros (psql -v):
--   ids              lista de id_usuario separados por coma, sin espacios. Obligatorio. Ej.: ids=3,5,8
--   cantidad         cuántas cuentas esperas eliminar (doble comprobación). Obligatorio.
--   permitir_admin   SI para permitir eliminar cuentas ADMINISTRADOR (por defecto NO).
--   confirmar        SI para confirmar (por defecto NO = ensayo con ROLLBACK).
--   autorizado_oficial SI solo cuando el responsable autorizó modificar climaplus_db.
--
-- Se detiene (sin cambiar nada) si: algún id no existe, la cantidad no coincide, hay un administrador en la lista
-- sin permitir_admin=SI, no quedaría ningún administrador activo, o una cuenta que se conserva tiene alertas que
-- apuntan a consultas/lecturas de una cuenta que se elimina.
--
-- Ensayo (no cambia nada):
--   psql -X -U postgres -d climaplus_db_ensayo -v ON_ERROR_STOP=1 -v ids=3,5 -v cantidad=2 -f database\scripts\F7_E2_limpieza_cuentas.sql
-- =====================================================================

\pset pager off
\set ON_ERROR_STOP on

\if :{?ids}
\else
  \echo 'Falta -v ids=...'
  \quit
\endif
\if :{?cantidad}
\else
  \echo 'Falta -v cantidad=N'
  \quit
\endif
\if :{?permitir_admin}
\else
  \set permitir_admin NO
\endif
\if :{?confirmar}
\else
  \set confirmar NO
\endif
\if :{?autorizado_oficial}
\else
  \set autorizado_oficial NO
\endif

SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL AS tiene_004 \gset

BEGIN;

CREATE TEMP TABLE parametros ON COMMIT DROP AS
SELECT string_to_array(:'ids', ',')::bigint[] AS ids,
       :'cantidad'::int AS cantidad,
       upper(:'permitir_admin') = 'SI' AS permitir_admin,
       upper(:'confirmar') = 'SI' AS confirmar,
       upper(:'autorizado_oficial') = 'SI' AS autorizado_oficial;

CREATE TEMP TABLE objetivo ON COMMIT DROP AS
SELECT u.id_usuario, u.correo, u.rol, u.activo
FROM usuario u, parametros p
WHERE u.id_usuario = ANY (p.ids);

CREATE TEMP TABLE dispositivos_objetivo ON COMMIT DROP AS
SELECT d.id_dispositivo FROM dispositivo d JOIN objetivo o ON o.id_usuario = d.id_usuario;

-- -------------------------------------------------------------- Validaciones (cualquier fallo = ROLLBACK)
DO $$
DECLARE
    p parametros%ROWTYPE;
    encontrados int;
    unicos int;
    admins_en_lista int;
    admins_restantes int;
    cruzadas int;
BEGIN
    SELECT * INTO p FROM parametros;
    SELECT count(*) INTO encontrados FROM objetivo;
    SELECT count(DISTINCT x) INTO unicos FROM unnest(p.ids) x;

    IF unicos <> encontrados THEN
        RAISE EXCEPTION 'De % id indicados solo existen %. Revisa la lista: no se cambió nada.', unicos, encontrados;
    END IF;
    IF encontrados <> p.cantidad THEN
        RAISE EXCEPTION 'Se esperaban % cuentas y la lista tiene %: no se cambió nada.', p.cantidad, encontrados;
    END IF;

    SELECT count(*) INTO admins_en_lista FROM objetivo WHERE rol = 'ADMINISTRADOR';
    IF admins_en_lista > 0 AND NOT p.permitir_admin THEN
        RAISE EXCEPTION 'La lista incluye % administrador(es). Para eliminarlos usa -v permitir_admin=SI (solo después de comprobar el administrador nuevo).', admins_en_lista;
    END IF;

    SELECT count(*) INTO admins_restantes FROM usuario
    WHERE rol = 'ADMINISTRADOR' AND activo AND id_usuario <> ALL (p.ids);
    IF admins_restantes = 0 THEN
        RAISE EXCEPTION 'Después de la limpieza no quedaría ningún administrador activo: no se cambió nada.';
    END IF;

    SELECT count(*) INTO cruzadas
    FROM alerta a
    LEFT JOIN consulta_clima q ON q.id_consulta = a.id_consulta
    LEFT JOIN lectura_sensor l ON l.id_lectura = a.id_lectura
    LEFT JOIN dispositivo d ON d.id_dispositivo = l.id_dispositivo
    WHERE a.id_usuario <> ALL (p.ids)
      AND (q.id_usuario = ANY (p.ids) OR d.id_usuario = ANY (p.ids));
    IF cruzadas > 0 THEN
        RAISE EXCEPTION '% alerta(s) de cuentas que se conservan apuntan a datos de cuentas a eliminar: no se cambió nada.', cruzadas;
    END IF;

    IF p.confirmar AND current_database() = 'climaplus_db' AND NOT p.autorizado_oficial THEN
        RAISE EXCEPTION 'Para confirmar en climaplus_db hace falta -v autorizado_oficial=SI: no se cambió nada.';
    END IF;
END $$;

-- -------------------------------------------------------------- Informe previo
\qecho '== Cuentas que se eliminarán'
SELECT id_usuario, correo, rol, activo FROM objetivo ORDER BY id_usuario;

\qecho '== Filas que se eliminarán por tabla'
SELECT 'alerta' AS tabla, count(*) AS filas FROM alerta a JOIN objetivo o ON o.id_usuario = a.id_usuario
UNION ALL SELECT 'lectura_sensor', count(*) FROM lectura_sensor l JOIN dispositivos_objetivo d ON d.id_dispositivo = l.id_dispositivo
UNION ALL SELECT 'dispositivo', count(*) FROM dispositivos_objetivo
UNION ALL SELECT 'consulta_clima', count(*) FROM consulta_clima q JOIN objetivo o ON o.id_usuario = q.id_usuario
UNION ALL SELECT 'favorito', count(*) FROM favorito f JOIN objetivo o ON o.id_usuario = f.id_usuario
UNION ALL SELECT 'configuracion_alerta', count(*) FROM configuracion_alerta c JOIN objetivo o ON o.id_usuario = c.id_usuario
UNION ALL SELECT 'usuario', count(*) FROM objetivo;
\if :tiene_004
SELECT 'desafio_autenticacion' AS tabla, count(*) AS filas FROM desafio_autenticacion x JOIN objetivo o ON o.id_usuario = x.id_usuario
UNION ALL SELECT 'identidad_externa', count(*) FROM identidad_externa i JOIN objetivo o ON o.id_usuario = i.id_usuario;
\endif

\qecho '== Totales ANTES'
CREATE TEMP TABLE totales_antes ON COMMIT DROP AS
SELECT (SELECT count(*) FROM usuario) AS usuarios, (SELECT count(*) FROM ubicacion) AS ubicaciones,
       (SELECT count(*) FROM consulta_clima WHERE id_usuario IS NULL) AS consultas_anonimas;
SELECT * FROM totales_antes;

-- -------------------------------------------------------------- Eliminación (en orden de dependencias)
\qecho '== Eliminando'
DELETE FROM alerta a USING objetivo o WHERE a.id_usuario = o.id_usuario;
DELETE FROM lectura_sensor l USING dispositivos_objetivo d WHERE l.id_dispositivo = d.id_dispositivo;
DELETE FROM dispositivo d USING dispositivos_objetivo x WHERE d.id_dispositivo = x.id_dispositivo;
DELETE FROM consulta_clima q USING objetivo o WHERE q.id_usuario = o.id_usuario;
DELETE FROM favorito f USING objetivo o WHERE f.id_usuario = o.id_usuario;
DELETE FROM configuracion_alerta c USING objetivo o WHERE c.id_usuario = o.id_usuario;
\if :tiene_004
DELETE FROM desafio_autenticacion x USING objetivo o WHERE x.id_usuario = o.id_usuario;
DELETE FROM identidad_externa i USING objetivo o WHERE i.id_usuario = o.id_usuario;
\endif
DELETE FROM usuario u USING objetivo o WHERE u.id_usuario = o.id_usuario;

-- -------------------------------------------------------------- Comprobación posterior
DO $$
DECLARE a totales_antes%ROWTYPE; borradas int;
BEGIN
    SELECT * INTO a FROM totales_antes;
    SELECT count(*) INTO borradas FROM objetivo;
    IF (SELECT count(*) FROM usuario) <> a.usuarios - borradas THEN RAISE EXCEPTION 'Conteo de usuarios inesperado'; END IF;
    IF (SELECT count(*) FROM ubicacion) <> a.ubicaciones THEN RAISE EXCEPTION 'Cambió el catálogo de ubicaciones'; END IF;
    IF (SELECT count(*) FROM consulta_clima WHERE id_usuario IS NULL) <> a.consultas_anonimas THEN RAISE EXCEPTION 'Cambiaron las consultas anónimas'; END IF;
    IF EXISTS (SELECT 1 FROM usuario u JOIN objetivo o ON o.id_usuario = u.id_usuario) THEN RAISE EXCEPTION 'Quedaron cuentas objetivo'; END IF;
END $$;

\qecho '== Totales DESPUÉS (dentro de la transacción)'
SELECT (SELECT count(*) FROM usuario) AS usuarios, (SELECT count(*) FROM ubicacion) AS ubicaciones,
       (SELECT count(*) FROM consulta_clima WHERE id_usuario IS NULL) AS consultas_anonimas,
       (SELECT count(*) FROM usuario WHERE rol = 'ADMINISTRADOR' AND activo) AS administradores_activos;

SELECT confirmar AS confirmar_si FROM parametros \gset
\if :confirmar_si
  COMMIT;
  \qecho '== CONFIRMADO: la limpieza se guardó.'
\else
  ROLLBACK;
  \qecho '== ENSAYO: se deshizo todo (ROLLBACK). Para confirmar: -v confirmar=SI'
\endif
