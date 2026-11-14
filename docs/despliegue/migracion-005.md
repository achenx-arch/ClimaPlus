# Migración 005: cambio de contraseña con código (climaplus_test y climaplus_db)

**Fecha:** 2026-11-13 · Autorizada por el responsable del proyecto para `climaplus_test` y, **solo después** de que la
compilación, las pruebas y la migración de prueba resulten satisfactorias, para `climaplus_db`.
`climaplus_db_ensayo` no se toca.

## 1. Qué cambia

| | |
|---|---|
| Migración EF | `20261113150000_CambioPasswordConCodigo` (`backend/ClimaPlus.Api/Data/Migrations/`) |
| Script SQL | `database/migrations/005_CambioPasswordConCodigo.sql` (idéntico al que genera EF; lo comprueba `ModeloAutenticacionTests.Script005_*`) |
| Esquema | Solo reemplaza `ck_desafio_autenticacion_proposito` para admitir `CAMBIAR_PASSWORD`, conservando `VERIFICAR_CORREO`, `INICIO_SESION`, `INICIO_GOOGLE`, `VINCULAR_GOOGLE` y `RECUPERAR_PASSWORD` |
| Datos | Ninguno: no crea, borra ni actualiza filas, tablas, columnas, índices, usuarios, roles ni credenciales |
| Bloqueo | `ALTER TABLE desafio_autenticacion` (unos milisegundos: valida las filas existentes contra el CHECK nuevo) |
| Reversión | `database/scripts/F7_revertir_005.sql` (sección 7) o restaurar el respaldo |

No hizo falta ningún otro cambio de esquema: la columna `proposito` es `varchar(24)` y `CAMBIAR_PASSWORD` tiene 16
caracteres; el índice único parcial `uq_desafio_autenticacion_vivo (id_usuario, proposito)` ya garantiza un solo
desafío vivo por cuenta y propósito.

**La API nueva necesita la 005**: sin ella, `/health` responde 503 (migración pendiente) y el paso 1 del cambio de
contraseña fallaría al guardar el desafío. Despliega el código nuevo y la 005 juntos.

## 2. Comportamiento nuevo (resumen)

1. `POST /api/usuarios/me/password/cambio` con contraseña actual, nueva y confirmación → si son correctas, código de
   6 dígitos al correo (5 minutos, 5 intentos, un solo uso) y 202. **No cambia nada.**
2. `POST /api/usuarios/me/password/cambio/verificar` con el código y los mismos datos → en **una transacción**: hash
   nuevo, desafío consumido, sello de seguridad renovado (todas las sesiones cerradas, también la actual) y demás
   desafíos pendientes anulados. Después, aviso por correo. 204.
3. El antiguo `PUT /api/usuarios/me/password` (sin código) ya no existe.

Protecciones: bloqueo de la contraseña actual por cuenta + IP (5 en 15 min) y por cuenta (20), 10 códigos incorrectos
por hora y cuenta, rate limiting por IP, el desafío debe ser de la cuenta de la sesión, sin contraseñas ni códigos en
los logs. Contrato completo: [`../api/contrato-auth-perfil.md`](../api/contrato-auth-perfil.md), sección 4.6.

## 3. Herramientas

| Script | Qué hace |
|---|---|
| `database/scripts/F7_aplicar_005.ps1` | Comprueba variables de entorno, conexión efectiva, estado y conexiones abiertas; con `-Aplicar`: respaldo `pg_dump -Fc` + SHA-256 + índice, verificación previa, **solo** la 005, verificación posterior y prueba de la restricción. Para `climaplus_db` exige además la 005 verificada en `climaplus_test` y el `.trx` de las pruebas sin fallos ni pruebas de PostgreSQL omitidas |
| `database/scripts/M005_verificacion.sql` | Solo lectura: migraciones, filas y huella md5 completa de las 10 tablas, usuarios por rol, restricciones e índices, la restricción de propósitos y los desafíos. No muestra correos ni contraseñas |
| `database/scripts/M005_comprobar_restriccion.sql` | Prueba la restricción en una tabla **temporal** (copia de la estructura) y termina con `ROLLBACK`: no escribe en ninguna tabla real |
| `database/scripts/F7_revertir_005.sql` | Reversión controlada (se detiene si hay desafíos `CAMBIAR_PASSWORD`) |
| `docs/herramientas/F7_verificar_api.ps1` | `/health`, los dos endpoints nuevos sin sesión (401) y el antiguo (404); opcionalmente el frontend |

Ensayo realizado por el asistente (PostgreSQL 16 local, base desechable con datos de muestra): 005 aplicada y
revertida y vuelta a aplicar; verificación previa y posterior idénticas salvo la migración y la restricción;
`M005_comprobar_restriccion.sql` correcto; pruebas SQL 01 a 09 correctas sobre una base nueva con 001 a 005; el script
`F7_aplicar_005.ps1` probado con `pwsh` en los casos correcto, ya aplicada, sin `.trx`, `.trx` con fallos o con
pruebas de PostgreSQL omitidas, conexión abierta, `ConnectionStrings__ClimaPlusDb`/`CLIMAPLUS_TEST_DB` apuntando a otra
base y `PGHOSTADDR` definida (todos se detienen sin modificar nada). El respaldo generado se restauró en otra base y
coincidió con la verificación previa. **No se ejecutó en tu PostgreSQL 18 de Windows**: eso lo hace la sección 5.

## 4. Preparación (PowerShell 5.1, una sola consola)

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
Set-Location $repo
$env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
$resultados = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "ClimaPlus-respaldos"
New-Item -ItemType Directory -Force $resultados | Out-Null

# Contraseña de PostgreSQL: se pide sin eco y solo vive en esta consola (no se escribe en ningún archivo).
$s = Read-Host "Contraseña de PostgreSQL (postgres)" -AsSecureString
$b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
$env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b)
$env:CLIMAPLUS_TEST_DB = "Host=localhost;Port=5432;Database=climaplus_test;Username=postgres;Password=$env:PGPASSWORD"
Remove-Variable s, b
```

Detén la API (`Ctrl+C` en la consola de `dotnet run`) y cierra pgAdmin antes de los pasos con `-Aplicar`.

## 5. Orden de ejecución

### 5.1 Compilar

```powershell
dotnet build backend\ClimaPlus.sln -c Release
```

Debe terminar con **0 errores**. Si hay errores, detente y compártelos (sin contraseñas).

### 5.2 climaplus_test: comprobar, respaldar y aplicar la 005

```powershell
powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_test
powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_test -Aplicar
```

Debe terminar con `RESULTADO: CORRECTO` y `Código de salida: 0`. Si `climaplus_test` ya tuviera la 005 (porque las
pruebas la aplicaron antes), el script lo indica, comprueba la restricción y no cambia nada.

### 5.3 Pruebas con PostgreSQL (y Mailpit si tienes Docker)

Las 27 pruebas que antes aparecían omitidas son 26 de PostgreSQL (necesitan `CLIMAPLUS_TEST_DB`) y 1 de entrega por
Mailpit (necesita `MAILPIT_URL` y Mailpit con SMTP en 1025). Las de PostgreSQL trabajan en transacciones que se revierten
y solo aceptan bases cuyo nombre termina en `_test`. La de Mailpit envía a una dirección `*.local` que nunca sale a
internet.

```powershell
# Opcional (Docker Desktop): Mailpit con la interfaz en 8025 y el SMTP en 1025, solo en este equipo.
docker run -d --rm --name climaplus-mailpit -p 127.0.0.1:8025:8025 -p 127.0.0.1:1025:1025 axllent/mailpit:v1.33.1
$env:MAILPIT_URL = "http://localhost:8025"

$trx = Join-Path $resultados ("pruebas-005-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
dotnet test backend\ClimaPlus.sln -c Release --logger "trx;LogFileName=pruebas-005.trx" --results-directory $trx
$archivoTrx = Join-Path $trx "pruebas-005.trx"

docker stop climaplus-mailpit   # si lo levantaste
```

Resultado esperado: **0 fallidas**. Con Mailpit, 0 omitidas; sin Mailpit, 1 omitida (la de entrega). Ninguna prueba de
PostgreSQL debe aparecer omitida.

Frontend:

```powershell
Set-Location frontend
# npm install   # solo si falta node_modules o cambió package-lock.json
npm run lint
npx vitest run
npm run build
Set-Location $repo
```

### 5.4 climaplus_db: SOLO si 5.1 a 5.3 fueron correctos

```powershell
# Comprobaciones (no modifica nada)
powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_db
# Respaldo + 005 en la base oficial
powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_db -Aplicar -ResultadoPruebas $archivoTrx
```

El script se detiene **antes de modificar la base oficial** si: alguna variable redirige la conexión
(`PGHOSTADDR`, `PGSERVICE`, `ConnectionStrings__ClimaPlusDb` hacia otra base, `CLIMAPLUS_TEST_DB` hacia algo que no sea
`climaplus_test`), la base conectada no es `climaplus_db`, no tiene exactamente las 4 migraciones anteriores, hay otras
conexiones abiertas, la 005 no está verificada en `climaplus_test`, el `.trx` es anterior a esa migración, tiene fallos
o tiene pruebas de PostgreSQL omitidas, o el respaldo no se pudo generar o validar.

Al terminar con `RESULTADO: CORRECTO`, la carpeta `Documentos\ClimaPlus-respaldos\005-climaplus_db-<fecha>\` contiene:
`respaldo-climaplus_db-<fecha>.dump` (+ `.sha256`), `respaldo-indice.txt`, `verificacion-antes.txt`,
`verificacion-despues.txt`, `aplicar-005.txt`, `comprobar-restriccion.txt` y `resumen.txt`. **Esa carpeta contiene
datos personales y hashes: nunca la subas a Git.**

## 6. Reiniciar la API y verificar

```powershell
# Consola 1: API (con tu configuración actual: secretos de usuario; no cambies las credenciales SMTP)
dotnet run --project backend\ClimaPlus.Api --launch-profile http

# Consola 2: frontend
Set-Location frontend; npm run dev

# Consola 3: comprobaciones
powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_verificar_api.ps1 -UrlApi http://localhost:5208 -UrlFrontend http://localhost:5173
```

En el log de inicio de la API debe aparecer la base de datos sin migraciones pendientes y `/health` debe responder
`Healthy`. Prueba manual (2 minutos): inicia sesión → Mi perfil → Cambiar contraseña → contraseña actual, nueva y
confirmación → **Continuar** → llega el correo "Confirma el cambio de contraseña" → escribe el código → **Confirmar
cambio** → la sesión se cierra → entra con la contraseña nueva (y comprueba que la anterior ya no sirve). Llega también
el aviso "Tu contraseña de ClimaPlus cambió".

## 7. Si algo sale mal

- **Falla antes del paso 8 del script:** no se modificó nada. Lee `resumen.txt`.
- **Falla la 005 (paso 8):** la transacción se revierte sola; la base queda como estaba. Revisa `aplicar-005.txt`.
- **Falla la verificación posterior (paso 9):** detén la API y compara `verificacion-antes.txt` con
  `verificacion-despues.txt`. Para volver atrás sin restaurar:

  ```powershell
  psql -X -h localhost -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -f database\scripts\F7_revertir_005.sql
  ```

  (La API nueva no funciona sin la 005: vuelve también al código anterior.)
- **Restaurar el respaldo completo** (última opción; API detenida, sin otras conexiones). Restaura en una base nueva y
  comprueba antes de reemplazar nada:

  ```powershell
  createdb -h localhost -U postgres climaplus_db_restaurada
  pg_restore -h localhost -U postgres -d climaplus_db_restaurada --no-owner "<carpeta>\respaldo-climaplus_db-<fecha>.dump"
  psql -X -A -t -h localhost -U postgres -d climaplus_db_restaurada -f database\scripts\M005_verificacion.sql
  ```

  Reemplazar `climaplus_db` por la restaurada requiere una decisión explícita del responsable.

## 8. Docker y servidor

- Volumen nuevo: PostgreSQL ejecuta `database/migrations/001` a `005` automáticamente la primera vez.
- Volumen existente: aplica la 005 a mano dentro del contenedor (API detenida, después de un respaldo como el de
  [`guia-despliegue.md`](guia-despliegue.md#respaldo-de-la-base-de-datos)):

  ```powershell
  docker compose stop api
  docker compose cp database\migrations\005_CambioPasswordConCodigo.sql db:/tmp/005.sql
  docker compose exec -T db psql -U climaplus -d climaplus_db -v ON_ERROR_STOP=1 -f /tmp/005.sql
  docker compose cp database\scripts\M005_comprobar_restriccion.sql db:/tmp/m005.sql
  docker compose exec -T db psql -U climaplus -d climaplus_db -v ON_ERROR_STOP=1 -f /tmp/m005.sql
  docker compose exec -T db rm /tmp/005.sql /tmp/m005.sql
  docker compose start api
  ```
