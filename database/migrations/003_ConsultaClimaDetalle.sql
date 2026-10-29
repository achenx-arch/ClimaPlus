START TRANSACTION;

ALTER TABLE consulta_clima ADD detalle jsonb;

ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_objeto CHECK (detalle IS NULL OR jsonb_typeof(detalle) = 'object');

ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_pronostico CHECK (tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261028062100_ConsultaClimaDetalle', '8.0.22');

COMMIT;
