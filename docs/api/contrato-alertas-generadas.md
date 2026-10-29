# Contrato API – Alertas generadas (M09, M15)

**Versión:** 1.1 (motor de alertas, Fase 2H) · **Fecha:** 2026-10-28 · **Rama:** `feature/backend`
**Responsable:** Integrante 2 (backend) · **Consumidor:** Integrante 3 (React)
**Pantallas:** M09 (Alertas), campana de la navbar y tarjeta de M06, M15 (Alertas del sistema, administrador)
**Requisitos:** RF-16 (generación), RF-17, RF-18 (usuario) y RF-29 (administrador); RN-BD-20 a RN-BD-27, RN-BD-34, RN-BD-35 y RN-BD-42.

## Estado

| Endpoint | Estado |
|---|---|
| `GET /api/alertas` | **Verificado** (221 pruebas) |
| `GET /api/alertas/contador` | **Verificado** |
| `PATCH /api/alertas/{idAlerta}/estado` | **Verificado** |
| `GET /api/admin/alertas` | **Verificado** |
| `GET /api/admin/alertas/{idAlerta}` | **Verificado** |
| Generación de alertas (motor), origen OpenWeather | **Implementado, pendiente de verificar** (Fase 2H; ver "Motor de alertas") |
| Generación de alertas, origen ESP32 | **No implementado** (fase de dispositivos, RF-24) |

Sin alertas, estas rutas responden con listas vacías y contador 0; el frontend muestra el estado vacío "Sin alertas por ahora" con el botón "Configurar alertas". No se crean alertas de ejemplo en la base de datos: solo las genera el motor.

## Reglas comunes

- JWT obligatorio. `/api/alertas/*`: USUARIO y ADMINISTRADOR, cada uno **solo sus propias alertas**. `/api/admin/alertas/*`: solo ADMINISTRADOR (un USUARIO recibe 403).
- Una alerta de otro usuario responde 404 en las rutas de usuario.
- Errores en `application/problem+json`; filtros inválidos → 400 con `errors.<parámetro>`.
- Orden: de la más reciente a la más antigua (`fechaGeneracion`).
- **Paginación:** 20 elementos por página, fija (08-mockups.md, "20 filas por página"). Parámetro `pagina` (1 por defecto, de 1 a 100000). Una página más allá del total responde 200 con `elementos: []`.

## Objeto `Alerta`

```json
{
  "idAlerta": 51,
  "tipo": "TEMPERATURA_ALTA",
  "mensaje": "Cobán: 33.4 °C supera tu umbral de 32 °C",
  "estado": "NUEVA",
  "valorDetectado": 33.4,
  "valorUmbral": 32.0,
  "fechaGeneracion": "2026-10-28T14:32:10Z",
  "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
  "origen": {
    "tipo": "OPENWEATHER",
    "idConsulta": 345,
    "idLectura": null,
    "idDispositivo": null,
    "nombreDispositivo": null
  }
}
```

| Campo | Notas |
|---|---|
| `tipo` | `LLUVIA`, `TEMPERATURA_ALTA` o `TEMPERATURA_BAJA`. Color del borde de la AlertCard según el tipo. |
| `mensaje` | Texto ya redactado por el backend (máx. 255, RN-BD-35). Mostrar como texto, nunca como HTML. |
| `estado` | `NUEVA` o `LEIDA`. |
| `valorDetectado`, `valorUmbral` | °C. `null` en alertas de lluvia (RN-BD-24). |
| `origen.tipo` | `OPENWEATHER` (consulta de clima actual) o `ESP32` (lectura del dispositivo). |
| `origen.idDispositivo`, `nombreDispositivo` | Solo en `ESP32`; sirven para el enlace a M12. |

## Usuario

### `GET /api/alertas?estado=&tipo=&pagina=`

| Parámetro | Valores | Por defecto |
|---|---|---|
| `estado` | `NUEVA`, `LEIDA` (omitido = todas) | todas |
| `tipo` | `LLUVIA`, `TEMPERATURA_ALTA`, `TEMPERATURA_BAJA` | todos |
| `pagina` | 1 a 100000 | 1 |

**200**

```json
{
  "elementos": [ /* Alerta */ ],
  "pagina": 1,
  "tamanoPagina": 20,
  "totalElementos": 0,
  "totalPaginas": 0
}
```

**400** filtro inválido (por ejemplo `estado=leida` en minúsculas, `pagina=0`) · **401**.

### `GET /api/alertas/contador`

**200** `{ "nuevas": 0 }`: alertas en estado `NUEVA` del usuario. Para la campana de la navbar y la tarjeta "alertas nuevas" de M06. **401**.

### `PATCH /api/alertas/{idAlerta}/estado`

```json
{ "estado": "LEIDA" }
```

- **200** con la alerta actualizada. Solo cambia `estado` (RN-BD-26). Es **idempotente**: marcar otra vez una alerta leída responde 200 igual.
- **400** si `estado` falta, no es exactamente `LEIDA` (no se puede volver a `NUEVA`) o hay otros campos.
- **404** si la alerta no existe o es de otro usuario. **401**.

Después de marcarla, el frontend puede volver a pedir `/api/alertas/contador` o restar 1 localmente.

## Administrador (M15, solo lectura)

### `GET /api/admin/alertas?tipo=&estado=&origen=&desde=&hasta=&idUsuario=&pagina=`

| Parámetro | Valores |
|---|---|
| `tipo`, `estado`, `pagina` | Igual que en `/api/alertas` |
| `origen` | `OPENWEATHER` o `ESP32` |
| `desde` | Fecha y hora ISO 8601 con zona, **incluida**. Ejemplo para Guatemala: `2026-10-01T00:00:00-06:00` (codificar `:` y `+` en la URL) |
| `hasta` | Igual formato, **excluida**. Para "hasta el 31 de octubre inclusive", enviar `2026-11-01T00:00:00-06:00` |
| `idUsuario` | Filtro "usuario" de M15 (entero ≥ 1). El buscador por nombre/correo vendrá con la gestión de usuarios (RF-26) |

**200** con la misma estructura de página; cada elemento añade:

```json
"usuario": { "idUsuario": 7, "nombre": "Ana López", "correo": "ana@ejemplo.com" }
```

**400** filtro inválido, fecha no ISO o `desde` ≥ `hasta` (`errors.desde`) · **401** · **403** si no es administrador.

### `GET /api/admin/alertas/{idAlerta}`

Detalle para el modal de M15 (mensaje completo). **200** · **404** · **401** · **403**.

M15 no tiene acciones de escritura: el administrador no cambia el estado de alertas ajenas.

## Motor de alertas (Fase 2H, RF-16)

**Cuándo se ejecuta.** Solo dentro de `GET /api/clima/actual` **con sesión** (D-02). No hay tareas en segundo plano ni evaluación periódica de favoritos: si el usuario no consulta, no hay alertas. Nunca se evalúa con el pronóstico (RN-BD-34) ni con consultas anónimas (RN-BD-42). El motor **no llama a OpenWeather**: usa los valores ya validados y guardados en la consulta.

**Qué reglas aplica.**

1. Reglas del usuario del token, `activa = true`, de una cuenta activa (RN-BD-22), para la ubicación consultada o globales.
2. Primero se descartan las inactivas; después, por tipo, la específica de la ubicación prevalece sobre la global (RN-BD-20). Una específica **pausada no bloquea** la global activa (DP-2). Una global aplica a **cualquier** ubicación que el usuario consulte (DP-1).
3. Condiciones de disparo (D-10, `diccionario-datos.md`):

| Tipo | Dispara si | `valorDetectado` / `valorUmbral` | Mensaje |
|---|---|---|---|
| `TEMPERATURA_ALTA` | `temperatura` **>** umbral (igual no dispara) | temperatura / umbral | `Cobán: 33.4 C supera tu umbral de 32 C` |
| `TEMPERATURA_BAJA` | `temperatura` **<** umbral (igual no dispara) | temperatura / umbral | `Cobán: 8.5 C está por debajo de tu umbral de 10 C` |
| `LLUVIA` | `codigoCondicion` en 200–399 o 500–599 (tormenta, llovizna, lluvia) | `null` / `null` (RN-BD-24) | `Lluvia detectada en Cobán` |

   Se compara la **temperatura** real, nunca la sensación térmica. Números con punto decimal y sin ceros sobrantes (`32`, `33.4`, `-5.25`). El nombre de la ubicación tiene hasta 100 caracteres, así que el mensaje nunca supera 255 (RN-BD-35).
4. Si en una consulta se cumplen alta y baja (posible con una global y una específica, P-2), se generan **ambas** (DP-9). Una consulta puede generar hasta 3 alertas, en el orden alta, baja, lluvia.

**Anti-duplicado (RN-BD-27, D-09).** No se crea otra alerta del mismo usuario, ubicación y tipo si ya existe una con `fechaGeneracion` de **menos de 60 minutos**, sea `NUEVA` o `LEIDA`. A los 60 minutos exactos ya se permite (DP-8). Una alerta omitida por duplicado solo deja una línea en el log; la consulta se guarda igual.

**Qué se guarda.** `idConsulta` = la consulta ACTUAL de esta misma petición y del mismo usuario (RN-BD-23, RN-BD-42); `idLectura` NULL; `estado` `NUEVA` (RN-BD-25); `fechaGeneracion` en UTC desde `TimeProvider`, con precisión de microsegundos. Las alertas nunca se editan para "resolverlas": si la condición deja de cumplirse no pasa nada, y si se mantiene se genera otra cuando termina la ventana (RN-BD-26).

**Caché.** También se evalúa cuando los datos vienen de la caché de 10 minutos (DP-5): cada petición es una consulta ACTUAL real del usuario.

**Atomicidad y concurrencia (DP-6, DP-7).** Con sesión, la consulta y sus alertas se guardan en **una transacción**: si falla algo, no queda ninguna de las dos (500). Dentro de la transacción se toma `pg_advisory_xact_lock(id_usuario)` antes de leer las alertas recientes, así que dos peticiones simultáneas del mismo usuario se ordenan y la segunda ve la alerta de la primera. El bloqueo se libera solo con el `COMMIT` o `ROLLBACK`. Con EF Core InMemory (pruebas) no hay transacción ni bloqueo; la concurrencia real se prueba con PostgreSQL. No hizo falta ninguna migración: el índice `ix_alerta_usuario_fecha` cubre la búsqueda del anti-duplicado.

**Errores de OpenWeather.** Si el proveedor falla o responde datos inválidos, la petición termina con 502/503/504 **antes** de guardar: no hay consulta ni alerta (no se generan alertas falsas).

**Seguridad.** El usuario de la alerta sale siempre del token; no hay endpoints nuevos de escritura. `alertasGeneradas` solo contiene alertas de esta petición. El log registra ids y tipo, nunca el mensaje, umbrales, datos meteorológicos ni la API key.

**Respuesta para M04.** `GET /api/clima/actual` incluye `alertasGeneradas: [{ idAlerta, tipo, mensaje, fechaGeneracion }]` (ver `contrato-clima-pronostico.md`).

**Limitaciones del alcance.** Origen ESP32 (RN-BD-33) pendiente de la fase de dispositivos. Sin notificaciones externas (fuera de alcance). Sin estados distintos de `NUEVA`/`LEIDA`.

## Pruebas automatizadas

`backend/ClimaPlus.Api.Tests/Alertas/AlertasTests.cs`, 18 métodos / 39 casos. Las alertas se crean **solo en la base InMemory de las pruebas**, para simular las que generará el motor:

- 401 sin token en las 5 rutas y con cuenta desactivada; 403 para USUARIO en las rutas de administración.
- Sin alertas: página vacía con `totalElementos` 0 y contador 0.
- Solo alertas propias, orden descendente, todos los campos, valores `null` en lluvia, sin datos de otros usuarios.
- Origen ESP32 con dispositivo.
- Filtros por estado y tipo, combinados; paginación de 20 (25 alertas → 20 + 5 + página vacía).
- Filtros inválidos → 400 con el parámetro (8 casos).
- Contador: solo `NUEVA` y solo propias.
- Marcar como leída: solo cambia el estado, idempotente, baja el contador; cuerpos inválidos → 400 sin cambios (6 casos); alerta ajena o inexistente → 404.
- Administrador: ve alertas de todos con el destinatario y sin datos sensibles; filtros por usuario, origen, tipo, estado y rango de fechas (con zona horaria, `desde` incluido y `hasta` excluido); filtros inválidos → 400 (6 casos); detalle 200/404; en `/api/alertas` solo ve las suyas.

Motor (Fase 2H):

- `Alertas/EvaluadorAlertasTests.cs` (unitarias, sin BD): `>` y `<` estrictos con igualdad exacta sin alerta; códigos límite de lluvia (200, 299, 300, 399, 500, 599 sí; 600, 701, 800, 804 no); valores y mensajes de cada tipo; formato numérico independiente de la cultura; sin reglas; inactivas; global; específica sobre global; específica pausada con global activa; otra ubicación; prioridad por tipo; alta y baja simultáneas; mensajes ≤ 255; ventana de 59:59.999 / 60:00 / 61 minutos.
- `Alertas/MotorAlertasTests.cs` (integración con `GET /api/clima/actual` y OpenWeather simulado): visitante; usuario sin reglas; reglas ajenas; reglas inactivas; alta con consulta de origen y todos los campos; baja; lluvia con NULL; igualdad; sensación térmica no se compara; global en varias ubicaciones; específica sobre global; específica pausada; tres alertas en una consulta; duplicado a los 59 y nueva a los 60 minutos; duplicado con la anterior `LEIDA`; independencia por usuario, ubicación y tipo; evaluación desde la caché; pronóstico sin alertas; 5 errores del proveedor sin consulta ni alerta; dueño, contador, otro usuario (404) y administrador; campos de la respuesta, sin API key ni mensajes en el log.
- `Datos/MotorAlertasPostgreSqlTests.cs` (solo con `CLIMAPLUS_TEST_DB` apuntando a una base `*_test`): las alertas cumplen los CHECK y el anti-duplicado se traduce a SQL; bloqueo sin transacción → error; 5 peticiones simultáneas del mismo usuario → una sola alerta.

**Diferencias con la auditoría:** RF-18 proponía `PATCH /api/alertas/{id}/leida`; se implementó `PATCH /api/alertas/{id}/estado` con `{ "estado": "LEIDA" }`, como se pidió en la autorización de esta fase.
