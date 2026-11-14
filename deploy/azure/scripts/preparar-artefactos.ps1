#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Genera en Windows los artefactos para la VM de Azure: la API publicada (dotnet publish, dependiente del
  runtime, válida para Linux x64) y el frontend compilado con la URL pública. No se conecta a la VM ni a la base.

.DESCRIPTION
  1. Registra el commit y si hay cambios sin confirmar (lo ideal es generar desde un commit ya publicado).
  2. dotnet publish -c Release (sin ejecutable nativo: se ejecuta con "dotnet ClimaPlus.Api.dll" en Ubuntu).
  3. Comprueba que la salida NO contiene appsettings.Development.json, appsettings.Local.json, *.secrets.json ni .env.
  4. npm run build con VITE_API_URL=https://<Dominio> y comprueba que la CSP apunta a ese origen.
  5. Empaqueta api.tar.gz y frontend.tar.gz (tar.exe de Windows 10/11) y escribe sus SHA-256.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File deploy\azure\scripts\preparar-artefactos.ps1 -Dominio climaplus-umg.westus2.cloudapp.azure.com
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)+$')]
    [string]$Dominio,
    [string]$Salida
)

$ErrorActionPreference = "Stop"
$codigoSalida = 1
$repo = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$ubicacionInicial = Get-Location

function Nativo([string]$Programa, [string[]]$Argumentos) {
    $previo = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    try { & $Programa @Argumentos 2>&1 | ForEach-Object { Write-Host "    $_" }; $codigo = $LASTEXITCODE }
    finally { $ErrorActionPreference = $previo }
    if ($codigo -ne 0) { throw "$Programa terminó con código $codigo." }
}

try {
    foreach ($h in "dotnet", "npm", "git", "tar") {
        if (-not (Get-Command $h -ErrorAction SilentlyContinue)) { throw "No se encontró '$h' en el PATH." }
    }
    if (-not $Salida) {
        $documentos = [Environment]::GetFolderPath("MyDocuments"); if (-not $documentos) { $documentos = $HOME }
        $Salida = Join-Path $documentos ("ClimaPlus-despliegue\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    }
    if (Test-Path $Salida) { throw "La carpeta $Salida ya existe: elige otra." }
    New-Item -ItemType Directory -Force -Path $Salida | Out-Null
    Set-Location $repo

    # 1. Versión
    $commit = (git rev-parse --short HEAD).Trim()
    $sucio = @(git status --porcelain).Count
    Write-Host "1. Commit $commit; archivos con cambios sin confirmar: $sucio"
    if ($sucio -gt 0) { Write-Host "   AVISO: los artefactos incluirán cambios que todavía no están en GitHub." -ForegroundColor Yellow }

    # 2. API
    Write-Host "2. dotnet publish (Release)"
    $api = Join-Path $Salida "api"
    Nativo "dotnet" @("publish", "backend\ClimaPlus.Api\ClimaPlus.Api.csproj", "-c", "Release", "-o", $api, "/p:UseAppHost=false", "--nologo")

    # 3. Sin configuración local ni secretos en la salida
    $prohibidos = @(Get-ChildItem $api -Recurse -File | Where-Object {
        $_.Name -in @("appsettings.Development.json", "appsettings.Local.json", ".env") -or $_.Name -like "*.secrets.json" -or $_.Extension -in @(".pfx", ".pem", ".key")
    })
    if ($prohibidos.Count -gt 0) { throw ("La salida contiene archivos que no deben desplegarse: " + (($prohibidos | ForEach-Object { $_.Name }) -join ", ")) }
    foreach ($necesario in "ClimaPlus.Api.dll", "appsettings.json", "appsettings.Production.json") {
        if (-not (Test-Path (Join-Path $api $necesario))) { throw "Falta $necesario en la salida de dotnet publish." }
    }
    Write-Host "3. Salida de la API correcta: sin appsettings.Development.json ni secretos."

    # 4. Frontend
    Write-Host "4. npm run build con VITE_API_URL=https://$Dominio"
    $anterior = $env:VITE_API_URL
    $env:VITE_API_URL = "https://$Dominio"
    try {
        Set-Location (Join-Path $repo "frontend")
        if (-not (Test-Path "node_modules")) { Nativo "npm" @("ci") }
        Nativo "npm" @("run", "build")
    } finally {
        if ($null -eq $anterior) { Remove-Item Env:VITE_API_URL -ErrorAction SilentlyContinue } else { $env:VITE_API_URL = $anterior }
        Set-Location $repo
    }
    $indice = Get-Content -Raw (Join-Path $repo "frontend\dist\index.html")
    if ($indice -notmatch [regex]::Escape("https://$Dominio")) { throw "La CSP de dist/index.html no contiene https://$Dominio." }
    $frontend = Join-Path $Salida "frontend"
    Copy-Item (Join-Path $repo "frontend\dist") $frontend -Recurse
    Write-Host "   CSP con connect-src https://$Dominio."

    # 5. Paquetes
    Nativo "tar" @("-czf", (Join-Path $Salida "api.tar.gz"), "-C", $api, ".")
    Nativo "tar" @("-czf", (Join-Path $Salida "frontend.tar.gz"), "-C", $frontend, ".")
    $lineas = foreach ($p in "api.tar.gz", "frontend.tar.gz") { "$((Get-FileHash (Join-Path $Salida $p) -Algorithm SHA256).Hash.ToLowerInvariant())  $p" }
    [IO.File]::WriteAllLines((Join-Path $Salida "SHA256SUMS"), [string[]]$lineas, (New-Object System.Text.UTF8Encoding $false))
    [IO.File]::WriteAllText((Join-Path $Salida "version.txt"), "commit=$commit`ncambios_sin_confirmar=$sucio`ndominio=$Dominio`nfecha=$(Get-Date -Format s)`n", (New-Object System.Text.UTF8Encoding $false))
    Write-Host "5. Paquetes listos en $Salida (api.tar.gz, frontend.tar.gz, SHA256SUMS, version.txt)."
    Write-Host "RESULTADO: CORRECTO"
    $codigoSalida = 0
}
catch {
    Write-Host ("DETENIDO: " + $_.Exception.Message) -ForegroundColor Red
    $codigoSalida = 1
}
finally {
    Set-Location $ubicacionInicial
}
exit $codigoSalida
