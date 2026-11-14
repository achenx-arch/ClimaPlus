# Migración 004 en la base oficial `climaplus_db`: respaldo, aplicación, verificación y recuperación

Procedimiento de la Etapa 2 de la Fase 7. **Nada de esto se ejecuta sin la autorización explícita del responsable
del proyecto.** Los pasos 1 y 2 solo leen; desde el paso 3 se crean archivos o bases, y desde el paso 5 se modifica
`climaplus_db`.

Qué hace la 004 y por qué la necesita la API: [`notas-version-autenticacion.md`](notas-version-autenticacion.md).
Script: `database/migrations/004_AutenticacionSegura.sql`. Reversión sin restaurar:
`database/scripts/F7_revertir_004.sql`.

## 0. Antes de empezar

- Usa las herramientas de **tu** PostgreSQL de Windows (por ejemplo `C:\Program Files\PostgreSQL\18\bin`), en
  PowerShell. `pg_dump` debe ser de la misma versión que el servidor o más nueva.
- La contraseña de PostgreSQL la pide cada comando; no la escribas en scripts ni en archivos.
- Trabaja en una carpeta fuera del repositorio para los respaldos (contienen datos personales y hashes de
  contraseñas): por ejemplo `D:\Respaldos\ClimaPlus`. **Nunca los subas a Git.**
- Detén la API (`Ctrl+C` en la terminal de `dotnet run`) antes del paso 5: la 004 bloquea la tabla `usuario`
  unos segundos.

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
$resp = "D:\Respaldos\ClimaPlus"
New-Item -ItemType Directory -Force $resp | Out-Null
Set-Location $resp
```

## 1. Diagnóstico (solo lectura)

**Forma recomendada:** el script `database/scripts/F7_diagnostico_climaplus_db.ps1` hace todo este paso de una vez y
deja una carpeta con `resumen.txt` (conclusiones), `migraciones-*.txt`, `esquema-*.sql`, `diferencias.txt` y
`previa.txt`. Abre todas las conexiones con `default_transaction_read_only=on` (cualquier escritura fallaría), pide
la contraseña una sola vez sin guardarla y no lista correos.

```powershell
cd $repo
powershell -ExecutionPolicy Bypass -File database\scripts\F7_diagnostico_climaplus_db.ps1
# Opcionales: -Puerto 5432 -Usuario postgres -Salida D:\Respaldos\ClimaPlus\diagnostico
```

Por defecto guarda los resultados en `Documentos\ClimaPlus-diagnostico\etapa2-AAAAMMDD-HHMM`. Comandos
equivalentes, uno por uno:

```powershell
# Migraciones registradas en cada base
foreach ($b in "climaplus_db","climaplus_test") {
  psql -U postgres -d $b -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;' | Out-File -Encoding utf8 "migraciones-$b.txt"
}

# Diferencias de esquema (solo estructura, sin datos)
foreach ($b in "climaplus_db","climaplus_test") {
  pg_dump -U postgres -d $b --schema-only --no-owner --no-privileges -f "esquema-$b.sql"
}
Compare-Object (Get-Content esquema-climaplus_db.sql) (Get-Content esquema-climaplus_test.sql) |
  Where-Object { $_.InputObject -notmatch '^\\(un)?restrict ' } | Out-File -Encoding utf8 diferencias.txt

# Fotografía de los datos antes de migrar
psql -U postgres -d climaplus_db -f "$repo\database\scripts\M004_verificacion_previa.sql" | Out-File -Encoding utf8 previa.txt
```

Qué esperar:
- `migraciones-climaplus_db.txt` con `InitialCreate`, `ConsultaClimaUsuarioOpcional` y `ConsultaClimaDetalle`.
  **Si falta alguna de las dos últimas**, hay que aplicar antes `002_ConsultaClimaUsuarioOpcional.sql` y/o
  `003_ConsultaClimaDetalle.sql` (están en `database/migrations` y tienen sus propias verificaciones
  `E02_E03_verificacion_previa.sql` / `_posterior.sql`). Detente y consúltalo antes de seguir.
- `diferencias.txt`: solo las tablas `desafio_autenticacion` e `identidad_externa`, sus restricciones e índices y
  las columnas `correo_verificado` y `sello_seguridad` de `usuario` (más `password_hash` sin `NOT NULL`). Cualquier
  otra diferencia se revisa antes de seguir.
- `previa.txt`: 3 migraciones, 8 tablas y 26 CHECK.

## 2. Decisión sobre las cuentas existentes

Después de la 004, **todas** las cuentas quedan con `correo_verificado = FALSE`: en su siguiente inicio de sesión
reciben un código de verificación en su correo. Elige una opción **antes** de migrar:

| Opción | Qué pasa | Riesgo |
|---|---|---|
| A (recomendada si los correos son reales) | No se toca nada: cada usuario confirma su correo en el siguiente inicio de sesión | Una cuenta con un correo ficticio queda sin poder entrar cuando el correo salga por SMTP real |
| B | Se marcan como verificadas solo las cuentas que indiques (paso 6) | Es una modificación de datos: requiere autorización aparte |

Para decidir, lista los correos (solo lectura):

```powershell
psql -U postgres -d climaplus_db -c "SELECT id_usuario, correo, rol, activo FROM usuario ORDER BY id_usuario;"
```

## 3. Respaldo

```powershell
$fecha = Get-Date -Format "yyyyMMdd-HHmm"
$dump  = "respaldo-climaplus_db-antes-004-$fecha.dump"
pg_dump -U postgres -d climaplus_db -Fc -f $dump      # -f: nunca uses ">" con un respaldo binario
pg_restore --list $dump | Select-Object -First 5        # debe listar el contenido sin errores
Get-FileHash $dump -Algorithm SHA256 | Format-List      # guarda el hash junto al archivo
```

## 4. Probar que el respaldo se puede restaurar

Crea una base **nueva** y restaura ahí (no toca `climaplus_db`):

```powershell
createdb -U postgres climaplus_db_restaurada
pg_restore -U postgres -d climaplus_db_restaurada --no-owner $dump
psql -U postgres -d climaplus_db_restaurada -f "$repo\database\scripts\M004_verificacion_previa.sql" | Out-File -Encoding utf8 previa-restaurada.txt
Compare-Object (Get-Content previa.txt) (Get-Content previa-restaurada.txt)   # sin salida = idénticas
```

Si hay diferencias, **no sigas**: el respaldo no sirve. Cuando termines la Etapa 2 puedes borrar la base de prueba
con `dropdb -U postgres climaplus_db_restaurada`.

## 5. Aplicar la 004 (con la API detenida)

```powershell
psql -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -f "$repo\database\migrations\004_AutenticacionSegura.sql"
psql -U postgres -d climaplus_db -f "$repo\database\scripts\M004_verificacion_posterior.sql" | Out-File -Encoding utf8 posterior.txt
```

Criterios de aceptación de `posterior.txt` frente a `previa.txt`:

| Sección | Esperado |
|---|---|
| 1 | Las 3 migraciones anteriores + `20261109180000_AutenticacionSegura` |
| 3 | Mismas filas en las 8 tablas; `desafio_autenticacion` e `identidad_externa` en 0 |
| 4 | **Misma huella** (contraseñas, roles y estado intactos) |
| 5 | Mismos usuarios por rol |
| 6 | 0 verificados, todos sin verificar, un sello distinto por usuario, 0 sin contraseña |
| 8 | 38 CHECK |

Si el comando de la migración muestra un `ERROR`, la transacción se revierte sola: la base queda como estaba.
Volver a ejecutar la 004 sobre una base que ya la tiene también falla sin cambiar nada.

## 6. (Solo con la opción B y autorización aparte) Marcar cuentas como verificadas

```powershell
psql -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -c "UPDATE usuario SET correo_verificado = TRUE WHERE correo IN ('admin@tu-dominio.com');"
```

Reemplaza la lista por los correos aprobados. La salida debe decir `UPDATE n` con el número esperado.

## 7. Volver a apuntar la API a `climaplus_db`

Sin mostrar la contraseña en pantalla:

```powershell
Set-Location "$repo\backend\ClimaPlus.Api"
$linea  = dotnet user-secrets list | Where-Object { $_ -like 'ConnectionStrings:ClimaPlusDb = *' }
$nueva  = ($linea -replace '^ConnectionStrings:ClimaPlusDb = ', '') -replace 'Database=[^;]+', 'Database=climaplus_db'
dotnet user-secrets set "ConnectionStrings:ClimaPlusDb" $nueva | Out-Null
Remove-Variable linea, nueva
dotnet user-secrets list | Select-String "ConnectionStrings" | ForEach-Object { if ($_ -match "Database=([^;]+)") { "Database=" + $Matches[1] } }
dotnet run --launch-profile http
```

Comprobaciones:
- La consola de la API **no** muestra `Base de datos: faltan_migraciones`.
- `Invoke-RestMethod http://localhost:5208/health` responde `Healthy`.
- Registro, inicio de sesión en dos pasos, recuperación y Google funcionan (lista de
  [`../api/pruebas-frontend-autenticacion.md`](../api/pruebas-frontend-autenticacion.md)).

## 8. Recuperación

| Situación | Qué hacer |
|---|---|
| La 004 dio error | Nada: se revirtió sola. Revisa el mensaje y `diferencias.txt` |
| Migró bien pero hay que volver a la versión anterior y **no** existen cuentas de Google | `psql -U postgres -d climaplus_db -v ON_ERROR_STOP=1 -f "$repo\database\scripts\F7_revertir_004.sql"` (se niega a seguir si hay cuentas de Google) |
| Cualquier otro caso | Restaurar el respaldo del paso 3 (abajo) |

Restaurar el respaldo, conservando la base actual para analizarla:

```powershell
# API detenida y sin otras conexiones (cierra pgAdmin)
psql -U postgres -d postgres -c "ALTER DATABASE climaplus_db RENAME TO climaplus_db_fallida;"
createdb -U postgres climaplus_db
pg_restore -U postgres -d climaplus_db --no-owner $dump
psql -U postgres -d climaplus_db -f "$repo\database\scripts\M004_verificacion_previa.sql" | Out-File -Encoding utf8 tras-restaurar.txt
Compare-Object (Get-Content previa.txt) (Get-Content tras-restaurar.txt)      # sin salida = idéntica al respaldo
```

## 9. Docker (volumen existente) — etapa aparte, también con autorización

Los scripts de `database/migrations` solo se ejecutan con un volumen **vacío**. Con un volumen que ya tiene datos:

```powershell
Set-Location $repo
# Respaldo: se genera DENTRO del contenedor y luego se copia (con ">" en PowerShell o sin -T el archivo se corrompe)
docker compose exec -T db pg_dump -U climaplus -d climaplus_db -Fc -f /tmp/respaldo-antes-004.dump
docker compose cp db:/tmp/respaldo-antes-004.dump "$resp\respaldo-docker-antes-004.dump"

# Verificación previa, migración y verificación posterior
docker compose cp database/migrations/004_AutenticacionSegura.sql db:/tmp/004.sql
docker compose cp database/scripts/M004_verificacion_previa.sql db:/tmp/previa.sql
docker compose cp database/scripts/M004_verificacion_posterior.sql db:/tmp/posterior.sql
docker compose exec -T db psql -U climaplus -d climaplus_db -f /tmp/previa.sql | Out-File -Encoding utf8 "$resp\docker-previa.txt"
docker compose stop api
docker compose exec -T db psql -U climaplus -d climaplus_db -v ON_ERROR_STOP=1 -f /tmp/004.sql
docker compose exec -T db psql -U climaplus -d climaplus_db -f /tmp/posterior.sql | Out-File -Encoding utf8 "$resp\docker-posterior.txt"
docker compose start api
```

**Nunca** uses `docker compose down -v` para "arreglar" la base: borra el volumen con todos los datos.

## 10. Ensayo realizado (Fase 7, Etapa 1)

El procedimiento se ensayó completo en un PostgreSQL 16 desechable, en una base con 001–003 y los datos de
`M004_datos_muestra_test.sql` (no en `climaplus_db` ni en `climaplus_test`):

| Paso | Resultado |
|---|---|
| Respaldo `-Fc` + restauración en base nueva | `previa` idéntica en la base restaurada |
| Aplicar 004 | Sin errores; huella y usuarios por rol idénticos; 38 CHECK; 3 usuarios sin verificar con 3 sellos distintos |
| Aplicar la 004 otra vez | Error controlado ("column correo_verificado already exists"), sin cambios (sigue con 4 migraciones) |
| `F7_revertir_004.sql` | Esquema idéntico al original (salvo las marcas aleatorias `\restrict` de `pg_dump`) y datos idénticos |
| Revertir sin la 004 aplicada | Se niega con un mensaje claro |
| Revertir con una cuenta solo de Google | Se niega; la base sigue con la 004 |

Pendiente de ejecutar en tu equipo: los pasos 1 a 8 sobre `climaplus_db` (PostgreSQL 18), cuando los autorices.
