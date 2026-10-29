
-- ClimaPlus
-- Pruebas de integridad: favorito y configuracion_alerta
-- Todos los cambios se revierten al finalizar.

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_error TEXT;
BEGIN
    -- Preparar registros válidos
    INSERT INTO usuario (nombre, correo, password_hash, rol, activo)
    VALUES ('Usuario Test 02',
            'test02.climaplus@example.com',
            'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
    VALUES ('Ubicacion Test 02', 'Alta Verapaz', 'GT',
            15.5100, -90.4100)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- 1. Favorito válido
    INSERT INTO favorito (id_usuario, id_ubicacion)
    VALUES (v_usuario, v_ubicacion);

    RAISE NOTICE 'PASS 01: favorito válido';

    -- 2. Favorito duplicado
    BEGIN
        INSERT INTO favorito (id_usuario, id_ubicacion)
        VALUES (v_usuario, v_ubicacion);

        RAISE EXCEPTION 'FAIL: favorito duplicado permitido';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'uq_favorito_usuario_ubicacion' THEN
                RAISE NOTICE 'PASS 02: favorito duplicado rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 3. Favorito con usuario inexistente
    BEGIN
        INSERT INTO favorito (id_usuario, id_ubicacion)
        VALUES (-999999, v_ubicacion);

        RAISE EXCEPTION 'FAIL: usuario inexistente permitido';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'fk_favorito_usuario' THEN
                RAISE NOTICE 'PASS 03: usuario inexistente rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 4. Configuración válida
    INSERT INTO configuracion_alerta
        (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
    VALUES
        (v_usuario, v_ubicacion, 'TEMPERATURA_ALTA', 35, TRUE);

    RAISE NOTICE 'PASS 04: configuración válida';

    -- 5. Configuración duplicada
    BEGIN
        INSERT INTO configuracion_alerta
            (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
        VALUES
            (v_usuario, v_ubicacion, 'TEMPERATURA_ALTA', 40, TRUE);

        RAISE EXCEPTION 'FAIL: configuración duplicada permitida';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'uq_configuracion_alerta_usuario_ubicacion_tipo' THEN
                RAISE NOTICE 'PASS 05: configuración duplicada rechazada';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 6. Tipo inválido
    BEGIN
        INSERT INTO configuracion_alerta
            (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
        VALUES
            (v_usuario, v_ubicacion, 'VIENTO', 20, TRUE);

        RAISE EXCEPTION 'FAIL: tipo inválido permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'ck_configuracion_alerta_tipo' THEN
                RAISE NOTICE 'PASS 06: tipo inválido rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 7. Umbral fuera de rango
    BEGIN
        INSERT INTO configuracion_alerta
            (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
        VALUES
            (v_usuario, v_ubicacion, 'TEMPERATURA_BAJA', 70, TRUE);

        RAISE EXCEPTION 'FAIL: umbral fuera de rango permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'ck_configuracion_alerta_umbral_rango' THEN
                RAISE NOTICE 'PASS 07: umbral fuera de rango rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 8. Lluvia con umbral (no permitido)
    BEGIN
        INSERT INTO configuracion_alerta
            (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
        VALUES
            (v_usuario, v_ubicacion, 'LLUVIA', 20, TRUE);

        RAISE EXCEPTION 'FAIL: lluvia con umbral permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'ck_configuracion_alerta_umbral_tipo' THEN
                RAISE NOTICE 'PASS 08: lluvia con umbral rechazada';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada %', v_error;
            END IF;
    END;

    -- 9. Lluvia válida sin umbral
    INSERT INTO configuracion_alerta
        (id_usuario, id_ubicacion, tipo, valor_umbral, activa)
    VALUES
        (v_usuario, v_ubicacion, 'LLUVIA', NULL, TRUE);

    RAISE NOTICE 'PASS 09: lluvia sin umbral válida';

    RAISE NOTICE 'Pruebas de favorito y configuracion_alerta finalizadas';
END $$;

ROLLBACK;
