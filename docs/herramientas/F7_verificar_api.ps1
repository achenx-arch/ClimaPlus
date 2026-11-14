#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Comprueba que la API (y opcionalmente el frontend) están ejecutándose y responden como se espera
  después de la migración 005. Solo hace peticiones de lectura o sin sesión: no inicia sesión ni cambia datos.

.DESCRIPTION
  1. GET /health                         -> 200 "Healthy" (incluye la base de datos y que no falten migraciones).
  2. GET /api/auth/me sin token          -> 401 (la autenticación está activa).
  3. POST /api/usuarios/me/password/cambio sin token          -> 401 (endpoint nuevo, protegido).
  4. POST /api/usuarios/me/password/cambio/verificar sin token -> 401 (endpoint nuevo, protegido).
  5. PUT  /api/usuarios/me/password sin token                 -> 404/405 (el cambio sin código ya no existe).
  6. Opcional (-UrlFrontend): la página principal responde 200 y es la aplicación de ClimaPlus.
  Código de salida: 0 = todo correcto; 1 = alguna comprobación falló.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_verificar_api.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_verificar_api.ps1 -UrlApi http://localhost:5208 -UrlFrontend http://localhost:5173
#>
[CmdletBinding()]
param(
    [string]$UrlApi = "http://localhost:5208",
    [string]$UrlFrontend,
    [int]$TimeoutSegundos = 15
)

$ErrorActionPreference = "Stop"
$UrlApi = $UrlApi.TrimEnd("/")
$fallas = 0

function Peticion([string]$Metodo, [string]$Url, [string]$Cuerpo) {
    # Invoke-WebRequest lanza excepción con 4xx/5xx en PowerShell 5.1: se recupera el código igualmente.
    try {
        $p = @{ Uri = $Url; Method = $Metodo; UseBasicParsing = $true; TimeoutSec = $TimeoutSegundos }
        if ($Cuerpo) { $p.Body = $Cuerpo; $p.ContentType = "application/json" }
        $r = Invoke-WebRequest @p
        $texto = $r.Content
        if ($texto -is [byte[]]) { $texto = [Text.Encoding]::UTF8.GetString($texto) }   # respuesta sin Content-Type de texto
        return [pscustomobject]@{ Codigo = [int]$r.StatusCode; Texto = [string]$texto }
    } catch {
        $respuesta = $_.Exception.Response
        if ($respuesta) { return [pscustomobject]@{ Codigo = [int]$respuesta.StatusCode; Texto = "" } }
        return [pscustomobject]@{ Codigo = 0; Texto = $_.Exception.Message }
    }
}
function Comprobar([string]$Descripcion, [bool]$Correcto, [string]$Detalle) {
    if ($Correcto) { Write-Host "OK    $Descripcion ($Detalle)" }
    else { Write-Host "FALLO $Descripcion ($Detalle)" -ForegroundColor Red; $script:fallas++ }
}

Write-Host "== ClimaPlus: verificación de servicios ($(Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))"
Write-Host "API: $UrlApi"

$salud = Peticion "GET" "$UrlApi/health" ""
Comprobar "GET /health" ($salud.Codigo -eq 200 -and $salud.Texto.Trim() -eq "Healthy") "HTTP $($salud.Codigo) $($salud.Texto.Trim())"
if ($salud.Codigo -eq 0) { Write-Host "      La API no responde en $UrlApi. ¿Está iniciada (dotnet run) y es la URL correcta?" -ForegroundColor Yellow }
if ($salud.Codigo -eq 503) { Write-Host "      La API responde pero la base de datos no está lista (conexión o migraciones pendientes). Revisa el log de inicio: 'Base de datos: ...'." -ForegroundColor Yellow }

$me = Peticion "GET" "$UrlApi/api/auth/me" ""
Comprobar "GET /api/auth/me sin sesión" ($me.Codigo -eq 401) "HTTP $($me.Codigo), esperado 401"

$cuerpo = '{"passwordActual":"x","passwordNueva":"y","confirmacion":"y"}'
$paso1 = Peticion "POST" "$UrlApi/api/usuarios/me/password/cambio" $cuerpo
Comprobar "POST /api/usuarios/me/password/cambio sin sesión" ($paso1.Codigo -eq 401) "HTTP $($paso1.Codigo), esperado 401"

$paso2 = Peticion "POST" "$UrlApi/api/usuarios/me/password/cambio/verificar" '{"desafio":"x","codigo":"000000","passwordActual":"x","passwordNueva":"y","confirmacion":"y"}'
Comprobar "POST /api/usuarios/me/password/cambio/verificar sin sesión" ($paso2.Codigo -eq 401) "HTTP $($paso2.Codigo), esperado 401"

$antiguo = Peticion "PUT" "$UrlApi/api/usuarios/me/password" '{"passwordActual":"x","passwordNueva":"y"}'
Comprobar "PUT /api/usuarios/me/password (cambio sin código) ya no existe" ($antiguo.Codigo -eq 404 -or $antiguo.Codigo -eq 405) "HTTP $($antiguo.Codigo), esperado 404 o 405"

if ($UrlFrontend) {
    $UrlFrontend = $UrlFrontend.TrimEnd("/")
    $web = Peticion "GET" "$UrlFrontend/" ""
    Comprobar "Frontend $UrlFrontend" ($web.Codigo -eq 200 -and $web.Texto -match 'id="root"') "HTTP $($web.Codigo)"
}

if ($fallas -eq 0) { Write-Host "RESULTADO: servicios correctos." ; exit 0 }
Write-Host "RESULTADO: $fallas comprobación(es) fallaron." -ForegroundColor Red
exit 1
