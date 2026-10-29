# Contrato API – Dispositivos ESP32 y lecturas (Fase 7)

**Versión:** 1.0 · **Fecha:** 2026-10-29 · **Rama:** `feature/fase7-dispositivos-lecturas`
**Pantallas:** M11 (Mis dispositivos), M12 (Detalle dispositivo), firmware del ESP32
**Requisitos:** RF-20 a RF-24. **Reglas:** RN-BD-03, RN-BD-23, RN-BD-27 a RN-BD-33; D-08, D-11, D-12; 07-seguridad-inicial.md sección 5.
**Base de datos:** sin migraciones nuevas. Usa `dispositivo` y `lectura_sensor` tal como están en `001_InitialCreate`.

## Estado

| Endpoint | Requisito | Estado |
|---|---|---|
| `GET /api/dispositivos` | RF-20 | Implementado, sin verificar en Windows |
| `POST /api/dispositivos` | RF-21 | Implementado, sin verificar en Windows |
| `GET /api/dispositivos/{id}` | RF-23 | Implementado, sin verificar en Windows |
| `PUT /api/dispositivos/{id}` | RF-22 | Implementado, sin verificar en Windows |
| `PATCH /api/dispositivos/{id}/estado` | RF-22 | Implementado, sin verificar en Windows |
| `GET /api/dispositivos/{id}/lecturas?pagina=` | RF-23 | Implementado, sin verificar en Windows |
| `GET /api/dispositivos/{id}/alertas?pagina=` | RF-23 | Implementado, sin verificar en Windows |
| `POST /api/lecturas` | RF-24 | Implementado, sin verificar en Windows |

Pruebas: 488 aprobadas, 0 omitidas, 0 fallidas en Linux con PostgreSQL 16 (400 anteriores + 88 nuevas). Falta confirmarlo en Windows.

## Reglas comunes de `/api/dispositivos`

- **Autenticación:** `Authorization: Bearer <token>`. Roles `USUARIO` y `ADMINISTRADOR` (dispositivos propios).
- **Aislamiento:** el dueño es el usuario del token; el cliente nunca envía `idUsuario`. Un dispositivo ajeno responde **404**, igual que uno inexistente.
- **Ubicación:** solo ids devueltos por `GET /api/clima/ubicaciones` (D-22), igual que en favoritos.
- **Nombre:** 1 a 100 caracteres; letras, números, espacios y `. _ - # ( )`. Se quitan los espacios sobrantes. Único por usuario (RN-BD-30): repetido → **409** con `errors.nombre`.
- **Sin borrado:** un dispositivo se desactiva (D-11). Desactivarlo revoca de inmediato su API Key.
- **Campos desconocidos** en el cuerpo → **400**.

## Objeto `Dispositivo`

```json
{
  "idDispositivo": 5,
  "nombre": "ESP32 Patio",
  "activo": true,
  "enLinea": true,
  "ultimaConexion": "2026-10-29T22:15:53.969139Z",
  "fechaCreacion": "2026-10-29T22:15:53.672513Z",
  "ubicacion": { "idUbicacion": 4, "nombre": "Cobán", "region": null, "pais": "GT", "latitud": 15.47, "longitud": -90.37 },
  "ultimaLectura": { "idLectura": 3, "fechaLectura": "2026-10-29T22:15:53.969139Z", "temperatura": 33.40, "humedad": 40.00, "lluvia": false, "luz": 90 }
}
```

- `enLinea` (DP-F7-1): `true` si la última lectura llegó hace **5 minutos o menos**. El firmware envía una por minuto.
- `ultimaConexion` y `ultimaLectura` son `null` si el dispositivo nunca envió datos ("Nunca conectado" en M11).
- Nunca incluye la API Key ni su hash.

## `POST /api/dispositivos` (RF-21)

Cuerpo: `{ "nombre": "ESP32 Patio", "idUbicacion": 4 }` → **201**, cabecera `Cache-Control: no-store`:

```json
{
  "dispositivo": { "...": "objeto Dispositivo" },
  "apiKey": "cpk_4RtzoMHR1XQiptshJCIBdEDyIRC_3qdRtmSpAN5GlS4",
  "aviso": "Guárdala ahora; no podrás verla de nuevo. Si la pierdes, registra el dispositivo otra vez."
}
```

- `apiKey` se muestra **una sola vez** (RN-BD-28). Es `cpk_` + 32 bytes aleatorios en Base64Url (47 caracteres). En la base solo se guarda su SHA-256 en hexadecimal (`api_key_hash`, único).
- Errores: 400 validación · 404 ubicación inexistente (`errors.idUbicacion`) · 409 nombre repetido.

## `PUT /api/dispositivos/{id}` y `PATCH /api/dispositivos/{id}/estado` (RF-22)

- PUT: `{ "nombre": "...", "idUbicacion": 4 }` → **200** con el Dispositivo. Las lecturas siguientes se evalúan con la ubicación nueva (RN-BD-33).
- PATCH: `{ "activo": false }` → **200**. Idempotente. Con `false`, el ESP32 recibe **403** en su siguiente lectura (RN-BD-29).

## `GET /api/dispositivos/{id}/lecturas` y `/alertas` (RF-23, M12)

Paginados de 20 en 20, los más recientes primero, con el formato `PaginaResponse` de siempre (`elementos`, `pagina`, `tamanoPagina`, `totalElementos`, `totalPaginas`).

- Lecturas: `{ idLectura, fechaLectura, temperatura, humedad, lluvia, luz }`. Las mediciones no enviadas son `null`.
- Alertas: el mismo objeto de `GET /api/alertas` (M09), solo las de origen `ESP32` de este dispositivo.

## `POST /api/lecturas` (RF-24) — lo usa el ESP32

**Autenticación:** cabecera `X-Device-Key: cpk_…`. No usa JWT: un token de usuario **no** sirve aquí, y la API Key no sirve en las demás rutas.

Cuerpo (todas las mediciones son opcionales, pero debe venir al menos una, RN-BD-31):

```json
{ "temperatura": 24.5, "humedad": 68, "lluvia": false, "luz": 72 }
```

| Campo | Tipo | Rango |
|---|---|---|
| `temperatura` | número, °C | −40 a 85; se guarda con 2 decimales |
| `humedad` | número, % | 0 a 100; se guarda con 2 decimales |
| `lluvia` | booleano | salida digital del sensor (D-08) |
| `luz` | entero, % | 0 a 100, normalizado por el firmware (D-08) |

**201**:

```json
{
  "idLectura": 3,
  "idDispositivo": 5,
  "fechaLectura": "2026-10-29T22:15:53.969139Z",
  "alertasGeneradas": [
    { "idAlerta": 3, "tipo": "TEMPERATURA_ALTA", "mensaje": "Cobán: 33.4 C supera tu umbral de 30 C", "fechaGeneracion": "2026-10-29T22:15:53.976259Z" }
  ]
}
```

- `fechaLectura` la pone el servidor (RN-BD-32); enviar `fechaLectura` u otro campo extra → **400**.
- En la misma transacción se guarda la lectura, se actualiza `dispositivo.ultima_conexion` (D-12) y se evalúan las alertas.
- `alertasGeneradas` le dice al ESP32 si debe encender el LED rojo y el buzzer (alertas físicas), sin otra petición.

**Alertas (RN-BD-33):** se usan las reglas del **dueño** del dispositivo para la **ubicación del dispositivo**, con el mismo motor de la consulta ACTUAL:

- TEMPERATURA_ALTA/BAJA solo si la lectura trae `temperatura`.
- LLUVIA solo con `lluvia: true`.
- La regla específica de la ubicación prevalece sobre la global (RN-BD-20).
- Anti-duplicado de 60 minutos **compartido** con las alertas de OpenWeather (RN-BD-27): si ya hubo una alerta del mismo tipo para esa ubicación en la última hora, no se repite.
- La alerta guarda `id_lectura` y deja `id_consulta` en NULL (RN-BD-23).

**Errores:**

| Código | Cuándo |
|---|---|
| 400 | Cuerpo inválido, fuera de rango, sin mediciones o con campos extra |
| 401 | Falta `X-Device-Key`, no empieza con `cpk_`, mide más de 128 caracteres o no corresponde a ningún dispositivo. Se comprueba **antes** de leer el cuerpo |
| 403 | Dispositivo desactivado, o cuenta del dueño desactivada |
| 429 | Más de 30 lecturas por minuto del mismo dispositivo (`RateLimiting:Dispositivos`), con `Retry-After` |

La API Key nunca se escribe en los logs; los rechazos se registran como `Seguridad: dispositivo_rechazado motivo=…`.

## Prueba manual sin hardware (PowerShell)

1. Inicia sesión en Swagger (`/api/auth/login`), pulsa **Authorize** y pega el token en **Bearer**.
2. `POST /api/dispositivos` con un `idUbicacion` existente. Copia el `apiKey` de la respuesta.
3. Simula al ESP32:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5208/api/lecturas `
  -Headers @{ "X-Device-Key" = "cpk_PEGA_AQUI_TU_CLAVE" } `
  -ContentType "application/json" `
  -Body '{"temperatura":24.5,"humedad":68,"lluvia":false,"luz":72}'
```

También se puede desde Swagger: **Authorize** → `DeviceKey` → pegar la clave → `POST /api/lecturas`.

## Decisiones de esta fase

| ID | Decisión |
|---|---|
| DP-F7-1 | "En línea" = última lectura hace ≤ 5 minutos. Se calcula al responder; no se guarda (D-11, D-12). |
| DP-F7-2 | La API Key se valida en un filtro de autorización (`[AutenticarDispositivo]`), antes de la validación del cuerpo: sin clave siempre 401. |
| DP-F7-3 | El dispositivo de una cuenta desactivada tampoco puede enviar lecturas (403), igual que su JWT deja de servir. |
| DP-F7-4 | `POST /api/lecturas` devuelve `alertasGeneradas` para las alertas físicas del ESP32. |
| DP-F7-5 | No hay "regenerar clave": si se pierde, se desactiva el dispositivo y se registra otro. |
| DP-F7-6 | El motor de alertas no cambió su comportamiento: se agregó `EvaluarLecturaAsync` y el guardado con anti-duplicado se comparte entre ambos orígenes. Las 400 pruebas anteriores siguen aprobadas. |
