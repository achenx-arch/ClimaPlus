
-- ClimaPlus
-- Pruebas de integridad: usuario y ubicacion
-- Todos los cambios se revierten al finalizar.

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_error TEXT;
BEGIN
    -- 1. Usuario válido
    INSERT INTO usuario (nombre, correo, password_hash, rol, activo)
    VALUES (
        'Usuario Prueba',
        'prueba.integridad.climaplus@example.com',
        'hash_solo_para_pruebas',
        'USUARIO',
        TRUE
    )
    RETURNING id_usuario INTO v_usuario;

    RAISE NOTICE 'PASS: usuario válido';

    -- 2. Correo duplicado
    BEGIN
        INSERT INTO usuario (nombre, correo, password_hash, rol, activo)
        VALUES (
            'Usuario Duplicado',
            'prueba.integridad.climaplus@example.com',
            'hash_solo_para_pruebas',
            'USUARIO',
            TRUE
        );

        RAISE EXCEPTION 'FAIL: se permitió correo duplicado';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'uq_usuario_correo' THEN
                RAISE NOTICE 'PASS: correo duplicado rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada: %', v_error;
            END IF;
    END;

    -- 3. Rol inválido
    BEGIN
        INSERT INTO usuario (nombre, correo, password_hash, rol, activo)
        VALUES (
            'Usuario Rol',
            'prueba.rol.climaplus@example.com',
            'hash_solo_para_pruebas',
            'SUPERADMIN',
            TRUE
        );

        RAISE EXCEPTION 'FAIL: se permitió rol inválido';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'ck_usuario_rol' THEN
                RAISE NOTICE 'PASS: rol inválido rechazado';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada: %', v_error;
            END IF;
    END;

    -- 4. Ubicación válida
    INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
    VALUES ('Ubicacion Prueba', 'Alta Verapaz', 'GT', 15.4700, -90.3700)
    RETURNING id_ubicacion INTO v_ubicacion;

    RAISE NOTICE 'PASS: ubicación válida';

    -- 5. Latitud fuera de rango
    BEGIN
        INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
        VALUES ('Latitud Invalida', 'Prueba', 'GT', 91, -90);

        RAISE EXCEPTION 'FAIL: se permitió latitud inválida';
    EXCEPTION
        WHEN check_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'ck_ubicacion_latitud' THEN
                RAISE NOTICE 'PASS: latitud inválida rechazada';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada: %', v_error;
            END IF;
    END;

    -- 6. Coordenadas duplicadas
    BEGIN
        INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
        VALUES ('Ubicacion Duplicada', 'Prueba', 'GT', 15.4700, -90.3700);

        RAISE EXCEPTION 'FAIL: se permitieron coordenadas duplicadas';
    EXCEPTION
        WHEN unique_violation THEN
            GET STACKED DIAGNOSTICS v_error = CONSTRAINT_NAME;
            IF v_error = 'uq_ubicacion_latitud_longitud' THEN
                RAISE NOTICE 'PASS: coordenadas duplicadas rechazadas';
            ELSE
                RAISE EXCEPTION 'FAIL: restricción inesperada: %', v_error;
            END IF;
    END;

    RAISE NOTICE 'Pruebas de usuario y ubicacion finalizadas';
END $$;

ROLLBACK;
