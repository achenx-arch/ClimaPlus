START TRANSACTION;

ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261028062000_ConsultaClimaUsuarioOpcional', '8.0.22');

COMMIT;
