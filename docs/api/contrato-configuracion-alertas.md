# Contrato API – Configuración de alertas (Fase 2H)

**Versión:** 1.0 · **Fecha:** 2026-10-27 · **Rama:** `feature/backend`
**Responsable:** Integrante 2 (backend) · **Consumidor:** Integrante 3 (React) · **Pantalla:** M10 (Configuración de alertas); acceso desde M07 con la ubicación preseleccionada
**Reglas:** RN-BD-16 a RN-BD-19, RN-BD-21 (validadas aquí), RN-BD-20 y RN-BD-22 (las aplicará el motor de alertas), D-10 (sin columna operador).

## Estado

| Endpoint | Estado |
|---|---|
| `GET /api/configuraciones-alerta` | **Implementado, pendiente de verificar** |
| `GET /api/configuraciones-alerta/{idConfiguracion}` | **Implementado, pendiente de verificar** |
| `POST /api/configuraciones-alerta` | **Implementado, pendiente de verificar** |
| `PUT /api/configuraciones-alerta/{idConfiguracion}` | **Implementado, pendiente de verificar** |
| `PATCH /api/configuraciones-alerta/{idConfiguracion}/estado` | **Implementado, pendiente de verificar** |
| `DELETE /api/configuraciones-alerta/{idConfiguracion}` | **Implementado, pendiente de verificar** |
| Generación de alertas a partir de estas reglas | **No implementado** (ver "Dependencias del motor de alertas") |

"Pendiente de verificar" = falta la compilación y ejecución de pruebas en Windows.

## Reglas comunes

- **Autenticación obligatoria** (`Authorization: Bearer <token>`). Roles `USUARIO` y `ADMINISTRADOR` (el administrador gestiona las reglas de su propia cuenta).
- **Aislamiento:** el dueño es el usuario del token; el cliente nunca envía `idUsuario` (si lo envía → 400). Una regla de otro usuario responde **404**, igual que una inexistente.
- **Errores:** `application/problem+json`. Los errores de campo vienen en `errors.<campo>` (400 y 409).
- **Fechas:** UTC ISO 8601. **Umbrales:** °C.

## Semántica de los tipos (D-10, diccionario de datos)

| `tipo` | Avisa cuando | `valorUmbral` |
|---|---|---|
| `TEMPERATURA_ALTA` | la temperatura medida **supera** el umbral (`>`) | Obligatorio, −50 a 60, máx. 2 decimales |
| `TEMPERATURA_BAJA` | la temperatura medida **baja** del umbral (`<`) | Obligatorio, −50 a 60, máx. 2 decimales |
| `LLUVIA` | OpenWeather informa lluvia (códigos 2xx, 3xx o 5xx) o el ESP32 detecta lluvia | **No se envía** (omitido o `null`) |

Los valores de `tipo` son exactamente esos textos en mayúsculas.

## Alcance de la regla

- `idUbicacion: null` (u omitido) → **regla global**: "Todas mis ubicaciones".
- `idUbicacion: <id>` → solo esa ubicación. El id sale de `GET /api/clima/ubicaciones` o de un favorito (`favorito.ubicacion.idUbicacion`).
- Pueden coexistir una regla global y reglas específicas del mismo tipo. Al evaluar, **la específica prevalece** sobre la global (RN-BD-20; lo aplicará el motor de alertas).

## Objeto `ConfiguracionAlerta`

```json
{
  "idConfiguracion": 4,
  "tipo": "TEMPERATURA_ALTA",
  "ubicacion": {
    "idUbicacion": 12,
    "nombre": "Cobán",
    "region": "Alta Verapaz",
    "pais": "GT",
    "latitud": 15.4708,
    "longitud": -90.3708
  },
  "valorUmbral": 30.5,
  "activa": true,
  "fechaCreacion": "2026-10-27T22:10:05.312Z"
}
```

Para M10: si `ubicacion` es `null` mostrar "Todas mis ubicaciones"; si `valorUmbral` es `null` mostrar "—".

---

## `GET /api/configuraciones-alerta`

Lista las reglas del usuario, de la más reciente a la más antigua. **200** con un arreglo (vacío si no hay). **401**.

## `GET /api/configuraciones-alerta/{idConfiguracion}`

**200** · **404** (no existe o es de otro usuario) · **401**.

## `POST /api/configuraciones-alerta`

```json
{ "tipo": "TEMPERATURA_BAJA", "idUbicacion": 12, "valorUmbral": 8.5, "activa": true }
```
```json
{ "tipo": "LLUVIA", "idUbicacion": null }
```

| Campo | Obligatorio | Reglas |
|---|---|---|
| `tipo` | Sí | `LLUVIA`, `TEMPERATURA_ALTA` o `TEMPERATURA_BAJA` |
| `idUbicacion` | No | `null` = global; si se envía, entero ≥ 1 de una ubicación existente |
| `valorUmbral` | Según el tipo | Ver tabla de tipos |
| `activa` | No | Por defecto `true` |

No se admiten otros campos.

**201** con encabezado `Location: /api/configuraciones-alerta/{id}` y la regla en el cuerpo.

## `PUT /api/configuraciones-alerta/{idConfiguracion}`

Edición desde el modal de M10. Reemplaza **todos** los campos editables: `tipo`, `idUbicacion`, `valorUmbral` y `activa` (aquí `activa` es **obligatorio**). Enviar `idUbicacion: null` convierte la regla en global.

**200** con la regla actualizada. Mismos errores que POST, más **404** si la regla no existe o es de otro usuario.

## `PATCH /api/configuraciones-alerta/{idConfiguracion}/estado`

Switch "activar/pausar" de M10. Solo cambia `activa`.

```json
{ "activa": false }
```

**200** con la regla · **400** si falta `activa`, no es booleano o hay otros campos · **404** · **401**.

Una regla pausada se conserva pero no generará alertas (RN-BD-22).

## `DELETE /api/configuraciones-alerta/{idConfiguracion}`

**204** eliminada · **404** · **401**. Las alertas ya generadas no se borran (guardan su propio umbral, RN-BD-24). Después de eliminarla se puede crear otra del mismo tipo y ubicación. M10 pide confirmación antes de llamar.

---

## Errores de validación y negocio

| HTTP | Campo | Causa | Mensaje |
|---|---|---|---|
| 400 | `tipo` | Falta o no es uno de los tres valores | "El tipo debe ser LLUVIA, TEMPERATURA_ALTA o TEMPERATURA_BAJA." |
| 400 | `valorUmbral` | Falta en un tipo de temperatura | "El umbral es obligatorio para las alertas de temperatura." |
| 400 | `valorUmbral` | Fuera de −50 a 60 (RN-BD-18) | "El umbral debe estar entre -50 y 60 °C." |
| 400 | `valorUmbral` | Más de 2 decimales | "El umbral admite como máximo 2 decimales." |
| 400 | `valorUmbral` | Enviado con `LLUVIA` (RN-BD-17) | "La alerta de lluvia no usa umbral; envía valorUmbral vacío." |
| 400 | `idUbicacion` | 0 o negativo | "La ubicación no es válida." |
| 400 | `activa` | Falta en PUT o PATCH | "Indica si la regla está activa." |
| 400 | — | JSON mal formado, tipos incorrectos o campos de más | — |
| 404 | — | `idUbicacion` no existe | "La ubicación no existe. Búscala de nuevo para usarla." |
| 409 | `tipo` | Ya existe una regla de ese tipo para esa ubicación, o ya existe la regla global de ese tipo (RN-BD-19) | "Ya tienes una regla de ese tipo para esta ubicación." |
| 409 | `valorUmbral` | Alta ≤ baja en la misma ubicación (RN-BD-21) | "El umbral de temperatura alta debe ser mayor que el de temperatura baja (X °C) para esta ubicación." (o el inverso para baja) |

### Cómo se aplica RN-BD-21

- Se comparan la regla `TEMPERATURA_ALTA` y la `TEMPERATURA_BAJA` del usuario con el **mismo** `idUbicacion` (las dos de la misma ubicación, o las dos globales). La alta debe ser **estrictamente mayor**.
- También cuentan las reglas pausadas, para que reactivarlas nunca deje una combinación inválida.
- **No** se compara una regla global con una específica (ver pendiente P-2).

---

## Pruebas automatizadas

`backend/ClimaPlus.Api.Tests/Alertas/ConfiguracionesAlertaTests.cs` (EF Core InMemory), 30 métodos / 61 casos:

- **Seguridad:** 401 sin token en las 6 rutas y con cuenta desactivada; otro usuario no puede ver, editar, pausar ni eliminar (404) y la regla no cambia; `idUsuario` en el cuerpo → 400; reglas iguales en dos usuarios son independientes; el administrador gestiona sus reglas.
- **Crear y listar:** lluvia global sin umbral y activa por defecto; temperatura con ubicación y `activa: false`; límites −50, 0, −12,75 y 60; lista vacía y orden.
- **Validación:** 14 casos de campo (tipo vacío, desconocido, en minúsculas, con SQL o `<script>`; umbral ausente, null, fuera de rango, con 3 decimales o enviado con lluvia; ubicación 0 o negativa); JSON mal formado, tipos incorrectos y campo `operador` → 400; ubicación inexistente → 404.
- **RN-BD-19:** duplicado específico y duplicado global → 409; global y específica del mismo tipo conviven.
- **RN-BD-21:** alta menor o igual que baja → 409; entre globales; ubicaciones distintas no se comparan; las reglas pausadas cuentan.
- **PUT:** cambia todos los campos; no choca consigo misma; cambiar a un tipo ya existente → 409 sin modificar la regla; romper RN-BD-21 → 409; datos inválidos → 400; inexistente → 404.
- **PATCH:** pausa y reactiva sin tocar los demás campos; cuerpos inválidos → 400.
- **DELETE:** 204, luego 404, y el tipo queda libre para una regla nueva; ids no numéricos → 404.

**Limitación:** InMemory no aplica `UNIQUE NULLS NOT DISTINCT` ni los CHECK. Estas reglas se validan antes en el servicio (cubierto por las pruebas) y PostgreSQL las vuelve a garantizar; si dos peticiones iguales llegan a la vez, el índice único responde 409. Comprobarlo contra PostgreSQL queda para la categoría de pruebas `PostgreSQL`.

---

## Dependencias del motor de alertas (resueltas en la Fase 2H)

El motor ya está implementado (`EvaluadorAlertas`, `MotorAlertas`; reglas completas en `contrato-alertas-generadas.md`, "Motor de alertas"). Las dependencias originales eran:

| # | Dependencia | Motivo |
|---|---|---|
| 1 | **E-02 y E-03 integradas en `main`** | Una alerta de OpenWeather se origina en una consulta `ACTUAL` guardada (`alerta.id_consulta`, RN-BD-23, RN-BD-34). Sin persistencia de `consulta_clima` no hay origen. |
| 2 | **Endpoint `GET /api/clima/actual`** | Es el punto donde se evalúan las reglas del usuario autenticado tras guardar la consulta. Las consultas anónimas (`id_usuario` NULL) nunca generan alertas. |
| 3 | **Reglas de evaluación ya aprobadas** | Solo reglas activas de usuarios activos (RN-BD-22); específica sobre global (RN-BD-20); `TEMPERATURA_ALTA` `>`, `TEMPERATURA_BAJA` `<`, `LLUVIA` con códigos 2xx/3xx/5xx; anti-duplicado de 60 min por usuario, ubicación y tipo sin importar el estado (RN-BD-27, D-09); valor detectado y umbral solo en las de temperatura (RN-BD-24); mensaje generado por el backend, máx. 255 (RN-BD-35); `PRONOSTICO` nunca genera alertas (RN-BD-34). |
| 4 | **Endpoints de alertas generadas (M09, M15)** | Listar, marcar como `LEIDA` (RN-BD-25, RN-BD-26) y contador de `NUEVA` para la campana y M06. Su contrato será `contrato-alertas-generadas.md`. |
| 5 | **Módulo de dispositivos y lecturas (ESP32)** | Para las alertas con origen `id_lectura` (RN-BD-33). Es otra fase. |

### Puntos abiertos para decidir (no se implementaron para no inventar requisitos)

- **P-1:** el anti-duplicado de 60 minutos es por *usuario + ubicación + tipo*. Si una regla global y una específica cubren la misma ubicación, solo se evalúa la específica (RN-BD-20), así que no hay doble alerta.
- **P-2:** RN-BD-21 dice "para una misma ubicación". Se implementó de forma literal (mismo `idUbicacion`). No se valida la combinación efectiva global + específica (por ejemplo, alta global 30 °C con baja de Cobán 35 °C). Si el equipo quiere validarla, hay que aprobarlo como regla.
- **P-3:** dos peticiones simultáneas pueden crear una alta y una baja que, juntas, incumplan RN-BD-21 (no hay restricción en la base de datos para eso). Es poco probable con un solo usuario; se documenta como riesgo aceptado.
