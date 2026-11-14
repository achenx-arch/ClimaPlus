#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Aplica ÚNICAMENTE la migración 005 (20261113150000_CambioPasswordConCodigo) sobre climaplus_test o
  climaplus_db, con respaldo verificado antes y comprobaciones después.

.DESCRIPTION
  La 005 solo reemplaza la restricción ck_desafio_autenticacion_proposito para admitir 'CAMBIAR_PASSWORD' (conserva
  los 5 propósitos anteriores). No toca tablas, columnas, filas, usuarios, roles ni credenciales.

  El destino se elige con -Destino y solo puede ser climaplus_test o climaplus_db. Nunca se conecta a
  climaplus_db_ensayo ni a otra base.

  Sin -Aplicar el script SOLO comprueba (no escribe nada):
    1. Variables de entorno que podrían redirigir la conexión (proceso, usuario y equipo).
    2. Conexión efectiva: la base conectada es exactamente el destino.
    3. Estado: exactamente las 4 migraciones anteriores y la restricción sin CAMBIAR_PASSWORD (o la 005 ya aplicada).
    4. No hay otras conexiones abiertas al destino (API detenida, pgAdmin cerrado).
    5. Solo para climaplus_db: la 005 ya está aplicada y verificada en climaplus_test, y el archivo .trx de las
       pruebas (-ResultadoPruebas) es posterior, sin fallos y sin pruebas de PostgreSQL omitidas.
  Con -Aplicar, si todo lo anterior pasa:
    6. Respaldo completo (pg_dump -Fc) + SHA-256 + lectura de su índice (pg_restore --list).
    7. Verificación previa (solo lectura: migraciones, filas y huella md5 completa de cada tabla, usuarios por rol,
       restricciones e índices, desafíos).
    8. Aplica database/migrations/005_CambioPasswordConCodigo.sql (una transacción; si falla se revierte sola),
       con lock_timeout de 10 s.
    9. Verificación posterior: 5 migraciones; filas, huellas, roles, restricciones e índices IDÉNTICOS; la restricción
       admite los 6 propósitos; prueba de la restricción en una tabla TEMPORAL (sin escribir en tablas reales).

  Código de salida: 0 = correcto; 1 = detenido o fallo. Siempre deja resumen.txt en la carpeta de resultados.
  No muestra ni guarda contraseñas: la de PostgreSQL se pide sin eco (o se toma de PGPASSWORD) y no se escribe.

.EXAMPLE
  # 1) Solo comprobaciones sobre la base de pruebas
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_test

.EXAMPLE
  # 2) Respaldo + 005 en la base de pruebas
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_test -Aplicar

.EXAMPLE
  # 3) Base oficial, SOLO después de compilar y pasar las pruebas (archivo .trx de dotnet test)
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_aplicar_005.ps1 -Destino climaplus_db -Aplicar -ResultadoPruebas "C:\ruta\pruebas-005.trx"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("climaplus_test", "climaplus_db")]
    [string]$Destino,
    [string]$Servidor = "localhost",
    [int]$Puerto = 5432,
    [string]$Usuario = "postgres",
    [string]$CarpetaRespaldos,
    [string]$ResultadoPruebas,
    [switch]$Aplicar
)

$ErrorActionPreference = "Stop"
$Migracion = "20261113150000_CambioPasswordConCodigo"
$Anteriores = @("20261016052608_InitialCreate", "20261028062000_ConsultaClimaUsuarioOpcional", "20261028062100_ConsultaClimaDetalle", "20261109180000_AutenticacionSegura")
$Propositos = @("VERIFICAR_CORREO", "INICIO_SESION", "INICIO_GOOGLE", "VINCULAR_GOOGLE", "RECUPERAR_PASSWORD", "CAMBIAR_PASSWORD")
$BaseProtegida = "climaplus_db_ensayo"

$utf8SinBom = New-Object System.Text.UTF8Encoding $false
$resumen = New-Object System.Collections.Generic.List[string]
$codigoSalida = 1
$Salida = $null
function Anotar([string]$linea) { $resumen.Add($linea); Write-Host $linea }
function Guardar([string]$nombre, $lineas) { if ($Salida) { [IO.File]::WriteAllLines((Join-Path $Salida $nombre), [string[]]@($lineas), $utf8SinBom) } }

$variablesPg = @("PGPASSWORD", "PGOPTIONS", "PGCLIENTENCODING", "PGCONNECT_TIMEOUT", "PGHOST", "PGHOSTADDR", "PGPORT", "PGDATABASE", "PGUSER", "PGSERVICE", "PGSERVICEFILE")
$anterior = @{}
foreach ($v in $variablesPg) { $anterior[$v] = [Environment]::GetEnvironmentVariable($v, "Process") }
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
function Escalar([string]$b, [string]$Sql, [string]$Descripcion) {
    # La consulta va en un archivo temporal: PowerShell 5.1 rompe las comillas dobles en argumentos de programas nativos.
    $tmp = [IO.Path]::Combine([IO.Path]::GetTempPath(), "climaplus-" + [guid]::NewGuid().ToString("N") + ".sql")
    [IO.File]::WriteAllText($tmp, $Sql, $utf8SinBom)
    try { $r = Ejecutar "psql" ((Conexion $b) + @("-A", "-t", "-f", $tmp)) $SOLO_LECTURA }
    finally { Remove-Item $tmp -Force -ErrorAction SilentlyContinue }
    if ($r.Codigo -ne 0) { throw "$Descripcion falló (código $($r.Codigo)): $(($r.Lineas | Select-Object -Last 5) -join ' | ')" }
    @($r.Lineas | Where-Object { $_.Trim() -ne "" } | ForEach-Object { $_.Trim() })
}
function Verificacion([string]$b, [string]$ArchivoSql) {
    $r = Ejecutar "psql" ((Conexion $b) + @("-A", "-t", "-f", $ArchivoSql)) $SOLO_LECTURA
    if ($r.Codigo -ne 0) { throw "La verificación de $b falló (código $($r.Codigo)): $(($r.Lineas | Select-Object -Last 5) -join ' | ')" }
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
function DescribirCadena([string]$cadena) {
    # Solo servidor, puerto y base: nunca la contraseña.
    $partes = @{}
    foreach ($p in ($cadena -split ';')) { $kv = $p -split '=', 2; if ($kv.Count -eq 2) { $partes[$kv[0].Trim().ToLowerInvariant()] = $kv[1].Trim() } }
    $base = $partes["database"]; if (-not $base) { $base = $partes["db"] }
    "Host=$($partes['host']) Port=$($partes['port']) Database=$base"
}
function BaseDeCadena([string]$cadena) {
    foreach ($p in ($cadena -split ';')) { $kv = $p -split '=', 2; if ($kv.Count -eq 2 -and @("database", "db") -contains $kv[0].Trim().ToLowerInvariant()) { return $kv[1].Trim() } }
    ""
}
function EstadoMigracion([string]$b) {
    $m = @(Escalar $b 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1' "Lectura de migraciones de $b")
    $def = @(Escalar $b "SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_desafio_autenticacion_proposito' AND conrelid = to_regclass('desafio_autenticacion')" "Lectura de la restricción en $b")
    [pscustomobject]@{ Migraciones = $m; Restriccion = ($def -join " ") }
}
function RestriccionCompleta([string]$def) {
    foreach ($p in $Propositos) { if ($def -notmatch "'$p'") { return $false } }
    $true
}

try {
    # -------------------------------------------------------------- Archivos y herramientas
    $repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $sql005 = Join-Path $repo "database\migrations\005_CambioPasswordConCodigo.sql"
    $verificacionSql = Join-Path $repo "database\scripts\M005_verificacion.sql"
    $comprobarSql = Join-Path $repo "database\scripts\M005_comprobar_restriccion.sql"
    foreach ($a in @($sql005, $verificacionSql, $comprobarSql)) { if (-not (Test-Path $a)) { throw "No se encontró $a. Ejecuta el script desde el repositorio de ClimaPlus." } }
    foreach ($h in "psql", "pg_dump", "pg_restore") {
        if (-not (Get-Command $h -ErrorAction SilentlyContinue)) { throw "No se encontró '$h'. Agrega la carpeta bin de PostgreSQL al PATH: `$env:Path = 'C:\Program Files\PostgreSQL\18\bin;' + `$env:Path" }
    }
    if ($Destino -eq $BaseProtegida) { throw "$BaseProtegida está protegida." }   # ValidateSet ya lo impide; doble seguro

    # -------------------------------------------------------------- Carpeta de resultados
    if (-not $CarpetaRespaldos) {
        $documentos = [Environment]::GetFolderPath("MyDocuments"); if (-not $documentos) { $documentos = $HOME }
        $CarpetaRespaldos = Join-Path $documentos "ClimaPlus-respaldos"
    }
    $sello = Get-Date -Format "yyyyMMdd-HHmmss"
    $Salida = Join-Path $CarpetaRespaldos ("005-$Destino-$sello")
    New-Item -ItemType Directory -Force -Path $Salida | Out-Null

    try { $codificacionConsola = [Console]::OutputEncoding; [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { $codificacionConsola = $null }

    $modo = "SOLO COMPROBACIONES (sin -Aplicar)"
    if ($Aplicar) { $modo = "RESPALDAR Y APLICAR la 005" }
    Anotar "ClimaPlus - Migración $Migracion sobre $Destino"
    Anotar ("Fecha: " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + " | servidor ${Servidor}:${Puerto} | usuario $Usuario | modo: $modo")
    Anotar "Resultados: $Salida"
    Anotar ""

    # -------------------------------------------------------------- 1. Variables de entorno
    $redirigen = @()
    foreach ($nivel in "Process", "User", "Machine") {
        foreach ($v in "PGHOSTADDR", "PGSERVICE", "PGSERVICEFILE") {
            if ([Environment]::GetEnvironmentVariable($v, $nivel)) { $redirigen += "$v ($nivel)" }
        }
    }
    if ($redirigen.Count -gt 0) { throw ("Hay variables que pueden redirigir psql a otro servidor: " + ($redirigen -join ", ") + ". Quítalas antes de continuar.") }
    $ignoradas = @()
    foreach ($v in "PGHOST", "PGPORT", "PGDATABASE", "PGUSER") {
        if ([Environment]::GetEnvironmentVariable($v, "Process")) { $ignoradas += $v; Remove-Item "Env:$v" -ErrorAction SilentlyContinue }
    }
    $nota = "ninguna"
    if ($ignoradas.Count -gt 0) { $nota = ($ignoradas -join ", ") + " (se ignoran: el script indica servidor, puerto, usuario y base explícitamente)" }
    Anotar "1. Variables PG que podrían redirigir la conexión: ninguna. Variables PG ignoradas en esta ejecución: $nota."
    foreach ($nivel in "Process", "User", "Machine") {
        $api = [Environment]::GetEnvironmentVariable("ConnectionStrings__ClimaPlusDb", $nivel)
        if ($api) {
            $baseApi = BaseDeCadena $api
            Anotar "   ConnectionStrings__ClimaPlusDb ($nivel) definida: $(DescribirCadena $api)"
            if ($Destino -eq "climaplus_db" -and $baseApi -ne "climaplus_db") { throw "ConnectionStrings__ClimaPlusDb ($nivel) apunta a '$baseApi', no a climaplus_db: la API usaría otra base. Corrígela antes de continuar." }
        }
        $pruebas = [Environment]::GetEnvironmentVariable("CLIMAPLUS_TEST_DB", $nivel)
        if ($pruebas) {
            $basePruebas = BaseDeCadena $pruebas
            Anotar "   CLIMAPLUS_TEST_DB ($nivel) definida: $(DescribirCadena $pruebas)"
            if ($basePruebas -ne "climaplus_test") { throw "CLIMAPLUS_TEST_DB ($nivel) apunta a '$basePruebas'. Debe apuntar a climaplus_test (nunca a la base oficial)." }
        }
    }

    $env:PGCLIENTENCODING = "UTF8"
    $env:PGCONNECT_TIMEOUT = "10"
    if (-not $env:PGPASSWORD) {
        $seguro = Read-Host "Contraseña de PostgreSQL para '$Usuario' (no se muestra ni se guarda)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
        try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    }

    # -------------------------------------------------------------- 2. Conexión efectiva
    $info = @(Escalar $Destino "SELECT current_database() || '|' || coalesce(inet_server_addr()::text, 'socket local') || '|' || current_setting('port') || '|' || current_user || '|' || split_part(version(), ',', 1)" "Conexión a $Destino")[0].Split("|")
    if ($info[0] -ne $Destino) { throw "La conexión efectiva es a '$($info[0])', no a '$Destino'. Detenido." }
    Anotar "2. Conexión efectiva: base $($info[0]) | servidor $($info[1]) puerto $($info[2]) | usuario $($info[3]) | $($info[4])"

    # -------------------------------------------------------------- 3. Estado de la migración
    $estado = EstadoMigracion $Destino
    if ($estado.Migraciones.Count -eq 0) { throw "$Destino no tiene migraciones registradas." }
    $yaAplicada = $estado.Migraciones -contains $Migracion
    if ($yaAplicada) {
        if (Compare-Object ($Anteriores + $Migracion) $estado.Migraciones) { throw ("Migraciones inesperadas en ${Destino}: " + ($estado.Migraciones -join ", ")) }
        if (-not (RestriccionCompleta $estado.Restriccion)) { throw "La 005 figura como aplicada pero la restricción no admite los 6 propósitos: $($estado.Restriccion)" }
        Anotar "3. La 005 YA está aplicada en $Destino (5 migraciones y restricción con los 6 propósitos). No se aplica de nuevo."
    } else {
        if (Compare-Object $Anteriores $estado.Migraciones) { throw ("$Destino no tiene exactamente las 4 migraciones anteriores: " + ($estado.Migraciones -join ", ")) }
        if (-not $estado.Restriccion) { throw "No existe ck_desafio_autenticacion_proposito en $Destino." }
        if ($estado.Restriccion -match "CAMBIAR_PASSWORD") { throw "La restricción ya admite CAMBIAR_PASSWORD sin estar registrada la 005: revisar antes de continuar." }
        Anotar "3. $Destino tiene exactamente las 4 migraciones anteriores (001 a 004) y la restricción sin CAMBIAR_PASSWORD."
    }

    # -------------------------------------------------------------- 4. Conexiones abiertas
    $otras = @(Escalar $Destino "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()" "Conteo de conexiones")[0]
    if ($otras -ne "0") { throw "Hay $otras conexión(es) abierta(s) a $Destino. Detén la API (Ctrl+C) y las pruebas, cierra pgAdmin u otras herramientas, y vuelve a ejecutar." }
    Anotar "4. No hay otras conexiones abiertas a $Destino."

    # -------------------------------------------------------------- 5. Condiciones de la base oficial
    if ($Destino -eq "climaplus_db") {
        $prueba = EstadoMigracion "climaplus_test"
        if (-not ($prueba.Migraciones -contains $Migracion) -or -not (RestriccionCompleta $prueba.Restriccion)) {
            throw "La 005 todavía no está aplicada y verificada en climaplus_test. Primero: -Destino climaplus_test -Aplicar y las pruebas."
        }
        $marca = Get-ChildItem $CarpetaRespaldos -Directory -Filter "005-climaplus_test-*" -ErrorAction SilentlyContinue |
            Where-Object { (Test-Path (Join-Path $_.FullName "resumen.txt")) -and (Select-String -Path (Join-Path $_.FullName "resumen.txt") -Pattern "^RESULTADO: CORRECTO" -Quiet) } |
            Sort-Object Name | Select-Object -Last 1
        if (-not $marca) { throw "No se encontró un resultado CORRECTO de este script para climaplus_test en $CarpetaRespaldos." }
        if ($Aplicar) {
            if (-not $ResultadoPruebas) { throw "Para aplicar en climaplus_db indica -ResultadoPruebas con el archivo .trx de dotnet test (ver docs/despliegue/migracion-005.md)." }
            if (-not (Test-Path $ResultadoPruebas)) { throw "No existe $ResultadoPruebas." }
            $trx = Get-Item $ResultadoPruebas
            $fechaMarca = (Get-Item (Join-Path $marca.FullName "resumen.txt")).LastWriteTime
            if ($trx.LastWriteTime -lt $fechaMarca) { throw "El .trx es anterior a la migración de climaplus_test ($($marca.Name)): vuelve a ejecutar las pruebas." }
            [xml]$xml = Get-Content -Raw -Encoding UTF8 $trx.FullName
            $contadores = $xml.TestRun.ResultSummary.Counters
            $total = [int]$contadores.total; $pasaron = [int]$contadores.passed; $fallaron = [int]$contadores.failed + [int]$contadores.error + [int]$contadores.timeout + [int]$contadores.aborted
            $resultados = @($xml.TestRun.Results.UnitTestResult)
            $omitidas = @($resultados | Where-Object { $_.outcome -eq "NotExecuted" } | ForEach-Object { $_.testName })
            $omitidasPg = @($omitidas | Where-Object { $_ -notmatch '\.Correo\.EntregaMailpitTests\.' })
            if ($total -le 0 -or $fallaron -ne 0) { throw "El .trx tiene $fallaron prueba(s) fallida(s) de $total. No se aplica en la base oficial." }
            if ($omitidasPg.Count -gt 0) { throw ("Hay pruebas omitidas que no son de Mailpit (¿falta CLIMAPLUS_TEST_DB?): " + ($omitidasPg -join ", ")) }
            foreach ($clave in "CambioPasswordConCodigoTests", "Esquema005_CheckDePropositos", "Script005_SoloReemplazaElCheck") {
                if (-not ($resultados | Where-Object { $_.testName -match $clave -and $_.outcome -eq "Passed" })) { throw "El .trx no contiene pruebas superadas de '$clave'." }
            }
            $notaMailpit = ""
            if ($omitidas.Count -gt 0) { $notaMailpit = " ($($omitidas.Count) omitida(s): solo la entrega por Mailpit)" }
            Anotar "5. Condiciones de la oficial: 005 aplicada y verificada en climaplus_test ($($marca.Name)); pruebas $pasaron/$total superadas, 0 fallidas$notaMailpit; .trx posterior a la migración de pruebas."
        } else {
            Anotar "5. climaplus_test ya tiene la 005 verificada ($($marca.Name)). Con -Aplicar se exigirá además -ResultadoPruebas (.trx)."
        }
    }

    if (-not $Aplicar -or $yaAplicada) {
        if ($yaAplicada) {
            $despues = Verificacion $Destino $verificacionSql
            Guardar "verificacion.txt" $despues
            $c = Ejecutar "psql" ((Conexion $Destino) + @("-f", $comprobarSql)) ""
            Guardar "comprobar-restriccion.txt" $c.Lineas
            if ($c.Codigo -ne 0 -or -not ($c.Lineas -match "acepta_cambiar_password=si") -or -not ($c.Lineas -match "rechaza_desconocido=si")) { throw "La comprobación de la restricción falló (comprobar-restriccion.txt)." }
            Anotar "   Comprobación de la restricción (tabla temporal): acepta CAMBIAR_PASSWORD y rechaza propósitos desconocidos."
            Anotar ""
            Anotar "RESULTADO: CORRECTO (la 005 ya estaba aplicada; no se modificó nada)."
        } else {
            Anotar ""
            Anotar "RESULTADO: comprobaciones previas correctas. NO se modificó nada. Para respaldar y aplicar: agrega -Aplicar."
        }
        $codigoSalida = 0
    } else {

    # -------------------------------------------------------------- 6. Respaldo completo
    $dump = Join-Path $Salida ("respaldo-$Destino-$sello.dump")
    $r = Ejecutar "pg_dump" @("-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-w", "-d", $Destino, "-Fc", "-f", $dump) ""
    Guardar "respaldo-log.txt" $r.Lineas
    if ($r.Codigo -ne 0) { throw "pg_dump falló (código $($r.Codigo)). NO se modificó la base. Detalle en respaldo-log.txt." }
    if (-not (Test-Path $dump) -or (Get-Item $dump).Length -eq 0) { throw "El respaldo no existe o está vacío. NO se modificó la base." }
    $hash = (Get-FileHash $dump -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($dump + ".sha256", "$hash  $(Split-Path -Leaf $dump)`r`n", $utf8SinBom)
    $indice = Ejecutar "pg_restore" @("--list", $dump) ""
    Guardar "respaldo-indice.txt" $indice.Lineas
    if ($indice.Codigo -ne 0) { throw "El índice del respaldo no se puede leer: archivo dañado. NO se modificó la base." }
    if (-not ($indice.Lineas | Where-Object { $_ -match "^;\s+Dbname:\s+$Destino\s*$" })) { throw "El respaldo no es de $Destino. NO se modificó la base." }
    $datos = @($indice.Lineas | Where-Object { $_ -match 'TABLE DATA public ' })
    foreach ($t in "usuario", "desafio_autenticacion", "identidad_externa", "__EFMigrationsHistory") {
        if (-not ($datos | Where-Object { $_ -match "TABLE DATA public `"?$([regex]::Escape($t))`"? " })) { throw "El respaldo no contiene los datos de la tabla $t. NO se modificó la base." }
    }
    if ((Get-FileHash $dump -Algorithm SHA256).Hash -ne $hash) { throw "El SHA-256 del respaldo cambió durante la verificación. NO se modificó la base." }
    Anotar ("6. Respaldo completo válido: $dump ({0:N1} KB, $($datos.Count) tablas con datos). SHA-256: $hash" -f ((Get-Item $dump).Length / 1KB))

    # -------------------------------------------------------------- 7. Verificación previa
    $antes = Verificacion $Destino $verificacionSql
    Guardar "verificacion-antes.txt" $antes
    $filasAntes = Seccion $antes "2"
    if ($filasAntes.Count -ne 10) { throw "La verificación previa no leyó las 10 tablas." }
    Anotar ("7. Verificación previa guardada (verificacion-antes.txt): " + ($filasAntes -join ", "))

    # -------------------------------------------------------------- 8. Aplicar la 005
    $otras = @(Escalar $Destino "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()" "Conteo de conexiones")[0]
    if ($otras -ne "0") { throw "Se abrió una conexión a $Destino después del respaldo. Detén la API y vuelve a ejecutar. NO se modificó la base." }
    $r = Ejecutar "psql" ((Conexion $Destino) + @("-f", $sql005)) "-c lock_timeout=10s -c statement_timeout=120s"
    Guardar "aplicar-005.txt" $r.Lineas
    if ($r.Codigo -ne 0) {
        Anotar "8. La 005 FALLÓ (código $($r.Codigo)); la transacción se revirtió sola. Detalle en aplicar-005.txt."
        throw "La migración no se aplicó."
    }
    Anotar "8. Migración $Migracion aplicada (aplicar-005.txt)."

    # -------------------------------------------------------------- 9. Verificación posterior
    $despues = Verificacion $Destino $verificacionSql
    Guardar "verificacion-despues.txt" $despues
    $fallas = @()
    if (Compare-Object ($Anteriores + $Migracion) (Seccion $despues "1")) { $fallas += "migraciones: $((Seccion $despues '1') -join ', ')" }
    $nombres = @{ "2" = "filas por tabla"; "3" = "huella de datos de las tablas"; "4" = "usuarios por rol/estado"; "5" = "restricciones e índices"; "7" = "desafíos por propósito" }
    foreach ($n in "2", "3", "4", "5", "7") {
        if (((Seccion $antes $n) -join "`n") -ne ((Seccion $despues $n) -join "`n")) { $fallas += "cambió: $($nombres[$n])" }
    }
    $definicion = (Seccion $despues "6") -join " "
    if (-not (RestriccionCompleta $definicion)) { $fallas += "la restricción no admite los 6 propósitos: $definicion" }
    $c = Ejecutar "psql" ((Conexion $Destino) + @("-f", $comprobarSql)) ""
    Guardar "comprobar-restriccion.txt" $c.Lineas
    if ($c.Codigo -ne 0 -or -not ($c.Lineas -match "acepta_cambiar_password=si") -or -not ($c.Lineas -match "rechaza_desconocido=si") -or -not ($c.Lineas -match "acepta_anteriores=5")) {
        $fallas += "la comprobación de la restricción en tabla temporal falló"
    }
    if ($fallas.Count -gt 0) {
        Anotar ("9. VERIFICACIÓN POSTERIOR CON DIFERENCIAS: " + ($fallas -join "; "))
        Anotar "   Detén todo y revisa verificacion-antes.txt / verificacion-despues.txt. Reversión: docs/despliegue/migracion-005.md, sección 7."
        throw "La verificación posterior no coincide."
    }
    Anotar "9. Verificación posterior correcta: 5 migraciones; filas, huella completa de las 10 tablas, usuarios por rol, restricciones e índices y desafíos IDÉNTICOS; la restricción admite los 6 propósitos (prueba en tabla temporal: acepta CAMBIAR_PASSWORD, acepta los 5 anteriores, rechaza desconocidos)."
    Anotar ""
    Anotar "RESULTADO: CORRECTO. La 005 quedó aplicada en $Destino y los datos existentes están intactos."
    Anotar "Respaldo: $dump"
    if ($Destino -eq "climaplus_db") { Anotar "Siguiente paso: inicia la API y verifica /health (docs/despliegue/migracion-005.md, sección 6)." }
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
            $Salida = Join-Path (Join-Path $documentos "ClimaPlus-respaldos") ("005-$Destino-detenido-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
            New-Item -ItemType Directory -Force -Path $Salida | Out-Null
        }
        Guardar "resumen.txt" $resumen
        Write-Host "Resumen: $(Join-Path $Salida 'resumen.txt')"
    } catch { Write-Host "No se pudo escribir resumen.txt: $($_.Exception.Message)" }
}
exit $codigoSalida
