# Incidencias pendientes del backend

| ID | Fecha | Estado | Área |
|---|---|---|---|
| INC-01 | 2026-10-27 | Pendiente | Swagger UI (documentación), no el backend |

## INC-01 – Swagger UI no acepta el patrón Unicode del parámetro `q` de `/api/clima/ubicaciones`

**Síntoma:** al probar `GET /api/clima/ubicaciones` desde Swagger UI, el parámetro `q` no funciona como se espera con valores válidos (por ejemplo, `Coban`). La misma petición enviada directamente al backend (navegador, `.http`, curl o Postman) responde correctamente: la búsqueda de Cobán con OpenWeather se verificó como funcional el 2026-10-27.

**No es un fallo del backend.** La validación real se hace en el servidor con .NET (`[RegularExpression]` sobre `BusquedaUbicacionRequest.Q`) y está cubierta por las pruebas automatizadas.

**Causa probable:** Swashbuckle publica la expresión regular del DTO como `pattern` en el documento OpenAPI:

```
^\s*[\p{L}\p{M}\d][\p{L}\p{M}\d .,'\-]*\s*$
```

Swagger UI valida `pattern` en el navegador con `RegExp` de JavaScript **sin la bandera `u`**. Sin esa bandera, `\p{L}` y `\p{M}` no son clases Unicode, así que Swagger UI interpreta mal el patrón y marca como inválidos textos correctos.

**Cómo probar mientras tanto:** usar `backend/ClimaPlus.Api/ClimaPlus.Api.http`, Postman o el navegador (`http://localhost:5208/api/clima/ubicaciones?q=Coban`).

**Opciones de solución (requieren decisión, no se aplicó ninguna):**

1. Un filtro de Swashbuckle (`IOperationFilter` o `ISchemaFilter`) que quite `pattern` del parámetro `q` en el documento OpenAPI, sin cambiar la validación del servidor. Recomendada: solo afecta a la documentación.
2. Cambiar el patrón por uno que JavaScript entienda sin `u` (rangos explícitos de letras latinas con tildes). Cambia la validación real, así que habría que volver a revisar los casos aceptados.
3. Dejarlo documentado y probar esa ruta fuera de Swagger.

**Impacto:** solo en las pruebas manuales con Swagger UI. El frontend React y la API no se ven afectados.
