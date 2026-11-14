#Requires -Version 5.1
<#
.SYNOPSIS
  ClimaPlus - Prueba de diagnóstico SMTP desde Windows PowerShell 5.1, sin la API: conexión, TLS (con validación
  normal del certificado), capacidades del servidor y, opcionalmente, autenticación.

.DESCRIPTION
  No envía ningún correo. Muestra:
    - Dirección usada (IPv4/IPv6) y respuesta inicial del servidor.
    - TLS negociado: protocolo, cifrado, certificado (sujeto, EMISOR, vigencia) y errores de validación.
      Si el emisor NO es la autoridad del proveedor (para Gmail: Google Trust Services), algo intercepta el tráfico
      (antivirus con análisis de correo, proxy o firewall).
    - Capacidades del servidor después de TLS (mecanismos AUTH, tamaño máximo).
    - Con -ProbarAutenticacion: solo el código de respuesta de AUTH (235 = correcto; 535 = usuario/contraseña de
      aplicación rechazados; 534 = la cuenta exige contraseña de aplicación).
  No muestra la contraseña, la IP de tu equipo ni el texto completo de las respuestas de autenticación.
  El certificado se valida con las reglas normales de Windows: si no es válido, la prueba se detiene (no lo acepta).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_probar_smtp.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_probar_smtp.ps1 -Puerto 465
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_probar_smtp.ps1 -ProbarAutenticacion -Usuario cuenta.envio@gmail.com
#>
[CmdletBinding()]
param(
    [string]$Servidor = "smtp.gmail.com",
    [int]$Puerto = 587,
    [ValidateSet("Auto", "StartTls", "SslAlConectar")]
    [string]$Modo = "Auto",
    [switch]$ProbarAutenticacion,
    [string]$Usuario,
    [int]$TimeoutSegundos = 15
)

$ErrorActionPreference = "Stop"
if ($Modo -eq "Auto") { if ($Puerto -eq 465) { $Modo = "SslAlConectar" } else { $Modo = "StartTls" } }
if ($ProbarAutenticacion -and -not $Usuario) { throw "Con -ProbarAutenticacion indica -Usuario (la cuenta de envío)." }

$script:codigoSalida = 1
$cliente = $null; $ssl = $null

function Ocultar([string]$linea) {
    # Quita direcciones IP (Gmail incluye la IP del cliente en su saludo) y direcciones de correo.
    $linea = $linea -replace '\[[0-9a-fA-F:.]+\]', '[IP]' -replace '\b\d{1,3}(\.\d{1,3}){3}\b', 'IP'
    $linea -replace '[\w.+-]+@[\w-]+(\.[\w-]+)+', '<correo>'
}
function LeerRespuesta($lector) {
    $lineas = @()
    do {
        $l = $lector.ReadLine()
        if ($null -eq $l) { throw "El servidor cerró la conexión sin responder (esto es lo que MailKit informa como SmtpProtocolException)." }
        $lineas += $l
    } while ($l.Length -ge 4 -and $l[3] -eq '-')
    $lineas
}
function Comando($escritor, $lector, [string]$texto, [string]$mostrar) {
    if (-not $mostrar) { $mostrar = $texto }
    Write-Host "  C: $mostrar"
    $escritor.Write($texto + "`r`n"); $escritor.Flush()
    $r = @(LeerRespuesta $lector)
    foreach ($l in $r) { Write-Host ("  S: " + (Ocultar $l)) }
    $r
}

try {
    Write-Host "== Prueba SMTP: ${Servidor}:${Puerto} modo=$Modo (PowerShell $($PSVersionTable.PSVersion))"

    # ---------------------------------------------------------------- Conexión TCP
    $cliente = New-Object System.Net.Sockets.TcpClient
    $cliente.ReceiveTimeout = $TimeoutSegundos * 1000
    $cliente.SendTimeout = $TimeoutSegundos * 1000
    $cliente.Connect($Servidor, $Puerto)
    $familia = $cliente.Client.RemoteEndPoint.AddressFamily
    Write-Host "1. TCP conectado (familia de direcciones: $familia)"
    $red = $cliente.GetStream()

    $script:errores = [System.Net.Security.SslPolicyErrors]::None
    $script:emisor = $null
    $validar = [System.Net.Security.RemoteCertificateValidationCallback] {
        param($remitente, $certificado, $cadena, $erroresPolitica)
        $script:errores = $erroresPolitica
        if ($certificado) { $script:emisor = $certificado.Issuer }
        return ($erroresPolitica -eq [System.Net.Security.SslPolicyErrors]::None)   # validación normal: no acepta certificados inválidos
    }
    $protocolos = [System.Security.Authentication.SslProtocols]::Tls12
    try { $protocolos = $protocolos -bor [System.Security.Authentication.SslProtocols]'Tls13' } catch { }

    function IniciarTls($flujo) {
        $s = New-Object System.Net.Security.SslStream($flujo, $false, $validar)
        try { $s.AuthenticateAsClient($Servidor, $null, $protocolos, $true) }
        catch {
            $detalle = "TLS falló: " + $_.Exception.GetType().Name
            if ($script:emisor) { $detalle += " | emisor del certificado: $script:emisor" }
            $detalle += " | errores de validación: $script:errores"
            throw $detalle
        }
        $c = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $s.RemoteCertificate
        Write-Host "2. TLS correcto: $($s.SslProtocol), cifrado $($s.CipherAlgorithm) $($s.CipherStrength) bits"
        Write-Host "   Certificado: $($c.Subject)"
        Write-Host "   Emisor:      $($c.Issuer)"
        Write-Host "   Vigencia:    $($c.NotBefore.ToString('yyyy-MM-dd')) a $($c.NotAfter.ToString('yyyy-MM-dd'))"
        if ($Servidor -like "*gmail.com" -and $c.Issuer -notmatch "Google Trust Services") {
            Write-Host "   AVISO: el emisor no es Google Trust Services. Un antivirus, proxy o firewall está interceptando el SMTP." -ForegroundColor Yellow
        }
        $s
    }

    $codificacion = New-Object System.Text.ASCIIEncoding
    if ($Modo -eq "SslAlConectar") {
        $ssl = IniciarTls $red
        $lector = New-Object System.IO.StreamReader($ssl, $codificacion)
        $escritor = New-Object System.IO.StreamWriter($ssl, $codificacion)
        $saludo = @(LeerRespuesta $lector)
        foreach ($l in $saludo) { Write-Host ("  S: " + (Ocultar $l)) }
        $ehlo = @(Comando $escritor $lector "EHLO climaplus.local")
    } else {
        $lector = New-Object System.IO.StreamReader($red, $codificacion)
        $escritor = New-Object System.IO.StreamWriter($red, $codificacion)
        $saludo = @(LeerRespuesta $lector)
        foreach ($l in $saludo) { Write-Host ("  S: " + (Ocultar $l)) }
        $ehlo = @(Comando $escritor $lector "EHLO climaplus.local")
        if (-not ($ehlo | Where-Object { $_ -match '^250[- ]STARTTLS' })) { throw "El servidor no ofrece STARTTLS en el puerto $Puerto." }
        $r = @(Comando $escritor $lector "STARTTLS")
        if ($r[-1] -notmatch '^220') { throw "STARTTLS rechazado: $(Ocultar $r[-1])" }
        $ssl = IniciarTls $red
        $lector = New-Object System.IO.StreamReader($ssl, $codificacion)
        $escritor = New-Object System.IO.StreamWriter($ssl, $codificacion)
        $ehlo = @(Comando $escritor $lector "EHLO climaplus.local")
    }
    $auth = ($ehlo | Where-Object { $_ -match '^250[- ]AUTH ' }) -replace '^250[- ]AUTH ', ''
    Write-Host "3. Capacidades tras TLS correctas. Mecanismos AUTH: $auth"

    # ---------------------------------------------------------------- Autenticación (opcional)
    if ($ProbarAutenticacion) {
        $seguro = Read-Host "Contraseña de aplicación de $Usuario (no se muestra ni se guarda)" -AsSecureString
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
        try {
            $clave = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
            if ($clave -match '\s') { Write-Host "   AVISO: la contraseña tiene espacios. Las contraseñas de aplicación de Google se usan sin espacios." -ForegroundColor Yellow }
            $credencial = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("`0$Usuario`0$clave"))
        } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr); $clave = $null }
        Write-Host "  C: AUTH PLAIN ********"
        $escritor.Write("AUTH PLAIN $credencial`r`n"); $escritor.Flush(); $credencial = $null
        $r = @(LeerRespuesta $lector)
        $codigo = ($r[-1] -split ' ')[0..1] -join ' '
        Write-Host "  S: $codigo (texto omitido)"
        switch -Regex ($r[-1]) {
            '^235' { Write-Host "4. Autenticación CORRECTA." }
            '^534' { Write-Host "4. Autenticación RECHAZADA (534): la cuenta exige una contraseña de aplicación y verificación en 2 pasos." }
            '^535' { Write-Host "4. Autenticación RECHAZADA (535): usuario o contraseña de aplicación incorrectos (o la contraseña de aplicación fue revocada)." }
            default { Write-Host "4. Respuesta de autenticación inesperada: $codigo" }
        }
        if ($r[-1] -notmatch '^235') { throw "Autenticación no superada." }
    }

    Comando $escritor $lector "QUIT" | Out-Null
    Write-Host "RESULTADO: correcto."
    $script:codigoSalida = 0
}
catch {
    Write-Host ("DETENIDO: " + $_.Exception.Message) -ForegroundColor Red
    $script:codigoSalida = 1
}
finally {
    if ($ssl) { $ssl.Dispose() }
    if ($cliente) { $cliente.Close() }
}
exit $script:codigoSalida
