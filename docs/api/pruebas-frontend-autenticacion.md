# Pruebas en el navegador de la autenticación (fase 6)

Guía para probar a mano, desde el frontend de React, todo lo que se integró en la fase 6: registro con verificación,
inicio de sesión en dos pasos, recuperación, cambio y creación de contraseña, y "Continuar con Google" (entrar,
vincular y desvincular). La versión con Postman está en
[`pruebas-autenticacion-postman-mailpit.md`](pruebas-autenticacion-postman-mailpit.md) y la configuración de Google en
[`../despliegue/google-oauth.md`](../despliegue/google-oauth.md).

> **Base de datos.** La API de esta rama necesita la migración 004. No la apliques a `climaplus_db` (tu base
> principal de Windows) ni al volumen de Docker existente sin respaldo y sin decidirlo. La sección 1 explica cómo
> comprobar, **solo leyendo**, si una base ya la tiene.

## 1. ¿La base que usa la API tiene la migración 004?

Si no la tiene, cualquier inicio de sesión (con contraseña o con Google) responde **500**: faltan las tablas
`desafio_autenticacion` e `identidad_externa` y las columnas nuevas de `usuario`. Estas consultas solo leen:

```powershell
# Base de Windows (ajusta usuario y nombre de la base que use tu API en User Secrets)
psql -U postgres -d climaplus_db -c "SELECT to_regclass('public.desafio_autenticacion') AS desafios, to_regclass('public.identidad_externa') AS google;"

# Base del contenedor de Docker
docker compose exec -T db psql -U climaplus -d climaplus_db -c "SELECT to_regclass('public.desafio_autenticacion') AS desafios, to_regclass('public.identidad_externa') AS google;"
```

Dos columnas vacías (`NULL`) = falta la 004. El procedimiento para aplicarla, con respaldo previo, está en
[`../despliegue/notas-version-autenticacion.md`](../despliegue/notas-version-autenticacion.md) ("Cómo aplicarla" y
"Docker con un volumen que ya existía"); la versión paso a paso, ensayada, está en
[`../despliegue/migracion-004-climaplus_db.md`](../despliegue/migracion-004-climaplus_db.md). **Queda pendiente de tu autorización.**
Desde la Fase 7, `GET /health` responde `Unhealthy` y la consola de la API avisa `faltan_migraciones` en ese caso.

Alternativa que no toca nada existente (también requiere tu decisión, porque crea un volumen nuevo): levantar Docker
con otro nombre de proyecto. El volumen nuevo empieza vacío y PostgreSQL ejecuta 001 a 004 solo:

```powershell
docker compose stop                     # detiene el proyecto actual SIN borrar su volumen (no uses "down -v")
docker compose -p climaplus-fase6 up -d --build
# ...pruebas...
docker compose -p climaplus-fase6 stop
```

## 2. Arrancar

### Desarrollo (Vite 5173 + API 5208)

```powershell
# Terminal 1: Mailpit (bandeja en http://localhost:8025)
docker run -d --name mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit:v1.33.1

# Terminal 2: API (Correo y Codigos:ClaveHmac ya configurados en User Secrets; Google opcional)
cd backend\ClimaPlus.Api
dotnet user-secrets list | Select-String "Google:ClientId|Codigos:ClaveHmac|Correo:Host" | ForEach-Object { ($_ -split "=")[0] }   # solo nombres, sin valores
dotnet run --launch-profile http

# Terminal 3: frontend
cd frontend
npm ci
npm run dev        # http://localhost:5173
```

`VITE_API_URL` por defecto es `http://localhost:5208`; CORS de la API ya permite `http://localhost:5173`.

### Docker (frontend 3000 + API 8080)

En `.env`: `FRONTEND_URL=http://localhost:3000`, `API_PUBLIC_URL=http://localhost:8080`, `CODIGOS_CLAVE_HMAC` y, para
Google, `GOOGLE_CLIENT_ID`. Luego `docker compose up -d --build`. Para Google, `http://localhost:3000` debe estar en
"Orígenes autorizados de JavaScript" del cliente OAuth.

## 3. Casos a probar en el navegador

| # | Pantalla | Pasos | Resultado esperado |
|---|---|---|---|
| 1 | Crear cuenta | Llenar el formulario → **Crear cuenta** | Pantalla "Verifica tu correo"; en Mailpit llega el código |
| 2 | Crear cuenta | Escribir el código → **Verificar correo** | Vuelve a Iniciar sesión con "Correo verificado. Ya puedes iniciar sesión." Sin sesión abierta |
| 3 | Crear cuenta | Código equivocado | Mensaje del campo; se puede reintentar. **Reenviar código** se habilita a los 60 s |
| 4 | Iniciar sesión | Correo + contraseña | Pantalla "Verifica que eres tú" (aún sin sesión); llega el código |
| 5 | Iniciar sesión | Código correcto | Entra al Dashboard (o a Administración si es ADMINISTRADOR) |
| 6 | Iniciar sesión | Contraseña mala 5 veces | 429 "Demasiados intentos…"; el botón queda deshabilitado |
| 7 | Iniciar sesión | Cuenta sin verificar | Pide el código de verificación de correo y entra al validarlo |
| 8 | ¿Olvidaste tu contraseña? | Correo → código → contraseña nueva | Vuelve a Iniciar sesión con aviso; la contraseña vieja ya no sirve; las sesiones abiertas se cierran |
| 9 | Recuperar | Esperar más de 10 min entre el código y la contraseña nueva | "Solicita un código nuevo" y vuelve al primer paso |
| 10 | Mi perfil | **Cambiar contraseña** → actual, nueva y confirmación → **Continuar** → código del correo (5 minutos) → **Confirmar cambio** | Antes del código no cambia nada; con un código incorrecto muestra el error y la sesión sigue; con el correcto cierra la sesión (el backend revoca todos los tokens) y solo entra la contraseña nueva |
| 11 | Iniciar sesión | **Continuar con Google** con un Gmail sin cuenta | Entra directo (sin código); en el perfil: "Sin contraseña (entras con Google)" |
| 12 | Iniciar sesión | Google con un Gmail que ya tiene cuenta local | Mensaje 409: inicia sesión con contraseña y vincula desde el perfil |
| 13 | Mi perfil (cuenta de Google) | **Establecer contraseña** → **Enviarme el código** → código → contraseña | Cierra la sesión; después entra con contraseña (dos pasos) o con Google |
| 14 | Mi perfil (cuenta local) | **Vincular Google** → contraseña actual → botón de Google → código | "Cuenta de Google vinculada…"; la sesión sigue con un JWT nuevo; las otras sesiones se cierran |
| 15 | Mi perfil | **Desvincular** → contraseña actual | "Cuenta de Google desvinculada…" |
| 16 | Mi perfil (solo Google) | — | **Vincular/Desvincular** deshabilitados hasta establecer contraseña |
| 17 | Iniciar sesión | API sin `Google:ClientId` | El botón de Google no aparece; el login normal funciona |

En las herramientas del navegador (F12 → Consola) no debe aparecer ninguna violación de CSP, y en Aplicación →
Almacenamiento de sesión solo `climaplus_sesion` con token y vencimiento (nunca contraseñas, códigos ni el ID token
de Google), y solo si se marcó **Recordarme**.

## 4. Diagnóstico de los 401 y 500 de Google

Lo que se encontró al revisar el código y la página `docs/herramientas/prueba-google.html`:

| Síntoma | Causa más probable | Qué hacer |
|---|---|---|
| **500** al pulsar el botón | La base de la API no tiene la migración 004 (sección 1). El nonce se consume antes de guardar, así que el reintento da 401 | Comprobar con la consulta de la sección 1; aplicar 004 solo con tu autorización |
| **401** en el segundo intento (`motivo=nonce`) | Se pulsó otra vez el botón con el **mismo nonce** (la página de prueba anterior no pedía uno nuevo). Es el comportamiento correcto: el nonce es de un solo uso | Corregido: la página y el frontend piden un nonce nuevo después de cada intento |
| **401** tras dejar la página abierta | El nonce venció (10 min) | El frontend lo renueva 30 s antes de vencer |
| **401** siempre | Client ID distinto entre Google Cloud y `Google:ClientId`, o la API no puede descargar las claves de Google (`motivo=claves_no_disponibles`) | Revisar el motivo en la consola de la API (abajo) |
| Error de Google "origin is not allowed" | Falta `http://localhost:5173` (o `:3000`) y `http://localhost` en los orígenes | Agregarlos en Google Cloud y esperar unos minutos |
| El botón no aparece y la consola muestra CSP | CSP o Referrer-Policy antiguos (`no-referrer`, COOP `same-origin`) | Corregido en la fase 6 (`vite.config.ts`, `index.html`, Nginx) |

**Obtener los registros en Windows** (no contienen tokens, códigos ni contraseñas):

```powershell
cd backend\ClimaPlus.Api
dotnet run --launch-profile http 2>&1 | Tee-Object -FilePath "$env:TEMP\api-google.log"
# Reproduce el error en el navegador, detén la API con Ctrl+C y luego:
Select-String -Path "$env:TEMP\api-google.log" -Pattern "google_","Exception","fail:" -Context 0,8
```

En Docker: `docker compose logs api --since 15m | Select-String "google_|Exception|fail:" -Context 0,8`.

El motivo aparece como `Seguridad: google_token_rechazado motivo=<motivo>`: `firma`, `audiencia` (Client ID distinto),
`emisor`, `vencido`, `algoritmo`, `invalido`, `nonce` (usado, vencido o de otro uso) o `claves_no_disponibles`. Los
rechazos de negocio salen como `google_login_rechazado motivo=correo_no_verificado | correo_de_cuenta_local | …`.
Para un 500, la línea `fail:` trae la excepción (por ejemplo `42P01: relation "desafio_autenticacion" does not exist`
= falta la 004).
Antes de compartir el archivo, revisa que no tenga datos personales que no quieras enviar.
