# Correo: Mailpit en desarrollo y SMTP real para la demostración y producción

ClimaPlus envía por correo los códigos de verificación, de inicio de sesión, de recuperación y de vinculación con
Google, y los avisos de seguridad (contraseña cambiada, Google vinculado o desvinculado). El envío ya soporta SMTP
real: **solo cambia la configuración**, no el código ni los contratos de la API.

## 1. Cómo funciona (sin cambios en la Fase 7)

| Tema | Comportamiento |
|---|---|
| Librería | MailKit (`Correo/SmtpEmailSender.cs`); valida el certificado del servidor con las reglas por defecto |
| Cola | Los endpoints no esperan al SMTP: el correo va a una cola en memoria y se envía en segundo plano con 3 intentos (esperas de 2 y 10 s). La respuesta HTTP es igual exista o no la cuenta |
| TLS | `StartTls` (587) o `SslAlConectar` (465). `Ninguna` solo se acepta con `mailpit`, `localhost` o una IP de loopback; con cualquier otro servidor la API no arranca |
| Producción | Con `ASPNETCORE_ENVIRONMENT=Production` la API no arranca sin `Correo:Host` y `Correo:Remitente` |
| Códigos | HMAC (nunca en texto plano en la base), 5 intentos, vencimiento (10 min verificación, 5 min inicio de sesión), reenvío cada 60 s y máximo 5 correos por cuenta y hora |
| Logs | `Correo: enviado tipo=… destino=a***@dominio intento=n` o `Correo: envio_fallido … error=<tipo>`; nunca el código ni el contenido |

## 2. Elegir el proveedor

| Entorno | Proveedor | Por qué |
|---|---|---|
| Desarrollo y pruebas | **Mailpit** | No envía nada a Internet; ves los correos en `http://localhost:8025` |
| Demostración del curso | **Gmail con contraseña de aplicación**, en una cuenta creada solo para ClimaPlus | No requiere dominio propio; el correo sale autenticado como gmail.com. Gmail limita el volumen diario (suficiente para una demostración) |
| Producción | **Brevo** (300 correos/día en el plan gratuito) o **Resend** (3 000/mes gratis), **con dominio propio** verificado (SPF, DKIM y DMARC) | Proveedores transaccionales; sin dominio propio los correos suelen caer en spam |

Las cuotas de los planes gratuitos pueden cambiar: confírmalas en la página del proveedor antes de desplegar.

## 3. Configuración por entorno

### 3.1 Desarrollo local con Mailpit (sin cambios)

```powershell
docker run -d --name mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit:v1.33.1
cd backend\ClimaPlus.Api
dotnet user-secrets set "Correo:Host" "localhost"
dotnet user-secrets set "Correo:Puerto" "1025"
dotnet user-secrets set "Correo:Seguridad" "Ninguna"
dotnet user-secrets set "Correo:Remitente" "no-reply@climaplus.local"
```

Con Docker Compose, deja `SMTP_*` vacíos en `.env`: la API usa el contenedor `mailpit`.

### 3.2 Demostración con Gmail (`dotnet run`)

1. Crea una cuenta de Gmail para ClimaPlus (por ejemplo `climaplus.<tu-nombre>@gmail.com`).
2. Activa la **verificación en 2 pasos** en esa cuenta (Cuenta de Google → Seguridad).
3. Crea una **contraseña de aplicación** (Cuenta de Google → Seguridad → Contraseñas de aplicaciones). Son 16
   letras; cópiala sin espacios. **No es** la contraseña de la cuenta.
4. Guárdala en User Secrets (no queda en el repositorio). `Read-Host -AsSecureString` evita que se vea en pantalla
   y en el historial:

```powershell
cd backend\ClimaPlus.Api
dotnet user-secrets set "Correo:Host" "smtp.gmail.com"
dotnet user-secrets set "Correo:Puerto" "587"
dotnet user-secrets set "Correo:Seguridad" "StartTls"
dotnet user-secrets set "Correo:Usuario" "climaplus.ejemplo@gmail.com"
dotnet user-secrets set "Correo:Remitente" "climaplus.ejemplo@gmail.com"   # debe ser la misma cuenta
$s = Read-Host "Contraseña de aplicación" -AsSecureString
dotnet user-secrets set "Correo:Password" ([Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)))
Remove-Variable s
```

Para volver a Mailpit, repite la sección 3.1 y borra el usuario y la contraseña:
`dotnet user-secrets remove "Correo:Usuario"` y `dotnet user-secrets remove "Correo:Password"`.

### 3.3 Docker / servidor

En `.env` (nunca en Git):

```dotenv
SMTP_HOST=smtp.gmail.com          # o smtp-relay.brevo.com
SMTP_PORT=587
SMTP_SEGURIDAD=StartTls
SMTP_USER=climaplus.ejemplo@gmail.com
SMTP_PASSWORD=<contraseña de aplicación o clave SMTP del proveedor>
SMTP_FROM=climaplus.ejemplo@gmail.com
SMTP_FROM_NAME=ClimaPlus
```

Con `docker-compose.prod.yml`, `SMTP_HOST`, `SMTP_PORT`, `SMTP_FROM` y `SMTP_SEGURIDAD` son obligatorios:
`docker compose up` se detiene con un mensaje si falta alguno (antes se usaba Mailpit por defecto y los códigos no
llegaban). La API debe poder salir a Internet por el puerto 587 (o 465).

**Administrador inicial:** `ADMIN_CORREO` debe ser un correo real al que tengas acceso: su primer inicio de sesión
pide un código enviado a ese correo.

## 4. Pruebas

### 4.1 Automáticas

- Siempre (sin red): `CorreoOptionsTests`, `PlantillasCorreoTests`, `ColaCorreosTests` y las pruebas de los flujos
  con el enviador falso en memoria.
- Entrega real por SMTP contra Mailpit (`Correo/EntregaMailpitTests.cs`): se omite si no defines `MAILPIT_URL`.
  Necesita el Mailpit de la sección 3.1 (con el puerto 1025 publicado; el de `docker-compose.yml` solo publica 8025).

```powershell
$env:MAILPIT_URL = "http://localhost:8025"
dotnet test backend --filter "Categoria=Mailpit"
Remove-Item Env:MAILPIT_URL
```

### 4.2 Manuales con el proveedor real

| # | Prueba | Esperado |
|---|---|---|
| C-01 | Crear cuenta con un Gmail tuyo | El código llega en menos de 1 minuto, en la bandeja de entrada (no en spam), remitente "ClimaPlus" |
| C-02 | Lo mismo con un Outlook/Hotmail | Igual que C-01 |
| C-03 | Iniciar sesión | Llega "Tu código para iniciar sesión"; vence a los 5 minutos |
| C-04 | Reenviar código | Antes de 60 s el botón está deshabilitado; después llega un código nuevo y el anterior deja de servir |
| C-05 | Recuperar contraseña con un correo inexistente | La pantalla responde igual que con uno existente y no llega nada |
| C-06 | Cambiar la contraseña | Llega el aviso "Tu contraseña de ClimaPlus cambió" |
| C-07 | Contraseña de aplicación equivocada | La API sigue respondiendo; en su consola aparece `Correo: envio_fallido … error=AuthenticationException` tres veces; ningún secreto en el log |
| C-08 | Revisar la consola de la API | Nunca aparecen códigos, contraseñas, tokens ni la contraseña de aplicación |

## 5. Problemas comunes

| Síntoma | Causa | Solución |
|---|---|---|
| `AuthenticationException` | Contraseña de la cuenta en lugar de la de aplicación, o falta la verificación en 2 pasos | Crea la contraseña de aplicación (sección 3.2) |
| La API no arranca: "Correo:Seguridad=Ninguna solo se permite con Mailpit o localhost" | `SMTP_SEGURIDAD` vacío con un servidor real | `StartTls` con 587 o `SslAlConectar` con 465 |
| Los correos llegan a spam | Remitente distinto de la cuenta autenticada, o proveedor sin dominio verificado | En Gmail, `SMTP_FROM` = `SMTP_USER`; en producción, dominio con SPF, DKIM y DMARC |
| No llega nada y no hay errores en la consola | Los límites de envío de la cuenta (5 por hora por cuenta, 3 recuperaciones por hora) | Espera o revisa `Correo:` y `Seguridad:` en la consola |
| `envio_fallido … etapa=Conectar error=SmtpProtocolException detalle=SmtpProtocolException[desconexion_inesperada]` | El servidor (o algo intermedio) cortó la sesión antes de terminar EHLO/STARTTLS/TLS. Causas típicas: puerto y modo cruzados (465 con `StartTls`), antivirus con análisis de correo o proxy que intercepta el SMTP | Sección 6 |
| `etapa=Autenticar … credenciales_rechazadas` | Gmail rechazó usuario o contraseña de aplicación | Contraseña de aplicación nueva, sin espacios; `Correo:Usuario` = la cuenta que la generó |
| `etapa=Enviar … comando=SenderNotAccepted estado_smtp=…` | El servidor no acepta el remitente | `Correo:Remitente` = `Correo:Usuario` en Gmail |
| El registro muestra `aviso_registro_existente` | El correo ya tiene cuenta: por seguridad no se envía un código nuevo desde el registro | Sección 7 |

## 6. Diagnóstico de un fallo SMTP (Fase 7)

Cada fallo del envío queda en la consola de la API con la **etapa** y una descripción técnica segura (tipos de la
cadena de excepciones, códigos SMTP y de socket; nunca contraseñas, códigos, asuntos, cuerpos ni direcciones):

```
Correo: envio_fallido tipo=codigo_verificar_correo destino=a***@gmail.com intento=1 etapa=Conectar error=SmtpProtocolException detalle=SmtpProtocolException[desconexion_inesperada] <- IOException[hresult=0x80131620] <- SocketException[socket=ConnectionReset] reintenta=True
```

| Etapa | Llamada de MailKit | Qué indica |
|---|---|---|
| `Conectar` | `ConnectAsync` (TCP, saludo, EHLO, STARTTLS o TLS directo) | Red, puerto/modo TLS, certificado o interceptación |
| `Autenticar` | `AuthenticateAsync` | Usuario o contraseña de aplicación |
| `Enviar` | `SendAsync` (MAIL FROM, RCPT TO, DATA) | Remitente, destinatario o contenido rechazado |
| `Desconectar` | `DisconnectAsync` | El correo ya salió; fallo al cerrar (poco relevante) |

**Diagnóstico ampliado temporal** (`Correo:Diagnostico`, por defecto `false`): registra además la configuración no
secreta (servidor, puerto, modo, si el puerto y el modo forman la combinación habitual, si hay usuario/contraseña, si
la contraseña tiene espacios, si usuario = remitente), el TLS negociado, las capacidades y los mecanismos AUTH del
servidor, y el resultado de cada etapa. Se activa **solo en la ventana** donde corre la API:

```powershell
$env:Correo__Diagnostico = "true"
dotnet run --project backend\ClimaPlus.Api --launch-profile http
# ...reproducir y revisar la consola; al terminar, cerrar la ventana (la variable desaparece)
```

**Prueba sin la API** (`docs/herramientas/F7_probar_smtp.ps1`, PowerShell 5.1): conexión, TLS con validación normal
del certificado (muestra el **emisor**: en Gmail debe ser *Google Trust Services*; otro emisor = interceptación),
capacidades y, opcionalmente, solo el código de respuesta de la autenticación.

## 7. Registro con un correo que ya tiene cuenta

Si el correo ya existe (aunque no esté verificado), `POST /api/auth/register` responde igual que un registro nuevo
(202 con un desafío **señuelo**), pero solo envía al dueño el aviso `aviso_registro_existente` (máximo 1 por hora).
"Reenviar código" sobre ese señuelo tampoco envía nada. Es intencional:

- No revela qué correos están registrados (enumeración de cuentas).
- Evita la **apropiación anticipada**: si alguien registró antes ese correo con *su* contraseña, verificarlo desde el
  registro dejaría entrar al atacante.

Para verificar una cuenta existente sin verificar:

1. **Iniciar sesión** con su correo y contraseña: la API envía un código `codigo_verificar_correo` y, al escribirlo,
   la cuenta queda verificada e inicia sesión.
2. Si no recuerdas la contraseña: **¿Olvidaste tu contraseña?** → el código de recuperación prueba que el buzón es
   tuyo; al guardar la contraseña nueva la cuenta queda verificada.
