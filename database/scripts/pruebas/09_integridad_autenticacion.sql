-- ClimaPlus
-- Pruebas de integridad: autenticación segura (migración 004)
-- usuario (correo_verificado, sello_seguridad, password_hash NULL),
-- desafio_autenticacion e identidad_externa.
-- Todos los cambios se revierten al finalizar.

BEGIN;

-- Ejecuta una sentencia que debe fallar y comprueba la restricción que la rechaza.
CREATE FUNCTION pg_temp.rechaza(p_sql TEXT, p_restriccion TEXT) RETURNS BOOLEAN
LANGUAGE plpgsql AS $f$
DECLARE
    v_restriccion TEXT;
BEGIN
    BEGIN
        EXECUTE p_sql;
    EXCEPTION WHEN integrity_constraint_violation THEN
        GET STACKED DIAGNOSTICS v_restriccion = CONSTRAINT_NAME;
        IF v_restriccion = p_restriccion THEN
            RETURN TRUE;
        END IF;
        RAISE EXCEPTION 'FAIL: se esperaba %, se obtuvo %', p_restriccion, v_restriccion;
    END;
    RETURN FALSE;
END $f$;

DO $$
DECLARE
    v_local BIGINT;
    v_google BIGINT;
    v_otro BIGINT;
    v_desafio UUID := gen_random_uuid();
    v_hash TEXT := repeat('c', 64);
    v_sello_local UUID;
    v_sello_google UUID;
    v_verificado BOOLEAN;
    v_filas INT;
    v_total INT := 0;
    v_esperado CONSTANT INT := 24;
BEGIN

    -- Preparar: cuenta local, cuenta solo de Google (sin contraseña) y una tercera
    INSERT INTO usuario (nombre, correo, password_hash)
    VALUES ('Usuario Test 09', 'test09.local@example.com', 'hash_prueba')
    RETURNING id_usuario, correo_verificado, sello_seguridad INTO v_local, v_verificado, v_sello_local;

    -- 1. Correo sin verificar por omisión
    IF v_verificado THEN RAISE EXCEPTION 'FAIL 01: correo_verificado debía ser FALSE'; END IF;
    v_total := v_total + 1; RAISE NOTICE 'PASS 01: correo_verificado es FALSE por omisión';

    -- 2. Cuenta sin contraseña local (Google) permitida
    INSERT INTO usuario (nombre, correo, password_hash)
    VALUES ('Google Test 09', 'test09.google@example.com', NULL)
    RETURNING id_usuario, sello_seguridad INTO v_google, v_sello_google;
    v_total := v_total + 1; RAISE NOTICE 'PASS 02: usuario con password_hash NULL permitido';

    -- 3. Cada usuario recibe un sello distinto
    IF v_sello_local IS NULL OR v_sello_local = v_sello_google THEN
        RAISE EXCEPTION 'FAIL 03: sello_seguridad vacío o repetido';
    END IF;
    v_total := v_total + 1; RAISE NOTICE 'PASS 03: sello_seguridad generado y distinto por usuario';

    INSERT INTO usuario (nombre, correo, password_hash)
    VALUES ('Otro Test 09', 'test09.otro@example.com', 'hash_prueba')
    RETURNING id_usuario INTO v_otro;

    -- 4. Desafío válido
    INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
    VALUES (v_desafio, v_local, 'INICIO_SESION', 'PENDIENTE', v_hash, now() + interval '5 minutes', now());
    v_total := v_total + 1; RAISE NOTICE 'PASS 04: desafío válido';

    -- 5. Segundo desafío vivo del mismo propósito
    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', %L, now(), now())', v_local, v_hash),
        'uq_desafio_autenticacion_vivo') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 05: un solo desafío vivo por usuario y propósito';
    END IF;

    -- 6. Otro propósito convive
    INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
    VALUES (gen_random_uuid(), v_local, 'RECUPERAR_PASSWORD', 'PENDIENTE', v_hash, now() + interval '10 minutes', now());
    v_total := v_total + 1; RAISE NOTICE 'PASS 06: propósitos distintos conviven';

    -- 7. Consumo atómico: la segunda actualización no encuentra el desafío
    UPDATE desafio_autenticacion SET estado = 'CONSUMIDO', finalizado_en = now()
    WHERE id_desafio = v_desafio AND estado = 'PENDIENTE' AND expira_en > now() AND intentos < 5;
    GET DIAGNOSTICS v_filas = ROW_COUNT;
    IF v_filas <> 1 THEN RAISE EXCEPTION 'FAIL 07: el primer consumo afectó % filas', v_filas; END IF;
    UPDATE desafio_autenticacion SET estado = 'CONSUMIDO', finalizado_en = now()
    WHERE id_desafio = v_desafio AND estado = 'PENDIENTE' AND expira_en > now() AND intentos < 5;
    GET DIAGNOSTICS v_filas = ROW_COUNT;
    IF v_filas <> 0 THEN RAISE EXCEPTION 'FAIL 07: el código se pudo consumir dos veces'; END IF;
    v_total := v_total + 1; RAISE NOTICE 'PASS 07: un desafío se consume una sola vez';

    -- 8. Tras consumirlo, se puede crear uno nuevo del mismo propósito
    INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
    VALUES (gen_random_uuid(), v_local, 'INICIO_SESION', 'PENDIENTE', v_hash, now() + interval '5 minutes', now());
    v_total := v_total + 1; RAISE NOTICE 'PASS 08: desafío nuevo tras finalizar el anterior';

    -- 9 a 18. Restricciones CHECK de desafio_autenticacion
    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
         VALUES (gen_random_uuid(), %s, ''OTRO'', ''PENDIENTE'', %L, now(), now())', v_otro, v_hash),
        'ck_desafio_autenticacion_proposito') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 09: propósito inválido rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''USADO'', %L, now(), now())', v_otro, v_hash),
        'ck_desafio_autenticacion_estado') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 10: estado inválido rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en, intentos)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', %L, now(), now(), 6)', v_otro, v_hash),
        'ck_desafio_autenticacion_intentos') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 11: más de 5 intentos rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en, reenvios)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', %L, now(), now(), 6)', v_otro, v_hash),
        'ck_desafio_autenticacion_reenvios') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 12: más de 5 reenvíos rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, expira_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', now())', v_otro),
        'ck_desafio_autenticacion_codigo') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 13: desafío sin código rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', %L, now())', v_otro, v_hash),
        'ck_desafio_autenticacion_envio') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 14: código sin fecha de envío rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, expira_en)
         VALUES (gen_random_uuid(), %s, ''VINCULAR_GOOGLE'', ''PENDIENTE'', now())', v_otro),
        'ck_desafio_autenticacion_sujeto_externo') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 15: vinculación sin sujeto de Google rechazada';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en, sujeto_externo)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''PENDIENTE'', %L, now(), now(), ''123'')', v_otro, v_hash),
        'ck_desafio_autenticacion_sujeto_externo') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 16: sujeto externo fuera de la vinculación rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en, verificado_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''VERIFICADO'', %L, now(), now(), now())', v_otro, v_hash),
        'ck_desafio_autenticacion_verificado') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 17: VERIFICADO fuera de la recuperación rechazado';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
         VALUES (gen_random_uuid(), %s, ''INICIO_SESION'', ''CONSUMIDO'', %L, now(), now())', v_otro, v_hash),
        'ck_desafio_autenticacion_finalizado') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 18: desafío finalizado sin fecha rechazado';
    END IF;

    -- 19. Vinculación pendiente sin código todavía (antes de confirmar la contraseña local) permitida
    INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, expira_en, sujeto_externo)
    VALUES (gen_random_uuid(), v_otro, 'VINCULAR_GOOGLE', 'PENDIENTE', now() + interval '5 minutes', '1098765432');
    v_total := v_total + 1; RAISE NOTICE 'PASS 19: vinculación pendiente sin código permitida';

    -- 20. Identidad de Google válida
    INSERT INTO identidad_externa (id_usuario, proveedor, sujeto, correo_proveedor)
    VALUES (v_google, 'GOOGLE', '1122334455', 'test09.google@example.com');
    v_total := v_total + 1; RAISE NOTICE 'PASS 20: identidad de Google válida';

    -- 21 a 23. Restricciones de identidad_externa
    IF pg_temp.rechaza(format(
        'INSERT INTO identidad_externa (id_usuario, proveedor, sujeto, correo_proveedor)
         VALUES (%s, ''GOOGLE'', ''1122334455'', ''test09.local@example.com'')', v_local),
        'uq_identidad_externa_proveedor_sujeto') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 21: el mismo sub de Google no se vincula a dos usuarios';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO identidad_externa (id_usuario, proveedor, sujeto, correo_proveedor)
         VALUES (%s, ''GOOGLE'', ''5566778899'', ''test09.google@example.com'')', v_google),
        'uq_identidad_externa_usuario_proveedor') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 22: una sola identidad de Google por usuario';
    END IF;

    IF pg_temp.rechaza(format(
        'INSERT INTO identidad_externa (id_usuario, proveedor, sujeto, correo_proveedor)
         VALUES (%s, ''FACEBOOK'', ''1'', ''test09.otro@example.com'')', v_otro),
        'ck_identidad_externa_proveedor') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 23: proveedor no admitido rechazado';
    END IF;

    -- 24. Llaves foráneas hacia usuario
    IF pg_temp.rechaza(format(
        'INSERT INTO desafio_autenticacion (id_desafio, id_usuario, proposito, estado, codigo_hash, expira_en, enviado_en)
         VALUES (gen_random_uuid(), -1, ''INICIO_SESION'', ''PENDIENTE'', %L, now(), now())', v_hash),
        'fk_desafio_autenticacion_usuario')
       AND pg_temp.rechaza(
        'INSERT INTO identidad_externa (id_usuario, proveedor, sujeto, correo_proveedor)
         VALUES (-1, ''GOOGLE'', ''999'', ''nadie@example.com'')',
        'fk_identidad_externa_usuario') THEN
        v_total := v_total + 1; RAISE NOTICE 'PASS 24: desafío e identidad de usuario inexistente rechazados';
    END IF;

    IF v_total <> v_esperado THEN
        RAISE EXCEPTION 'FAIL: % de % pruebas aprobadas', v_total, v_esperado;
    END IF;

    RAISE NOTICE 'TOTAL: % de % pruebas de autenticación aprobadas', v_total, v_esperado;

END $$;

ROLLBACK;
