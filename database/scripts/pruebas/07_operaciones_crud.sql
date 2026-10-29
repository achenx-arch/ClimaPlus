
-- ==================================================
-- CLIMAPLUS - SCRIPT 07
-- PRUEBAS DE OPERACIONES CRUD
-- ==================================================

BEGIN;

DO $$
DECLARE
    v_usuario BIGINT;
    v_ubicacion BIGINT;
    v_favorito BIGINT;
    v_total INTEGER := 0;
    v_cantidad INTEGER;
BEGIN

    -- 01: CREATE usuario
    INSERT INTO usuario
        (nombre, correo, password_hash, rol, activo)
    VALUES
        ('Usuario CRUD 07',
         'crud07.climaplus@example.com',
         'hash_prueba',
         'USUARIO',
         TRUE)
    RETURNING id_usuario INTO v_usuario;

    IF v_usuario IS NULL THEN
        RAISE EXCEPTION 'FAIL 01: usuario no creado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 01: usuario creado';

    -- 02: READ usuario
    SELECT COUNT(*) INTO v_cantidad
    FROM usuario
    WHERE id_usuario = v_usuario
      AND correo = 'crud07.climaplus@example.com';

    IF v_cantidad <> 1 THEN
        RAISE EXCEPTION 'FAIL 02: usuario no encontrado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 02: usuario consultado';

    -- 03: UPDATE nombre
    UPDATE usuario
    SET nombre = 'Usuario CRUD Modificado'
    WHERE id_usuario = v_usuario;

    IF NOT EXISTS (
        SELECT 1 FROM usuario
        WHERE id_usuario = v_usuario
          AND nombre = 'Usuario CRUD Modificado'
    ) THEN
        RAISE EXCEPTION 'FAIL 03: nombre no actualizado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 03: nombre actualizado';

    -- 04: UPDATE estado
    UPDATE usuario
    SET activo = FALSE
    WHERE id_usuario = v_usuario;

    IF NOT EXISTS (
        SELECT 1 FROM usuario
        WHERE id_usuario = v_usuario
          AND activo = FALSE
    ) THEN
        RAISE EXCEPTION 'FAIL 04: estado no actualizado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 04: usuario desactivado';

    -- 05: CREATE ubicacion
    INSERT INTO ubicacion
        (nombre, region, pais, latitud, longitud)
    VALUES
        ('Ubicacion CRUD 07',
         'Alta Verapaz',
         'GT',
         15.5000,
         -90.5000)
    RETURNING id_ubicacion INTO v_ubicacion;

    IF v_ubicacion IS NULL THEN
        RAISE EXCEPTION 'FAIL 05: ubicacion no creada';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 05: ubicacion creada';

    -- 06: READ ubicacion
    IF NOT EXISTS (
        SELECT 1 FROM ubicacion
        WHERE id_ubicacion = v_ubicacion
          AND nombre = 'Ubicacion CRUD 07'
    ) THEN
        RAISE EXCEPTION 'FAIL 06: ubicacion no encontrada';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 06: ubicacion consultada';

    -- 07: UPDATE ubicacion
    UPDATE ubicacion
    SET region = 'Region Actualizada'
    WHERE id_ubicacion = v_ubicacion;

    IF NOT EXISTS (
        SELECT 1 FROM ubicacion
        WHERE id_ubicacion = v_ubicacion
          AND region = 'Region Actualizada'
    ) THEN
        RAISE EXCEPTION 'FAIL 07: region no actualizada';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 07: region actualizada';

    -- 08: CREATE favorito
    INSERT INTO favorito
        (id_usuario, id_ubicacion)
    VALUES
        (v_usuario, v_ubicacion)
    RETURNING id_favorito INTO v_favorito;

    IF NOT EXISTS (
        SELECT 1 FROM favorito
        WHERE id_favorito = v_favorito
          AND id_usuario = v_usuario
          AND id_ubicacion = v_ubicacion
    ) THEN
        RAISE EXCEPTION 'FAIL 08: favorito no creado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 08: favorito creado';

    -- 09: DELETE favorito
    DELETE FROM favorito
    WHERE id_favorito = v_favorito;

    IF EXISTS (
        SELECT 1 FROM favorito
        WHERE id_favorito = v_favorito
    ) THEN
        RAISE EXCEPTION 'FAIL 09: favorito no eliminado';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 09: favorito eliminado';

    -- 10: DELETE usuario y ubicacion
    DELETE FROM usuario
    WHERE id_usuario = v_usuario;

    DELETE FROM ubicacion
    WHERE id_ubicacion = v_ubicacion;

    IF EXISTS (
        SELECT 1 FROM usuario
        WHERE id_usuario = v_usuario
    ) OR EXISTS (
        SELECT 1 FROM ubicacion
        WHERE id_ubicacion = v_ubicacion
    ) THEN
        RAISE EXCEPTION 'FAIL 10: registros no eliminados';
    END IF;

    v_total := v_total + 1;
    RAISE NOTICE 'PASS 10: usuario y ubicacion eliminados';

    -- RESULTADO
    IF v_total <> 10 THEN
        RAISE EXCEPTION 'FAIL: % de 10 pruebas aprobadas',
            v_total;
    END IF;

    RAISE NOTICE 'TOTAL: % de 10 pruebas CRUD aprobadas',
        v_total;

END $$;

ROLLBACK;
