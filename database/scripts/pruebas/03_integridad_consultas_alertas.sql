
-- ClimaPlus
-- Pruebas de integridad: consulta_clima y alerta
-- Todos los cambios se revierten al finalizar.

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_consulta BIGINT;
    v_error TEXT;
BEGIN

    -- Preparación de registros relacionados
    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario Test 03',
         'test03.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion Test 03', 'Alta Verapaz',
         'GT', 15.5300, -90.4300)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- 1. Consulta ACTUAL válida
    INSERT INTO consulta_clima
        (id_usuario, id_ubicacion, tipo_consulta,
         temperatura, sensacion_termica, humedad,
         velocidad_viento, codigo_condicion,
         descripcion_condicion)
    VALUES
        (v_usuario, v_ubicacion, 'ACTUAL',
         25, 26, 75, 12, 800, 'Despejado')
    RETURNING id_consulta INTO v_consulta;

    RAISE NOTICE 'PASS 01: consulta ACTUAL válida';

    -- 2. Consulta anónima válida
    INSERT INTO consulta_clima
        (id_usuario, id_ubicacion, tipo_consulta,
         temperatura, sensacion_termica, humedad,
         velocidad_viento, codigo_condicion,
         descripcion_condicion)
    VALUES
        (NULL, v_ubicacion, 'ACTUAL',
         24, 25, 70, 10, 800, 'Despejado');

    RAISE NOTICE 'PASS 02: consulta anónima válida';

    -- 3. Tipo de consulta inválido
    BEGIN
     INSERT INTO consulta_clima
    (id_ubicacion, tipo_consulta,
     temperatura, sensacion_termica, humedad,
     velocidad_viento, codigo_condicion,
     descripcion_condicion)
VALUES
    (v_ubicacion, 'FUTURO',
     25, 26, 75, 12, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: tipo inválido permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
IF v_error NOT IN (
    'ck_consulta_clima_tipo_consulta',
    'ck_consulta_clima_mediciones_tipo'
) THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 03: tipo inválido rechazado';
    END;

    -- 4. Humedad fuera de rango
    BEGIN
        INSERT INTO consulta_clima
            (id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_ubicacion, 'ACTUAL',
             25, 26, 120, 10, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: humedad inválida permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_humedad' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 04: humedad inválida rechazada';
    END;

    -- 5. Pronóstico válido con JSONB
    INSERT INTO consulta_clima
        (id_ubicacion, tipo_consulta, detalle)
    VALUES
        (v_ubicacion, 'PRONOSTICO',
         '{"dias":[{"temperatura":25}]}'::jsonb);

    RAISE NOTICE 'PASS 05: pronóstico con JSONB válido';

    -- 6. Pronóstico sin detalle
    BEGIN
        INSERT INTO consulta_clima
            (id_ubicacion, tipo_consulta)
        VALUES (v_ubicacion, 'PRONOSTICO');

        RAISE EXCEPTION 'FAIL: pronóstico sin detalle permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_detalle_pronostico' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 06: pronóstico sin detalle rechazado';
    END;

    -- 7. JSONB de tipo arreglo no permitido
    BEGIN
        INSERT INTO consulta_clima
            (id_ubicacion, tipo_consulta, detalle)
        VALUES
            (v_ubicacion, 'PRONOSTICO', '[1,2,3]'::jsonb);

        RAISE EXCEPTION 'FAIL: JSONB arreglo permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_detalle_objeto' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 07: JSONB arreglo rechazado';
    END;

    -- 8. Alerta válida originada en consulta
    INSERT INTO alerta
        (id_usuario, id_ubicacion, id_consulta,
         tipo, valor_detectado, valor_umbral,
         mensaje, estado)
    VALUES
        (v_usuario, v_ubicacion, v_consulta,
         'TEMPERATURA_ALTA', 35, 30,
         'Temperatura elevada', 'NUEVA');

    RAISE NOTICE 'PASS 08: alerta válida';

    -- 9. Alerta sin origen
    BEGIN
        INSERT INTO alerta
            (id_usuario, id_ubicacion, tipo,
             valor_detectado, valor_umbral, mensaje)
        VALUES
            (v_usuario, v_ubicacion,
             'TEMPERATURA_ALTA', 35, 30,
             'Sin origen');

        RAISE EXCEPTION 'FAIL: alerta sin origen permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_alerta_origen_unico' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 09: alerta sin origen rechazada';
    END;

    -- 10. Estado inválido
    BEGIN
        INSERT INTO alerta
            (id_usuario, id_ubicacion, id_consulta,
             tipo, valor_detectado, valor_umbral,
             mensaje, estado)
        VALUES
            (v_usuario, v_ubicacion, v_consulta,
             'TEMPERATURA_ALTA', 35, 30,
             'Estado incorrecto', 'PENDIENTE');

        RAISE EXCEPTION 'FAIL: estado inválido permitido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_alerta_estado' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 10: estado inválido rechazado';
    END;

    -- 11. Lluvia con valores numéricos
    BEGIN
        INSERT INTO alerta
            (id_usuario, id_ubicacion, id_consulta,
             tipo, valor_detectado, valor_umbral,
             mensaje)
        VALUES
            (v_usuario, v_ubicacion, v_consulta,
             'LLUVIA', 20, 10, 'Lluvia detectada');

        RAISE EXCEPTION 'FAIL: lluvia con valores permitida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_alerta_valores_tipo' THEN
                RAISE EXCEPTION 'Restricción inesperada: %', v_error;
            END IF;
            RAISE NOTICE 'PASS 11: lluvia con valores rechazada';
    END;

    RAISE NOTICE 'Pruebas de consultas y alertas finalizadas';

END $$;

ROLLBACK;
