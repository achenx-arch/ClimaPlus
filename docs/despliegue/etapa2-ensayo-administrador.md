# Etapa 2 · Ensayo del administrador real y de la limpieza en `climaplus_db_ensayo`

Procedimiento para **Windows PowerShell 5.1**. Todo ocurre en la copia `climaplus_db_ensayo` creada por
`F7_E2_respaldo_y_ensayo.ps1`. `climaplus_db` y `climaplus_test` solo se **leen** (para comprobar que no cambian).
User Secrets no se modifica: la API de ensayo recibe la conexión por una variable de entorno que existe solo en su
ventana y desaparece al cerrarla.

> ⚠ No vuelvas a ejecutar `F7_E2_respaldo_y_ensayo.ps1 -Reemplazar` a mitad de este ensayo: recrea la copia y se
> pierde lo hecho aquí (no afecta a la base oficial).

Lo que se puede compartir: las salidas de `F7_E2_verificar_ensayo.sql` (correos enmascarados), de los scripts de
promoción y limpieza, y las respuestas de `/health`. **Nunca** compartas contraseñas, códigos de verificación, tokens
ni la cadena de conexión.

## Ventanas

| Ventana | Para qué | Puerto |
|---|---|---|
| A | API de ensayo, apuntando a `climaplus_db_ensayo` | `http://localhost:5209` |
| B | Consultas y scripts de PostgreSQL | — |
| C | Frontend de ensayo, apuntando a la API 5209 | `http://localhost:5173` |

El puerto 5209 distingue la API de ensayo de la habitual (5208). Si tienes el frontend habitual abierto en 5173,
detenlo antes (Ctrl+C) para que el de ensayo use ese puerto (es el único que CORS permite).

## 0. Ventana B: preparar la sesión

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
$env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
Set-Location $repo
$verificar = Join-Path $repo "database\scripts\F7_E2_verificar_ensayo.sql"
$promover  = Join-Path $repo "database\scripts\F7_E2_promover_administrador.sql"
$limpieza  = Join-Path $repo "database\scripts\F7_E2_limpieza_cuentas.sql"

# Contraseña de PostgreSQL solo para esta ventana (no se guarda ni se muestra)
$s = Read-Host "Contraseña de PostgreSQL (postgres)" -AsSecureString
$b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
$env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b); Remove-Variable s, b

# Correo real del nuevo administrador (no es secreto; se usa en varios comandos)
$correo = (Read-Host "Correo del nuevo administrador").Trim().ToLower()

# Ayudante: ejecutar la verificación en modo SOLO LECTURA sobre la base indicada
function Verificar([string]$base) {
    $env:PGOPTIONS = "-c default_transaction_read_only=on"
    try { psql -X -w -U postgres -d $base -v ON_ERROR_STOP=1 -v "correo=$correo" -f $verificar }
    finally { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
    "Código de salida: $LASTEXITCODE"
}
```

## 1. Confirmar la 004 en la copia (solo lectura)

```powershell
Verificar climaplus_db_ensayo | Tee-Object -FilePath "$env:TEMP\e2-01-copia.txt"
```

Esperado: sección 2 con 4 migraciones, la última `20261109180000_AutenticacionSegura`; sección 3 con `t` en las cuatro
columnas; sección 1 con `solo_lectura = on`; "Código de salida: 0".

Contraste (deben seguir sin la 004 la oficial, y con ella la de pruebas):

```powershell
Verificar climaplus_db   | Tee-Object -FilePath "$env:TEMP\e2-01-oficial.txt"
Verificar climaplus_test | Select-Object -First 25
```

## 2. Ventana A: API aislada sobre la copia

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
Set-Location "$repo\backend\ClimaPlus.Api"

# Toma la conexión de User Secrets, cambia SOLO el nombre de la base y la guarda en una variable de ESTA ventana.
$linea = dotnet user-secrets list | Where-Object { $_ -like 'ConnectionStrings:ClimaPlusDb = *' } | Select-Object -First 1
if (-not $linea) { throw "No hay ConnectionStrings:ClimaPlusDb en User Secrets." }
$cadena = ($linea -replace '^ConnectionStrings:ClimaPlusDb = ', '') -replace '(?i)(Database|Initial Catalog)=[^;]*', 'Database=climaplus_db_ensayo'
if ($cadena -notmatch '(?i)(^|;)Database=climaplus_db_ensayo(;|$)') { throw "No se pudo apuntar a climaplus_db_ensayo." }
$env:ConnectionStrings__ClimaPlusDb = $cadena
Remove-Variable linea, cadena
"Esta ventana usa: Database=climaplus_db_ensayo"

# Correo: muestra solo a qué servidor SMTP se enviará (sin usuarios ni contraseñas)
dotnet user-secrets list | Select-String '^Correo:(Host|Puerto|Seguridad|Remitente) '
```

Si `Correo:Host` es `localhost` (Mailpit), el código **no** llegará a tu bandeja real. Para usar Gmail solo en esta
ventana (sin tocar User Secrets; ver `docs/despliegue/correo-smtp.md` para la contraseña de aplicación):

```powershell
$env:Correo__Host = "smtp.gmail.com"
$env:Correo__Puerto = "587"
$env:Correo__Seguridad = "StartTls"
$env:Correo__Usuario = "cuenta.de.envio@gmail.com"
$env:Correo__Remitente = "cuenta.de.envio@gmail.com"
$s = Read-Host "Contraseña de aplicación de Gmail" -AsSecureString
$b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
$env:Correo__Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b); Remove-Variable s, b
```

Arranca la API en el puerto 5209 (las variables de entorno tienen prioridad sobre User Secrets):

```powershell
dotnet run --launch-profile http -- --urls http://localhost:5209
```

En la consola no debe aparecer `Base de datos: faltan_migraciones`. Comprueba desde la **ventana B**:

```powershell
Invoke-RestMethod http://localhost:5209/health        # Healthy
```

## 3. Ventana C: frontend de ensayo

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
Set-Location "$repo\frontend"
$env:VITE_API_URL = "http://localhost:5209"
npm run dev
```

Abre `http://localhost:5173` en una **ventana privada** del navegador (para no arrastrar sesiones anteriores).

### Registrar y verificar la cuenta real

1. **Crear cuenta** con tu nombre, el correo real (`$correo`) y una contraseña segura (8–128 caracteres, con letras y
   números; recomendado 12 o más, sin reutilizarla).
2. Escribe el código que llega a tu correo → "Correo verificado. Ya puedes iniciar sesión."
3. Inicia sesión (correo, contraseña y el nuevo código): debe entrar al **Dashboard** de usuario (todavía es USUARIO).

Comprobación en la **ventana B** (solo lectura):

```powershell
Verificar climaplus_db_ensayo | Tee-Object -FilePath "$env:TEMP\e2-03-registrada.txt"
Verificar climaplus_db   | Select-String -Pattern "== 4" -Context 0,4   # 0 filas: la cuenta NO está en la oficial
Verificar climaplus_test | Select-String -Pattern "== 4" -Context 0,4   # 0 filas: tampoco en la de pruebas
```

Esperado en la copia, sección 4: una fila con `rol = USUARIO`, `activo = t`, `correo_verificado = t`,
`con_password = t`. Si la cuenta aparece en `climaplus_test`, la API no estaba apuntando a la copia: detente.

## 4. Promover a ADMINISTRADOR (solo en la copia)

Primero el ensayo (termina con ROLLBACK, no cambia nada):

```powershell
psql -X -w -U postgres -d climaplus_db_ensayo -v ON_ERROR_STOP=1 -v "correo=$correo" -f $promover
"Código de salida: $LASTEXITCODE"
```

Esperado: "Después" con `rol = ADMINISTRADOR` y la línea `== ENSAYO: se deshizo todo (ROLLBACK)`.

Luego, confirmado **en la copia** (fíjate en `-d climaplus_db_ensayo`):

```powershell
psql -X -w -U postgres -d climaplus_db_ensayo -v ON_ERROR_STOP=1 -v "correo=$correo" -v confirmar=SI -f $promover | Tee-Object -FilePath "$env:TEMP\e2-04-promocion.txt"
"Código de salida: $LASTEXITCODE"
Verificar climaplus_db_ensayo | Select-String -Pattern "== 4" -Context 0,4   # rol = ADMINISTRADOR
```

## 5. Comprobar el panel administrativo

La promoción cierra las sesiones abiertas de esa cuenta.

1. En el navegador, cierra sesión (o recarga: la sesión anterior ya no es válida) e inicia sesión otra vez con correo,
   contraseña y código.
2. Debe llevarte a **Administración** (`/admin`), con el resumen de alertas.
3. Abre **Alertas** (`/admin/alertas`): la lista carga sin errores (F12 → Red: `GET /api/admin/alertas` → 200).
4. Contraste (opcional): antes de la promoción, la misma cuenta veía "Acceso denegado" en `/admin`; el backend responde 403 a `GET /api/admin/alertas` para cualquier USUARIO.

Anota la cantidad de alertas que muestra el panel para compararla después de la limpieza.

## 6. Ensayar la limpieza de las dos cuentas ficticias (solo en la copia)

1. En la salida de `e2-03-registrada.txt` (sección 5) identifica el `id_usuario` de las **dos** cuentas ficticias.
   La cuenta nueva **no** debe estar en la lista.
2. Fotografía previa:

   ```powershell
   Verificar climaplus_db_ensayo | Out-File -Encoding utf8 "$env:TEMP\e2-06-antes.txt"
   ```

3. Ensayo con ROLLBACK (reemplaza `ID1,ID2` por los dos números, sin espacios):

   ```powershell
   psql -X -w -U postgres -d climaplus_db_ensayo -v ON_ERROR_STOP=1 -v ids=ID1,ID2 -v cantidad=2 -f $limpieza | Tee-Object -FilePath "$env:TEMP\e2-06-limpieza-ensayo.txt"
   "Código de salida: $LASTEXITCODE"
   ```

   - Si una de las dos es el **administrador anterior**, el script se detiene con "La lista incluye 1
     administrador(es)". Como el nuevo administrador ya funcionó en el paso 5, repite añadiendo
     `-v permitir_admin=SI`.
   - Revisa "Filas que se eliminarán por tabla" y que termine con `== ENSAYO: se deshizo todo (ROLLBACK)`.

4. Si el informe es el esperado, confirmar **en la copia** (mismos `ids`, y `permitir_admin=SI` solo si aplica):

   ```powershell
   psql -X -w -U postgres -d climaplus_db_ensayo -v ON_ERROR_STOP=1 -v ids=ID1,ID2 -v cantidad=2 -v confirmar=SI -f $limpieza | Tee-Object -FilePath "$env:TEMP\e2-06-limpieza-confirmada.txt"
   "Código de salida: $LASTEXITCODE"
   Verificar climaplus_db_ensayo | Out-File -Encoding utf8 "$env:TEMP\e2-06-despues.txt"
   Compare-Object (Get-Content "$env:TEMP\e2-06-antes.txt") (Get-Content "$env:TEMP\e2-06-despues.txt") |
       Where-Object { $_.InputObject -notmatch "solo_lectura|^ climaplus_db_ensayo \|" }
   ```

   Esperado en la comparación: desaparecen **solo** las filas de las dos cuentas (sección 5) y bajan los totales de
   sus tablas dependientes (sección 6) exactamente en lo que indicó el informe. No cambian: la fila de la cuenta
   nueva, el total de `ubicacion` ni el de `consulta_clima (anónimas)`.

## 7. El administrador nuevo después de la limpieza

1. Cierra sesión e inicia sesión de nuevo (correo, contraseña y código): entra a `/admin`.
2. `/admin/alertas` carga (200). La cantidad de alertas puede bajar solo por las alertas de las cuentas eliminadas.
3. Ventana B:

   ```powershell
   Invoke-RestMethod http://localhost:5209/health                                   # Healthy
   Verificar climaplus_db_ensayo | Select-String -Pattern "== 4" -Context 0,4       # ADMINISTRADOR, activo, verificado
   ```

4. Opcional: iniciar sesión con una cuenta eliminada responde "Correo o contraseña incorrectos".

## 8. Cerrar el ensayo y comprobar que nada oficial cambió

1. Ventana A: `Ctrl+C` y **cierra la ventana** (desaparecen la conexión de ensayo y, si las usaste, las variables de
   correo). Ventana C: `Ctrl+C` y cierra la ventana.
2. Ventana B:

   ```powershell
   # User Secrets sigue igual (solo muestra el nombre de la base)
   Set-Location "$repo\backend\ClimaPlus.Api"
   dotnet user-secrets list | Select-String "ConnectionStrings" | ForEach-Object { if ($_ -match "(?i)Database=([^;]+)") { "User Secrets -> Database=" + $Matches[1] } }
   Set-Location $repo

   # La base oficial no cambió: compara con la fotografía del respaldo de la Etapa 2
   $carpeta = Get-ChildItem ([Environment]::GetFolderPath("MyDocuments") + "\ClimaPlus-respaldos") -Directory | Sort-Object Name | Select-Object -Last 1
   $env:PGOPTIONS = "-c default_transaction_read_only=on"
   psql -X -w -U postgres -d climaplus_db -f "$repo\database\scripts\M004_verificacion_previa.sql" | Out-File -Encoding utf8 "$env:TEMP\e2-08-oficial.txt"
   Remove-Item Env:PGOPTIONS
   Compare-Object (Get-Content (Join-Path $carpeta.FullName "previa-oficial.txt")) (Get-Content "$env:TEMP\e2-08-oficial.txt")   # sin salida = idéntica

   Remove-Item Env:PGPASSWORD
   ```

## Criterios de aceptación del ensayo

- [ ] Paso 1: la copia tiene las 4 migraciones; `climaplus_db` sigue sin la 004.
- [ ] Paso 2: `/health` de la API 5209 = Healthy, sin `faltan_migraciones`.
- [ ] Paso 3: cuenta registrada y verificada **solo** en la copia.
- [ ] Paso 4: ensayo con ROLLBACK y promoción confirmada en la copia, ambos con código 0.
- [ ] Paso 5: login en dos pasos lleva a `/admin`; `GET /api/admin/alertas` = 200.
- [ ] Paso 6: informe previo revisado; limpieza confirmada solo en la copia; solo cambian las filas de las dos cuentas.
- [ ] Paso 7: el administrador nuevo sigue entrando y usando el panel.
- [ ] Paso 8: User Secrets sin cambios y `climaplus_db` idéntica a `previa-oficial.txt`.

Con estos resultados se pedirá la autorización para repetir el procedimiento sobre `climaplus_db`
(`etapa2-cuentas-reales.md`, fase 5).
