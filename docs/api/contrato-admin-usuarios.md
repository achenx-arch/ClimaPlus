# Contrato API – Gestión de usuarios (M14)

**Versión:** 1.0 · **Fecha:** 2026-11-14 · **Pantalla:** M14 Gestión de usuarios (solo ADMINISTRADOR)
**Requisitos:** RF-26 (listar y filtrar), RF-27 (cambiar rol), RF-28 (activar/desactivar); RN-BD-05, RN-BD-08, RN-BD-09, RN-BD-37/38.
**Base de datos:** sin cambios de esquema; usa solo la tabla `usuario` (`rol`, `activo`, `sello_seguridad`). No hay migración nueva.

## Estado

| Endpoint | Estado |
|---|---|
| `GET /api/admin/usuarios` | Implementado; pruebas `Admin/AdminUsuariosTests` y `Datos/AdminUsuariosPostgreSqlTests` escritas, **pendientes de ejecutar con `dotnet test`** |
| `GET /api/admin/usuarios/{idUsuario}` | Igual |
| `PATCH /api/admin/usuarios/{idUsuario}/rol` | Igual |
| `PATCH /api/admin/usuarios/{idUsuario}/estado` | Igual |

## Reglas comunes

- JWT obligatorio y rol **ADMINISTRADOR** (`[Authorize(Roles = "ADMINISTRADOR")]`): sin token → 401; USUARIO → 403.
- La validación del JWT ya consulta la base en cada petición (`OnTokenValidated`): un token de una cuenta desactivada,
  con rol distinto o con el sello renovado responde 401.
- Errores en `application/problem+json`. Validación → 400 con `errors.<campo>`; reglas de negocio → 409 con
  `errors.rol` o `errors.activo`; usuario inexistente → 404.
- **Nunca** se devuelven contraseñas, hashes, `sello_seguridad`, desafíos, códigos, tokens ni el identificador de Google.
- No hay borrado de cuentas (RN-BD-08): solo desactivación.
- El registro público y el perfil no pueden cambiar `rol` ni `activo` (RN-BD-05, RN-BD-37/38): sus cuerpos rechazan
  campos desconocidos (400).

## Objeto `UsuarioAdmin`

Mismos campos que el perfil (`UsuarioResponse`):

```json
{
  "idUsuario": 12,
  "nombre": "Ana López",
  "correo": "ana.lopez@ejemplo.com",
  "rol": "USUARIO",
  "activo": true,
  "correoVerificado": true,
  "tienePassword": true,
  "tieneGoogle": false,
  "fechaCreacion": "2026-10-25T19:54:00Z"
}
```

## `GET /api/admin/usuarios` (RF-26)

| Parámetro | Valores | Notas |
|---|---|---|
| `busqueda` | texto, máx. 150 | Contenido en el nombre **o** el correo, sin distinguir mayúsculas; se recortan espacios |
| `rol` | `USUARIO` · `ADMINISTRADOR` | Exacto (mayúsculas) |
| `estado` | `ACTIVO` · `INACTIVO` | |
| `pagina` | 1 a 100000 (1 por defecto) | 20 por página, fija |

Orden: más recientes primero (`fechaCreacion`, luego `idUsuario`). Respuesta 200: `Pagina<UsuarioAdmin>`
(`elementos`, `pagina`, `tamanoPagina`, `totalElementos`, `totalPaginas`).

## `GET /api/admin/usuarios/{idUsuario}`

200 `UsuarioAdmin` · 404 si no existe.

## `PATCH /api/admin/usuarios/{idUsuario}/rol` (RF-27)

Cuerpo: `{ "rol": "ADMINISTRADOR" }` (o `"USUARIO"`). Campos extra → 400.

| Caso | Respuesta |
|---|---|
| Cambio válido | 200 `UsuarioAdmin`; se renueva `sello_seguridad` (todas las sesiones de esa cuenta dejan de valer) |
| Mismo rol que ya tiene | 200 sin cambios (no se renueva el sello) |
| El administrador intenta quitarse su propio rol | 409, `errors.rol` |
| Degradar al último administrador activo (RN-BD-09) | 409, `errors.rol` |
| Usuario inexistente | 404 |

## `PATCH /api/admin/usuarios/{idUsuario}/estado` (RF-28)

Cuerpo: `{ "activo": false }` (o `true`). Campos extra → 400.

| Caso | Respuesta |
|---|---|
| Cambio válido | 200 `UsuarioAdmin`; se renueva `sello_seguridad`. Desactivada, la cuenta no puede iniciar sesión (403) |
| Mismo estado | 200 sin cambios |
| El administrador intenta desactivar su propia cuenta | 409, `errors.activo` |
| Desactivar al último administrador activo (RN-BD-09) | 409, `errors.activo` |
| Usuario inexistente | 404 |

Al reactivar una cuenta, los tokens anteriores siguen sin valer (el sello cambió): el usuario inicia sesión de nuevo.

## Concurrencia

Los cambios de rol y estado se ejecutan en una transacción que primero toma `pg_advisory_xact_lock(14, 26)`. Dos
administradores que se degradan o desactivan mutuamente a la vez quedan en fila: el segundo vuelve a contar los
administradores activos ya confirmados y recibe 409 si sería el último. La clave de dos enteros no coincide con los
bloqueos de una sola clave (`idUsuario`) del motor de alertas.

## Auditoría

Categoría de log `ClimaPlus.Seguridad` (la misma de los demás eventos de seguridad), sin correos ni secretos:

- `Seguridad: admin_rol_cambiado idAdmin= idUsuario= anterior= nuevo= ip=`
- `Seguridad: admin_estado_cambiado idAdmin= idUsuario= activo= ip=`
- `Seguridad: admin_rol_rechazado` / `admin_estado_rechazado` con `motivo=propio` o `motivo=ultimo_administrador`

No existe una tabla de auditoría en la base; estos eventos quedan en los logs de la API.
