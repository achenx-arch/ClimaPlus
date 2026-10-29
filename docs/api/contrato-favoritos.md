# Contrato API – Favoritos (Fase 2F)

**Versión:** 1.0 · **Fecha:** 2026-10-27 · **Rama:** `feature/backend`
**Responsable:** Integrante 2 (backend) · **Consumidor:** Integrante 3 (React) · **Pantallas:** M07 (Favoritos), M06 (Dashboard), botón ★ de M04/M05
**Reglas:** RN-BD-02 (sin duplicados), 07-seguridad-inicial.md (dueño del recurso), D-22 (ubicaciones solo desde Geocoding).

## Estado

| Endpoint | Estado |
|---|---|
| `GET /api/favoritos` | **Implementado, pendiente de verificar** |
| `GET /api/favoritos/{idFavorito}` | **Implementado, pendiente de verificar** |
| `POST /api/favoritos` | **Implementado, pendiente de verificar** |
| `DELETE /api/favoritos/{idFavorito}` | **Implementado, pendiente de verificar** |
| `ultimaConsulta` en cada favorito | **Implementado**; devolverá `null` hasta que el registro de consultas (E-02/E-03) esté integrado |

"Pendiente de verificar" = falta la compilación y ejecución de pruebas en Windows.

## Reglas comunes

- **Autenticación obligatoria:** `Authorization: Bearer <token>`. Roles `USUARIO` y `ADMINISTRADOR`.
- **Aislamiento:** el dueño es siempre el usuario del token. El cliente nunca envía `idUsuario`. Un favorito de otro usuario responde **404** (igual que uno inexistente, para no revelar que existe).
- **Ubicaciones:** solo se aceptan ids devueltos por `GET /api/clima/ubicaciones` (ver `contrato-clima-pronostico.md`). Flujo de "Agregar ubicación" en M07: buscar → elegir resultado → `POST /api/favoritos` con su `idUbicacion`.
- **Errores:** `application/problem+json`. 401 sin token, con token inválido/expirado o con la cuenta desactivada.
- **Fechas:** UTC ISO 8601.

## Objeto `Favorito`

```json
{
  "idFavorito": 7,
  "fechaCreacion": "2026-10-27T20:15:42.123Z",
  "ubicacion": {
    "idUbicacion": 12,
    "nombre": "Cobán",
    "region": "Alta Verapaz",
    "pais": "GT",
    "latitud": 15.4708,
    "longitud": -90.3708
  },
  "ultimaConsulta": {
    "idConsulta": 345,
    "fechaConsulta": "2026-10-27T18:21:04Z",
    "temperatura": 22.46,
    "codigoCondicion": 802,
    "descripcion": "nubes dispersas"
  }
}
```

| Campo | Notas |
|---|---|
| `fechaCreacion` | Fecha en que se agregó (M07: "fecha en que se agregó"). |
| `ubicacion` | Mismo formato que la búsqueda de ubicaciones. |
| `ultimaConsulta` | Última consulta de **clima actual** que el propio usuario hizo de esa ubicación (M06/M07). `null` si no hay. No dispara llamadas a OpenWeather (M06: el dashboard no consulta el clima de cada favorito). Para datos actualizados, el botón "Ver clima ahora" llamará a `GET /api/clima/actual?idUbicacion=` cuando esté integrado. |

---

## `GET /api/favoritos`

Lista los favoritos del usuario, del más reciente al más antiguo. Sin paginación.

- **200** con un arreglo (vacío si no hay: mostrar "Aún no tienes favoritos" y el botón "Buscar una ubicación").
- **401**.

Para pintar la ★ en M04/M05, el frontend puede buscar en esta lista el `idUbicacion` mostrado; si está, guarda el `idFavorito` para poder eliminarlo.

## `GET /api/favoritos/{idFavorito}`

- **200** con el favorito.
- **404** si no existe o es de otro usuario.
- **401**.

## `POST /api/favoritos`

```json
{ "idUbicacion": 12 }
```

| Campo | Reglas |
|---|---|
| `idUbicacion` | Obligatorio, entero ≥ 1. No se admiten otros campos. |

| HTTP | Cuándo | Mensaje / campo |
|---|---|---|
| **201** | Creado. Encabezado `Location: /api/favoritos/{idFavorito}` y el favorito en el cuerpo. | Toast: "Ubicación agregada a favoritos" |
| 400 | Falta `idUbicacion`, no es un entero ≥ 1, JSON mal formado o campos de más. | `errors.idUbicacion` |
| 401 | Sin sesión válida. | En M04/M05 sin sesión el botón dice "Inicia sesión para guardar favoritos". |
| 404 | La ubicación no existe. | "La ubicación no existe. Búscala de nuevo para agregarla." |
| 409 | Ya está en los favoritos del usuario (RN-BD-02). | `errors.idUbicacion`: "Esta ubicación ya está en tus favoritos." |

Dos usuarios distintos sí pueden guardar la misma ubicación. Si dos peticiones iguales llegan al mismo tiempo, la restricción `uq_favorito_usuario_ubicacion` deja pasar una y la otra recibe 409.

## `DELETE /api/favoritos/{idFavorito}`

- **204** eliminado. Solo se borra el favorito: la ubicación sigue en el catálogo y en el historial. Se puede volver a agregar.
- **404** si no existe, ya se eliminó o es de otro usuario.
- **401**.

Antes de llamar, M07 muestra el modal "¿Eliminar {nombre} de tus favoritos?".

---

## Pruebas automatizadas

`backend/ClimaPlus.Api.Tests/Favoritos/FavoritosTests.cs` (EF Core InMemory):

- 401 sin token en las cuatro rutas, con token inválido y con cuenta desactivada.
- Agregar: 201, `Location`, datos de la ubicación, `ultimaConsulta` null, fecha en UTC.
- Lista vacía; solo favoritos propios, ordenados del más reciente al más antiguo.
- Duplicado → 409 con `errors.idUbicacion`; la misma ubicación para dos usuarios → 201 en ambos.
- Ubicación inexistente → 404.
- Cuerpos inválidos → 400 (vacío, null, 0, negativo, texto, decimal, `idUsuario` extra, campo con `<script>`, JSON mal formado).
- Administrador también puede usar favoritos.
- Otro usuario no puede ver ni eliminar el favorito (404) y el favorito sigue existiendo.
- Eliminar → 204; repetir → 404; la ubicación se conserva; se puede volver a agregar.
- Id no numérico o fuera de rango en la ruta → 404.
- `ultimaConsulta`: la ACTUAL más reciente del propio usuario (ignora PRONOSTICO y consultas de otros usuarios).

**Limitación:** InMemory no aplica `uq_favorito_usuario_ubicacion` ni las FK. El caso de inserción simultánea y la traducción a SQL de la consulta de `ultimaConsulta` se comprueban en la verificación manual con PostgreSQL (Swagger) y quedan pendientes para la categoría de pruebas `PostgreSQL`.
