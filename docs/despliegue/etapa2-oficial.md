# Etapa 2 · Procedimiento definitivo sobre `climaplus_db`

Windows PowerShell 5.1 + PostgreSQL 18.3. Parte de lo ya aprobado (respaldo con SHA-256, restauración en
`climaplus_db_ensayo`, 004 en la copia, `/health` Healthy). **Cada bloque marcado 🔒 modifica `climaplus_db` y se
ejecuta solo con la autorización del responsable.**

Orden: 1 → 2 🔒 → 3 → 4 → 5 🔒 → 6 → 7 🔒 (más adelante).

## 0. Sesión (una ventana de PowerShell **nueva**)

Una ventana nueva garantiza que no quede la variable `ConnectionStrings__ClimaPlusDb` de la API de ensayo.

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
$env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
Set-Location $repo
```

Detén cualquier API (Ctrl+C en su ventana) y cierra pgAdmin u otras conexiones a `climaplus_db`.

## 1. Comprobaciones previas (no modifica nada)

```powershell
powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_aplicar_004_oficial.ps1
"Código de salida: $LASTEXITCODE"
```

Comprueba, sin escribir: respaldo de la Etapa 2 (SHA-256, índice legible, base `climaplus_db`), que `climaplus_db`
tenga exactamente las 3 migraciones previas y no la 004, que **no haya cambiado desde el respaldo** y que no haya
otras conexiones. Toma por defecto la carpeta más reciente de `Documentos\ClimaPlus-respaldos` (otra:
`-CarpetaRespaldo "ruta"`).

Esperado: `RESULTADO: comprobaciones previas correctas. NO se modificó nada.` y código 0. Si dice que la base cambió
desde el respaldo, primero haz un respaldo nuevo (`F7_E2_respaldo_y_ensayo.ps1 -Reemplazar`).

## 2. 🔒 Aplicar la migración 004 en `climaplus_db`

```powershell
powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_aplicar_004_oficial.ps1 -Aplicar
"Código de salida: $LASTEXITCODE"
```

Repite las comprobaciones del paso 1 y, solo si pasan, aplica `004_AutenticacionSegura.sql` (una transacción,
`ON_ERROR_STOP`, `lock_timeout` de 10 s). Después verifica en modo solo lectura:

| Comprobación | Esperado |
|---|---|
| Migraciones | Las 3 anteriores + `20261109180000_AutenticacionSegura` |
| Filas de las 8 tablas | Idénticas a antes; `desafio_autenticacion` e `identidad_externa` en 0 |
| Huella de usuarios (id, correo, hash, rol, activo) | Idéntica |
| Usuarios por rol | Idénticos |
| Columnas nuevas | Todos sin verificar, un sello distinto por usuario, ninguno sin contraseña |
| CHECK | 38 |

Esperado: `RESULTADO: la 004 quedó aplicada en climaplus_db y los datos existentes están intactos.` y código 0.
Los detalles quedan en `…\ClimaPlus-respaldos\etapa2-…\aplicacion-004-AAAAMMDD-HHMMSS\` (`resumen.txt`,
`previa-antes-de-004.txt`, `aplicar-004.txt`, `posterior.txt`).

- Si la 004 falla, se revierte sola (nada cambia) y el código es 1.
- Si la verificación posterior muestra diferencias: no uses la API; recuperación en
  `migracion-004-climaplus_db.md`, sección 8 (restaurar el respaldo verificado en el paso 1).

## 3. API apuntando a `climaplus_db`

```powershell
# a) La variable de entorno no debe existir en ningún ámbito (tendría prioridad sobre User Secrets)
foreach ($ambito in "Process", "User", "Machine") {
    $v = [Environment]::GetEnvironmentVariable("ConnectionStrings__ClimaPlusDb", $ambito)
    if ($v) { if ($v -match '(?i)Database=([^;]+)') { "$ambito : DEFINIDA -> $($Matches[1])" } else { "$ambito : DEFINIDA" } } else { "$ambito : no definida" }
}
```

Si aparece alguna como DEFINIDA: `Process` → `Remove-Item Env:ConnectionStrings__ClimaPlusDb`;
`User` → `[Environment]::SetEnvironmentVariable("ConnectionStrings__ClimaPlusDb", $null, "User")` y abre una
ventana nueva; `Machine` → lo mismo con `"Machine"` en PowerShell como administrador.

```powershell
# b) User Secrets -> climaplus_db (sin mostrar la contraseña)
Set-Location "$repo\backend\ClimaPlus.Api"
$linea = dotnet user-secrets list | Where-Object { $_ -like 'ConnectionStrings:ClimaPlusDb = *' } | Select-Object -First 1
if (-not $linea) { throw "No hay ConnectionStrings:ClimaPlusDb en User Secrets." }
$cadena = ($linea -replace '^ConnectionStrings:ClimaPlusDb = ', '') -replace '(?i)(Database|Initial Catalog)=[^;]*', 'Database=climaplus_db'
if ($cadena -notmatch '(?i)(^|;)Database=climaplus_db(;|$)') { throw "No se pudo apuntar a climaplus_db." }
dotnet user-secrets set "ConnectionStrings:ClimaPlusDb" $cadena | Out-Null
Remove-Variable linea, cadena
dotnet user-secrets list | Select-String "ConnectionStrings" | ForEach-Object { if ($_ -match "(?i)Database=([^;]+)") { "User Secrets -> Database=" + $Matches[1] } }

# c) Correo real configurado (solo muestra el servidor, no credenciales)
dotnet user-secrets list | Select-String '^Correo:(Host|Puerto|Seguridad|Remitente) '
```

Esperado: `User Secrets -> Database=climaplus_db` y `Correo:Host` de tu proveedor real (no `localhost`); si no,
configúralo con `docs/despliegue/correo-smtp.md`, sección 3.2.

Las pruebas automáticas no dependen de este cambio: usan `CLIMAPLUS_TEST_DB` (`climaplus_test`).

```powershell
# d) Arrancar la API (esta ventana queda ocupada)
dotnet run --launch-profile http
```

En otra ventana:

```powershell
Invoke-RestMethod http://localhost:5208/health     # Healthy (con una base sin la 004 respondería Unhealthy)
```

En la consola de la API no debe aparecer `Base de datos: faltan_migraciones`.

## 4. Registrar y verificar el administrador real

```powershell
Set-Location "$repo\frontend"
npm run dev        # http://localhost:5173 (usa la API 5208)
```

En una ventana privada del navegador: **Crear cuenta** con tu correo real y una contraseña segura → escribe el código
del correo → **Iniciar sesión** (correo, contraseña y código) → entra al Dashboard de usuario.

Comprobación (solo lectura; correos enmascarados, se puede compartir):

```powershell
Set-Location $repo
$correo = (Read-Host "Correo del nuevo administrador").Trim().ToLower()
$env:PGOPTIONS = "-c default_transaction_read_only=on"
psql -X -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -v "correo=$correo" -f database\scripts\F7_E2_verificar_ensayo.sql
Remove-Item Env:PGOPTIONS
```

Esperado en la sección 4: `rol = USUARIO`, `activo = t`, **`correo_verificado = t`**, `con_password = t`.
No sigas al paso 5 si `correo_verificado` es `f`.

## 5. 🔒 Promover la cuenta a ADMINISTRADOR

El script vuelve a exigir correo verificado, contraseña local, cuenta activa y rol USUARIO.

```powershell
# Ensayo (ROLLBACK): muestra el antes y el después sin guardar nada
psql -X -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -v "correo=$correo" -f database\scripts\F7_E2_promover_administrador.sql
"Código de salida: $LASTEXITCODE"

# Confirmado
psql -X -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -v "correo=$correo" -v confirmar=SI -v autorizado_oficial=SI -f database\scripts\F7_E2_promover_administrador.sql
"Código de salida: $LASTEXITCODE"
```

Esperado: `== CONFIRMADO: la cuenta ahora es ADMINISTRADOR` y código 0. Se cierran las sesiones abiertas de esa cuenta.

## 6. Comprobar el administrador nuevo (las cuentas ficticias se conservan)

1. Inicia sesión otra vez (correo, contraseña y código) → debe llevarte a **Administración** (`/admin`).
2. **Alertas** (`/admin/alertas`) carga; en F12 → Red, `GET /api/admin/alertas` → 200.
3. Repite la comprobación del paso 4: sección 4 con `rol = ADMINISTRADOR`; sección 6 con 2 administradores activos
   (el anterior y el nuevo).

No se elimina ninguna cuenta en esta fase.

## 7. 🔒 (Más adelante) Eliminar las dos cuentas ficticias

Solo cuando el paso 6 esté aprobado. Toma los dos `id_usuario` de la sección 5 de la verificación.

```powershell
# Informe previo (ROLLBACK): cuentas, filas por tabla y totales antes/después; no cambia nada
psql -X -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -v ids=ID1,ID2 -v cantidad=2 -f database\scripts\F7_E2_limpieza_cuentas.sql
```

Si una de las dos es el administrador anterior, el script se detiene; agrega `-v permitir_admin=SI` (el nuevo
administrador ya quedó comprobado en el paso 6). Para confirmar, con un respaldo nuevo previo
(`F7_E2_respaldo_y_ensayo.ps1 -Reemplazar`):

```powershell
psql -X -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -v ids=ID1,ID2 -v cantidad=2 -v confirmar=SI -v autorizado_oficial=SI -f database\scripts\F7_E2_limpieza_cuentas.sql
```

La limpieza nunca borra ubicaciones ni consultas anónimas, y comprueba al final que sus totales no cambiaron y que
queda al menos un administrador activo; si algo no cuadra, se deshace entera.
