# Contrato API – Clima y pronóstico (Fase 2E)

**Versión:** 1.1 (Fase 2H: `alertasGeneradas`) · **Fecha:** 2026-10-28 · **Rama:** `feature/backend`
**Responsable:** Integrante 2 (backend) · **Consumidor:** Integrante 3 (React)
**Decisiones aplicadas:** D-20 a D-27, E-02 y E-03 (aprobadas), C2 (sin índice UV), C5 (búsqueda solo por nombre).

## Estado de cada parte

| Parte | Ruta | Estado |
|---|---|---|
| Búsqueda de ubicaciones (M01) | `GET /api/clima/ubicaciones` | **Verificado** (pruebas y OpenWeather real; ver INC-01 para Swagger UI) |
| Cliente de OpenWeather, validación y normalización | — | **Verificado** (pruebas automatizadas) |
| Clima actual (M04) | `GET /api/clima/actual?idUbicacion=` | **Verificado** (pruebas y OpenWeather real, Fase 2E) |
| Pronóstico de 5 días (M05) | `GET /api/clima/pronostico?idUbicacion=` | **Verificado** |
| Alertas al consultar el clima actual + campo `alertasGeneradas` | `GET /api/clima/actual` | **Implementado, pendiente de verificar** (Fase 2H; motor en `contrato-alertas-generadas.md`) |
| Índice UV | — | **No implementado** (C2: requiere One Call 3.0, de pago) |

E-02 y E-03 están aplicadas en `climaplus_db` (2026-10-28): cada consulta se guarda en `consulta_clima` antes de mostrarse (guía, sección 4), con `id_usuario` NULL para visitantes y los datos extra o el pronóstico en `detalle`.

## Reglas comunes

- **Base:** `http://localhost:5208` en desarrollo (ver `launchSettings.json`).
- **Acceso:** público (`[AllowAnonymous]`). El frontend puede enviar o no el token.
  - Sin encabezado `Authorization` → se atiende como visitante.
  - Con token válido → se atiende igual (en la integración final, la consulta quedará en el historial del usuario).
  - **Con token inválido, expirado o de una cuenta desactivada → 401** (D-21). El frontend debe cerrar la sesión y repetir la petición sin token si quiere continuar como visitante.
- **Rate limiting:** 30 peticiones por minuto por IP, compartidas por todas las rutas `/api/clima/*` (D-23). Al superarlo: 429 con encabezado `Retry-After` (segundos).
- **Errores:** siempre `application/problem+json` (RFC 9457). Los mensajes son genéricos: nunca incluyen datos de OpenWeather ni de la configuración.
- **Unidades:** °C, m/s, %, hPa. Descripciones en español (`lang=es`). Fechas en UTC con formato ISO 8601 (`2026-10-27T18:20:00Z`).
- **Seguridad de la API key:** la clave solo existe en el backend (User Secrets `OpenWeather:ApiKey`). Nunca se envía al frontend, no aparece en errores y no se escribe en los logs.

### Errores del proveedor (D-24)

| Situación | HTTP | `title` |
|---|---|---|
| OpenWeather responde 401/403 (clave inválida), 429 (cuota), 5xx, o falla la red | 503 | Servicio no disponible |
| Falta `OpenWeather:ApiKey` en el servidor | 503 | Servicio no disponible |
| OpenWeather no responde en 8 segundos | 504 | Tiempo de espera agotado |
| JSON mal formado, incompleto o con valores fuera de rango | 502 | Respuesta inválida del proveedor |
| OpenWeather responde 404 | 404 | No encontrado |

No hay reintentos automáticos. El frontend puede ofrecer un botón "Reintentar" para 502, 503 y 504.

---

## 1. `GET /api/clima/ubicaciones` – Buscar ciudades (M02)

**Estado:** implementado, pendiente de verificar.

Busca por nombre en OpenWeather Geocoding, guarda o reutiliza cada resultado en la tabla `ubicacion` y responde con los registros guardados. El `idUbicacion` devuelto es el que usarán clima, pronóstico, favoritos y alertas.

### Parámetros (query string)

| Parámetro | Tipo | Obligatorio | Reglas |
|---|---|---|---|
| `q` | string | Sí | 2 a 100 caracteres. Letras (con tildes y ñ), dígitos, espacios, `.` `,` `'` `-`. Debe empezar con letra o dígito. Admite `Coban`, `Cobán`, `San José`, `Coban,GT`. |
| `limite` | int | No | 1 a 5. Por defecto 5. |

### Ejemplo

```http
GET /api/clima/ubicaciones?q=Coban&limite=5
```

**200 OK**

```json
[
  {
    "idUbicacion": 12,
    "nombre": "Cobán",
    "region": "Alta Verapaz",
    "pais": "GT",
    "latitud": 15.4708,
    "longitud": -90.3708
  }
]
```

| Campo | Tipo | Notas |
|---|---|---|
| `idUbicacion` | number (int64) | Identificador en la tabla `ubicacion`. |
| `nombre` | string | Nombre en español si OpenWeather lo tiene (`local_names.es`); si no, el nombre principal. Máx. 100. |
| `region` | string \| null | Departamento o estado. Sirve para distinguir ciudades homónimas. |
| `pais` | string | Código ISO 3166-1 alfa-2 en mayúsculas. |
| `latitud`, `longitud` | number | Redondeadas a 4 decimales (D-07). |

- Sin coincidencias → **200 con `[]`** (no es un error). Sugerencia de UI: "No se encontraron ciudades con ese nombre".
- El orden es el de OpenWeather (relevancia).
- Los resultados que no cumplen las reglas de `ubicacion` (país inválido, coordenadas fuera de rango, sin nombre) se descartan.
- Dos resultados que, redondeados, tienen las mismas coordenadas se devuelven una sola vez.
- Los textos vienen de OpenWeather: el frontend debe mostrarlos como texto (React lo hace por defecto; no usar `dangerouslySetInnerHTML`).

### Errores

| HTTP | Cuándo |
|---|---|
| 400 | `q` vacío, muy corto o largo, con símbolos no permitidos (`<`, `>`, `;`…), o `limite` fuera de 1–5. `errors.q` o `errors.limite` traen el mensaje. |
| 401 | Token inválido o expirado (D-21). |
| 404 / 429 / 502 / 503 / 504 | Ver las tablas anteriores. |

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "q": [ "La búsqueda debe tener entre 2 y 100 caracteres." ] }
}
```

> **Swagger UI:** ver INC-01 en `incidencias-pendientes.md`. Swagger UI no interpreta bien el patrón Unicode de `q`; el backend sí. Para probar esta ruta, usar el archivo `.http`, Postman o el navegador.

### Comportamiento interno

1. Valida la entrada y normaliza los espacios.
2. Si la misma búsqueda (sin distinguir mayúsculas) se hizo en la última hora, reutiliza el resultado de Geocoding desde la caché en memoria (máx. 500 búsquedas). Si no, llama a Geocoding.
3. Valida cada resultado y redondea las coordenadas.
4. Por cada resultado, busca la ubicación por `(latitud, longitud)`; si no existe, la inserta. Si dos peticiones la insertan al mismo tiempo, `uq_ubicacion_latitud_longitud` rechaza la segunda y se lee la fila existente.
5. Responde con las filas leídas de la base de datos.

Una ubicación ya guardada no se modifica aunque OpenWeather devuelva después otro nombre.

---

## 2. `GET /api/clima/actual?idUbicacion={id}` – Clima actual (M03/M04)

**Estado:** verificado (Fase 2E). El campo `alertasGeneradas` y la evaluación de alertas (Fase 2H) están pendientes de verificar.

### Parámetros

| Parámetro | Reglas |
|---|---|
| `idUbicacion` | Obligatorio, entero ≥ 1. Sale de `GET /api/clima/ubicaciones` o de un favorito. No se aceptan coordenadas ni nombres (D-20, D-22) |

Con o sin token. Con token válido, la consulta queda en el historial del usuario; sin token, se guarda con `id_usuario` NULL y no aparece en ningún historial (E-02). Token inválido o vencido → 401 (D-21).

### Flujo (D-23)

1. Valida `idUbicacion`; si la ubicación no existe → 404, sin llamar a OpenWeather.
2. Busca datos vigentes: una consulta ACTUAL de esa ubicación cuyos datos se pidieron a OpenWeather **hace menos de 10 minutos** (de cualquier usuario o visitante). La antigüedad se mide desde la llamada a OpenWeather, no desde la última vez que se reutilizaron.
3. Si no hay, llama a OpenWeather (`/data/2.5/weather`, `units=metric`, `lang=es`), valida y normaliza.
4. **Guarda una fila nueva** en `consulta_clima` para esta petición (usuario del token o NULL), también cuando los datos vienen de la caché. Así cada consulta queda registrada para el historial y las estadísticas.
5. **Solo con sesión (Fase 2H):** en la misma transacción que el paso 4, evalúa las reglas de alerta del usuario con los valores guardados y crea las alertas que correspondan (ver `contrato-alertas-generadas.md`, "Motor de alertas"). También cuando los datos vienen de la caché. Sin sesión nunca se evalúan alertas (RN-BD-42).
6. Responde con la fila leída de la base de datos. `consulta.desdeCache` indica si se reutilizaron datos; `alertasGeneradas`, las alertas creadas por esta petición.

Nunca se expone el id ni el usuario de la consulta reutilizada: cada respuesta trae el id de su propia fila.

### Respuesta – 200 OK

```json
{
  "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
  "consulta": { "idConsulta": 345, "fechaConsultaUtc": "2026-10-27T18:21:04Z", "desdeCache": false },
  "temperatura": 22.46,
  "sensacionTermica": 22.1,
  "humedad": 70,
  "velocidadViento": 3.1,
  "codigoCondicion": 802,
  "descripcion": "nubes dispersas",
  "detalle": {
    "version": 1,
    "observadoEnUtc": "2026-10-27T18:20:00Z",
    "zonaHorariaSegundos": -21600,
    "esDeDia": true,
    "temperaturaMin": 16.0,
    "temperaturaMax": 25.0,
    "presionHpa": 1018,
    "direccionVientoGrados": 45,
    "amanecerUtc": "2026-10-27T11:58:00Z",
    "atardecerUtc": "2026-10-27T23:41:00Z"
  },
  "alertasGeneradas": [
    { "idAlerta": 91, "tipo": "TEMPERATURA_ALTA", "mensaje": "Cobán: 33.4 C supera tu umbral de 32 C", "fechaGeneracion": "2026-10-27T18:21:04.512345Z" }
  ]
}
```

- **`alertasGeneradas` (Fase 2H, campo nuevo; los demás no cambian):** siempre presente. Es `[]` sin sesión, sin reglas que se cumplan o cuando el anti-duplicado de 60 minutos las bloquea. Cada elemento trae `idAlerta`, `tipo`, `mensaje` y `fechaGeneracion` (UTC). M04 muestra un toast "Nueva alerta: {mensaje}" por cada elemento y actualiza la campana; el detalle completo está en `GET /api/alertas`. El pronóstico no tiene este campo (nunca genera alertas).
- `temperatura`, `sensacionTermica`, `humedad`, `velocidadViento`, `codigoCondicion` y `descripcion` son siempre obligatorios. Si OpenWeather no los envía o están fuera de rango, la respuesta es 502 (no se inventan valores).
- Rangos (iguales a los CHECK de `consulta_clima`): temperatura −90 a 60; sensación −90 a 70; humedad 0 a 100; viento 0 a 999,99; código 200 a 899; descripción hasta 100 caracteres.
- En `detalle`, los campos opcionales pueden ser `null` si OpenWeather no los envía o están fuera de rango (presión entre 1 y 1999; dirección del viento entre 0 y 360).
- **Hora local de la ubicación** = valor UTC + `zonaHorariaSegundos`.
- **Icono:** el frontend puede elegirlo con `codigoCondicion` y `detalle.esDeDia` (grupos de OpenWeather: 2xx tormenta, 3xx llovizna, 5xx lluvia, 6xx nieve, 7xx atmósfera, 800 despejado, 80x nubes).
- **"Actualizado hace X min" (M04):** usar `detalle.observadoEnUtc` (hora de la observación de OpenWeather); `consulta.fechaConsultaUtc` es la hora de esta consulta.
- **Sin índice UV** (C2): requiere One Call 3.0, de pago. El mockup provisional del frontend muestra "uv"; debe quitarse o mostrarse como no disponible.
- **Viento:** en m/s (`units=metric`). Para mostrar km/h, multiplicar por 3,6 en el frontend.

### Errores

| HTTP | Cuándo |
|---|---|
| 400 | `idUbicacion` falta, no es entero o es ≤ 0 (`errors.idUbicacion`) |
| 401 | Token inválido, vencido o de una cuenta desactivada |
| 404 | La ubicación no existe, o OpenWeather respondió 404 |
| 429 | Más de 30 peticiones por minuto desde la misma IP en `/api/clima/*` |
| 502 / 503 / 504 | Ver "Errores del proveedor". En estos casos **no se guarda nada** y no se evalúan alertas |
| 500 | Error inesperado al guardar. Con sesión, la consulta y sus alertas se revierten juntas (transacción) |

---

## 3. `GET /api/clima/pronostico?idUbicacion={id}` – Pronóstico de 5 días (M05)

**Estado:** implementado, pendiente de verificar.

Mismos parámetros, autenticación, errores y flujo que el clima actual, con datos vigentes por **30 minutos** y `/data/2.5/forecast`. En `consulta_clima` se guarda como PRONOSTICO: columnas de medición en NULL y el pronóstico completo en `detalle` (RN-BD-43). El pronóstico **nunca** genera alertas (RN-BD-34).

Si los datos vienen de la caché, los días se calcularon cuando se pidieron a OpenWeather (hace menos de 30 minutos).

### Respuesta – 200 OK

```json
{
  "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
  "consulta": { "idConsulta": 346, "fechaConsultaUtc": "2026-10-27T20:00:10Z", "desdeCache": false },
  "zonaHorariaSegundos": -21600,
  "dias": [
    {
      "fecha": "2026-10-27",
      "completo": false,
      "temperaturaMax": 17.0,
      "temperaturaMin": 15.0,
      "codigoCondicion": 800,
      "descripcion": "cielo claro",
      "probabilidadLluvia": 20,
      "bloques": [
        {
          "fechaHoraUtc": "2026-10-27T21:00:00Z",
          "temperatura": 15.0,
          "humedad": 60,
          "velocidadViento": 2.5,
          "codigoCondicion": 800,
          "descripcion": "cielo claro",
          "probabilidadLluvia": 0,
          "esDeDia": true
        }
      ]
    }
  ]
}
```

### Reglas de agrupación (D-25)

- Los bloques de 3 horas se agrupan por **fecha local de la ubicación**.
- Se incluyen hasta **5 días** desde el día local actual; los bloques sobrantes se descartan.
- `completo` = el día tiene los 8 bloques. El primer día normalmente es incompleto.
- `temperaturaMax` y `temperaturaMin` = máxima y mínima de las temperaturas de los bloques del día.
- `codigoCondicion` y `descripcion` del día = los del bloque más cercano a las 12:00 locales.
- `probabilidadLluvia` del día = máxima de los bloques, en % entero (0–100).
- Los bloques repetidos se eliminan y se ordenan por hora.
- Si un bloque tiene datos inválidos o falta la zona horaria, todo el pronóstico se rechaza (502).

---

## Pruebas automatizadas de esta fase

Archivos: `backend/ClimaPlus.Api.Tests/Clima/`. Todas usan un proveedor simulado (D-27): **ninguna llama a OpenWeather real** y la clave de pruebas es falsa.

| Archivo | Qué cubre |
|---|---|
| `UbicacionesTests.cs` | Guardado y lectura desde la BD; reutilización de ids; caché; redondeo y duplicados; descarte de resultados inválidos; lista vacía; validación de entradas (400 sin llamar al proveedor); traducción de errores 401/403/429/5xx/404, red, timeout y JSON inválido; la clave no aparece en respuestas ni logs; token inválido o de cuenta desactivada → 401; sin token y con token válido → 200; rate limiting → 429; sin clave → 503. |
| `ClimaActualTests.cs` | Visitante (`id_usuario` NULL) y usuario con sesión; respuesta igual a la fila guardada; parámetros enviados a OpenWeather; la consulta aparece como `ultimaConsulta` del favorito; caché de 10 min sin llamar al proveedor pero guardando otra fila; las copias no alargan la vigencia (11 min → nueva llamada); datos de otro usuario sin exponer su consulta; ubicación inexistente → 404; 7 parámetros inválidos → 400; errores 401/429/500/404, timeout y red → 503/404/504 sin guardar nada; respuestas inválidas o fuera de rango → 502 sin guardar; token inválido → 401; la clave no aparece en respuesta, BD ni logs; un PRONOSTICO no sirve de caché del ACTUAL. |
| `PronosticoTests.cs` | Fila PRONOSTICO sin mediciones y con días y bloques en `detalle`; usuario con sesión y ninguna alerta; caché de 30 min y nueva llamada a los 31; un ACTUAL no sirve de caché del pronóstico; pronósticos inválidos (sin bloques, sin zona horaria, humedad 150, todo en el pasado) → 502 sin guardar; proveedor caído → 503; ubicación inexistente → 404; parámetros inválidos → 400; cuenta desactivada → 401. |
| `ClimaTransformadorTests.cs` | Normalización del clima actual; rechazo por cada dato obligatorio o fuera de rango; recorte de descripción; valores opcionales; agrupación del pronóstico por fecha local (zonas negativa y positiva); máxima, mínima, mediodía y lluvia máxima; duplicados y desorden; rechazo por bloque inválido; claves JSON de `detalle`. |

**Limitación:** las pruebas de integración usan EF Core InMemory, que no aplica `UNIQUE` ni `CHECK`. El caso de inserción concurrente (violación de `uq_ubicacion_latitud_longitud`) solo se puede probar contra PostgreSQL; queda pendiente para la categoría de pruebas `PostgreSQL` con una base de datos aislada (D-27).

Las pruebas de `actual` y `pronostico` usan un reloj de pruebas (`RelojPruebas`, `TimeProvider`) para simular el paso del tiempo sin esperar.

## Verificación manual con OpenWeather real (Swagger)

1. `GET /api/clima/ubicaciones?q=Coban` (desde el archivo `.http` o el navegador, por INC-01) → anotar `idUbicacion`.
2. Sin autorizar: `GET /api/clima/actual?idUbicacion={id}` → 200, `desdeCache: false`.
3. Repetir antes de 10 min → 200, `desdeCache: true`, otro `idConsulta`.
4. Autorizar con un token y repetir → 200; en PostgreSQL la nueva fila tiene el `id_usuario` del token.
5. `GET /api/clima/pronostico?idUbicacion={id}` → 200 con 5 días (el primero suele ser incompleto).
6. `GET /api/clima/actual?idUbicacion=999999` → 404; `idUbicacion=0` → 400; con `Authorization: Bearer x` → 401.
