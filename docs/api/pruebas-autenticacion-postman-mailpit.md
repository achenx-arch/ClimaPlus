# Pruebas manuales de la autenticación segura con Postman y Mailpit

Guía para probar a mano el registro con verificación, el inicio de sesión en dos pasos, la recuperación de
contraseña, el cambio desde el perfil (fases 2 a 4) y Google (fase 5). El contrato completo está en
[`contrato-auth-perfil.md`](contrato-auth-perfil.md). Las pruebas automáticas (`dotnet test`) no necesitan nada de
esto: usan un buzón falso en memoria.

> Usa solo una base de pruebas o tu base local de desarrollo. **No apliques la migración 004 a `climaplus_db`**
> mientras no decidas desplegar esta versión.

## 1. Preparar Mailpit y la API

```powershell
# Mailpit: servidor SMTP de pruebas (puerto 1025) con bandeja web (http://localhost:8025)
docker run -d --name mailpit -p 127.0.0.1:1025:1025 -p 127.0.0.1:8025:8025 axllent/mailpit:v1.33.1

cd backend\ClimaPlus.Api
dotnet user-secrets set "Correo:Host" "localhost"
dotnet user-secrets set "Correo:Puerto" "1025"
dotnet user-secrets set "Correo:Seguridad" "Ninguna"
dotnet user-secrets set "Correo:Remitente" "no-reply@climaplus.local"

# Si aún no existe: clave HMAC de los códigos, distinta de Jwt:Clave
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
dotnet user-secrets set "Codigos:ClaveHmac" ([Convert]::ToBase64String($bytes))

dotnet run --launch-profile http
```

Con Docker Compose (`docker compose up -d`) Mailpit ya viene incluido y la bandeja está en `http://localhost:8025`.

En Postman, crea un entorno con `base = http://localhost:5208` y las variables vacías `desafio`, `permiso` y
`token`. Todas las peticiones llevan `Content-Type: application/json`.

## 2. Registro y verificación del correo

1. `POST {{base}}/api/auth/register`
   ```json
   { "nombre": "Ana López", "correo": "ana.prueba@ejemplo.com", "password": "Ejemplo2026" }
   ```
   Espera **202**. Copia `desafio` a la variable `desafio`.
2. Abre `http://localhost:8025`: llega "Verifica tu correo en ClimaPlus" con el código de 6 dígitos.
3. `POST {{base}}/api/auth/verificar-correo`
   ```json
   { "desafio": "{{desafio}}", "codigo": "123456" }
   ```
   Con el código del correo espera **204**. Con otro código, **400**.

## 3. Inicio de sesión en dos pasos

1. `POST {{base}}/api/auth/login` con `{ "correo": "ana.prueba@ejemplo.com", "password": "Ejemplo2026" }` →
   **202** sin `token`. Copia `desafio`.
2. Lee el código "Tu código para iniciar sesión en ClimaPlus" en Mailpit (vence en 5 minutos).
3. `POST {{base}}/api/auth/login/verificar` con `{ "desafio": "{{desafio}}", "codigo": "<código>" }` → **200**.
   Copia `token` a la variable `token`.
4. `GET {{base}}/api/auth/me` con `Authorization: Bearer {{token}}` → **200** con `correoVerificado`,
   `tienePassword` y `tieneGoogle`.

## 4. Recuperación de contraseña

1. `POST {{base}}/api/auth/password/solicitar`
   ```json
   { "correo": "ana.prueba@ejemplo.com" }
   ```
   Espera **202**. Copia `desafio`. En Mailpit llega "Tu código para recuperar la contraseña de ClimaPlus".
   - Repite con un correo que no exista: la respuesta es igual (202, misma forma) y **no** llega ningún correo.
2. `POST {{base}}/api/auth/password/verificar`
   ```json
   { "desafio": "{{desafio}}", "codigo": "<código del correo>" }
   ```
   Espera **200** `{ "permiso": "...", "expiraEn": "..." }`. Copia `permiso`.
   - Repite con el mismo código: **400** (ya se usó).
3. `POST {{base}}/api/auth/password/restablecer`
   ```json
   { "permiso": "{{permiso}}", "passwordNueva": "NuevaClave2027", "confirmacion": "NuevaClave2027" }
   ```
   Espera **204**. En Mailpit llega "Tu contraseña de ClimaPlus cambió".
4. Comprueba la revocación: `GET {{base}}/api/auth/me` con el `token` del punto 3 → **401**.
5. Repite el restablecer con el mismo `permiso` → **400** (`errors.permiso`).
6. El login con `Ejemplo2026` responde **401**; con `NuevaClave2027` sigue los dos pasos y responde 200.

Otros casos que vale la pena ver:

| Caso | Resultado esperado |
|---|---|
| `confirmacion` distinta de `passwordNueva` | 400 con `errors.confirmacion`; el permiso sigue valiendo |
| `passwordNueva` igual a la actual | 400 con `errors.passwordNueva`; el permiso sigue valiendo |
| Esperar más de 10 minutos con el permiso | 400 con `errors.permiso` |
| Pedir otra recuperación teniendo un permiso | El permiso anterior deja de valer (400) |
| Usar el `permiso` como `Authorization: Bearer` | 401 |
| Usar en `password/verificar` el desafío del login o del registro | 400 |
| Más de 3 solicitudes por hora para la misma cuenta | 202, pero desde la cuarta no llegan correos |
| Más de 5 solicitudes cada 15 minutos desde la misma IP | 429 con `Retry-After` |

## 5. Cambio de contraseña desde el perfil (con código, migración 005)

Paso 1: `POST {{base}}/api/usuarios/me/password/cambio` con `Authorization: Bearer {{token}}`:

```json
{ "passwordActual": "NuevaClave2027", "passwordNueva": "OtraClave2028", "confirmacion": "OtraClave2028" }
```

- **202** con `proposito: "CAMBIAR_PASSWORD"` y `desafio`: llega a Mailpit "Confirma el cambio de contraseña" con el
  código (vence en 5 minutos). **La contraseña todavía no cambió** y el `token` sigue sirviendo.

Paso 2: `POST {{base}}/api/usuarios/me/password/cambio/verificar` con el mismo `Authorization`:

```json
{ "desafio": "<desafio del paso 1>", "codigo": "<código de Mailpit>", "passwordActual": "NuevaClave2027", "passwordNueva": "OtraClave2028", "confirmacion": "OtraClave2028" }
```

- **204**: llega el aviso "Tu contraseña de ClimaPlus cambió" y el mismo `token` responde **401** en
  `GET /api/auth/me` (se cierran todas las sesiones, también la actual). Solo entra la contraseña nueva.

| Caso | Resultado esperado |
|---|---|
| Paso 1 con `passwordActual` incorrecta | 400 `errors.passwordActual`; no llega código; la sesión sigue activa |
| Paso 1 con `confirmacion` distinta | 400 `errors.confirmacion` |
| 5 contraseñas actuales incorrectas seguidas | La sexta (aunque sea correcta) responde 429 con `Retry-After` (bloqueo de 15 minutos, cuenta + IP) |
| Paso 2 con un código incorrecto | 400 `errors.codigo`; la contraseña no cambia; el código correcto sigue sirviendo (hasta 5 intentos) |
| Paso 2 después de 5 minutos | 400 (código vencido): pedir otro con "Reenviar código" o repetir el paso 1 |
| Repetir el paso 2 con el mismo código tras un cambio correcto (con la sesión nueva) | 400 (un solo uso) |
| Paso 2 con la sesión de otra cuenta | 400; ninguna de las dos cuentas cambia |
| `PUT /api/usuarios/me/password` (endpoint antiguo, sin código) | 404: ya no existe |
| Cualquiera de los dos pasos sin token | 401 |

## 6. Google: entrar, vincular, desvincular y establecer contraseña

Requiere configurar Google Cloud y `Google:ClientId` ([`../despliegue/google-oauth.md`](../despliegue/google-oauth.md)).
El ID token de Google solo se obtiene en un navegador, así que se usa la página de prueba
`docs/herramientas/prueba-google.html` (sección 4 de esa guía). Casos a revisar:

| Caso | Resultado esperado |
|---|---|
| Primera vez con un Gmail sin cuenta en ClimaPlus | 200, `rol: "USUARIO"`, `tienePassword: false`, `tieneGoogle: true`; no llega código a Mailpit |
| Otra vez con el mismo Google | 200 y el mismo `idUsuario` |
| Gmail que ya tiene cuenta local (sin Google vinculado) | 409; la cuenta local no cambia |
| Pulsar dos veces con el mismo ID token (reenviarlo desde Postman) | El segundo responde 401 (nonce ya usado) |
| Vincular con la contraseña correcta | 202 → código en Mailpit (al correo de ClimaPlus) → 200 con un token nuevo; el token anterior da 401 |
| Vincular con la contraseña incorrecta | 400 `errors.passwordActual` |
| Desvincular con la contraseña correcta | 200 con un token nuevo; el aviso llega a Mailpit |
| Desvincular una cuenta creada con Google | 409: primero "Establecer contraseña" |
| "Establecer contraseña" en una cuenta creada con Google | `POST /api/usuarios/me/password/establecer` → 202, código en Mailpit, luego `password/verificar` y `password/restablecer` (sección 4). Después entra con contraseña (dos pasos) o con Google |

## 7. Qué revisar en la consola de la API

Aparecen eventos `Seguridad:` como `recuperacion_solicitada`, `recuperacion_verificada`, `password_restablecida`
y `password_cambiada`, con el id del usuario o el correo enmascarado. **Nunca** deben aparecer contraseñas,
códigos, desafíos, permisos ni tokens.
