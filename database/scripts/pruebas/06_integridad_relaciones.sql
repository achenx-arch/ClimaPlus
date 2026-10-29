
-- =====================================================
-- CLIMAPLUS - SCRIPT 06
-- Pruebas de integridad referencial
-- FOREIGN KEY, RESTRICT y CASCADE
-- =====================================================

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_dispositivo BIGINT;
    v_consulta BIGINT;
    v_lectura BIGINT;
    v_otro_usuario BIGINT;
    v_otro_ubicacion BIGINT;
    v_error TEXT;
    v_total INTEGER := 0;
    v_cantidad INTEGER;
BEGIN

    -- ================================================
    -- DATOS TEMPORALES
    -- ================================================

    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario FK 06',
         'fk06.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion FK 06', 'Alta Verapaz',
         'GT', 15.6000, -90.6000)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- ================================================
    -- PARTE A: FOREIGN KEYS
    -- ================================================

    -- 01: Dispositivo con ubicacion inexistente
    BEGIN
        INSERT INTO dispositivo
            (id_usuario, id_ubicacion, nombre, api_key_hash)
        VALUES
            (v_usuario, -999999,
             'ESP32-FK-Invalido', repeat('d',64));

        RAISE EXCEPTION 'FAIL: ubicacion inexistente aceptada';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_dispositivo_ubicacion' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 01: dispositivo con ubicacion inexistente rechazado';
    END;

    -- 02: Favorito con ubicacion inexistente
    BEGIN
        INSERT INTO favorito (id_usuario, id_ubicacion)
        VALUES (v_usuario, -999999);

        RAISE EXCEPTION 'FAIL: favorito invalido aceptado';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_favorito_ubicacion' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 02: favorito con ubicacion inexistente rechazado';
    END;

    -- 03: Consulta con ubicacion inexistente
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_usuario, -999999, 'ACTUAL',
             25, 26, 70, 10, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: consulta invalida aceptada';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_consulta_clima_ubicacion' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 03: consulta con ubicacion inexistente rechazada';
    END;

    -- 04: Configuracion con usuario inexistente
    BEGIN
        INSERT INTO configuracion_alerta
            (id_usuario, id_ubicacion, tipo,
             valor_umbral, activa)
        VALUES
            (-999999, v_ubicacion,
             'TEMPERATURA_ALTA', 35, TRUE);

        RAISE EXCEPTION 'FAIL: configuracion invalida aceptada';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_configuracion_alerta_usuario' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 04: configuracion con usuario inexistente rechazada';
    END;

    -- ================================================
    -- PARTE B: ON DELETE RESTRICT
    -- ================================================

    INSERT INTO dispositivo
        (id_usuario, id_ubicacion, nombre, api_key_hash)
    VALUES
        (v_usuario, v_ubicacion,
         'ESP32-FK06', repeat('e',64))
    RETURNING id_dispositivo INTO v_dispositivo;

    INSERT INTO lectura_sensor
        (id_dispositivo, temperatura, humedad)
    VALUES
        (v_dispositivo, 25, 65)
    RETURNING id_lectura INTO v_lectura;

    -- 05: No permitir borrar dispositivo con lecturas
    BEGIN
        DELETE FROM dispositivo
        WHERE id_dispositivo = v_dispositivo;

        RAISE EXCEPTION 'FAIL: dispositivo con lecturas eliminado';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_lectura_sensor_dispositivo' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 05: RESTRICT protege dispositivo con lecturas';
    END;

    -- 06: No permitir borrar ubicacion con dispositivo
    BEGIN
        DELETE FROM ubicacion
        WHERE id_ubicacion = v_ubicacion;

        RAISE EXCEPTION 'FAIL: ubicacion referenciada eliminada';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_dispositivo_ubicacion' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 06: RESTRICT protege ubicacion';
    END;

    -- 07: No permitir borrar usuario con dispositivo
    BEGIN
        DELETE FROM usuario
        WHERE id_usuario = v_usuario;

        RAISE EXCEPTION 'FAIL: usuario referenciado eliminado';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'fk_dispositivo_usuario' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_total := v_total + 1;
            RAISE NOTICE 'PASS 07: RESTRICT protege usuario con dispositivo';
    END;

    -- ================================================
    -- PARTE C: ON DELETE CASCADE
    -- ================================================

    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario Cascade 06',
         'cascade06.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_otro_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion Cascade 06', 'Alta Verapaz',
         'GT', 15.7000, -90.7000)
    RETURNING id_ubicacion INTO v_otro_ubicacion;

    INSERT INTO favorito (id_usuario, id_ubicacion)
    VALUES (v_otro_usuario, v_otro_ubicacion);

    INSERT INTO configuracion_alerta
        (id_usuario, id_ubicacion, tipo,
         valor_umbral, activa)
    VALUES
        (v_otro_usuario, v_otro_ubicacion,
         'TEMPERATURA_ALTA', 35, TRUE);

    -- 08: CASCADE elimina favoritos
    -- 09: CASCADE elimina configuraciones
    DELETE FROM usuario
    WHERE id_usuario = v_otro_usuario;

    SELECT COUNT(*) INTO v_cantidad
    FROM favorito
    WHERE id_usuario = v_otro_usuario;

    IF v_cantidad <> 0 THEN
        RAISE EXCEPTION 'FAIL: favoritos no eliminados';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 08: CASCADE elimina favoritos';

    SELECT COUNT(*) INTO v_cantidad
    FROM configuracion_alerta
    WHERE id_usuario = v_otro_usuario;

    IF v_cantidad <> 0 THEN
        RAISE EXCEPTION 'FAIL: configuraciones no eliminadas';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 09: CASCADE elimina configuraciones';

    -- ================================================
    -- RESULTADO FINAL
    -- ================================================

    IF v_total <> 9 THEN
        RAISE EXCEPTION 'FAIL: % de 9 pruebas aprobadas',
            v_total;
    END IF;

    RAISE NOTICE 'TOTAL: % de 9 pruebas de relaciones aprobadas',
        v_total;

END $$;

ROLLBACK;
