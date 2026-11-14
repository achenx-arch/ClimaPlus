# Contrato de API – Autenticación y perfil (Fase 2D, actualizado con la autenticación segura, fases 2 a 5)

**Para:** integrante de frontend (M02, M03, M16, M17)
**Backend:** `backend/ClimaPlus.Api` · ASP.NET Core .NET 8
**Estado:** implementado, pendiente de ejecutar las pruebas en Windows
**Requerimientos:** RF-01, RF-02, RF-03 y la enmienda E-01 (RF-31 a RF-33)

---

## 1. Convenciones

| Tema | Regla |
|---|---|
| URL base (desarrollo) | `http://localhost:5208` (perfil "http") o `https://localhost:7060` (perfil "https") |
| Formato | JSON, propiedades en `camelCase`, UTF-8 |
| Autenticación | Encabezado `Authorization: Bearer <token>` en toda ruta privada |
| Fechas | ISO 8601 en UTC, con `Z` (ej. `2026-10-25T19:54:00Z`). Mostrarlas en hora local |
| Errores | `application/problem+json` (RFC 9457). Ver sección 5 |
| Campos extra | Cualquier propiedad no documentada en el cuerpo → **400**. No envíes `rol`, `activo` ni `idUsuario` |
| CORS | Origen permitido en desarrollo: `http://localhost:5173` (Vite). Otro origen se agrega en `appsettings.json` → `Cors:OrigenesPermitidos` |

## 2. Resumen de rutas

| Método | Ruta | Acceso | Pantalla | Éxito |
|---|---|---|---|---|
| POST | `/api/auth/register` | Público | M03 | 202 |
| POST | `/api/auth/verificar-correo` | Público (con desafío) | M03 (pantalla del código) | 204 |
| POST | `/api/auth/reenviar-codigo` | Público (con desafío) | Pantallas de código | 202 |
| POST | `/api/auth/login` | Público | M02 (paso 1: contraseña) | 202 |
| POST | `/api/auth/login/verificar` | Público (con desafío) | M02 (paso 2: código) | 200 |
| GET | `/api/auth/me` | Con sesión | Navbar, restaurar sesión | 200 |
| GET | `/api/usuarios/me` | Con sesión | M16, M17 | 200 |
| PUT | `/api/usuarios/me` | Con sesión | M16, M17 | 200 |
| POST | `/api/usuarios/me/password/cambio` | Con sesión | M16, M17 (Cambiar contraseña, paso 1) | 202 |
| POST | `/api/usuarios/me/password/cambio/verificar` | Con sesión (con desafío) | M16, M17 (Cambiar contraseña, paso 2: código) | 204 |
| POST | `/api/usuarios/me/password/establecer` | Con sesión (cuentas de Google) | M16, M17 (Establecer contraseña) | 202 |
| POST | `/api/auth/password/solicitar` | Público | Recuperar contraseña, paso 1 | 202 |
| POST | `/api/auth/password/verificar` | Público (con desafío) | Recuperar contraseña, paso 2 | 200 |
| POST | `/api/auth/password/restablecer` | Público (con permiso) | Recuperar contraseña, paso 3 | 204 |
| GET | `/api/auth/google/iniciar` | Público (`uso=vincular`: con sesión) | Botón "Continuar con Google" | 200 |
| POST | `/api/auth/google` | Público (con ID token de Google) | M02, M03 | 200 |
| POST | `/api/usuarios/me/google/vincular` | Con sesión | M16, M17 (Vincular Google, paso 1) | 202 |
| POST | `/api/usuarios/me/google/vincular/verificar` | Con sesión | M16, M17 (Vincular Google, paso 2) | 200 |
| POST | `/api/usuarios/me/google/desvincular` | Con sesión | M16, M17 (Desvincular Google) | 200 |

`/api/auth/me` y `/api/usuarios/me` devuelven el mismo objeto `Usuario`. Usa el primero para la sesión y la navbar, y el segundo en la pantalla de perfil.

## 3. Objetos

### Usuario (respuesta)

| Campo | Tipo | Ejemplo | Notas |
|---|---|---|---|
| `idUsuario` | number | `12` | |
| `nombre` | string | `"Ana López"` | |
| `correo` | string | `"ana.lopez@ejemplo.com"` | Siempre en minúsculas |
| `rol` | string | `"USUARIO"` | `USUARIO` o `ADMINISTRADOR`. Solo lectura |
| `activo` | boolean | `true` | Solo lectura |
| `fechaCreacion` | string (fecha UTC) | `"2026-10-25T19:54:00Z"` | "Fecha de registro" en M16/M17 |
| `correoVerificado` | boolean | `true` | El usuario confirmó su correo con un código |
| `tienePassword` | boolean | `true` | Tiene contraseña local. `false` en cuentas creadas con Google: mostrar "Establecer contraseña" en lugar de "Cambiar contraseña" |
| `tieneGoogle` | boolean | `false` | Tiene una cuenta de Google vinculada. Junto con `tienePassword` indica el método de acceso |

Nunca incluye la contraseña ni su hash.

### Reglas de validación

| Campo | Regla | Mensaje |
|---|---|---|
| `nombre` | Obligatorio, máx. 100. Letras (con tildes y ñ), espacios, `.`, `'` y `-` | "El nombre solo puede contener letras, espacios, puntos, apóstrofos y guiones." |
| `correo` | Obligatorio, máx. 150, formato `usuario@dominio.ext` con caracteres ASCII | "Ingresa un correo válido." |
| `password` / `passwordNueva` (y `confirmacion` igual a `passwordNueva`) | 8 a 128 caracteres, al menos una letra y un número (D-15) | "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número." |

El backend quita los espacios del principio y del final, reduce los espacios repetidos del nombre y pasa el correo a minúsculas. Valida igual en el formulario para dar respuesta inmediata, pero la validación que vale es la del servidor.

## 4. Endpoints

### 4.1 `POST /api/auth/register` – Registro (M03)

Crea una cuenta con rol `USUARIO` **sin verificar** y envía al correo un código de 6 dígitos. El rol no se puede elegir.

```json
{
  "nombre": "Ana López",
  "correo": "ana.lopez@ejemplo.com",
  "password": "Ejemplo2026"
}
```

Respuesta 202 (`RespuestaDesafio`):

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "proposito": "VERIFICAR_CORREO",
  "destino": "a***@ejemplo.com",
  "expiraEn": "2026-11-09T19:10:00Z",
  "reenviarEn": 60
}
```

| Código | Cuándo | Qué hacer en M03 |
|---|---|---|
| 202 | Siempre que los datos sean válidos, **también si el correo ya existe** | Mostrar la pantalla del código: "Te enviamos un código a `destino`" |
| 400 | Datos inválidos o campos extra | Mostrar `errors.<campo>` bajo cada input |
| 429 | Más de 5 registros en 10 minutos desde la misma IP | Mostrar el `detail` y esperar `Retry-After` segundos |

- Ya no existe el 409: si el correo está registrado, la respuesta tiene la misma forma, pero el `desafio` es un **señuelo** que ningún código valida, y al dueño del buzón solo le llega un aviso ("alguien intentó registrarse con tu correo", máximo uno por hora). Así no se puede averiguar qué correos tienen cuenta.
- `desafio` es un token corto (audiencia `ClimaPlus.Desafio`) que solo sirve para verificar o reenviar el código. **No es una sesión**: cualquier endpoint privado lo rechaza con 401. Guárdalo solo en memoria (estado de React), nunca en `localStorage`.
- No devuelve sesión: después de verificar, el usuario inicia sesión.

### 4.1.1 `POST /api/auth/verificar-correo` – Confirmar el correo

```json
{ "desafio": "eyJhbGciOiJIUzI1NiIs...", "codigo": "048213" }
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 204 | Código correcto: el correo queda verificado | Ir a M02 con el mensaje "Correo verificado, inicia sesión" |
| 400 | Código incorrecto, vencido, ya usado, desafío agotado o señuelo (`errors.codigo`); formato inválido (`errors.codigo`); desafío inválido o vencido | Mostrar el error bajo el campo del código |
| 429 | Más de 20 validaciones de código por minuto desde la misma IP | Esperar `Retry-After` |

Reglas del código: 6 dígitos (`^[0-9]{6}$`), vence a los **10 minutos**, sirve **una sola vez** y admite **5 intentos**; al quinto fallo el desafío se anula y el usuario obtiene un código nuevo iniciando sesión (sección 4.2). Además, una cuenta acepta como máximo **10 códigos incorrectos por hora** en este endpoint; al llegar al tope responde el mismo 400 hasta que pase la hora. Ese tope no afecta al inicio de sesión del dueño. La respuesta 400 es siempre la misma: no indica cuántos intentos quedan ni si el desafío existe. En la base solo se guarda el HMAC-SHA256 del código (clave `Codigos:ClaveHmac`, distinta de la del JWT).

### 4.1.2 `POST /api/auth/reenviar-codigo` – Pedir otro código

```json
{ "desafio": "eyJhbGciOiJIUzI1NiIs..." }
```

Respuesta 202 (`RespuestaReenvio`): `{ "desafio", "proposito", "expiraEn", "reenviarEn" }`. Usa el **nuevo** `desafio` y el nuevo `expiraEn`; el código anterior deja de valer y la vigencia se renueva. No repite `destino`.

| Código | Cuándo | Qué hacer |
|---|---|---|
| 202 | Reenvío aceptado | Reiniciar el contador de `reenviarEn` segundos |
| 400 | Desafío inválido, alterado o vencido (`errors.desafio`) | Volver a empezar el flujo |
| 429 | Antes de 60 s desde el último envío, o ya se hicieron 5 reenvíos de ese desafío (cabecera `Retry-After`); o más de 10 reenvíos por hora desde la misma IP | Mostrar el `detail` y deshabilitar el botón `Retry-After` segundos |

Topes silenciosos (responden 202 pero no envían correo): un desafío ya usado o anulado, un señuelo, o una cuenta que ya recibió 5 correos con código en la última hora. Los señuelos respetan las mismas esperas que un desafío real.

Sirve para cualquier propósito de desafío (verificación de correo ahora; inicio de sesión, Google y recuperación en las fases siguientes).

### 4.2 `POST /api/auth/login` – Inicio de sesión, paso 1: contraseña (M02)

```json
{
  "correo": "ana.lopez@ejemplo.com",
  "password": "Ejemplo2026"
}
```

Si la contraseña es correcta, **no devuelve el JWT**: envía un código de 6 dígitos al correo y responde 202 con un `RespuestaDesafio` (misma forma que el registro):

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "proposito": "INICIO_SESION",
  "destino": "a***@ejemplo.com",
  "expiraEn": "2026-11-09T18:05:00Z",
  "reenviarEn": 60
}
```

| Código | Cuándo | Qué hacer en M02 |
|---|---|---|
| 202 | Contraseña correcta; código enviado | Mostrar la pantalla del código: "Te enviamos un código a `destino`" |
| 400 | Formato inválido | Errores por campo |
| 401 | Correo o contraseña incorrectos (mensaje único, no dice cuál falló) | Alerta "Correo o contraseña incorrectos" |
| 403 | Cuenta desactivada (solo se informa si la contraseña era correcta) | Alerta amarilla "Tu cuenta está desactivada. Contacta al administrador." |
| 429 | Bloqueo temporal de la cuenta o límite de la IP | Mostrar `detail`; deshabilitar el botón `Retry-After` segundos |

- `proposito` es `INICIO_SESION` (código válido **5 minutos**) o `VERIFICAR_CORREO` (10 minutos) si la cuenta todavía no verificó su correo: cuentas creadas antes de esta versión o registros que no terminaron. En ambos casos el siguiente paso es el mismo, `POST /api/auth/login/verificar`; con `VERIFICAR_CORREO` el texto de la pantalla puede decir "Verifica tu correo para entrar".
- Cada desafío nuevo anula el anterior del mismo propósito: si el usuario vuelve a escribir la contraseña, solo vale el último código.
- El botón "Reenviar código" usa `POST /api/auth/reenviar-codigo` (sección 4.1.2) con el `desafio` del paso 1; el nuevo token devuelto sigue sirviendo para este paso 2.

**Bloqueo temporal (condición 2 del diseño).** Cuentan como fallo una contraseña incorrecta y un código de inicio de sesión incorrecto:

| Bloqueo | Regla | Efecto |
|---|---|---|
| Cuenta + IP | 5 fallos en 15 min | Ese correo queda bloqueado desde esa IP (también con la contraseña correcta). El dueño puede entrar desde otra red |
| Cuenta | 20 fallos en 15 min sumando todas las IP | Ese correo queda bloqueado desde cualquier IP (ataque distribuido) |

Se aplica igual a correos inexistentes (no revela qué cuentas existen), se libera solo y responde 429 con `Retry-After`. Solo un inicio de sesión **completo** (contraseña y código) reinicia el contador de esa IP; escribir bien la contraseña no lo reinicia, así quien conozca la contraseña no puede probar códigos sin límite. Además, la IP tiene su propio límite de peticiones (política `RateLimiting:Autenticacion`).

### 4.2.1 `POST /api/auth/login/verificar` – Inicio de sesión, paso 2: código

```json
{ "desafio": "eyJhbGciOiJIUzI1NiIs...", "codigo": "048213" }
```

Respuesta 200 (`LoginResponse`, igual que el login anterior):

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.<ejemplo>.<ejemplo>",
  "tipoToken": "Bearer",
  "expiraEn": "2026-10-25T20:54:00Z",
  "usuario": {
    "idUsuario": 12,
    "nombre": "Ana López",
    "correo": "ana.lopez@ejemplo.com",
    "rol": "USUARIO",
    "activo": true,
    "fechaCreacion": "2026-10-25T19:54:00Z",
    "correoVerificado": true,
    "tienePassword": true,
    "tieneGoogle": false
  }
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Código correcto: sesión iniciada. Si el desafío era `VERIFICAR_CORREO`, el correo queda verificado | Guardar el token (sección 7). `USUARIO` → M06, `ADMINISTRADOR` → M13 |
| 400 | Código incorrecto, vencido, ya usado o desafío agotado (`errors.codigo`); desafío inválido, alterado, de otro flujo (por ejemplo, el del registro) o un JWT de sesión | Error bajo el campo del código; con desafío inválido, volver al paso 1 |
| 403 | La cuenta se desactivó entre los dos pasos | Alerta de cuenta desactivada |
| 429 | Cuenta bloqueada (los códigos incorrectos suman al bloqueo de la sección 4.2) o más de 20 validaciones de código por minuto desde la IP | Mostrar `detail` y esperar `Retry-After` |

- El código sirve una sola vez y admite 5 intentos; al quinto fallo el desafío se anula y hay que volver al paso 1.
- El token del desafío (audiencia `ClimaPlus.Desafio`) **no es una sesión**: cualquier endpoint privado lo rechaza con 401. Solo los desafíos emitidos por el paso 1 sirven aquí.
- Los roles no cambian: un `ADMINISTRADOR` también usa los dos pasos y su JWT conserva el rol. El ESP32 no usa este flujo: sigue autenticándose con `X-Device-Key`.

### 4.3 `GET /api/auth/me` – Sesión actual

Sin cuerpo. Requiere token. 200 → `Usuario`; 401 → sesión inválida.

### 4.4 `GET /api/usuarios/me` – Ver perfil (M16, M17)

Sin cuerpo. Requiere token. 200 → `Usuario`.

Rellena "Información personal" (`nombre`, `correo`) e "Información de la cuenta" (`rol`, `activo` como "Activa", `fechaCreacion` como "Fecha de registro"). Rol y estado se muestran como solo lectura.

### 4.5 `PUT /api/usuarios/me` – Guardar cambios (M16, M17)

Envía siempre los dos campos.

```json
{
  "nombre": "Ana María López",
  "correo": "ana.lopez@ejemplo.com"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Guardado. Cuerpo: `Usuario` actualizado | Toast "Cambios guardados"; actualizar el nombre en la navbar |
| 400 | Datos inválidos, o se envió `rol`, `activo` u otro campo | Errores por campo |
| 401 | Sesión inválida | Ver sección 6 |
| 409 | El correo pertenece a otra cuenta (`errors.correo`) | Error bajo el campo correo |

Cambiar el correo **no** invalida la sesión: el token identifica al usuario por su id.

### 4.6 Cambiar contraseña con código por correo (M16, M17) – migración 005

Dos pasos, con la sesión iniciada. **La contraseña no cambia hasta validar el código** enviado al correo de la cuenta
(propósito `CAMBIAR_PASSWORD`, 6 dígitos, vence en **5 minutos**, máximo 5 intentos, un solo uso). Sustituye al antiguo
`PUT /api/usuarios/me/password`, que cambiaba la contraseña sin código y **ya no existe** (404).

#### 4.6.1 `POST /api/usuarios/me/password/cambio` – Paso 1: contraseñas

```json
{
  "passwordActual": "Ejemplo2026",
  "passwordNueva": "NuevaClave2027",
  "confirmacion": "NuevaClave2027"
}
```

Respuesta 202 (`RespuestaDesafio`):

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "proposito": "CAMBIAR_PASSWORD",
  "destino": "a***@ejemplo.com",
  "expiraEn": "2026-11-13T18:05:00Z",
  "reenviarEn": 60
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 202 | Contraseñas correctas: código enviado. **Todavía no cambió nada** (ni la contraseña ni las sesiones) | Mostrar la pantalla del código (4.6.2) |
| 400 | `errors.passwordActual`: la actual es incorrecta · `errors.passwordNueva`: no cumple la política o es igual a la actual · `errors.confirmacion`: no coincide | Error bajo el campo indicado |
| 401 | Sesión inválida | Ver sección 6 |
| 409 | La cuenta no tiene contraseña local (creada con Google) o no verificó su correo | Ofrecer "Establecer contraseña" (4.7) o "¿Olvidaste tu contraseña?" |
| 429 | Demasiadas contraseñas actuales incorrectas (5 por cuenta e IP o 20 por cuenta en 15 minutos, el mismo bloqueo del login), demasiados códigos incorrectos para la cuenta (10 por hora) o más de 5 peticiones por minuto desde la IP | Mostrar `detail` y esperar `Retry-After` |

La API no guarda la contraseña nueva en ningún sitio entre los dos pasos: el cliente la conserva en memoria y la vuelve
a enviar en el paso 2. Un paso 1 nuevo anula el código anterior. "Reenviar código" usa `POST /api/auth/reenviar-codigo`
(4.1.2) con el `desafio` (uno cada 60 s, máximo 5).

#### 4.6.2 `POST /api/usuarios/me/password/cambio/verificar` – Paso 2: código

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "codigo": "482913",
  "passwordActual": "Ejemplo2026",
  "passwordNueva": "NuevaClave2027",
  "confirmacion": "NuevaClave2027"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 204 | Código válido: contraseña cambiada (sin cuerpo) | **Todas las sesiones se cierran, también la actual**: borrar el token y llevar a M02 con el aviso "Contraseña actualizada, inicia sesión de nuevo" |
| 400 | `errors.codigo`: incorrecto, vencido, ya usado, agotado, de otra cuenta o de otro propósito (mismo mensaje) · `errors.passwordActual` / `passwordNueva` / `confirmacion` como en el paso 1 (no gastan el código) | Error bajo el campo; con error de contraseña, volver al paso 1 |
| 401 | Sesión inválida | Ver sección 6 |
| 409 | Igual que en el paso 1 | Igual que en el paso 1 |
| 429 | 10 códigos incorrectos en una hora para la cuenta, contraseña actual bloqueada o más de 20 peticiones por minuto desde la IP | Mostrar `detail` y esperar `Retry-After` |

Con el código válido, en **una sola transacción**: se guarda el hash nuevo, el desafío pasa a `CONSUMIDO`, se renueva el
sello de seguridad (todos los JWT anteriores responden 401) y se anulan los demás desafíos pendientes de la cuenta
(incluido un permiso de recuperación sin usar). Después se envía el aviso "Tu contraseña de ClimaPlus cambió". El
desafío debe pertenecer a la cuenta de la sesión. Nunca se registran contraseñas ni códigos en los logs.

### 4.7 `POST /api/usuarios/me/password/establecer` – Establecer contraseña (cuentas de Google)

Sin cuerpo. Requiere token. Para cuentas creadas con Google que todavía no tienen contraseña local (`tienePassword: false`). **La sesión sola no basta**: la API envía un código `RECUPERAR_PASSWORD` al correo de la cuenta y la contraseña se crea con los pasos 2 y 3 de la recuperación (secciones 4.8.2 y 4.8.3).

Respuesta 202 (`RespuestaDesafio`):

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "proposito": "RECUPERAR_PASSWORD",
  "destino": "a***@gmail.com",
  "expiraEn": "2026-11-10T18:10:00Z",
  "reenviarEn": 60
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 202 | Código enviado | Pantalla del código (igual que la recuperación) |
| 401 | Sin sesión o sesión inválida | Ver sección 6 |
| 409 | La cuenta ya tiene contraseña | Usar "Cambiar contraseña" (4.6) |
| 429 | Más de 5 por IP cada 15 minutos, o más de 3 códigos de recuperación por hora para la cuenta | Mostrar `detail` y esperar `Retry-After` |

La vinculación con Google se conserva: después, la cuenta puede entrar con Google o con correo y contraseña. Al crear la contraseña se cierran todas las sesiones (sello nuevo).

### 4.8 Recuperación de contraseña ("¿Olvidaste tu contraseña?")

Tres pasos. Ninguno entrega una sesión: al terminar, el usuario inicia sesión en dos pasos con su contraseña nueva.

#### 4.8.1 `POST /api/auth/password/solicitar`

`SolicitarRecuperacionRequest`:

```json
{ "correo": "ana.lopez@ejemplo.com" }
```

Respuesta 202 (`RespuestaDesafio`), **siempre la misma forma, exista o no la cuenta**:

```json
{
  "desafio": "eyJhbGciOiJIUzI1NiIs...",
  "proposito": "RECUPERAR_PASSWORD",
  "destino": "a***@ejemplo.com",
  "expiraEn": "2026-11-10T18:10:00Z",
  "reenviarEn": 60
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 202 | Siempre que el correo tenga formato válido | "Si el correo tiene una cuenta, te enviamos un código". Mostrar la pantalla del código |
| 400 | Correo con formato inválido o campos extra | Error bajo el campo |
| 429 | Más de 5 solicitudes cada 15 minutos desde la misma IP | Mostrar `detail` y esperar `Retry-After` |

- Solo una cuenta **activa** recibe el código (vigencia 10 minutos). Para un correo sin cuenta o una cuenta desactivada, el `desafio` es un señuelo que ningún código valida.
- Tope silencioso: una cuenta recibe como máximo **3 códigos de recuperación por hora** (y 5 códigos por hora sumando todos los propósitos). Pasado el tope, la respuesta es la misma pero no se envía nada y el código vivo anterior sigue valiendo.
- Cada solicitud nueva anula el código y el permiso anteriores de recuperación. No afecta al inicio de sesión: el dueño puede seguir entrando con su contraseña.
- "Reenviar código" usa `POST /api/auth/reenviar-codigo` (4.1.2) con este `desafio`.

#### 4.8.2 `POST /api/auth/password/verificar`

`VerificarCodigoRequest` (el mismo de los otros códigos):

```json
{ "desafio": "eyJhbGciOiJIUzI1NiIs...", "codigo": "048213" }
```

Respuesta 200 (`RespuestaPermiso`):

```json
{
  "permiso": "eyJhbGciOiJIUzI1NiIs...",
  "expiraEn": "2026-11-10T18:12:00Z"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Código correcto | Guardar `permiso` solo en memoria y mostrar el formulario de contraseña nueva |
| 400 | Código incorrecto, vencido, ya usado, desafío agotado o señuelo (`errors.codigo`); desafío de otro flujo (registro, login) o alterado | Error bajo el campo del código |
| 429 | Más de 20 validaciones de código por minuto desde la IP | Esperar `Retry-After` |

- 5 intentos por código; al quinto fallo se anula y hay que solicitar otro. Además, 10 códigos incorrectos por hora en la recuperación de una cuenta bloquean este paso para esa cuenta durante la hora (mismo 400), sin afectar a su inicio de sesión.
- El `permiso` (audiencia `ClimaPlus.Restablecer`) dura **10 minutos**, sirve **una sola vez** y solo en el paso 3. No es una sesión (los endpoints privados responden 401) ni un desafío.

#### 4.8.3 `POST /api/auth/password/restablecer`

`RestablecerPasswordRequest`:

```json
{
  "permiso": "eyJhbGciOiJIUzI1NiIs...",
  "passwordNueva": "NuevaClave2027",
  "confirmacion": "NuevaClave2027"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 204 | Contraseña cambiada (sin cuerpo) | Ir a M02 con "Contraseña actualizada, inicia sesión" |
| 400 | `errors.passwordNueva`: no cumple la política o es igual a la actual · `errors.confirmacion`: no coincide · `errors.permiso`: permiso inválido, vencido o ya usado | Con `permiso`, volver al paso 1; con los demás, corregir y reintentar (el permiso sigue valiendo) |
| 429 | Más de 20 por minuto desde la IP | Esperar `Retry-After` |

Al restablecer, en una sola transacción: se consume el desafío (no se puede repetir aunque lleguen dos peticiones a la vez), se guarda el hash de la contraseña nueva, se renueva el sello de seguridad (**todas las sesiones abiertas responden 401**) y la cuenta queda con el correo verificado. Después se envía el aviso "Tu contraseña de ClimaPlus cambió". No inicia sesión.

### 4.9 "Continuar con Google" (Google Identity Services)

Configuración y flujo completo: [`../despliegue/google-oauth.md`](../despliegue/google-oauth.md). Resumen:

- La identidad de Google es su `sub`. **El correo nunca vincula cuentas por sí solo.**
- Entrar con Google **emite el JWT directamente** (Google es la prueba de identidad; no hay código por correo).
- Cuentas creadas con Google: rol `USUARIO` (Google nunca da `ADMINISTRADOR`), `correoVerificado: true`,
  `tienePassword: false`. Pueden crear contraseña con "Establecer contraseña" (4.7).
- Ningún token de Google ni de ClimaPlus viaja en la URL ni queda en los logs.

#### 4.9.1 `GET /api/auth/google/iniciar?uso=login|vincular`

Sin cuerpo. `uso` es opcional (`login` por defecto). `uso=vincular` exige `Authorization: Bearer` y el nonce queda
atado a esa cuenta. Respuesta 200 (`RespuestaInicioGoogle`):

```json
{
  "clientId": "1234567890-abc123.apps.googleusercontent.com",
  "nonce": "q8m3v2...43 caracteres base64url",
  "expiraEn": "2026-11-10T20:40:00Z"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Listo | `google.accounts.id.initialize({ client_id: clientId, nonce, callback })` y `renderButton(...)` |
| 400 | `uso` distinto de `login` o `vincular` (`errors.uso`) | — |
| 401 | `uso=vincular` sin sesión | Ver sección 6 |
| 503 | Google no está configurado en el servidor | Ocultar el botón de Google |

El nonce sirve **una sola vez** y vence a los **10 minutos**: pide uno nuevo cada vez que muestres el botón.

#### 4.9.2 `POST /api/auth/google`

`GoogleLoginRequest` (el `credential` que Google entrega al `callback`):

```json
{ "credencial": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij..." }
```

Respuesta 200: `LoginResponse`, igual que `POST /api/auth/login/verificar` (4.2.1), con `tieneGoogle: true`.

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Cuenta vinculada a ese Google, o cuenta nueva creada | Guardar el token. `USUARIO` → M06, `ADMINISTRADOR` → M13 |
| 400 | `credencial` vacía; o cuenta nueva con correo no verificado por Google o no admitido (`errors.credencial`) | Mostrar el `detail` |
| 401 | ID token inválido: firma, emisor, audiencia, `azp`, vencido, sin `sub`, sin nonce, nonce desconocido, vencido, ya usado o de vinculación | "No se pudo validar el inicio con Google. Vuelve a intentarlo" y pedir un nonce nuevo |
| 403 | Cuenta desactivada | Alerta de cuenta desactivada |
| 409 | El correo de ese Google ya es de una cuenta de ClimaPlus que no tiene este Google vinculado | "Ya tienes cuenta con este correo: inicia sesión con tu contraseña y vincula Google desde tu perfil" |
| 429 | Límite por IP (`RateLimiting:Autenticacion`) | Esperar `Retry-After` |
| 503 | Google no configurado | Ocultar el botón |

El 409 solo lo ve quien demostró ante Google que controla ese correo, por eso no expone cuentas a terceros.

#### 4.9.3 `POST /api/usuarios/me/google/vincular` (paso 1)

Requiere sesión. Nonce de `GET /api/auth/google/iniciar?uso=vincular`. `VincularGoogleRequest`:

```json
{
  "credencial": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij...",
  "passwordActual": "Ejemplo2026"
}
```

Respuesta 202 (`RespuestaDesafio`, `proposito: "VINCULAR_GOOGLE"`). El código va al **correo de la cuenta de
ClimaPlus** (no al de Google) y vence en 10 minutos.

| Código | Cuándo |
|---|---|
| 202 | Contraseña y Google válidos: código enviado |
| 400 | `errors.passwordActual`: contraseña incorrecta (suma al bloqueo del login) · `errors.credencial`: ID token inválido o nonce que no es de vinculación de esta cuenta |
| 401 | Sin sesión |
| 409 | La cuenta ya tiene Google · ese Google ya está vinculado a otra cuenta · la cuenta no tiene el correo verificado o no tiene contraseña (verificar con "¿Olvidaste tu contraseña?") |
| 429 | Cuenta bloqueada por intentos o límite por IP |
| 503 | Google no configurado |

#### 4.9.4 `POST /api/usuarios/me/google/vincular/verificar` (paso 2)

Requiere sesión. `VerificarCodigoRequest`: `{ "desafio": "...", "codigo": "048213" }`. El desafío debe ser de la
cuenta de la sesión.

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Vinculado. Cuerpo: `LoginResponse` con un **JWT nuevo** | Reemplazar el token guardado: las demás sesiones se cerraron |
| 400 | Código incorrecto, vencido, usado, agotado, de otra cuenta o de otro propósito (`errors.codigo`) | Error bajo el campo |
| 409 | Ese Google se vinculó a otra cuenta entre los dos pasos, o la cuenta ya tiene Google | Mostrar el `detail` |

Llega el aviso "Vinculaste Google a tu cuenta de ClimaPlus".

#### 4.9.5 `POST /api/usuarios/me/google/desvincular`

Requiere sesión. `DesvincularGoogleRequest`:

```json
{ "passwordActual": "Ejemplo2026" }
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 200 | Desvinculado. Cuerpo: `LoginResponse` con un **JWT nuevo** | Reemplazar el token; las demás sesiones (también las abiertas con Google) se cerraron |
| 400 | Contraseña actual incorrecta (`errors.passwordActual`) | Error bajo el campo |
| 401 | Sin sesión | Ver sección 6 |
| 404 | La cuenta no tiene Google vinculado | — |
| 409 | La cuenta solo entra con Google (sin contraseña local) | Ofrecer "Establecer contraseña" (4.7) antes de desvincular |
| 429 | Cuenta bloqueada por intentos o límite por IP | Esperar `Retry-After` |

Se usa `POST .../desvincular` (y no `DELETE` con cuerpo) porque la operación necesita la contraseña actual en el
cuerpo y algunos clientes y proxies descartan el cuerpo de un `DELETE`. Usuario, favoritos, historial, reglas,
alertas y dispositivos no cambian. Llega el aviso "Desvinculaste Google de tu cuenta de ClimaPlus".

## 5. Formato de errores

**Error de validación (400 y 409):**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "correo": [ "Ingresa un correo válido." ],
    "password": [ "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número." ]
  }
}
```

Las claves de `errors` son los nombres de campo del JSON. Un campo extra o un JSON mal formado aparece con una clave que empieza por `$`: muéstralo como error general del formulario.

**Otros errores (401, 403, 404, 429, 500):**

```json
{
  "title": "No autenticado",
  "status": 401,
  "detail": "Falta el token de acceso, no es válido o expiró. Inicia sesión de nuevo."
}
```

Muestra `detail` al usuario; `type` puede no venir.  Un 500 nunca incluye detalles internos: muestra un mensaje genérico.

## 6. Reglas de sesión para el frontend

| Situación | Respuesta de la API | Acción en React |
|---|---|---|
| Sin token en ruta privada | 401 | Redirigir a M02 |
| Token expirado (60 min) o inválido | 401 | Borrar el token, redirigir a M02, toast "Tu sesión expiró" |
| Cuenta desactivada o rol cambiado por un administrador | 401 en la siguiente petición | Igual que el caso anterior |
| Rol sin permiso | 403 | Mostrar "Acceso denegado"; no cerrar sesión |
| Demasiadas peticiones | 429 con `Retry-After` | Mostrar `detail`; no reintentar automáticamente |

Ocultar botones o rutas según el rol es solo una ayuda visual. Quien decide los permisos es el backend.

## 7. Almacenamiento seguro del token (D-18, propuesta)

1. **Guardarlo en memoria**, en el contexto de autenticación de React, junto con `expiraEn` y el `usuario`. **Nunca en `localStorage`.** Cualquier script inyectado (XSS) puede leer `localStorage`.
2. Al recargar la página la sesión se pierde y el usuario vuelve a M02. Es el costo de no guardarlo en el navegador. Si el equipo prefiere conservar la sesión al recargar, la alternativa menos mala es `sessionStorage`, que se borra al cerrar la pestaña, pero sigue expuesta a XSS: decídanlo en equipo.
3. Programar el cierre de sesión al llegar a `expiraEn`, y tratar cualquier 401 como sesión terminada.
4. Al cerrar sesión, borrar el token de memoria. No hace falta llamar al backend: los tokens no se guardan en el servidor (D-01).
5. Prevenir XSS en React: no usar `dangerouslySetInnerHTML`; mostrar nombres, correos y mensajes como texto.
6. No mostrar el token en consola, en la URL ni en mensajes de error.

## 8. Cuentas de prueba

No hay cuentas fijas. Crea una con `POST /api/auth/register`. El administrador inicial lo crea el backend al arrancar, solo si cada integrante configuró `AdminInicial` en sus User Secrets (ver `docs/api/pruebas-fase-2d.md`). Nunca compartan contraseñas reales en el repositorio.
