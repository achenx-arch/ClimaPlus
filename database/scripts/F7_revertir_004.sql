-- =====================================================================
-- ClimaPlus – Reversión de la migración 004 (autenticación segura)
--
-- NO SE EJECUTA como parte de ningún despliegue. Solo para volver atrás si, después de aplicar la 004, se decide
-- regresar a la versión anterior de la API. La vía preferida es restaurar el respaldo tomado antes de la 004
-- (docs/despliegue/migracion-004-climaplus_db.md, sección 6); este script es la alternativa sin restaurar.
--
-- Equivale al Down() de 20261109180000_AutenticacionSegura. Todo en una transacción: si algo falla, no cambia nada.
-- Se niega a continuar si ya existen cuentas sin contraseña local (creadas con Google) o identidades de Google,
-- porque no se puede volver a exigir password_hash sin inventar contraseñas.
--
-- Qué se pierde: los desafíos de código pendientes, las vinculaciones con Google, la marca de "correo verificado" y
-- los sellos de sesión (los JWT emitidos dejan de llevar un sello válido para la API anterior: hay que iniciar sesión
-- de nuevo).
--
-- Uso (PowerShell):
--   psql -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -f database/scripts/F7_revertir_004.sql
-- =====================================================================

START TRANSACTION;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109180000_AutenticacionSegura') THEN
        RAISE EXCEPTION 'La migración 004 no está aplicada en esta base: no hay nada que revertir.';
    END IF;
    IF EXISTS (SELECT 1 FROM usuario WHERE password_hash IS NULL) THEN
        RAISE EXCEPTION 'Hay cuentas sin contraseña local (creadas con Google): restaura el respaldo previo en lugar de usar este script.';
    END IF;
    IF EXISTS (SELECT 1 FROM identidad_externa) THEN
        RAISE EXCEPTION 'Hay cuentas vinculadas con Google: restaura el respaldo previo en lugar de usar este script.';
    END IF;
END $$;

DROP TABLE desafio_autenticacion;

DROP TABLE identidad_externa;

ALTER TABLE usuario DROP COLUMN correo_verificado;

ALTER TABLE usuario DROP COLUMN sello_seguridad;

ALTER TABLE usuario ALTER COLUMN password_hash SET NOT NULL;

DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109180000_AutenticacionSegura';

COMMIT;
