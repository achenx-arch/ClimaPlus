# E-02 y E-03 – Informe de implementación para revisar antes de aplicar

**Fecha:** 2026-10-28 · **Rama:** `feature/backend` (sin commits) · **Implementa:** ACHX, en lugar del Integrante 1, que revisará
**Estado:** código y migraciones **preparados**. Las migraciones 002 y 003 **NO se han aplicado** a ninguna base de datos.

## 1. Condición inicial

| Comprobación | Resultado |
|---|---|
| Última compilación y pruebas antes de tocar el modelo | 221 de 221 aprobadas, 0 fallidas (confirmado por ACHX el 2026-10-28) |
| Rama | `feature/backend`, en `535c933` (igual que `main`) |
| Cambios previos sin commit | Fases 2E, 2F, 2H y alertas generadas: **preservados**, ninguno se modificó salvo `AyudasDatos.cs` (pruebas) |
| Migraciones existentes | Solo `20261016052608_InitialCreate` (+ Designer y snapshot); `database/migrations/001_InitialCreate.sql` |
| `InitialCreate` y `001_InitialCreate.sql` | **No se tocaron** |

## 2. Archivos

### Modelo (Domain / Data)

| Archivo | Cambio |
|---|---|
| `backend/ClimaPlus.Api/Domain/Entities/ConsultaClima.cs` | `long? IdUsuario`, `Usuario? Usuario`, nuevo `string? Detalle` (sección 2.1 de la especificación) |
| `backend/ClimaPlus.Api/Data/Configurations/ConsultaClimaConfiguration.cs` | `id_usuario` sin `IsRequired`; relación `IsRequired(false)` con `Restrict`; columna `detalle jsonb`; 2 CHECK (sección 2.2) |
| `backend/ClimaPlus.Api/Data/Migrations/20261028062000_ConsultaClimaUsuarioOpcional.cs` + `.Designer.cs` | Migración 002 (nueva) |
| `backend/ClimaPlus.Api/Data/Migrations/20261028062100_ConsultaClimaDetalle.cs` + `.Designer.cs` | Migración 003 (nueva) |
| `backend/ClimaPlus.Api/Data/Migrations/ClimaPlusDbContextModelSnapshot.cs` | Snapshot actualizado con E-02 y E-03 |

### Scripts SQL

| Archivo | Contenido |
|---|---|
| `database/migrations/002_ConsultaClimaUsuarioOpcional.sql` | Script de la migración 002 |
| `database/migrations/003_ConsultaClimaDetalle.sql` | Script de la migración 003 |
| `database/scripts/E02_E03_verificacion_previa.sql` | Inspección **de solo lectura** antes de aplicar |
| `database/scripts/E02_E03_verificacion_posterior.sql` | Inspección **de solo lectura** después de aplicar |
| `database/scripts/pruebas/E02_E03_consulta_clima.sql` | Pruebas SQL de la especificación (sección 4) en `BEGIN … ROLLBACK` |

### Pruebas

| Archivo | Qué prueba |
|---|---|
| `ClimaPlus.Api.Tests/Datos/ModeloConsultaClimaTests.cs` | Sin base de datos: modelo EF, orden de migraciones, snapshot sin cambios pendientes y SQL generado por EF idéntico a 002 y 003 |
| `ClimaPlus.Api.Tests/Datos/ConsultaAnonimaTests.cs` | InMemory: consulta anónima guardada con `id_usuario` NULL y `detalle`; nunca aparece como `ultimaConsulta` de un favorito |
| `ClimaPlus.Api.Tests/Datos/ConsultaClimaPostgreSqlTests.cs` | PostgreSQL **aislado** (`*_test`): esquema, E-02, E-03, CHECK y FK |
| `ClimaPlus.Api.Tests/Infraestructura/BaseDatosPruebas.cs` | Acceso a la base de pruebas, `[PostgreSqlFact]` y fixture que aplica las migraciones **solo** a una base cuyo nombre termina en `_test` |
| `ClimaPlus.Api.Tests/Infraestructura/AyudasDatos.cs` | `CrearConsultaAsync` acepta `idUsuario` NULL y `detalle`; las de PRONOSTICO llevan `detalle` |

### Documentos

`docs/enmiendas/E-02-consulta-anonima.md` y `E-03-detalle-jsonb.md` (nuevos); estado actualizado en `E-02-E-03-especificacion-tecnica.md`; `docs/base-datos/diccionario-datos.md`, `modelo-relacional.puml` y `.png` (regenerado con PlantUML 1.2024.7); `docs/fase-1/02`, `03`, `04`, `05`, `07`, `08` y `09`, según la sección 5 de la especificación y la sección 6 del análisis.

## 3. Migraciones

### 002 – `20261028062000_ConsultaClimaUsuarioOpcional` (E-02)

| | |
|---|---|
| Up | `AlterColumn<long>("id_usuario", "consulta_clima", type: "bigint", nullable: true)` |
| Down | `nullable: false` **sin valor por defecto**: falla si hay consultas anónimas y no toca datos |
| Diferencia con lo que genera `dotnet ef` | El andamiaje de EF pondría `defaultValue: 0L` en `Down`, lo que al revertir intentaría poner `id_usuario = 0` (no existe ese usuario) y dejaría `DEFAULT 0` en la columna. Se quitó a propósito. `Up` es idéntico |

```sql
START TRANSACTION;

ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261028062000_ConsultaClimaUsuarioOpcional', '8.0.22');

COMMIT;
```

### 003 – `20261028062100_ConsultaClimaDetalle` (E-03)

| | |
|---|---|
| Up | `AddColumn<string>("detalle", type: "jsonb", nullable: true)` + 2 `AddCheckConstraint` |
| Down | `DropCheckConstraint` ×2 + `DropColumn("detalle")` (pierde el contenido de `detalle`) |

```sql
START TRANSACTION;

ALTER TABLE consulta_clima ADD detalle jsonb;

ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_objeto CHECK (detalle IS NULL OR jsonb_typeof(detalle) = 'object');

ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_pronostico CHECK (tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261028062100_ConsultaClimaDetalle', '8.0.22');

COMMIT;
```

Los dos scripts contienen **exactamente** lo que exige la sección 3 de la especificación: ningún `DROP`, `CREATE TABLE`, `DELETE`, `TRUNCATE` ni cambios en otras tablas o índices. Cada uno es una transacción: si algo falla, no queda aplicado a medias.

### Cómo se generaron

En este entorno no hay .NET, así que las migraciones, sus Designer y el snapshot se escribieron a mano siguiendo el formato exacto de EF Core 8.0.22 (partiendo del Designer y el snapshot de `InitialCreate`). Para no depender de esa escritura manual, **las pruebas lo verifican con el propio EF Core**:

- `Migraciones_EstanEnOrden_YNoHayCambiosPendientes`: compara el snapshot con el modelo con el mismo criterio que `dotnet ef migrations has-pending-model-changes`.
- `Script002_…` y `Script003_…`: generan el SQL con el `IMigrator` de EF (sin conectarse) y lo comparan línea a línea con los archivos `002` y `003`.

Si alguna falla, se corrige el archivo antes de aplicar nada. Como verificación adicional se pueden usar los comandos de `dotnet ef` de la sección 6.

## 4. Verificaciones

| Verificación | Cómo | Estado |
|---|---|---|
| Sintaxis C# de todos los archivos | tree-sitter | Hecho aquí: 0 errores |
| Compilación y regresión (221 pruebas) | `dotnet build` y `dotnet test` en Windows | **Pendiente** (ACHX) |
| Snapshot sin cambios pendientes | Prueba automática y `dotnet ef migrations has-pending-model-changes` | **Pendiente** |
| SQL de EF = archivos 002 y 003 | Prueba automática y `dotnet ef migrations script` | **Pendiente** |
| Ocho tablas intactas, 26 CHECK, 12 columnas en `consulta_clima` | Prueba de modelo (sin BD) | **Pendiente** |
| Estado real de `climaplus_db` | `E02_E03_verificacion_previa.sql` | **Pendiente** (solo lectura) |
| Restricciones reales (CHECK, FK, JSON) | Pruebas `PostgreSQL` en `climaplus_test` y script SQL de pruebas | **Pendiente**, requiere autorización: aplican las migraciones a la base de pruebas |

Resultado esperado de `dotnet test` sin la variable `CLIMAPLUS_TEST_DB`: **231 aprobadas y 14 omitidas** (221 anteriores + 7 de modelo + 3 InMemory; las 14 de PostgreSQL se omiten).

## 5. Riesgos

| Riesgo | Mitigación |
|---|---|
| Error en la escritura manual del snapshot o los Designer | Pruebas automáticas de snapshot y SQL; `has-pending-model-changes`; alternativa: borrar los dos pares de archivos y regenerarlos con `dotnet ef migrations add` (sección 3 de la especificación) |
| Existen consultas PRONOSTICO antes de aplicar 003 | Verificación previa (consulta 4). Deberían ser 0: el pronóstico aún no se guarda |
| Revertir 002 con consultas anónimas | `Down` falla sin tocar datos. Revertir solo en desarrollo |
| Revertir 003 | Se pierde el contenido de `detalle`; respaldo previo |
| Cualquier visitante crea filas | Rate limiting de 30/min por IP y reutilización de consultas recientes (D-23); índice parcial más adelante si hiciera falta |
| JSON en un modelo relacional | Justificación de E-03 (dato de solo lectura); CHECK de forma; contenido validado por el backend |
| Las pruebas `PostgreSQL` aplican migraciones | Solo si existe `CLIMAPLUS_TEST_DB` y la base termina en `_test`; nunca `climaplus_db`; cada prueba hace `ROLLBACK` |
| Revisión del Integrante 1 | Este informe y las dos enmiendas son el material de revisión; nada está aplicado ni en Git |

## 6. Comandos (PowerShell, desde la raíz del repositorio)

### 6.1 Compilar y probar (no toca PostgreSQL)

```powershell
dotnet restore backend\ClimaPlus.sln --force
dotnet build backend\ClimaPlus.sln --no-restore --no-incremental
dotnet test backend\ClimaPlus.sln --no-build --logger "console;verbosity=normal"
```

### 6.2 Verificar con `dotnet ef` (no se conecta a la base)

```powershell
cd backend\ClimaPlus.Api
dotnet ef migrations list --no-connect
dotnet ef migrations has-pending-model-changes
dotnet ef migrations script 20261016052608_InitialCreate 20261028062000_ConsultaClimaUsuarioOpcional -o "$env:TEMP\002_ef.sql"
dotnet ef migrations script 20261028062000_ConsultaClimaUsuarioOpcional 20261028062100_ConsultaClimaDetalle -o "$env:TEMP\003_ef.sql"
cd ..\..
fc.exe /W database\migrations\002_ConsultaClimaUsuarioOpcional.sql "$env:TEMP\002_ef.sql"
fc.exe /W database\migrations\003_ConsultaClimaDetalle.sql "$env:TEMP\003_ef.sql"
```

### 6.3 Inspeccionar `climaplus_db` (solo lectura)

```powershell
psql -h localhost -U postgres -d climaplus_db -f database\scripts\E02_E03_verificacion_previa.sql > "$env:TEMP\E02_E03_previa.txt"
```

Esperado: solo `InitialCreate`; 8 tablas; 0 consultas PRONOSTICO; 11 columnas y 7 CHECK en `consulta_clima`; 24 CHECK en total.

### 6.4 Respaldo (antes de aplicar)

```powershell
New-Item -ItemType Directory -Force D:\respaldos\climaplus | Out-Null
$fecha = Get-Date -Format "yyyyMMdd_HHmm"
pg_dump -h localhost -U postgres -d climaplus_db -F c -f "D:\respaldos\climaplus\climaplus_db_antes_E02_E03_$fecha.dump"
pg_restore --list "D:\respaldos\climaplus\climaplus_db_antes_E02_E03_$fecha.dump" | Select-String "consulta_clima"
```

`pg_dump` y `psql` piden la contraseña; no la escribas en scripts ni en el repositorio. La carpeta del respaldo debe quedar **fuera** del repositorio.

Restauración (solo si hiciera falta, en una base nueva, sin borrar la actual):

```powershell
createdb -h localhost -U postgres climaplus_db_restaurada
pg_restore -h localhost -U postgres -d climaplus_db_restaurada "D:\respaldos\climaplus\climaplus_db_antes_E02_E03_<fecha>.dump"
```

### 6.5 Pruebas contra PostgreSQL aislado (solo cuando se autorice)

Aplican las migraciones 002 y 003 **a `climaplus_test`**, nunca a `climaplus_db`.

```powershell
createdb -h localhost -U postgres climaplus_test
$env:CLIMAPLUS_TEST_DB = "Host=localhost;Port=5432;Database=climaplus_test;Username=postgres;Password=<contraseña>"
dotnet test backend\ClimaPlus.sln --no-build --filter "Categoria=PostgreSQL" --logger "console;verbosity=normal"
Remove-Item Env:CLIMAPLUS_TEST_DB
```

### 6.6 Aplicar a `climaplus_db` (solo después de la revisión y la autorización)

```powershell
cd backend\ClimaPlus.Api
dotnet ef database update 20261028062000_ConsultaClimaUsuarioOpcional
dotnet ef database update 20261028062100_ConsultaClimaDetalle
cd ..\..
psql -h localhost -U postgres -d climaplus_db -f database\scripts\E02_E03_verificacion_posterior.sql > "$env:TEMP\E02_E03_posterior.txt"
psql -h localhost -U postgres -d climaplus_db -f database\scripts\pruebas\E02_E03_consulta_clima.sql
```

Aplicarlas por separado permite detenerse entre una y otra. Guardar las salidas como evidencia en `docs/base-datos/evidencias/`.

## 7. Criterios de aceptación

1. `dotnet build` sin errores ni advertencias nuevas.
2. `dotnet test`: 231 aprobadas, 14 omitidas, 0 fallidas (incluye las 221 anteriores).
3. `has-pending-model-changes` sin cambios; los scripts de `dotnet ef` coinciden con 002 y 003.
4. Verificación previa: 0 consultas PRONOSTICO y solo `InitialCreate` aplicada.
5. Respaldo creado y listado con `pg_restore --list`.
6. Pruebas `PostgreSQL` (14) aprobadas en `climaplus_test`.
7. Tras aplicar: verificación posterior con 3 migraciones, `id_usuario` NULL, `detalle jsonb`, 9 CHECK en `consulta_clima`, 26 en total, 8 tablas y el mismo número de filas que antes.
8. Pruebas SQL: casos 1 y 2 aceptados; 3 a 7 rechazados por la restricción indicada.
9. Revisión del Integrante 1 registrada antes del commit.

## 8. Lo que sigue (después de aplicar)

Implementar `IRegistroConsultaClima` y los endpoints `GET /api/clima/actual` y `/pronostico` (D-20, D-23), el historial (2G) y el motor de alertas (2H, RN-BD-42).
