#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Fase 7, Etapa 2: respaldo verificado de climaplus_db y ensayo (004 + limpieza opcional) en la COPIA
  climaplus_db_ensayo.

.DESCRIPTION
  Sobre la base oficial SOLO lee (pg_dump y consultas con default_transaction_read_only=on).
  El ÚNICO destino de escritura, restauración, eliminación o migración es climaplus_db_ensayo (fijo, no configurable).
  climaplus_db y climaplus_test nunca pueden ser destino.

  Sin parámetros:
    1. Fotografía y auditoría de la base oficial (solo lectura).
    2. Respaldo completo con pg_dump -Fc -f, SHA-256 y verificación del índice del archivo.
    3. Crea climaplus_db_ensayo (se detiene si ya existe, salvo -Reemplazar), restaura el respaldo y comprueba que es
       idéntica a la oficial.
    4. Aplica la migración 004 SOLO en la copia y verifica la huella de datos.
    5. Auditoría de usuarios en la copia.
  NO ejecuta ninguna limpieza de usuarios. La limpieza solo se ensaya en la copia si se pasa -IdsLimpieza.

  Código de salida: 0 si todo terminó bien; 1 ante cualquier fallo o verificación no superada. En ambos casos se
  escribe resumen.txt en la carpeta de resultados. El respaldo y las auditorías contienen datos personales: guárdalos
  fuera del repositorio.

  Compatible con Windows PowerShell 5.1 y PowerShell 7; probado con las herramientas de PostgreSQL 16 y pensado para
  las de PostgreSQL 18 (pg_dump/pg_restore deben ser de la misma versión que el servidor o más nuevos).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_respaldo_y_ensayo.ps1
  if ($LASTEXITCODE -ne 0) { "Revisa resumen.txt" }

.EXAMPLE
  # Recrear la copia y ensayar (con ROLLBACK) la limpieza de las cuentas 3, 5 y 8 en la copia
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_E2_respaldo_y_ensayo.ps1 -Reemplazar -IdsLimpieza "3,5,8" -CantidadLimpieza 3
#>
[CmdletBinding()]
param(
    [string]$Servidor = "localhost",
    [int]$Puerto = 5432,
    [string]$Usuario = "postgres",
    [string]$BaseOficial = "climaplus_db",
    [string]$Salida,
    [switch]$Reemplazar,
    [string]$IdsLimpieza,
    [int]$CantidadLimpieza,
    [switch]$PermitirAdminEnLimpieza,
    [switch]$ConfirmarLimpiezaEnEnsayo
)

$ErrorActionPreference = "Stop"

# Única base en la que este script puede escribir. No es un parámetro a propósito.
$BaseEnsayo = "climaplus_db_ensayo"
# Bases que jamás pueden ser destino de escritura, restauración, eliminación o migración.
$BasesProtegidas = @("climaplus_db", "climaplus_test", "postgres", "template0", "template1")

if (-not $Salida) {
    $documentos = [Environment]::GetFolderPath("MyDocuments")
    $Salida = Join-Path (Join-Path $documentos "ClimaPlus-respaldos") ("etapa2-" + (Get-Date -Format "yyyyMMdd-HHmm"))
}
New-Item -ItemType Directory -Force -Path $Salida | Out-Null

$utf8SinBom = New-Object System.Text.UTF8Encoding $false
$resumen = New-Object System.Collections.Generic.List[string]
$codigoSalida = 1   # se pone en 0 solo si todo termina bien
function Anotar([string]$linea) { $resumen.Add($linea); Write-Host $linea }
function Guardar([string]$nombre, $lineas) { [IO.File]::WriteAllLines((Join-Path $Salida $nombre), [string[]]@($lineas), $utf8SinBom) }

$anterior = @{ PGPASSWORD = $env:PGPASSWORD; PGOPTIONS = $env:PGOPTIONS; PGCLIENTENCODING = $env:PGCLIENTENCODING; PGCONNECT_TIMEOUT = $env:PGCONNECT_TIMEOUT }
$codificacionConsola = $null

# Destino de escritura: solo climaplus_db_ensayo. Cualquier otro nombre detiene el script.
function AsegurarDestino([string]$Base) {
    if ($Base -cne $BaseEnsayo -or $BasesProtegidas -contains $Base.ToLowerInvariant() -or $Base -eq $BaseOficial) {
        throw "BLOQUEADO: '$Base' no puede ser destino de escritura. Solo se permite $BaseEnsayo."
    }
}

# Ejecuta un programa de PostgreSQL y devuelve su salida y código.
#   -SoloLectura: PGOPTIONS = default_transaction_read_only=on (para leer la base oficial).
#   sin -SoloLectura: PGOPTIONS se ELIMINA durante la llamada, para que ningún valor heredado (por ejemplo un
#   default_transaction_read_only de la sesión) interfiera con createdb, dropdb, pg_restore o la migración en la copia.
function Ejecutar([string]$Programa, [string[]]$Argumentos, [switch]$SoloLectura) {
    if ($SoloLectura) { $env:PGOPTIONS = "-c default_transaction_read_only=on" }
    else { Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
    $previo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"   # en PowerShell 5.1, stderr de un programa nativo no debe detener el script
    try {
        $texto = & $Programa @Argumentos 2>&1 | ForEach-Object { "$_" }
        $codigo = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previo
        Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue
    }
    [pscustomobject]@{ Codigo = $codigo; Lineas = @($texto) }
}

# Igual que Ejecutar, pero detiene el script si el programa termina con código distinto de 0.
function EjecutarOFallar([string]$Descripcion, [string]$Programa, [string[]]$Argumentos, [switch]$SoloLectura, [string]$Archivo) {
    $r = Ejecutar $Programa $Argumentos -SoloLectura:$SoloLectura
    if ($Archivo) { Guardar $Archivo $r.Lineas }
    if ($r.Codigo -ne 0) {
        $detalle = ($r.Lineas | Select-Object -Last 5) -join " | "
        throw "$Descripcion falló (código $($r.Codigo)): $detalle"
    }
    $r
}

function ArgsConexion([string]$Base) { @("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "-d", $Base) }

function LeerOficial([string]$Descripcion, [string]$ArchivoSql, [string]$ArchivoSalida) {
    EjecutarOFallar $Descripcion "psql" (@("-X") + (ArgsConexion $BaseOficial) + @("-v", "ON_ERROR_STOP=1", "-P", "pager=off", "-f", $ArchivoSql)) -SoloLectura -Archivo $ArchivoSalida
}

function EnCopia([string]$Descripcion, [string]$ArchivoSql, [string]$ArchivoSalida, [string[]]$Variables = @()) {
    AsegurarDestino $BaseEnsayo
    $a = @("-X") + (ArgsConexion $BaseEnsayo) + @("-v", "ON_ERROR_STOP=1", "-P", "pager=off")
    foreach ($v in $Variables) { $a += @("-v", $v) }
    $a += @("-f", $ArchivoSql)
    EjecutarOFallar $Descripcion "psql" $a -Archivo $ArchivoSalida
}

# Consulta de una sola fila y columna (solo lectura).
function Escalar([string]$Base, [string]$Sql, [string]$Descripcion) {
    $r = EjecutarOFallar $Descripcion "psql" (@("-X") + (ArgsConexion $Base) + @("-v", "ON_ERROR_STOP=1", "-A", "-t", "-c", $Sql)) -SoloLectura
    $valores = @($r.Lineas | Where-Object { $_.Trim() -ne "" })
    if ($valores.Count -ne 1) { throw "$Descripcion devolvió una respuesta inesperada: $($r.Lineas -join ' | ')" }
    $valores[0].Trim()
}

function Seccion([string[]]$Lineas, [string]$Numero) {
    # Texto de la sección "N. ..." de los informes M004_verificacion_*.sql (sin el título).
    $dentro = $false; $r = @()
    foreach ($l in $Lineas) {
        if ($l -match '^(\d+)\. ') { $dentro = ($Matches[1] -eq $Numero); continue }
        if ($dentro) { $r += $l }
    }
    ($r | Where-Object { $_.Trim() -ne "" }) -join "`n"
}

try {
    # -------------------------------------------------------------- Validaciones
    $repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $scripts = Join-Path $repo "database\scripts"
    $migracion004 = Join-Path $repo "database\migrations\004_AutenticacionSegura.sql"
    foreach ($archivo in @($migracion004, (Join-Path $scripts "M004_verificacion_previa.sql"), (Join-Path $scripts "M004_verificacion_posterior.sql"),
                           (Join-Path $scripts "F7_E2_auditoria_usuarios.sql"), (Join-Path $scripts "F7_E2_limpieza_cuentas.sql"))) {
        if (-not (Test-Path $archivo)) { throw "No se encontró $archivo. Ejecuta el script desde el repositorio de ClimaPlus." }
    }
    if ($BaseOficial -eq $BaseEnsayo) { throw "La base de origen no puede ser la copia de ensayo." }
    AsegurarDestino $BaseEnsayo
    if ($IdsLimpieza -and $IdsLimpieza -notmatch '^\d+(,\d+)*$') { throw "IdsLimpieza debe ser una lista de números separados por coma, sin espacios (ej.: 3,5,8)." }
    if ($IdsLimpieza -and $CantidadLimpieza -le 0) { throw "Con IdsLimpieza indica también -CantidadLimpieza (cuántas cuentas esperas eliminar)." }
    if (-not $IdsLimpieza -and ($ConfirmarLimpiezaEnEnsayo -or $PermitirAdminEnLimpieza)) { throw "-ConfirmarLimpiezaEnEnsayo y -PermitirAdminEnLimpieza requieren -IdsLimpieza." }
    foreach ($h in "psql", "pg_dump", "pg_restore", "createdb", "dropdb") {
        if (-not (Get-Command $h -ErrorAction SilentlyContinue)) {
            throw "No se encontró '$h'. Agrega la carpeta bin de PostgreSQL al PATH, por ejemplo: `$env:Path += ';C:\Program Files\PostgreSQL\18\bin'"
        }
    }

    # -------------------------------------------------------------- Entorno
    try { $codificacionConsola = [Console]::OutputEncoding; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { $codificacionConsola = $null }
    $env:PGCLIENTENCODING = "UTF8"
    $env:PGCONNECT_TIMEOUT = "10"
    if (-not $env:PGPASSWORD) {
        $seguro = Read-Host "Contraseña de PostgreSQL para '$Usuario' (no se guarda)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
        try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }

    Anotar "ClimaPlus - Respaldo verificado y ensayo en copia aislada (Fase 7, Etapa 2)"
    Anotar ("Fecha: " + (Get-Date -Format "yyyy-MM-dd HH:mm") + " | servidor ${Servidor}:${Puerto} | origen: $BaseOficial (solo lectura) | copia: $BaseEnsayo")
    Anotar ("Herramientas: " + ((Ejecutar "pg_dump" @("--version")).Lineas -join " ") + " | servidor PostgreSQL " + (Escalar $BaseOficial "SHOW server_version" "Versión del servidor"))
    Anotar "Carpeta: $Salida"
    Anotar ""

    # -------------------------------------------------------------- 1. Base oficial (solo lectura)
    $previaOficial = (LeerOficial "Verificación previa de $BaseOficial" (Join-Path $scripts "M004_verificacion_previa.sql") "previa-oficial.txt").Lineas
    LeerOficial "Auditoría de usuarios de $BaseOficial" (Join-Path $scripts "F7_E2_auditoria_usuarios.sql") "auditoria-usuarios-oficial.txt" | Out-Null
    Anotar "1. $BaseOficial leída en modo solo lectura: previa-oficial.txt y auditoria-usuarios-oficial.txt"

    # -------------------------------------------------------------- 2. Respaldo (custom, -Fc -f) + SHA-256
    $dump = Join-Path $Salida ("respaldo-$BaseOficial-" + (Get-Date -Format "yyyyMMdd-HHmm") + ".dump")
    EjecutarOFallar "pg_dump de $BaseOficial" "pg_dump" (@("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "-d", $BaseOficial, "-Fc", "-f", $dump)) -SoloLectura | Out-Null
    if (-not (Test-Path $dump) -or (Get-Item $dump).Length -eq 0) { throw "El respaldo no se creó o está vacío." }
    $hash = (Get-FileHash $dump -Algorithm SHA256).Hash
    Guardar ((Split-Path $dump -Leaf) + ".sha256") ("$hash  " + (Split-Path $dump -Leaf))
    $indice = EjecutarOFallar "Lectura del índice del respaldo" "pg_restore" @("--list", $dump) -Archivo "respaldo-indice.txt"
    $tamano = "{0:N1} KB" -f ((Get-Item $dump).Length / 1KB)
    Anotar "2. Respaldo: $(Split-Path $dump -Leaf) ($tamano), SHA-256 $hash, índice legible ($($indice.Lineas.Count) líneas)"

    # -------------------------------------------------------------- 3. Copia de ensayo
    AsegurarDestino $BaseEnsayo
    $existe = Escalar "postgres" "SELECT count(*) FROM pg_database WHERE datname = '$BaseEnsayo'" "Comprobación de la copia existente"
    if ($existe -notmatch '^[01]$') { throw "Respuesta inesperada al comprobar la copia: $existe" }
    if ($existe -eq "1") {
        if (-not $Reemplazar) { throw "La base $BaseEnsayo ya existe. Vuelve a ejecutar con -Reemplazar para recrearla (solo se borra esa copia)." }
        AsegurarDestino $BaseEnsayo
        EjecutarOFallar "Borrado de la copia anterior (¿la API u otra herramienta está conectada a ella?)" "dropdb" @("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "--maintenance-db=postgres", $BaseEnsayo) | Out-Null
        Anotar "   Copia anterior $BaseEnsayo eliminada (-Reemplazar)."
    }
    AsegurarDestino $BaseEnsayo
    EjecutarOFallar "createdb $BaseEnsayo" "createdb" @("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "--maintenance-db=postgres", $BaseEnsayo) | Out-Null
    EjecutarOFallar "Restauración en $BaseEnsayo" "pg_restore" (@("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "-d", $BaseEnsayo, "--no-owner", "--exit-on-error", $dump)) -Archivo "restauracion.txt" | Out-Null
    $previaCopia = (EnCopia "Verificación previa de la copia" (Join-Path $scripts "M004_verificacion_previa.sql") "previa-copia.txt").Lineas
    $diferencias = @(Compare-Object $previaOficial $previaCopia)
    if ($diferencias.Count -ne 0) { throw "La copia restaurada NO coincide con la oficial ($($diferencias.Count) líneas distintas; ver previa-oficial.txt y previa-copia.txt). El respaldo no es confiable." }
    Anotar "3. Restauración en ${BaseEnsayo}: IDÉNTICA a la oficial (migraciones, tablas, filas, huella, usuarios por rol y CHECK)"

    # -------------------------------------------------------------- 4. Migración 004 SOLO en la copia
    $ya004 = Escalar $BaseEnsayo "SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL" "Comprobación de la 004 en la copia"
    if ($ya004 -eq "t") {
        Anotar "4. La copia ya tenía la 004 (por lo tanto la base de origen también): no se aplica."
    } elseif ($ya004 -eq "f") {
        EnCopia "Migración 004 en la copia (la transacción se revierte sola si falla)" $migracion004 "aplicar-004-copia.txt" | Out-Null
        $posterior = (EnCopia "Verificación posterior en la copia" (Join-Path $scripts "M004_verificacion_posterior.sql") "posterior-copia.txt").Lineas
        $huella = (Seccion $previaOficial "4") -eq (Seccion $posterior "4")
        $roles = (Seccion $previaOficial "5") -eq (Seccion $posterior "5")
        $registrada = (Seccion $posterior "1") -match "AutenticacionSegura"
        $sino = @{ $true = "sí"; $false = "NO" }
        Anotar ("4. 004 aplicada en la copia: migración registrada=" + $sino[[bool]$registrada] + ", huella de usuarios idéntica=" + $sino[[bool]$huella] +
            ", usuarios por rol idénticos=" + $sino[[bool]$roles] + " (detalle en posterior-copia.txt)")
        if (-not ($huella -and $roles -and $registrada)) { throw "La verificación posterior de la 004 no coincide." }
    } else {
        throw "Respuesta inesperada al comprobar la 004 en la copia: $ya004"
    }

    # -------------------------------------------------------------- 5. Auditoría en la copia
    EnCopia "Auditoría de usuarios en la copia" (Join-Path $scripts "F7_E2_auditoria_usuarios.sql") "auditoria-usuarios-copia.txt" | Out-Null
    Anotar "5. Auditoría de usuarios en la copia (ya con la 004): auditoria-usuarios-copia.txt"

    # -------------------------------------------------------------- 6. Limpieza: solo si se pide, y solo en la copia
    if ($IdsLimpieza) {
        $vars = @("ids=$IdsLimpieza", "cantidad=$CantidadLimpieza")
        if ($PermitirAdminEnLimpieza) { $vars += "permitir_admin=SI" }
        EnCopia "Ensayo de limpieza en la copia (ROLLBACK)" (Join-Path $scripts "F7_E2_limpieza_cuentas.sql") "limpieza-ensayo-copia.txt" $vars | Out-Null
        Anotar "6. Limpieza ENSAYADA en la copia con ROLLBACK (informe en limpieza-ensayo-copia.txt)"
        if ($ConfirmarLimpiezaEnEnsayo) {
            EnCopia "Limpieza confirmada en la copia" (Join-Path $scripts "F7_E2_limpieza_cuentas.sql") "limpieza-confirmada-copia.txt" ($vars + "confirmar=SI") | Out-Null
            EnCopia "Auditoría posterior de la copia" (Join-Path $scripts "F7_E2_auditoria_usuarios.sql") "auditoria-usuarios-copia-despues.txt" | Out-Null
            Anotar "   Limpieza CONFIRMADA solo en la copia; estado final en auditoria-usuarios-copia-despues.txt"
        }
    } else {
        Anotar "6. Limpieza de usuarios: NO se ejecutó (no se indicó -IdsLimpieza)."
    }

    Anotar ""
    Anotar "RESULTADO: correcto. $BaseOficial solo se leyó; los cambios están únicamente en $BaseEnsayo."
    $codigoSalida = 0
}
catch {
    Anotar ""
    Anotar ("DETENIDO: " + $_.Exception.Message)
    Anotar "$BaseOficial no se modificó (solo se leyó, si se llegó a conectar)."
    $codigoSalida = 1
}
finally {
    foreach ($clave in $anterior.Keys) {
        if ($null -eq $anterior[$clave]) { Remove-Item "Env:$clave" -ErrorAction SilentlyContinue }
        else { Set-Item "Env:$clave" $anterior[$clave] }
    }
    if ($null -ne $codificacionConsola) { try { [Console]::OutputEncoding = $codificacionConsola } catch { } }
    Anotar ("Código de salida: " + $codigoSalida)
    try { Guardar "resumen.txt" $resumen } catch { Write-Host "No se pudo escribir resumen.txt: $($_.Exception.Message)" }
}
exit $codigoSalida
