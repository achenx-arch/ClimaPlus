START TRANSACTION;

ALTER TABLE desafio_autenticacion DROP CONSTRAINT ck_desafio_autenticacion_proposito;

ALTER TABLE desafio_autenticacion ADD CONSTRAINT ck_desafio_autenticacion_proposito CHECK (proposito IN ('VERIFICAR_CORREO', 'INICIO_SESION', 'INICIO_GOOGLE', 'VINCULAR_GOOGLE', 'RECUPERAR_PASSWORD', 'CAMBIAR_PASSWORD'));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261113150000_CambioPasswordConCodigo', '8.0.22');

COMMIT;
