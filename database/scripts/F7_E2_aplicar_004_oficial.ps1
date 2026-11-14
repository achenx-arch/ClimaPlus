#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Fase 7, Etapa 2: aplica ÚNICAMENTE la migración 20261109180000_AutenticacionSegura sobre climaplus_db,
  con respaldo verificado antes y comprobaciones después.

.DESCRIPTION
  La base es fija (climaplus_db). Sin -Aplicar el script SOLO comprueba (no escribe nada):
    1. Respaldo de la Etapa 2: existe, su SHA-256 coincide, su índice se lee y es de climaplus_db.
    2. climaplus_db tiene exactamente las 3 migraciones anteriores y no tiene la 004.
    3. climaplus_db no cambió desde el respaldo (M004_verificacion_previa idéntica a previa-oficial.txt del respaldo).
       Si cambió, se detiene: hay que hacer un respaldo nuevo.
    4. No hay otras conexiones abiertas a climaplus_db (detén la API y cierra pgAdmin).
  Con -Aplicar, si todo lo anterior pasa:
    5. Ejecuta database/migrations/004_AutenticacionSegura.sql (una transacción; si falla, se revierte sola) con
       ON_ERROR_STOP y lock_timeout de 10 s.
    6. Verificación posterior (solo lectura): 4 migraciones, filas por tabla, huella de usuarios (id, correo, hash, rol,
       activo) y usuarios por rol IDÉNTICOS a antes; tablas nuevas vacías; sello distinto por usuario; 38 CHECK.

  No toca usuarios, no promueve ni elimina cuentas. Código de salida: 0 = correcto; 1 = detenido o fallo.
  Siempre deja resumen.txt en la carpeta de resultados (dentro de la carpeta del respaldo).

.EXAMPLE
  # Solo comprobaciones (no modifica nada)
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_aplicar_004_oficial.ps1

.EXAMPLE
  # Aplicar la 004 en climaplus_db (requiere autorización del responsable)
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_aplicar_004_oficial.ps1 -Aplicar
#>
[CmdletBinding()]
param(
    [string]$Servidor = "localhost",
    [int]$Puerto = 5432,
    [string]$Usuario = "postgres",
    [string]$CarpetaRespaldo,
    [switch]$Aplicar
)

$ErrorActionPreference = "Stop"
$Base = "climaplus_db"   # fijo a propósito
$Migracion = "20261109180000_AutenticacionSegura"
$Anteriores = @("20261016052608_InitialCreate", "20261028062000_ConsultaClimaUsuarioOpcional", "20261028062100_ConsultaClimaDetalle")

$utf8SinBom = New-Object System.Text.UTF8Encoding $false
$resumen = New-Object System.Collections.Generic.List[string]
$codigoSalida = 1
$Salida = $null
function Anotar([string]$linea) { $resumen.Add($linea); Write-Host $linea }
function Guardar([string]$nombre, $lineas) { if ($Salida) { [IO.File]::WriteAllLines((Join-Path $Salida $nombre), [string[]]@($lineas), $utf8SinBom) } }

$anterior = @{ PGPASSWORD = $env:PGPASSWORD; PGOPTIONS = $env:PGOPTIONS; PGCLIENTENCODING = $env:PGCLIENTENCODING; PGCONNECT_TIMEOUT = $env:PGCONNECT_TIMEOUT }
$codificacionConsola = $null

function Ejecutar([string]$Programa, [string[]]$Argumentos, [string]$Opciones) {
    if ($Opciones) { $env:PGOPTIONS = $Opciones } else { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
    $previo = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    try { $texto = & $Programa @Argumentos 2>&1 | ForEach-Object { "$_" }; $codigo = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previo; Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
    [pscustomobject]@{ Codigo = $codigo; Lineas = @($texto) }
}
$SOLO_LECTURA = "-c default_transaction_read_only=on"
function Conexion([string]$b) { @("-X", "-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "-d", $b, "-v", "ON_ERROR_STOP=1", "-P", "pager=off") }
function Leer([string]$Descripcion, [string]$ArchivoSql) {
    $r = Ejecutar "psql" ((Conexion $Base) + @("-f", $ArchivoSql)) $SOLO_LECTURA
    if ($r.Codigo -ne 0) { throw "$Descripcion falló (código $($r.Codigo)): $(($r.Lineas | Select-Object -Last 5) -join ' | ')" }
    $r.Lineas
}
function Escalar([string]$b, [string]$Sql, [string]$Descripcion) {
    # La consulta va en un archivo temporal: PowerShell 5.1 rompe las comillas dobles en argumentos de programas nativos.
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "climaplus-" + [guid]::NewGuid().ToString("N") + ".sql")
    [IO.File]::WriteAllText($tmp, $Sql, $utf8SinBom)
    try { $r = Ejecutar "psql" ((Conexion $b) + @("-A", "-t", "-f", $tmp)) $SOLO_LECTURA }
    finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
    if ($r.Codigo -ne 0) { throw "$Descripcion falló (código $($r.Codigo)): $(($r.Lineas | Select-Object -Last 5) -join ' | ')" }
    @($r.Lineas | Where-Object { $_.Trim() -ne "" } | ForEach-Object { $_.Trim() })
}
function Seccion([string[]]$Lineas, [string]$Numero) {
    $dentro = $false; $r = @()
    foreach ($l in $Lineas) {
        if ($l -match '^(\d+)\. ') { $dentro = ($Matches[1] -eq $Numero); continue }
        if ($dentro -and $l.Trim() -ne "") { $r += $l }
    }
    , $r
}

try {
    # -------------------------------------------------------------- Archivos y herramientas
    $repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $sql004 = Join-Path $repo "database\migrations\004_AutenticacionSegura.sql"
    $previaSql = Join-Path $repo "database\scripts\M004_verificacion_previa.sql"
    $posteriorSql = Join-Path $repo "database\scripts\M004_verificacion_posterior.sql"
    foreach ($a in @($sql004, $previaSql, $posteriorSql)) { if (-not (Test-Path $a)) { throw "No se encontró $a. Ejecuta el script desde el repositorio de ClimaPlus." } }
    foreach ($h in "psql", "pg_restore") {
        if (-not (Get-Command $h -ErrorAction SilentlyContinue)) { throw "No se encontró '$h'. Agrega la carpeta bin de PostgreSQL al PATH: `$env:Path = 'C:\Program Files\PostgreSQL\18\bin;' + `$env:Path" }
    }

    # -------------------------------------------------------------- Carpeta del respaldo
    if (-not $CarpetaRespaldo) {
        $documentos = [Environment]::GetFolderPath("MyDocuments"); if (-not $documentos) { $documentos = $HOME }
        $raiz = Join-Path $documentos "ClimaPlus-respaldos"
        if (-not (Test-Path $raiz)) { throw "No existe $raiz. Indica -CarpetaRespaldo." }
        $candidata = Get-ChildItem $raiz -Directory | Where-Object { (Test-Path (Join-Path $_.FullName "previa-oficial.txt")) -and (Get-ChildItem $_.FullName -Filter "respaldo-climaplus_db-*.dump") } | Sort-Object Name | Select-Object -Last 1
        if (-not $candidata) { throw "No se encontró ninguna carpeta de respaldo válida en $raiz (con respaldo-climaplus_db-*.dump y previa-oficial.txt)." }
        $CarpetaRespaldo = $candidata.FullName
    }
    $Salida = Join-Path $CarpetaRespaldo ("aplicacion-004-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    New-Item -ItemType Directory -Force -Path $Salida | Out-Null

    try { $codificacionConsola = [Console]::OutputEncoding; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { $codificacionConsola = $null }
    $env:PGCLIENTENCODING = "UTF8"
    $env:PGCONNECT_TIMEOUT = "10"
    if (-not $env:PGPASSWORD) {
        $seguro = Read-Host "Contraseña de PostgreSQL para '$Usuario' (no se guarda)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
        try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }

    $modo = "SOLO COMPROBACIONES (sin -Aplicar)"
    if ($Aplicar) { $modo = "APLICAR la 004" }
    Anotar "ClimaPlus - Migración $Migracion sobre $Base"
    Anotar ("Fecha: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + " | servidor ${Servidor}:${Puerto} | modo: $modo")
    Anotar "Respaldo: $CarpetaRespaldo"
    Anotar ""

    # -------------------------------------------------------------- 1. Respaldo válido
    $dump = Get-ChildItem $CarpetaRespaldo -Filter "respaldo-climaplus_db-*.dump" | Sort-Object Name | Select-Object -Last 1
    $archivoHash = $dump.FullName + ".sha256"
    if (-not (Test-Path $archivoHash)) { throw "Falta $archivoHash." }
    if ($dump.Length -eq 0) { throw "El respaldo está vacío." }
    $esperado = ((Get-Content $archivoHash -TotalCount 1) -split '\s+')[0].Trim().ToUpperInvariant()
    $actual = (Get-FileHash $dump.FullName -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($esperado -ne $actual) { throw "El SHA-256 del respaldo NO coincide (esperado $esperado, actual $actual). El archivo cambió o está dañado." }
    $indice = Ejecutar "pg_restore" @("--list", $dump.FullName)
    if ($indice.Codigo -ne 0) { throw "El índice del respaldo no se puede leer: archivo dañado." }
    if (-not ($indice.Lineas | Where-Object { $_ -match '^;\s+Dbname:\s+climaplus_db\s*$' })) { throw "El respaldo no es de climaplus_db." }
    if (-not ($indice.Lineas | Where-Object { $_ -match 'TABLE DATA public usuario ' })) { throw "El respaldo no contiene los datos de la tabla usuario." }
    $fechaRespaldo = ($indice.Lineas | Where-Object { $_ -match '^;\s+Archive created at' } | Select-Object -First 1)
    Anotar ("1. Respaldo válido: $($dump.Name) ({0:N1} KB), SHA-256 coincide, índice legible, base climaplus_db. $fechaRespaldo" -f ($dump.Length / 1KB))

    # -------------------------------------------------------------- 2. Estado de las migraciones
    $migraciones = @(Escalar $Base 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' "Lectura de migraciones")
    $tiene004 = @(Escalar $Base "SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL OR to_regclass('public.identidad_externa') IS NOT NULL" "Comprobación de tablas de la 004")[0]
    if ($migraciones -contains $Migracion) { throw "La 004 ya está registrada en $Base. No se aplica de nuevo." }
    if ($tiene004 -ne "f") { throw "$Base ya tiene tablas de la 004 sin estar registrada: revisar antes de continuar." }
    if (Compare-Object $Anteriores $migraciones) { throw ("Las migraciones de $Base no son exactamente las 3 esperadas: " + ($migraciones -join ", ")) }
    Anotar "2. $Base tiene exactamente las 3 migraciones previas y no tiene la 004."

    # -------------------------------------------------------------- 3. Sin cambios desde el respaldo
    $previa = Leer "Verificación previa" $previaSql
    Guardar "previa-antes-de-004.txt" $previa
    $dif = @(Compare-Object (Get-Content (Join-Path $CarpetaRespaldo "previa-oficial.txt") -Encoding UTF8) $previa)
    if ($dif.Count -ne 0) { throw "$Base cambió desde el respaldo ($($dif.Count) líneas distintas con previa-oficial.txt). Haz un respaldo nuevo con F7_E2_respaldo_y_ensayo.ps1 -Reemplazar antes de migrar." }
    Anotar "3. $Base no cambió desde el respaldo (verificación previa idéntica)."

    # -------------------------------------------------------------- 4. Conexiones abiertas
    $otras = @(Escalar $Base "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()" "Conteo de conexiones")[0]
    if ($otras -ne "0") { throw "Hay $otras conexión(es) abierta(s) a $Base. Detén la API (Ctrl+C) y cierra pgAdmin u otras herramientas, y vuelve a ejecutar." }
    Anotar "4. No hay otras conexiones abiertas a $Base."

    if (-not $Aplicar) {
        Anotar ""
        Anotar "RESULTADO: comprobaciones previas correctas. NO se modificó nada. Para aplicar la 004: agrega -Aplicar."
        $codigoSalida = 0
    } else {

    # -------------------------------------------------------------- 5. Aplicar la 004
    $r = Ejecutar "psql" ((Conexion $Base) + @("-f", $sql004)) "-c lock_timeout=10s"
    Guardar "aplicar-004.txt" $r.Lineas
    if ($r.Codigo -ne 0) {
        Anotar "5. La 004 FALLÓ (código $($r.Codigo)); la transacción se revirtió sola. Detalle en aplicar-004.txt."
        throw "La migración no se aplicó."
    }
    Anotar "5. Migración $Migracion aplicada (aplicar-004.txt)."

    # -------------------------------------------------------------- 6. Verificación posterior
    $posterior = Leer "Verificación posterior" $posteriorSql
    Guardar "posterior.txt" $posterior
    $fallas = @()
    $m = @(Escalar $Base 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' "Lectura de migraciones")
    if (Compare-Object ($Anteriores + $Migracion) $m) { $fallas += "migraciones: $($m -join ', ')" }
    # Filas por tabla: se comparan como "tabla=filas" (el ancho de las columnas cambia al aparecer las tablas nuevas).
    $normalizar = { param($lineas) @($lineas | Where-Object { $_ -match '^\s*([a-z_]+)\s*\|\s*(\d+)\s*$' } | ForEach-Object { $_ -replace '^\s*([a-z_]+)\s*\|\s*(\d+)\s*$', '$1=$2' }) }
    $filasAntes = & $normalizar (Seccion $previa "3"); $filasDespues = & $normalizar (Seccion $posterior "3")
    if ($filasAntes.Count -ne 8) { $fallas += "no se pudieron leer las 8 tablas de la verificación previa" }
    foreach ($l in $filasAntes) { if ($filasDespues -notcontains $l) { $fallas += "filas por tabla cambiaron: $l" } }
    foreach ($nueva in "desafio_autenticacion=0", "identidad_externa=0") { if ($filasDespues -notcontains $nueva) { $fallas += "tabla nueva no vacía o ausente: $nueva" } }
    if (((Seccion $previa "4") -join "`n") -ne ((Seccion $posterior "4") -join "`n")) { $fallas += "la huella de usuarios cambió" }
    if (((Seccion $previa "5") -join "`n") -ne ((Seccion $posterior "5") -join "`n")) { $fallas += "los usuarios por rol cambiaron" }
    $c = @(Escalar $Base "SELECT count(*) FILTER (WHERE correo_verificado) || '|' || count(DISTINCT sello_seguridad) || '|' || count(*) || '|' || count(*) FILTER (WHERE password_hash IS NULL) || '|' || (SELECT count(*) FROM desafio_autenticacion) || '|' || (SELECT count(*) FROM identidad_externa) FROM usuario" "Columnas nuevas")[0].Split("|")
    if ($c[0] -ne "0") { $fallas += "hay usuarios marcados como verificados" }
    if ($c[1] -ne $c[2]) { $fallas += "los sellos no son distintos por usuario" }
    if ($c[3] -ne "0") { $fallas += "hay usuarios sin contraseña" }
    if ($c[4] -ne "0" -or $c[5] -ne "0") { $fallas += "las tablas nuevas no están vacías" }
    $checks = @(Escalar $Base "SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE c.contype = 'c' AND n.nspname = current_schema()" "Conteo de CHECK")[0]
    if ($checks -ne "38") { $fallas += "CHECK = $checks (esperado 38)" }
    if ($fallas.Count -gt 0) {
        Anotar ("6. VERIFICACIÓN POSTERIOR CON DIFERENCIAS: " + ($fallas -join "; "))
        Anotar "   Detén todo y revisa posterior.txt. Para volver atrás: docs/despliegue/migracion-004-climaplus_db.md, sección 8."
        throw "La verificación posterior no coincide."
    }
    Anotar "6. Verificación posterior correcta: 4 migraciones; filas por tabla, huella de usuarios y usuarios por rol idénticos; $($c[2]) usuarios sin verificar con sellos distintos; tablas nuevas vacías; 38 CHECK."
    Anotar ""
    Anotar "RESULTADO: la 004 quedó aplicada en $Base y los datos existentes están intactos."
    $codigoSalida = 0
    }
}
catch {
    Anotar ""
    Anotar ("DETENIDO: " + $_.Exception.Message)
    $codigoSalida = 1
}
finally {
    foreach ($clave in $anterior.Keys) {
        if ($null -eq $anterior[$clave]) { Remove-Item "Env:$clave" -ErrorAction SilentlyContinue } else { Set-Item "Env:$clave" $anterior[$clave] }
    }
    if ($null -ne $codificacionConsola) { try { [Console]::OutputEncoding = $codificacionConsola } catch { } }
    Anotar ("Código de salida: " + $codigoSalida)
    try {
        if (-not $Salida) {
            $documentos = [Environment]::GetFolderPath("MyDocuments"); if (-not $documentos) { $documentos = $HOME }
            $Salida = Join-Path (Join-Path $documentos "ClimaPlus-respaldos") ("aplicacion-004-detenida-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
            New-Item -ItemType Directory -Force -Path $Salida | Out-Null
        }
        Guardar "resumen.txt" $resumen
        Write-Host "Resumen: $(Join-Path $Salida 'resumen.txt')"
    } catch { Write-Host "No se pudo escribir resumen.txt: $($_.Exception.Message)" }
}
exit $codigoSalida
