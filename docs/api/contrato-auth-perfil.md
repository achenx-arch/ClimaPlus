# Contrato de API – Autenticación y perfil (Fase 2D)

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
| POST | `/api/auth/register` | Público | M03 | 201 |
| POST | `/api/auth/login` | Público | M02 | 200 |
| GET | `/api/auth/me` | Con sesión | Navbar, restaurar sesión | 200 |
| GET | `/api/usuarios/me` | Con sesión | M16, M17 | 200 |
| PUT | `/api/usuarios/me` | Con sesión | M16, M17 | 200 |
| PUT | `/api/usuarios/me/password` | Con sesión | M16, M17 (Cambiar contraseña) | 204 |

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

Nunca incluye la contraseña ni su hash.

### Reglas de validación

| Campo | Regla | Mensaje |
|---|---|---|
| `nombre` | Obligatorio, máx. 100. Letras (con tildes y ñ), espacios, `.`, `'` y `-` | "El nombre solo puede contener letras, espacios, puntos, apóstrofos y guiones." |
| `correo` | Obligatorio, máx. 150, formato `usuario@dominio.ext` con caracteres ASCII | "Ingresa un correo válido." |
| `password` / `passwordNueva` | 8 a 128 caracteres, al menos una letra y un número (D-15) | "La contraseña debe tener entre 8 y 128 caracteres e incluir al menos una letra y un número." |

El backend quita los espacios del principio y del final, reduce los espacios repetidos del nombre y pasa el correo a minúsculas. Valida igual en el formulario para dar respuesta inmediata, pero la validación que vale es la del servidor.

## 4. Endpoints

### 4.1 `POST /api/auth/register` – Registro (M03)

Crea una cuenta con rol `USUARIO`. El rol no se puede elegir.

```json
{
  "nombre": "Ana López",
  "correo": "ana.lopez@ejemplo.com",
  "password": "Ejemplo2026"
}
```

| Código | Cuándo | Qué hacer en M03 |
|---|---|---|
| 201 | Cuenta creada. Cuerpo: `Usuario` | Ir a M02 con el mensaje "Cuenta creada, inicia sesión" |
| 400 | Datos inválidos o campos extra | Mostrar `errors.<campo>` bajo cada input |
| 409 | Correo ya registrado (`errors.correo`) | Mostrar el error bajo el campo correo |
| 429 | Demasiados intentos | Mostrar el `detail` y esperar `Retry-After` segundos |

No devuelve token: el usuario inicia sesión después, como indica el mockup.

### 4.2 `POST /api/auth/login` – Inicio de sesión (M02)

```json
{
  "correo": "ana.lopez@ejemplo.com",
  "password": "Ejemplo2026"
}
```

Respuesta 200:

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
    "fechaCreacion": "2026-10-25T19:54:00Z"
  }
}
```

| Código | Cuándo | Qué hacer en M02 |
|---|---|---|
| 200 | Sesión iniciada | Guardar el token (sección 7). `USUARIO` → M06, `ADMINISTRADOR` → M13 |
| 400 | Formato inválido | Errores por campo |
| 401 | Correo o contraseña incorrectos (mensaje único, no dice cuál falló) | Alerta "Correo o contraseña incorrectos" |
| 403 | Cuenta desactivada | Alerta amarilla "Tu cuenta está desactivada. Contacta al administrador." |
| 429 | Demasiados intentos | Mostrar `detail`; deshabilitar el botón `Retry-After` segundos |

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

### 4.6 `PUT /api/usuarios/me/password` – Cambiar contraseña (M16, M17)

```json
{
  "passwordActual": "Ejemplo2026",
  "passwordNueva": "NuevaClave2027"
}
```

| Código | Cuándo | Qué hacer |
|---|---|---|
| 204 | Contraseña cambiada (sin cuerpo) | Cerrar el modal; toast "Contraseña actualizada" |
| 400 | `errors.passwordActual`: la actual es incorrecta · `errors.passwordNueva`: no cumple la política o es igual a la actual | Error bajo el campo indicado |
| 401 | Sesión inválida | Ver sección 6 |
| 429 | Demasiados intentos | Mostrar `detail` |

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
