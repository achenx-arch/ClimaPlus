#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Fase 7, Etapa 2, paso 1: diagnóstico de SOLO LECTURA de climaplus_db frente a climaplus_test.

.DESCRIPTION
  No modifica ninguna base de datos:
    - todas las conexiones se abren con default_transaction_read_only=on (cualquier escritura fallaría);
    - solo ejecuta SELECT, M004_verificacion_previa.sql (solo SELECT) y pg_dump --schema-only (sin datos);
    - no lista correos ni datos personales (solo totales y dominios de correo);
    - la contraseña de PostgreSQL se pide una vez, vive solo en esta sesión y se borra al terminar.

  Genera una carpeta con:
    resumen.txt                 conclusiones (es lo que hay que revisar primero)
    migraciones-<base>.txt      historial de migraciones de cada base
    esquema-<base>.sql          estructura de cada base (sin datos)
    diferencias.txt             diferencias de estructura entre climaplus_db y climaplus_test
    previa.txt                  fotografía de climaplus_db antes de la 004 (M004_verificacion_previa.sql)

.EXAMPLE
  cd "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_diagnostico_climaplus_db.ps1

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File database\scripts\F7_diagnostico_climaplus_db.ps1 -Puerto 5433 -Usuario postgres
#>
[CmdletBinding()]
param(
    [string]$Servidor = "localhost",
    [int]$Puerto = 5432,
    [string]$Usuario = "postgres",
    [string]$BaseOficial = "climaplus_db",
    [string]$BasePruebas = "climaplus_test",
    [string]$Salida
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Salida) {
    $documentos = [Environment]::GetFolderPath("MyDocuments")
    $Salida = Join-Path (Join-Path $documentos "ClimaPlus-diagnostico") ("etapa2-" + (Get-Date -Format "yyyyMMdd-HHmm"))
}
New-Item -ItemType Directory -Force -Path $Salida | Out-Null
$temporal = Join-Path $Salida "_tmp"
New-Item -ItemType Directory -Force -Path $temporal | Out-Null
$utf8SinBom = New-Object System.Text.UTF8Encoding $false
$resumen = New-Object System.Collections.Generic.List[string]
function Anotar([string]$linea) { $resumen.Add($linea); Write-Host $linea }

# ------------------------------------------------------------------ Herramientas
foreach ($herramienta in "psql", "pg_dump") {
    if (-not (Get-Command $herramienta -ErrorAction SilentlyContinue)) {
        throw "No se encontró '$herramienta'. Agrega la carpeta bin de PostgreSQL al PATH de esta ventana, por ejemplo:`n  `$env:Path += ';C:\Program Files\PostgreSQL\18\bin'"
    }
}

# ------------------------------------------------------------------ Entorno de solo lectura
$anterior = @{
    PGPASSWORD       = $env:PGPASSWORD
    PGOPTIONS        = $env:PGOPTIONS
    PGCLIENTENCODING = $env:PGCLIENTENCODING
    PGCONNECT_TIMEOUT = $env:PGCONNECT_TIMEOUT
}
$codificacionConsola = [Console]::OutputEncoding
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$env:PGCLIENTENCODING = "UTF8"
$env:PGCONNECT_TIMEOUT = "10"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
if (-not $env:PGPASSWORD) {
    $seguro = Read-Host "Contraseña de PostgreSQL para '$Usuario' (no se guarda)" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try { $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# Ejecuta psql con un archivo SQL (evita problemas de comillas de PowerShell 5.1 con argumentos nativos).
function Invoke-PsqlArchivo([string]$Base, [string]$Archivo, [switch]$Plano) {
    $argumentos = @("-X", "-w", "-h", $Servidor, "-p", "$Puerto", "-U", $Usuario, "-d", $Base, "-v", "ON_ERROR_STOP=1", "-P", "pager=off")
    if ($Plano) { $argumentos += @("-A", "-t", "-F", "|") }
    $argumentos += @("-f", $Archivo)
    $previo = $ErrorActionPreference
    $ErrorActionPreference = "Continue"   # en PowerShell 5.1, stderr de un programa nativo no debe detener el script
    try { $texto = & psql @argumentos 2>&1 | ForEach-Object { "$_" } }
    finally { $ErrorActionPreference = $previo }
    [pscustomobject]@{ Codigo = $LASTEXITCODE; Lineas = @($texto) }
}

function Consultar([string]$Base, [string]$Sql) {
    $archivo = Join-Path $temporal ("q" + [guid]::NewGuid().ToString("N") + ".sql")
    [IO.File]::WriteAllText($archivo, $Sql, $utf8SinBom)
    $r = Invoke-PsqlArchivo -Base $Base -Archivo $archivo -Plano
    Remove-Item $archivo -Force
    if ($r.Codigo -ne 0) { throw ("Error en '$Base': " + ($r.Lineas -join " ")) }
    return @($r.Lineas | Where-Object { $_ -ne "" })
}

function Literal([string]$texto) { "'" + $texto.Replace("'", "''") + "'" }
function SiNo([string]$valor) { switch ($valor) { "t" { "sí" } "f" { "no" } "YES" { "sí" } "NO" { "no" } default { $valor } } }

try {
    Anotar "ClimaPlus - Diagnóstico de SOLO LECTURA (Fase 7, Etapa 2, paso 1)"
    Anotar ("Fecha: " + (Get-Date -Format "yyyy-MM-dd HH:mm"))
    Anotar ("Herramientas: " + ((& psql --version) -join " ") + " | " + ((& pg_dump --version) -join " "))
    Anotar "Servidor: ${Servidor}:${Puerto} (usuario $Usuario)"
    Anotar "Carpeta de resultados: $Salida"
    Anotar ""

    # -------------------------------------------------------------- Conexión efectiva de la API (sin secretos)
    $proyectoApi = Join-Path $repo "backend\ClimaPlus.Api"
    if ($env:ConnectionStrings__ClimaPlusDb) {
        Anotar "AVISO: la variable de entorno ConnectionStrings__ClimaPlusDb está definida y tiene prioridad sobre User Secrets."
    }
    if ((Get-Command dotnet -ErrorAction SilentlyContinue) -and (Test-Path $proyectoApi)) {
        $previo = $ErrorActionPreference; $ErrorActionPreference = "Continue"
        try { $secretos = & dotnet user-secrets list --project $proyectoApi 2>&1 | ForEach-Object { "$_" } }
        finally { $ErrorActionPreference = $previo }
        $linea = $secretos | Where-Object { $_ -like "ConnectionStrings:ClimaPlusDb = *" } | Select-Object -First 1
        if ($linea) {
            $hostApi = if ($linea -match "(?i)(?:Host|Server)=([^;]+)") { $Matches[1] } else { "?" }
            $baseApi = if ($linea -match "(?i)(?:Database|Initial Catalog)=([^;]+)") { $Matches[1] } else { "?" }
            Anotar "Conexión de la API (User Secrets): Host=$hostApi Database=$baseApi (el resto no se muestra)"
        } else {
            Anotar "Conexión de la API: no hay ConnectionStrings:ClimaPlusDb en User Secrets."
        }
        $linea = $null; $secretos = $null
    } else {
        Anotar "Conexión de la API: no se pudo consultar (dotnet no disponible)."
    }
    Anotar ""

    # -------------------------------------------------------------- Migraciones que espera el código
    $esperadas = @()
    foreach ($archivoSql in Get-ChildItem (Join-Path $repo "database\migrations") -Filter "0*.sql" | Sort-Object Name) {
        foreach ($m in (Select-String -Path $archivoSql.FullName -Pattern "VALUES \('(\d{14}_[A-Za-z0-9]+)'").Matches) {
            $esperadas += $m.Groups[1].Value
        }
    }
    $migracionesEf = @(Get-ChildItem (Join-Path $repo "backend\ClimaPlus.Api\Data\Migrations") -Filter "*.cs" |
        Where-Object { $_.Name -match "^\d{14}_[A-Za-z0-9]+\.cs$" } | ForEach-Object { $_.BaseName } | Sort-Object)
    Anotar ("Migraciones en database/migrations: " + ($esperadas -join ", "))
    if (Compare-Object $esperadas $migracionesEf) {
        Anotar ("AVISO: no coinciden con las migraciones de EF Core: " + ($migracionesEf -join ", "))
    } else {
        Anotar "Coinciden con las migraciones de EF Core del backend."
    }
    Anotar ""

    # -------------------------------------------------------------- Cada base
    $faltantesOficial = @()
    $existeOficial = $false
    foreach ($base in @($BaseOficial, $BasePruebas)) {
        Anotar "[$base]"
        $existe = @(Consultar "postgres" ("SELECT count(*) FROM pg_database WHERE datname = " + (Literal $base) + ";"))
        if ($existe[0] -ne "1") { Anotar "  NO EXISTE en este servidor."; Anotar ""; continue }
        if ($base -eq $BaseOficial) { $existeOficial = $true }

        $info = @(Consultar $base @"
SELECT current_setting('server_version'),
       pg_size_pretty(pg_database_size(current_database())),
       pg_encoding_to_char(d.encoding), d.datcollate,
       (SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid()),
       current_setting('default_transaction_read_only')
FROM pg_database d WHERE d.datname = current_database();
"@)[0].Split("|")
        Anotar ("  PostgreSQL " + $info[0] + " | tamaño " + $info[1] + " | " + $info[2] + "/" + $info[3] + " | otras conexiones abiertas: " + $info[4] + " | sesión de solo lectura: " + $info[5])

        $tieneHistorial = @(Consultar $base "SELECT to_regclass('public.""__EFMigrationsHistory""') IS NOT NULL;")[0] -eq "t"
        $aplicadas = @()
        if ($tieneHistorial) {
            $aplicadas = @(Consultar $base 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY 1;')
        }
        [IO.File]::WriteAllLines((Join-Path $Salida "migraciones-$base.txt"), [string[]]$aplicadas, $utf8SinBom)
        $faltan = @($esperadas | Where-Object { $aplicadas -notcontains $_ })
        $sobran = @($aplicadas | Where-Object { $esperadas -notcontains $_ })
        if (-not $tieneHistorial) { Anotar "  Sin tabla __EFMigrationsHistory." }
        Anotar ("  Migraciones aplicadas (" + $aplicadas.Count + "): " + ($(if ($aplicadas.Count) { $aplicadas -join ", " } else { "ninguna" })))
        Anotar ("  FALTAN: " + ($(if ($faltan.Count) { $faltan -join ", " } else { "ninguna" })) + " | desconocidas: " + ($(if ($sobran.Count) { $sobran -join ", " } else { "ninguna" })))
        if ($base -eq $BaseOficial) { $faltantesOficial = $faltan }

        $e = @(Consultar $base @"
SELECT to_regclass('public.desafio_autenticacion') IS NOT NULL,
       to_regclass('public.identidad_externa') IS NOT NULL,
       EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'correo_verificado'),
       EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'sello_seguridad'),
       (SELECT is_nullable FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'usuario' AND column_name = 'password_hash'),
       EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'consulta_clima' AND column_name = 'id_usuario' AND is_nullable = 'YES'),
       (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'),
       (SELECT count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE c.contype = 'c' AND n.nspname = 'public');
"@)[0].Split("|")
        Anotar ("  Estructuras de la 004: desafio_autenticacion=" + (SiNo $e[0]) + ", identidad_externa=" + (SiNo $e[1]) + ", usuario.correo_verificado=" + (SiNo $e[2]) + ", usuario.sello_seguridad=" + (SiNo $e[3]) + ", password_hash admite NULL=" + (SiNo $e[4]))
        Anotar ("  Estructura de la 002: consulta_clima.id_usuario admite NULL=" + (SiNo $e[5]) + " | tablas (con el historial): " + $e[6] + " | CHECK: " + $e[7])

        if (@(Consultar $base "SELECT to_regclass('public.usuario') IS NOT NULL;")[0] -eq "t") {
            $u = @(Consultar $base "SELECT count(*), count(*) FILTER (WHERE activo), count(*) FILTER (WHERE rol = 'ADMINISTRADOR'), count(*) FILTER (WHERE rol = 'USUARIO') FROM usuario;")[0].Split("|")
            Anotar ("  Usuarios: " + $u[0] + " (activos " + $u[1] + ", ADMINISTRADOR " + $u[2] + ", USUARIO " + $u[3] + ")")
            $dominios = Consultar $base "SELECT d || ' (' || n || ')' FROM (SELECT lower(split_part(correo, '@', 2)) AS d, count(*) AS n FROM usuario GROUP BY 1) x ORDER BY n DESC, d;"
            Anotar ("  Dominios de correo: " + ($dominios -join ", "))
        }

        $archivoEsquema = Join-Path $Salida "esquema-$base.sql"
        $previo = $ErrorActionPreference; $ErrorActionPreference = "Continue"
        try { $errores = & pg_dump -w -h $Servidor -p "$Puerto" -U $Usuario --schema-only --no-owner --no-privileges -f $archivoEsquema $base 2>&1 | ForEach-Object { "$_" } }
        finally { $ErrorActionPreference = $previo }
        if ($LASTEXITCODE -ne 0) { Anotar ("  ERROR de pg_dump: " + ($errores -join " ")) } else { Anotar "  Estructura guardada en esquema-$base.sql" }
        Anotar ""
    }

    # -------------------------------------------------------------- Diferencias de estructura
    $a = Join-Path $Salida "esquema-$BaseOficial.sql"
    $b = Join-Path $Salida "esquema-$BasePruebas.sql"
    if ((Test-Path $a) -and (Test-Path $b)) {
        $filtro = { $_ -notmatch '^\\(un)?restrict ' -and $_ -notmatch '^--' -and $_.Trim() -ne "" }
        $lineasA = @(Get-Content $a -Encoding UTF8 | Where-Object $filtro)
        $lineasB = @(Get-Content $b -Encoding UTF8 | Where-Object $filtro)
        $dif = @(Compare-Object $lineasA $lineasB)
        $texto = @("Diferencias de estructura (sin datos). '<=' solo en $BaseOficial; '=>' solo en $BasePruebas.", "")
        $texto += $dif | ForEach-Object { $_.SideIndicator + " " + $_.InputObject }
        [IO.File]::WriteAllLines((Join-Path $Salida "diferencias.txt"), [string[]]$texto, $utf8SinBom)
        $soloOficial = @($dif | Where-Object { $_.SideIndicator -eq "<=" }).Count
        $soloPruebas = @($dif | Where-Object { $_.SideIndicator -eq "=>" }).Count
        Anotar "Diferencias de estructura: $soloOficial líneas solo en $BaseOficial, $soloPruebas solo en $BasePruebas (diferencias.txt)"
        $tablasSoloPruebas = @($dif | Where-Object { $_.SideIndicator -eq "=>" -and $_.InputObject -match '^CREATE TABLE public\.(\S+)' } | ForEach-Object { $Matches[1] })
        $tablasSoloOficial = @($dif | Where-Object { $_.SideIndicator -eq "<=" -and $_.InputObject -match '^CREATE TABLE public\.(\S+)' } | ForEach-Object { $Matches[1] })
        Anotar ("  Tablas solo en ${BasePruebas}: " + ($(if ($tablasSoloPruebas.Count) { $tablasSoloPruebas -join ", " } else { "ninguna" })))
        Anotar ("  Tablas solo en ${BaseOficial}: " + ($(if ($tablasSoloOficial.Count) { $tablasSoloOficial -join ", " } else { "ninguna" })))
        Anotar ""
    }

    # -------------------------------------------------------------- Fotografía previa de la base oficial
    if ($existeOficial) {
        $previa = Join-Path $repo "database\scripts\M004_verificacion_previa.sql"
        $r = Invoke-PsqlArchivo -Base $BaseOficial -Archivo $previa
        [IO.File]::WriteAllLines((Join-Path $Salida "previa.txt"), [string[]]$r.Lineas, $utf8SinBom)
        if ($r.Codigo -eq 0) { Anotar "Fotografía previa de $BaseOficial guardada en previa.txt" }
        else { Anotar "AVISO: M004_verificacion_previa.sql terminó con errores (ver previa.txt)" }
        Anotar ""
    }

    # -------------------------------------------------------------- Conclusión
    if (-not $existeOficial) {
        Anotar "CONCLUSIÓN: $BaseOficial no existe en ${Servidor}:${Puerto}. Revisa el servidor, el puerto o el nombre de la base."
    } elseif ($faltantesOficial.Count -eq 0) {
        Anotar "CONCLUSIÓN: $BaseOficial tiene todas las migraciones del repositorio. No hace falta migrar."
    } else {
        Anotar ("CONCLUSIÓN: a $BaseOficial le faltan " + $faltantesOficial.Count + " migración(es): " + ($faltantesOficial -join ", "))
        Anotar "Se deben aplicar en ese orden, siguiendo docs/despliegue/migracion-004-climaplus_db.md, SOLO con autorización."
    }
    Anotar "Este diagnóstico no modificó ninguna base (conexiones con default_transaction_read_only=on)."
}
finally {
    foreach ($clave in $anterior.Keys) {
        if ($null -eq $anterior[$clave]) { Remove-Item "Env:$clave" -ErrorAction SilentlyContinue }
        else { Set-Item "Env:$clave" $anterior[$clave] }
    }
    [Console]::OutputEncoding = $codificacionConsola
    Remove-Item $temporal -Recurse -Force -ErrorAction SilentlyContinue
    [IO.File]::WriteAllLines((Join-Path $Salida "resumen.txt"), [string[]]$resumen, $utf8SinBom)
}
