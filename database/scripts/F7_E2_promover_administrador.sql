-- =====================================================================
-- ClimaPlus – Fase 7, Etapa 2: promover a ADMINISTRADOR una cuenta real ya registrada y verificada
--
-- Por qué existe: el seeder (AdminSeeder) solo crea el administrador inicial cuando NO hay ningún administrador; mientras
-- exista el anterior no actúa, y el plan exige no borrar el anterior hasta probar el nuevo. Este script promueve una
-- cuenta que se registró por el flujo normal (correo verificado con código y contraseña elegida por su dueño).
--
-- Requiere la migración 004. POR DEFECTO NO CAMBIA NADA (ROLLBACK); confirma con -v confirmar=SI y, sobre
-- climaplus_db, además -v autorizado_oficial=SI.
--
-- Comprobaciones: la cuenta existe, está activa, es USUARIO, tiene el correo verificado y contraseña local.
-- Efecto: rol = ADMINISTRADOR y un sello de seguridad nuevo (las sesiones abiertas de esa cuenta se cierran; al
-- volver a entrar, su token ya lleva el rol nuevo).
--
-- Uso:
--   psql -X -U postgres -d climaplus_db_ensayo -v correo=nombre@dominio.com -f database\scripts\F7_E2_promover_administrador.sql
-- =====================================================================

\pset pager off
\set ON_ERROR_STOP on
\if :{?correo}
\else
  \echo 'Falta -v correo=...'
  \quit
\endif
\if :{?confirmar}
\else
  \set confirmar NO
\endif
\if :{?autorizado_oficial}
\else
  \set autorizado_oficial NO
\endif

BEGIN;

CREATE TEMP TABLE parametros ON COMMIT DROP AS
SELECT lower(btrim(:'correo')) AS correo,
       upper(:'confirmar') = 'SI' AS confirmar,
       upper(:'autorizado_oficial') = 'SI' AS autorizado_oficial;

DO $$
DECLARE p parametros%ROWTYPE; u record;
BEGIN
    IF to_regclass('public.desafio_autenticacion') IS NULL THEN
        RAISE EXCEPTION 'La base no tiene la migración 004: no se cambió nada.';
    END IF;
    SELECT * INTO p FROM parametros;
    SELECT * INTO u FROM usuario WHERE correo = p.correo;
    IF NOT FOUND THEN RAISE EXCEPTION 'No existe una cuenta con ese correo: no se cambió nada.'; END IF;
    IF u.rol <> 'USUARIO' THEN RAISE EXCEPTION 'La cuenta ya es %: no se cambió nada.', u.rol; END IF;
    IF NOT u.activo THEN RAISE EXCEPTION 'La cuenta está desactivada: no se cambió nada.'; END IF;
    IF NOT u.correo_verificado THEN RAISE EXCEPTION 'La cuenta no ha verificado su correo: no se cambió nada.'; END IF;
    IF u.password_hash IS NULL THEN RAISE EXCEPTION 'La cuenta no tiene contraseña local (solo Google): establece una antes. No se cambió nada.'; END IF;
    IF p.confirmar AND current_database() = 'climaplus_db' AND NOT p.autorizado_oficial THEN
        RAISE EXCEPTION 'Para confirmar en climaplus_db hace falta -v autorizado_oficial=SI: no se cambió nada.';
    END IF;
END $$;

\qecho '== Antes'
SELECT id_usuario, correo, rol, activo, correo_verificado FROM usuario WHERE correo = (SELECT correo FROM parametros);

UPDATE usuario SET rol = 'ADMINISTRADOR', sello_seguridad = gen_random_uuid()
WHERE correo = (SELECT correo FROM parametros) AND rol = 'USUARIO';

\qecho '== Después (dentro de la transacción)'
SELECT id_usuario, correo, rol, activo, correo_verificado FROM usuario WHERE correo = (SELECT correo FROM parametros);
SELECT count(*) AS administradores_activos FROM usuario WHERE rol = 'ADMINISTRADOR' AND activo;

SELECT confirmar AS confirmar_si FROM parametros \gset
\if :confirmar_si
  COMMIT;
  \qecho '== CONFIRMADO: la cuenta ahora es ADMINISTRADOR. Debe cerrar sesión y volver a entrar.'
\else
  ROLLBACK;
  \qecho '== ENSAYO: se deshizo todo (ROLLBACK). Para confirmar: -v confirmar=SI'
\endif
