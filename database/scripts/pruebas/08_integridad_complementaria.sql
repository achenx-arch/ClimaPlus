
-- ==========================================================
-- CLIMAPLUS - SCRIPT 08
-- PRUEBAS COMPLEMENTARIAS E-02 Y E-03
-- ==========================================================

BEGIN;

DO $$
DECLARE
    v_ubicacion BIGINT;
    v_usuario BIGINT;
    v_consulta BIGINT;
    v_total INTEGER := 0;
    v_restriccion TEXT;
BEGIN

    -- Datos temporales
    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario Prueba 08',
         'prueba08.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion Prueba 08', 'Alta Verapaz',
         'GT', 15.8000, -90.8000)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- 01: E-02 - Consulta anonima permitida
INSERT INTO consulta_clima
    (
        id_usuario,
        id_ubicacion,
        tipo_consulta,
        temperatura,
        sensacion_termica,
        humedad,
        velocidad_viento,
        codigo_condicion,
        descripcion_condicion
    )
VALUES
    (
        NULL,
        v_ubicacion,
        'ACTUAL',
        24,
        25,
        70,
        8,
        800,
        'Despejado'
    )
RETURNING id_consulta INTO v_consulta;

    IF NOT EXISTS (
        SELECT 1 FROM consulta_clima
        WHERE id_consulta = v_consulta
          AND id_usuario IS NULL
    ) THEN
        RAISE EXCEPTION 'FAIL 01: consulta anonima';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 01: consulta anonima permitida';

    -- 02: E-03 - Pronostico con detalle JSONB
    INSERT INTO consulta_clima
        (id_usuario, id_ubicacion,
         tipo_consulta, detalle)
    VALUES
        (v_usuario, v_ubicacion,
         'PRONOSTICO',
         '{"dias":[{"temperatura":24}]}'::jsonb)
    RETURNING id_consulta INTO v_consulta;

    IF NOT EXISTS (
        SELECT 1 FROM consulta_clima
        WHERE id_consulta = v_consulta
          AND detalle -> 'dias' IS NOT NULL
    ) THEN
        RAISE EXCEPTION 'FAIL 02: JSONB no almacenado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 02: JSONB almacenado correctamente';

    -- 03: Pronostico sin detalle rechazado
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta)
        VALUES
            (v_usuario, v_ubicacion, 'PRONOSTICO');

        RAISE EXCEPTION 'FAIL 03: pronostico sin detalle aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS
                v_restriccion = CONSTRAINT_NAME;

            IF v_restriccion <> 'ck_consulta_clima_detalle_pronostico' THEN
                RAISE EXCEPTION
                    'FAIL 03: restriccion inesperada: %',
                    v_restriccion;
            END IF;

            v_total := v_total + 1;
            RAISE NOTICE 'PASS 03: pronostico sin detalle rechazado';
    END;

    -- 04: JSONB no objeto rechazado
    BEGIN
  INSERT INTO consulta_clima
    (id_usuario, id_ubicacion, tipo_consulta,
     temperatura, sensacion_termica, humedad,
     velocidad_viento, codigo_condicion,
     descripcion_condicion, detalle)
VALUES
    (v_usuario, v_ubicacion, 'ACTUAL',
     25, 26, 70, 8, 800,
     'Despejado', '[1,2,3]'::jsonb);

        RAISE EXCEPTION 'FAIL 04: arreglo JSONB aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS
                v_restriccion = CONSTRAINT_NAME;

            IF v_restriccion <> 'ck_consulta_clima_detalle_objeto' THEN
                RAISE EXCEPTION
                    'FAIL 04: restriccion inesperada: %',
                    v_restriccion;
            END IF;

            v_total := v_total + 1;
            RAISE NOTICE 'PASS 04: JSONB no objeto rechazado';
    END;

    -- 05: Consulta ACTUAL con mediciones validas
    INSERT INTO consulta_clima
    (id_usuario, id_ubicacion, tipo_consulta,
     temperatura, sensacion_termica, humedad,
     velocidad_viento, codigo_condicion,
     descripcion_condicion)
VALUES
    (v_usuario, v_ubicacion, 'ACTUAL',
     26, 27, 65, 8, 800,
     'Despejado')
RETURNING id_consulta INTO v_consulta;

    IF NOT EXISTS (
        SELECT 1 FROM consulta_clima
        WHERE id_consulta = v_consulta
          AND temperatura = 26
          AND humedad = 65
          AND velocidad_viento = 8
    ) THEN
        RAISE EXCEPTION 'FAIL 05: mediciones incorrectas';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 05: mediciones ACTUAL validas';

    -- 06: Usuario inexistente rechazado
    BEGIN
INSERT INTO consulta_clima
    (id_usuario, id_ubicacion, tipo_consulta,
     temperatura, sensacion_termica, humedad,
     velocidad_viento, codigo_condicion,
     descripcion_condicion)
VALUES
    (-999999, v_ubicacion, 'ACTUAL',
     25, 26, 70, 8, 800,
     'Despejado');

        RAISE EXCEPTION 'FAIL 06: usuario inexistente aceptado';
    EXCEPTION
        WHEN foreign_key_violation THEN
            GET STACKED DIAGNOSTICS
                v_restriccion = CONSTRAINT_NAME;

            IF v_restriccion <> 'fk_consulta_clima_usuario' THEN
                RAISE EXCEPTION
                    'FAIL 06: restriccion inesperada: %',
                    v_restriccion;
            END IF;

            v_total := v_total + 1;
            RAISE NOTICE 'PASS 06: usuario inexistente rechazado';
    END;

    IF v_total <> 6 THEN
        RAISE EXCEPTION
            'FAIL: % de 6 pruebas aprobadas', v_total;
    END IF;

    RAISE NOTICE
        'TOTAL: % de 6 pruebas complementarias aprobadas',
        v_total;

END $$;

ROLLBACK;
