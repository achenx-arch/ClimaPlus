
-- ClimaPlus
-- Script 05: Validacion de restricciones CHECK pendientes
-- Las pruebas se revierten al finalizar.

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_error TEXT;
    v_pruebas INTEGER := 0;
     v_consulta BIGINT;
BEGIN

    -- Preparar datos validos
    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario Test 05',
         'test05.climaplus@example.com',
         'hash_prueba', 'USUARIO', TRUE)
    RETURNING id_usuario INTO v_usuario;

    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion Test 05', 'Alta Verapaz',
         'GT', 15.5400, -90.4400)
    RETURNING id_ubicacion INTO v_ubicacion;

    -- 01: Longitud fuera de rango
    BEGIN
        INSERT INTO ubicacion
            (nombre, pais, latitud, longitud)
        VALUES ('Longitud Invalida', 'GT', 15, 200);

        RAISE EXCEPTION 'FAIL: longitud invalida aceptada';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_ubicacion_longitud' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 01: longitud invalida rechazada';
    END;

    -- 02: Codigo de pais invalido
    BEGIN
        INSERT INTO ubicacion
            (nombre, pais, latitud, longitud)
        VALUES ('Pais Invalido', 'gt', 15, -90);

        RAISE EXCEPTION 'FAIL: pais invalido aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_ubicacion_pais' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 02: pais invalido rechazado';
    END;

    -- 03: Correo con mayusculas
    BEGIN
        INSERT INTO usuario
            (nombre, correo, password_hash, rol)
        VALUES
            ('Usuario Correo', 'TEST05@EXAMPLE.COM',
             'hash_prueba', 'USUARIO');

        RAISE EXCEPTION 'FAIL: correo con mayusculas aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_usuario_correo_minusculas' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 03: correo con mayusculas rechazado';
    END;

    -- 04: Nombre vacio
    BEGIN
        INSERT INTO usuario
            (nombre, correo, password_hash, rol)
        VALUES
            ('   ', 'nombre05@example.com',
             'hash_prueba', 'USUARIO');

        RAISE EXCEPTION 'FAIL: nombre vacio aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_usuario_nombre_no_vacio' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 04: nombre vacio rechazado';
    END;

    -- Las siguientes pruebas utilizan consultas ACTUAL
    -- con mediciones validas para aislar cada restriccion.

    -- 05: Codigo de condicion fuera de rango
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_usuario, v_ubicacion, 'ACTUAL',
             25, 26, 70, 10, 999, 'Prueba');

        RAISE EXCEPTION 'FAIL: codigo invalido aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_codigo_condicion' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 05: codigo invalido rechazado';
    END;

    -- 06: Sensacion termica fuera de rango
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_usuario, v_ubicacion, 'ACTUAL',
             25, 80, 70, 10, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: sensacion invalida aceptada';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_sensacion_termica' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 06: sensacion termica invalida rechazada';
    END;

    -- 07: Temperatura fuera de rango
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_usuario, v_ubicacion, 'ACTUAL',
             75, 26, 70, 10, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: temperatura invalida aceptada';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_temperatura' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 07: temperatura invalida rechazada';
    END;

    -- 08: Velocidad del viento negativa
    BEGIN
        INSERT INTO consulta_clima
            (id_usuario, id_ubicacion, tipo_consulta,
             temperatura, sensacion_termica, humedad,
             velocidad_viento, codigo_condicion,
             descripcion_condicion)
        VALUES
            (v_usuario, v_ubicacion, 'ACTUAL',
             25, 26, 70, -5, 800, 'Despejado');

        RAISE EXCEPTION 'FAIL: viento negativo aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_consulta_clima_velocidad_viento' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 08: viento negativo rechazado';
    END;

    -- 09: Tipo de alerta invalido
    BEGIN
    -- Preparar origen valido para la alerta
INSERT INTO consulta_clima
    (id_usuario, id_ubicacion, tipo_consulta,
     temperatura, sensacion_termica, humedad,
     velocidad_viento, codigo_condicion,
     descripcion_condicion)
VALUES
    (v_usuario, v_ubicacion, 'ACTUAL',
     25, 26, 70, 10, 800, 'Despejado')
RETURNING id_consulta INTO v_consulta;
   
       INSERT INTO alerta
    (id_usuario, id_ubicacion, id_consulta,
     tipo, mensaje, estado)
VALUES
    (v_usuario, v_ubicacion, v_consulta,
     'VIENTO', 'Alerta de prueba', 'NUEVA');

        RAISE EXCEPTION 'FAIL: tipo de alerta invalido aceptado';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error <> 'ck_alerta_tipo' THEN
                RAISE EXCEPTION 'Restriccion inesperada: %', v_error;
            END IF;
            v_pruebas := v_pruebas + 1;
            RAISE NOTICE 'PASS 09: tipo de alerta invalido rechazado';
    END;

    IF v_pruebas <> 9 THEN
        RAISE EXCEPTION 'FAIL: solo % de 9 pruebas aprobadas',
            v_pruebas;
    END IF;

    RAISE NOTICE 'TOTAL: % de 9 pruebas CHECK aprobadas',
        v_pruebas;

END $$;

ROLLBACK;
