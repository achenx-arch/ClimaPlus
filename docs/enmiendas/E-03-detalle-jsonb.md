# Enmienda E-03 – Columna `detalle JSONB` en `consulta_clima`

| Campo | Valor |
|---|---|
| Fecha | 2026-10-28 |
| Estado | **Aprobada** (ACHX, 2026-10-27, opción 1). **Implementada en código, migración sin aplicar.** Revisión pendiente del Integrante 1 (base de datos) |
| Implementa | ACHX, temporalmente en lugar del Integrante 1 |
| Fase | 2E-DB |
| Especificación | [`E-02-E-03-especificacion-tecnica.md`](E-02-E-03-especificacion-tecnica.md) (formato de `detalle`: sección 6) |
| Migración | `20261028062100_ConsultaClimaDetalle` · `database/migrations/003_ConsultaClimaDetalle.sql` |
| Decisiones afectadas | D-03 **MODIFICADA** |
| Reglas | RN-BD-36 reescrita; RN-BD-43 nueva |

## 1. Motivo

El pronóstico de 5 días es obligatorio en la Variante 6 y, por la guía oficial (sección 4), debe guardarse antes de mostrarse. D-03 solo registraba el evento de la consulta `PRONOSTICO`, sin sus datos. M04 también muestra datos que no tienen columna (presión, máxima y mínima, dirección del viento, amanecer, atardecer).

Se eligió la **opción 1** del análisis: una sola columna JSONB, sin tabla de pronóstico. El pronóstico es un dato de solo lectura que nunca se filtra ni se agrega por sus valores internos.

## 2. Cambio

| Elemento | Con E-03 |
|---|---|
| Columna | `detalle JSONB NULL` en `consulta_clima` (12 columnas en total) |
| CHECK | `ck_consulta_clima_detalle_objeto`: `detalle IS NULL OR jsonb_typeof(detalle) = 'object'` |
| CHECK | `ck_consulta_clima_detalle_pronostico`: `tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL` |
| Entidad | `string? Detalle` (se usa `string` para que EF InMemory siga funcionando en las pruebas) |
| Configuración | `HasColumnName("detalle").HasColumnType("jsonb")` y los dos CHECK |
| Totales | `consulta_clima`: 9 CHECK; modelo: 26 CHECK (antes 24) |
| No cambia | Las 6 columnas de medición y sus CHECK; ninguna otra tabla; no hay tabla de pronóstico |

## 3. Qué guarda `detalle`

El mismo objeto normalizado que recibe React, con `"version": 1` y fechas UTC ISO 8601. **Nunca** la respuesta cruda de OpenWeather ni datos de la API key.

- **ACTUAL:** `observadoEnUtc`, `zonaHorariaSegundos`, `esDeDia`, `temperaturaMin`, `temperaturaMax`, `presionHpa`, `direccionVientoGrados`, `amanecerUtc`, `atardecerUtc`.
- **PRONOSTICO:** `zonaHorariaSegundos` y `dias[]` (fecha local, completo, máxima, mínima, condición, probabilidad de lluvia y `bloques[]` de 3 horas).

Lo genera `DetalleSerializador` a partir de `ClimaTransformador` (Fase 2E).

## 4. Reglas

- **RN-BD-36 (reescrita):** una consulta ACTUAL tiene sus 6 mediciones en columnas; una PRONOSTICO no tiene mediciones en columnas y guarda el pronóstico en `detalle`.
- **RN-BD-43 (nueva):** `detalle` es un objeto JSON con el formato normalizado del backend y es obligatorio en las consultas PRONOSTICO. Nunca contiene la respuesta cruda de OpenWeather ni datos de la API key.

## 5. Condición previa y reversión

- Antes de aplicar: no debe haber consultas `PRONOSTICO` (si las hubiera, la CHECK nueva impediría aplicar la migración). Se comprueba con `database/scripts/E02_E03_verificacion_previa.sql`.
- `Down` quita los dos CHECK y la columna. No afecta a las demás columnas, pero **pierde el contenido de `detalle`**.

## 6. Documentos actualizados

Los mismos que E-02 (ver [`E-02-consulta-anonima.md`](E-02-consulta-anonima.md)).
