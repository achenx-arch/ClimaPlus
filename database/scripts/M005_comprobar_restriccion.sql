-- =====================================================================
-- ClimaPlus – Migración 005: comprueba que ck_desafio_autenticacion_proposito ACEPTA 'CAMBIAR_PASSWORD' y sigue
-- rechazando un propósito desconocido, SIN escribir en ninguna tabla real.
-- Copia la estructura y los CHECK de desafio_autenticacion en una tabla TEMPORAL (solo existe en esta sesión),
-- inserta ahí y termina con ROLLBACK. No toca filas, secuencias ni relaciones de la base.
-- Salida esperada después de la 005:
--   acepta_cambiar_password=si
--   rechaza_desconocido=si
--   acepta_anteriores=5
-- =====================================================================
\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE prueba_005 (LIKE desafio_autenticacion INCLUDING DEFAULTS INCLUDING CONSTRAINTS) ON COMMIT DROP;

DO $$
DECLARE
    v_proposito TEXT;
    v_aceptados INT := 0;
    v_rechaza BOOLEAN := FALSE;
BEGIN
    INSERT INTO prueba_005 (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
    VALUES (gen_random_uuid(), 1, 'CAMBIAR_PASSWORD', 'PENDIENTE', repeat('a', 64), now() + interval '5 minutes', now());
    RAISE NOTICE 'acepta_cambiar_password=si';

    FOREACH v_proposito IN ARRAY ARRAY['VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'RECUPERAR_PASSWORD'] LOOP
        INSERT INTO prueba_005 (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
        VALUES (gen_random_uuid(), 1, v_proposito, 'PENDIENTE', repeat('a', 64), now() + interval '5 minutes', now());
        v_aceptados := v_aceptados + 1;
    END LOOP;
    INSERT INTO prueba_005 (id_desafio, id_usuario, proposito, estado, expira_en, sujeto_externo)
    VALUES (gen_random_uuid(), 1, 'VINCULAR_GOOGLE', 'PENDIENTE', now() + interval '5 minutes', 'sub-prueba');
    v_aceptados := v_aceptados + 1;
    RAISE NOTICE 'acepta_anteriores=%', v_aceptados;

    BEGIN
        INSERT INTO prueba_005 (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
        VALUES (gen_random_uuid(), 1, 'OTRO_PROPOSITO', 'PENDIENTE', repeat('a', 64), now() + interval '5 minutes', now());
    EXCEPTION WHEN check_violation THEN
        v_rechaza := TRUE;
    END;
    IF NOT v_rechaza THEN
        RAISE EXCEPTION 'FALLO: la restricción aceptó un propósito desconocido';
    END IF;
    RAISE NOTICE 'rechaza_desconocido=si';
END $$;

ROLLBACK;
