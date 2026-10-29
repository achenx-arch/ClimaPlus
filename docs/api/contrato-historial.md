# Contrato API – Historial de consultas (Fase 2G)

**Versión:** 1.0 · **Fecha:** 2026-10-28 · **Rama:** `feature/backend`
**Responsable:** Integrante 2 (backend) · **Consumidor:** Integrante 3 (React) · **Pantalla:** M08 (Historial)
**Reglas:** RF-11, RN-BD-13 (consulta con usuario opcional, E-02), RN-BD-43 (detalle de PRONOSTICO, E-03), 07-seguridad-inicial.md (dueño del recurso).

## Estado

| Endpoint | Estado |
|---|---|
| `GET /api/historial` | **Implementado, pendiente de verificar** |
| `GET /api/historial/{idConsulta}` | **Implementado, pendiente de verificar** |

Solo lectura. No hay endpoints para crear, editar ni borrar consultas: se registran solas al llamar a `GET /api/clima/actual` y `GET /api/clima/pronostico` con sesión (`contrato-clima-pronostico.md`).

## Reglas comunes

- **Autenticación obligatoria:** `Authorization: Bearer <token>`. Roles `USUARIO` y `ADMINISTRADOR`; el administrador ve **solo su propia cuenta** (08-mockups.md: M06–M12 para su cuenta). No existe un historial global ni por `idUsuario`.
- **Aislamiento:** el usuario sale siempre del token validado. Un `idUsuario` en la URL se ignora (no es un parámetro del contrato).
- **Anónimas:** las consultas de visitantes (`id_usuario` NULL) nunca aparecen y su detalle responde 404.
- **No revelar existencia:** una consulta de otro usuario, una anónima y una inexistente responden el mismo 404 (`"La consulta no existe."`).
- **Errores:** `application/problem+json`. 401 sin token, con token inválido, firmado con otra clave, expirado o con la cuenta desactivada.
- **Fechas:** UTC ISO 8601. La pantalla convierte a la hora local.

## GET /api/historial

Lista paginada, **de la más reciente a la más antigua** (`fechaConsultaUtc` desc; empate: `idConsulta` desc). Tamaño fijo de **20**.

| Parámetro | Tipo | Obligatorio | Regla |
|---|---|---|---|
| `tipo` | `ACTUAL` \| `PRONOSTICO` | No | Omitido = todos. Mayúsculas exactas |
| `idUbicacion` | entero ≥ 1 | No | Una ubicación sin consultas del usuario devuelve lista vacía (no 404) |
| `desde` | fecha ISO 8601 con zona | No | **Incluido**. Ej. `2026-10-01T00:00:00-06:00` (codificar `+` como `%2B`) |
| `hasta` | fecha ISO 8601 con zona | No | **Excluido**. Debe ser posterior a `desde` |
| `pagina` | entero 1–100000 | No (1) | Una página posterior a la última devuelve `elementos: []` con 200 |

Para "un día" en Guatemala: `desde=<día>T00:00:00-06:00&hasta=<día siguiente>T00:00:00-06:00`.

**200**

```json
{
  "elementos": [
    {
      "idConsulta": 58,
      "tipo": "PRONOSTICO",
      "fechaConsultaUtc": "2026-10-28T19:05:11.402Z",
      "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
      "temperatura": null,
      "humedad": null,
      "velocidadViento": null,
      "codigoCondicion": null,
      "descripcion": null
    },
    {
      "idConsulta": 57,
      "tipo": "ACTUAL",
      "fechaConsultaUtc": "2026-10-28T19:04:50.118Z",
      "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
      "temperatura": 18.4,
      "humedad": 88,
      "velocidadViento": 2.1,
      "codigoCondicion": 500,
      "descripcion": "lluvia ligera"
    }
  ],
  "pagina": 1,
  "tamanoPagina": 20,
  "totalElementos": 2,
  "totalPaginas": 1
}
```

- En `PRONOSTICO` las mediciones son `null`: M08 muestra **"—"** (criterio de aceptación de RF-11).
- `velocidadViento` en **m/s** (igual que el clima actual). La lista **no** incluye el JSON `detalle`.
- Sin consultas: `elementos: []`, `totalElementos: 0`, `totalPaginas: 0` → estado vacío "Todavía no has consultado el clima".

**400** (`errors` por campo, camelCase)

| Caso | Campo |
|---|---|
| `tipo` distinto de `ACTUAL`/`PRONOSTICO` (incluye minúsculas) | `tipo` |
| `idUbicacion` < 1 o no numérico | `idUbicacion` |
| `desde`/`hasta` no es una fecha | `desde` / `hasta` |
| `desde` ≥ `hasta` | `desde` ("La fecha 'desde' debe ser anterior a 'hasta'.") |
| `pagina` < 1, > 100000 o no numérica | `pagina` |

## GET /api/historial/{idConsulta}

**200 – ACTUAL** (`pronostico` es `null`; `actual.detalle` es el mismo objeto que devuelve `GET /api/clima/actual`)

```json
{
  "idConsulta": 57,
  "tipo": "ACTUAL",
  "fechaConsultaUtc": "2026-10-28T19:04:50.118Z",
  "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
  "actual": {
    "temperatura": 18.4,
    "sensacionTermica": 18.6,
    "humedad": 88,
    "velocidadViento": 2.1,
    "codigoCondicion": 500,
    "descripcion": "lluvia ligera",
    "detalle": {
      "version": 1,
      "observadoEnUtc": "2026-10-28T19:00:00Z",
      "zonaHorariaSegundos": -21600,
      "esDeDia": true,
      "temperaturaMin": 17.9,
      "temperaturaMax": 19.2,
      "presionHpa": 1016,
      "direccionVientoGrados": 40,
      "amanecerUtc": "2026-10-28T11:58:00Z",
      "atardecerUtc": "2026-10-28T23:41:00Z"
    }
  },
  "pronostico": null
}
```

**200 – PRONOSTICO** (`actual` es `null`; `dias` tiene el mismo formato que `GET /api/clima/pronostico`)

```json
{
  "idConsulta": 58,
  "tipo": "PRONOSTICO",
  "fechaConsultaUtc": "2026-10-28T19:05:11.402Z",
  "ubicacion": { "idUbicacion": 12, "nombre": "Cobán", "region": "Alta Verapaz", "pais": "GT", "latitud": 15.4708, "longitud": -90.3708 },
  "actual": null,
  "pronostico": {
    "zonaHorariaSegundos": -21600,
    "dias": [
      {
        "fecha": "2026-10-29",
        "completo": true,
        "temperaturaMax": 22.3,
        "temperaturaMin": 15.1,
        "codigoCondicion": 500,
        "descripcion": "lluvia ligera",
        "probabilidadLluvia": 80,
        "bloques": [
          { "fechaHoraUtc": "2026-10-29T06:00:00Z", "temperatura": 15.6, "humedad": 95, "velocidadViento": 1.2, "codigoCondicion": 804, "descripcion": "nubes", "probabilidadLluvia": 20, "esDeDia": false }
        ]
      }
    ]
  }
}
```

- **Exposición segura del JSONB:** el `detalle` guardado se lee con los modelos tipados del formato versión 1 (sección 6 de la especificación E-02/E-03). Solo salen los campos de ese formato; cualquier otra clave se descarta. Si el contenido no se puede leer, `actual.detalle` es `null` (o `pronostico.dias` es `[]`) y el servidor registra solo `idConsulta`, nunca el contenido.
- **404:** consulta inexistente, de otro usuario o anónima; también un id no numérico o fuera de rango de `long` (la ruta no coincide).

## Uso en M08 (React)

| Pantalla | Dato del API |
|---|---|
| Fecha/hora | `fechaConsultaUtc` → hora local |
| Ubicación | `ubicacion.nombre`, `ubicacion.region`, `ubicacion.pais` |
| Tipo (badge) | `tipo` (`PRONOSTICO` sin tilde; mostrar "Pronóstico") |
| Temperatura, condición, humedad, viento | campos de la fila; `null` → "—" |
| Filtros | `tipo`, `idUbicacion` (de los favoritos o de una búsqueda), `desde`/`hasta` |
| "Consultar de nuevo" | `tipo` + `ubicacion.idUbicacion` → `GET /api/clima/actual` o `/pronostico` |

Notas para la integración (el frontend no se modificó en este bloque): el mock actual de `History.tsx` usa `ciudad`, `fecha`/`hora` separadas, `icono` y el tipo `"PRONÓSTICO"` con tilde; el adaptador debe mapear desde este contrato. El ícono se elige con `codigoCondicion`. La búsqueda de texto libre del mock no forma parte de los filtros aprobados; puede filtrar en el cliente sobre la página cargada o usar `idUbicacion`.
