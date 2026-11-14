-- =====================================================================
-- ClimaPlus – Revertir la migración 005 (SOLO si fuera imprescindible y con autorización).
-- Vuelve a dejar ck_desafio_autenticacion_proposito con los 5 propósitos de la 004 y quita la 005 del historial.
-- Antes, la API debe estar detenida y en la versión anterior (sin el cambio de contraseña con código).
--
-- Si existen desafíos CAMBIAR_PASSWORD, la restricción anterior no los admite: este script se DETIENE y no cambia
-- nada. En ese caso, decidir con el responsable si se eliminan esos desafíos (son temporales: códigos de 5 minutos)
-- con:  DELETE FROM desafio_autenticacion WHERE proposito = 'CAMBIAR_PASSWORD';
-- No toca usuarios ni ninguna otra tabla.
-- Uso: psql -X -U postgres -d <base> -v ON_ERROR_STOP=1 -f database/scripts/F7_revertir_005.sql
-- =====================================================================
\set ON_ERROR_STOP on
BEGIN;
SET LOCAL lock_timeout = '10s';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113150000_CambioPasswordConCodigo') THEN
        RAISE EXCEPTION 'La 005 no está aplicada en %: no hay nada que revertir.', current_database();
    END IF;
    IF EXISTS (SELECT 1 FROM desafio_autenticacion WHERE proposito = 'CAMBIAR_PASSWORD') THEN
        RAISE EXCEPTION 'Hay desafíos CAMBIAR_PASSWORD en %: revisar con el responsable antes de revertir (ver encabezado).', current_database();
    END IF;
END $$;

ALTER TABLE desafio_autenticacion DROP CONSTRAINT ck_desafio_autenticacion_proposito;
ALTER TABLE desafio_autenticacion ADD CONSTRAINT ck_desafio_autenticacion_proposito CHECK (proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD'));
DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113150000_CambioPasswordConCodigo';

COMMIT;
\echo 'Migración 005 revertida.'
