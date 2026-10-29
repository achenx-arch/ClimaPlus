
-- ClimaPlus
-- Pruebas de integridad: dispositivo y lectura_sensor
-- Todos los cambios se revierten al finalizar.

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_dispositivo BIGINT;
    v_error TEXT;
    v_hash TEXT := repeat('a', 64);
BEGIN

    -- Preparar usuario y ubicación
    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario Test 04',
         'test04.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion Test 04', 'Alta Verapaz',
         'GT', 15.5400, -90.4400)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- 1. Dispositivo válido
    INSERT INTO dispositivo
        (id_usuario, id_ubicacion, nombre, api_key_hash)
    VALUES
        (v_usuario, v_ubicacion, 'ESP32-Test', v_hash)
    RETURNING id_dispositivo INTO v_dispositivo;

    RAISE NOTICE 'PASS 01: dispositivo válido';

    -- 2. Nombre duplicado para el mismo usuario
    BEGIN
        INSERT INTO dispositivo
            (id_usuario, id_ubicacion, nombre, api_key_hash)
        VALUES
            (v_usuario, v_ubicacion,
             'ESP32-Test', repeat('b', 64));

        RAISE EXCEPTION 'FAIL: nombre duplicado permitido';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'uq_dispositivo_usuario_nombre' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 02: nombre duplicado rechazado';
    END;

    -- 3. API key hash duplicado
    BEGIN
        INSERT INTO dispositivo
            (id_usuario, id_ubicacion, nombre, api_key_hash)
        VALUES
            (v_usuario, v_ubicacion, 'ESP32-Otro', v_hash);

        RAISE EXCEPTION 'FAIL: API key duplicada permitida';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'uq_dispositivo_api_key_hash' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 03: API key duplicada rechazada';
    END;

    -- 4. Dispositivo con usuario inexistente
    BEGIN
        INSERT INTO dispositivo
            (id_usuario, id_ubicacion, nombre, api_key_hash)
        VALUES
            (-999999, v_ubicacion,
             'ESP32-Invalido', repeat('c', 64));

        RAISE EXCEPTION 'FAIL: usuario inexistente permitido';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_dispositivo_usuario' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 04: usuario inexistente rechazado';
    END;

    -- 5. Lectura válida
    INSERT INTO lectura_sensor
        (id_dispositivo, temperatura, humedad, lluvia, luz)
    VALUES
        (v_dispositivo, 28, 75, FALSE, 60);

    RAISE NOTICE 'PASS 05: lectura válida';

    -- 6. Temperatura fuera de rango
    BEGIN
        INSERT INTO lectura_sensor
            (id_dispositivo, temperatura)
        VALUES (v_dispositivo, 100);

        RAISE EXCEPTION 'FAIL: temperatura inválida permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_lectura_sensor_temperatura' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 06: temperatura inválida rechazada';
    END;

    -- 7. Humedad fuera de rango
    BEGIN
        INSERT INTO lectura_sensor
            (id_dispositivo, humedad)
        VALUES (v_dispositivo, 120);

        RAISE EXCEPTION 'FAIL: humedad inválida permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_lectura_sensor_humedad' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 07: humedad inválida rechazada';
    END;

    -- 8. Luz fuera de rango
    BEGIN
        INSERT INTO lectura_sensor
            (id_dispositivo, luz)
        VALUES (v_dispositivo, 150);

        RAISE EXCEPTION 'FAIL: luz inválida permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_lectura_sensor_luz' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 08: luz inválida rechazada';
    END;

    -- 9. Lectura sin mediciones
    BEGIN
        INSERT INTO lectura_sensor (id_dispositivo)
        VALUES (v_dispositivo);

        RAISE EXCEPTION 'FAIL: lectura vacía permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_lectura_sensor_al_menos_una_medicion' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 09: lectura vacía rechazada';
    END;

    -- 10. Lectura con dispositivo inexistente
    BEGIN
        INSERT INTO lectura_sensor
            (id_dispositivo, temperatura)
        VALUES (-999999, 25);

        RAISE EXCEPTION 'FAIL: dispositivo inexistente permitido';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_lectura_sensor_dispositivo' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 10: dispositivo inexistente rechazado';
    END;

    -- 11. Lectura válida con solo lluvia
    INSERT INTO lectura_sensor
        (id_dispositivo, lluvia)
    VALUES (v_dispositivo, TRUE);

    RAISE NOTICE 'PASS 11: lectura con solo lluvia válida';

    RAISE NOTICE 'Pruebas de dispositivos y sensores finalizadas';

END $$;

ROLLBACK;
