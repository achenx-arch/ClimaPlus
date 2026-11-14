# "Continuar con Google": configuración en Google Cloud y en ClimaPlus

Guía de la fase 5 de la autenticación segura. El contrato de los endpoints está en
[`../api/contrato-auth-perfil.md`](../api/contrato-auth-perfil.md) (sección 4.9).

## 1. Flujo elegido y por qué

**Google Identity Services (GIS) + validación del ID token en la API**, con un nonce de un solo uso emitido por la API.

1. El navegador pide `GET /api/auth/google/iniciar` → `{ clientId, nonce, expiraEn }`.
2. Muestra el botón oficial de Google (`google.accounts.id.initialize({ client_id, nonce, callback })`, modo popup).
3. Google autentica a la persona y entrega al navegador un **ID token** (JWT firmado por Google) que incluye el nonce.
4. El navegador lo envía en el cuerpo de `POST /api/auth/google` (`{ "credencial": "..." }`).
5. La API lo valida con Microsoft.IdentityModel (firma RS256 con las claves públicas del documento de descubrimiento
   de Google, emisor `accounts.google.com`, audiencia = nuestro Client ID, `azp`, vencimiento, `sub` y el nonce, que
   se borra al usarlo) y **en esa misma respuesta** entrega el JWT de ClimaPlus.

Por qué este flujo y no Authorization Code + PKCE con callback en la API:

| Tema | GIS + ID token (elegido) | Code + PKCE con callback |
|---|---|---|
| Frontend React en otro origen (3000/5173 → 8080/5208) | El token viaja por `fetch` en el cuerpo, como el resto de la API | El callback llega a la API; para entregar el JWT al frontend sin ponerlo en la URL hacen falta cookies entre orígenes y CORS con credenciales |
| Secretos | Ninguno: solo el Client ID (público) | Client Secret en el servidor |
| Lo que ClimaPlus necesita | Identidad (sub, correo verificado) | Acceso a APIs de Google en nombre del usuario (no se usa) |
| CSRF / replay | Nonce de la API, un solo uso, 10 min; el token va en un POST JSON (no en un formulario ni en la URL) | `state` + PKCE |

Google es la prueba de identidad: **no se pide además el código por correo** al entrar con Google (decisión del
10-nov-2026, ver notas de versión). El código sí se exige para **vincular** Google a una cuenta local.

## 2. Configurar Google Cloud (una sola vez)

> Los nombres de los menús son los de la consola actual (Google Auth Platform). Si Google los cambia, busca
> "Google Auth Platform" en la barra de búsqueda de la consola.

1. **Proyecto.** Entra a <https://console.cloud.google.com/> con tu cuenta, abre el selector de proyectos (arriba a
   la izquierda) y elige **Nuevo proyecto** → nombre `ClimaPlus` → **Crear**. Comprueba que quede seleccionado.
2. **Google Auth Platform → Branding (marca / pantalla de consentimiento).** Si te pide empezar, pulsa
   **Comenzar** y completa:
   - Nombre de la aplicación: `ClimaPlus`.
   - Correo de asistencia del usuario: tu correo.
   - Público (Audience): **Externo**.
   - Información de contacto del desarrollador: tu correo.
   - Acepta la política de datos de usuario y pulsa **Crear**.
3. **Data Access (acceso a datos).** No agregues permisos sensibles. ClimaPlus solo usa `openid`, `email` y
   `profile`, que no requieren verificación de Google.
4. **Audience → Usuarios de prueba.** Mientras la app esté en estado **Prueba** (Testing), solo entran las cuentas
   de esta lista: pulsa **Add users** y agrega los Gmail con los que vas a probar (máximo 100).
5. **Clients → Crear cliente.**
   - Tipo de aplicación: **Aplicación web**. Nombre: `ClimaPlus web (desarrollo)`.
   - **Orígenes autorizados de JavaScript** (solo esquema + host + puerto, sin rutas). Para desarrollo con los puertos
     reales del repositorio:

     | Origen | Para qué |
     |---|---|
     | `http://localhost` | Google lo pide además del puerto cuando se prueba en localhost |
     | `http://localhost:5173` | Frontend con Vite (`npm run dev`) y la página de prueba `docs/herramientas/prueba-google.html` |
     | `http://localhost:3000` | Frontend en Docker (`docker compose up`) |

   - **URI de redireccionamiento autorizados: déjalo vacío.** El flujo elegido usa el popup de GIS, no redirige a la
     API. (Solo haría falta con `ux_mode: "redirect"` o con el flujo de código.)
   - Pulsa **Crear**. Copia el **ID de cliente** (`…apps.googleusercontent.com`).
   - Google también muestra un **secreto del cliente**: **no lo necesitas, no lo copies a ClimaPlus** ni lo pegues en
     ningún archivo, chat o captura.

Los cambios de orígenes pueden tardar unos minutos en aplicarse.

## 3. Configurar ClimaPlus

### `dotnet run` (API en `http://localhost:5208`)

```powershell
cd backend\ClimaPlus.Api
dotnet user-secrets set "Google:ClientId" "<tu-client-id>.apps.googleusercontent.com"
dotnet run --launch-profile http
```

Sin `Google:ClientId` la API arranca igual y `GET /api/auth/google/iniciar` responde **503** (botón desactivado).
La API valida al arrancar que el valor termine en `.apps.googleusercontent.com`.

### Docker (API en `http://localhost:8080`, frontend en `http://localhost:3000`)

En tu archivo `.env` (no se sube a Git; `.env.example` solo tiene la línea vacía):

```dotenv
GOOGLE_CLIENT_ID=<tu-client-id>.apps.googleusercontent.com
```

`docker-compose.yml` lo pasa a la API como `Google__ClientId`. Después: `docker compose up -d --build`.

La API necesita salir por HTTPS a `accounts.google.com` y `www.googleapis.com` para descargar las claves públicas de
Google (se guardan en caché y se renuevan solas). Sin esa salida, todo inicio con Google responde 401.

## 4. Probar con una cuenta de Google real

Desde la fase 6 el botón oficial está en el frontend: **Iniciar sesión → Continuar con Google**, y en **Mi perfil →
Google → Vincular / Desvincular**. Los casos y el diagnóstico de errores están en
[`../api/pruebas-frontend-autenticacion.md`](../api/pruebas-frontend-autenticacion.md).

1. Arranca la API (`dotnet run --launch-profile http`, Google configurado, Mailpit levantado) y el frontend
   (`npm run dev` en `frontend`, `http://localhost:5173`).
2. Resultado esperado:
   - Cuenta nueva: entra directo (sin código) y el perfil dice "Sin contraseña (entras con Google)".
   - Con el mismo Google otra vez: entra a la misma cuenta.
   - Si ese Gmail ya tiene cuenta local en ClimaPlus: mensaje 409 (no se vincula solo por el correo).
3. Para **vincular** una cuenta local: entra con correo, contraseña y código, ve a **Mi perfil → Vincular Google**,
   escribe la contraseña actual, elige la cuenta de Google y escribe el código que llega a Mailpit.

Para aislar una falla de la API sin el frontend queda la página `docs/herramientas/prueba-google.html`
(`python -m http.server 5173` en `docs\herramientas` con el frontend detenido, y abrir
`http://localhost:5173/prueba-google.html`). Pide un nonce nuevo después de cada intento.

Si Google muestra "The given origin is not allowed for the given client ID", falta el origen exacto en el paso 2.5.

## 5. Paso a producción

- **Orígenes:** crea un cliente aparte (o agrega orígenes) con el dominio real en **HTTPS**, por ejemplo
  `https://climaplus.ejemplo.com`. Google no acepta orígenes HTTP salvo `localhost`.
- **Publicar la app:** en Google Auth Platform → Audience, pasa de **Prueba** a **En producción**. Con solo
  `openid email profile` normalmente no hace falta la verificación de Google, pero sí completar la marca (logo,
  dominio autorizado, enlaces de privacidad y condiciones si Google los pide).
- **Configuración:** `GOOGLE_CLIENT_ID` como secreto del servidor o variable del contenedor, nunca en Git.
- **Frontend (hecho en la fase 6):** la CSP (meta generada por Vite) permite `https://accounts.google.com/gsi/` en
  `script-src` (solo `/gsi/client`), `style-src` (`/gsi/style`), `frame-src` y `connect-src`; Nginx envía
  `Cross-Origin-Opener-Policy: same-origin-allow-popups` y `Referrer-Policy: strict-origin-when-cross-origin`
  (detalle en `../seguridad/owasp-top10.md`, A05). Si cambias el dominio, no hace falta tocar la CSP.
- **Salida a Internet:** la API debe poder llegar a `accounts.google.com` y `www.googleapis.com` (HTTPS).
- **Proxy:** como en las fases anteriores, configura `ProxyInverso` para que los bloqueos por IP vean la IP real.
