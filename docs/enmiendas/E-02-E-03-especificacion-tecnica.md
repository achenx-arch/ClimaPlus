# E-02 y E-03 – Especificación técnica para el Integrante 1 (base de datos)

| Campo | Valor |
|---|---|
| Fecha | 2026-10-27 |
| Estado | **Aprobadas** por el responsable del backend (ACHX). **Implementadas en código por ACHX el 2026-10-28** (en lugar del Integrante 1, que las revisará). Migraciones **sin aplicar**. Ver [`E-02-E-03-informe-implementacion.md`](E-02-E-03-informe-implementacion.md) |
| Origen | Análisis "ClimaPlus – Fase 2E: impacto de la persistencia y plan Variante 6" y guía oficial UMG (sección 4, p. 3; Variante 6, p. 10) |
| Rama sugerida | `feature/db-consulta-anonima` (desde `main` en `535c933`) |
| Bloquea | Fases 2E (clima y pronóstico), 2G (historial) y el motor de alertas de 2H |

Este documento dice **exactamente** qué cambiar. No cambia nada más del modelo: las 8 tablas, las demás columnas, FK, índices y CHECK quedan iguales, y `InitialCreate` y `001_InitialCreate.sql` **no se tocan**.

---

## 1. Resumen de los cambios

| Enmienda | Cambio en `consulta_clima` | Migración EF | Script SQL |
|---|---|---|---|
| E-02 | `id_usuario` pasa de `NOT NULL` a `NULL` (NULL = visitante sin sesión) | `ConsultaClimaUsuarioOpcional` | `database/migrations/002_ConsultaClimaUsuarioOpcional.sql` |
| E-03 | Nueva columna `detalle JSONB NULL` + 2 CHECK | `ConsultaClimaDetalle` | `database/migrations/003_ConsultaClimaDetalle.sql` |

Resultado esperado: `consulta_clima` pasa de 11 a 12 columnas, y el modelo de 24 a **26** restricciones CHECK.

---

## 2. Cambios de código (solo dos archivos)

### 2.1 `backend/ClimaPlus.Api/Domain/Entities/ConsultaClima.cs`

Cambiar estas dos propiedades:

```csharp
    /// <summary>
    /// Usuario autenticado que hizo la consulta; NULL = visitante sin sesión (E-02).
    /// Las consultas con NULL nunca aparecen en un historial personal ni generan alertas (RN-BD-42).
    /// </summary>
    public long? IdUsuario { get; set; }
```

```csharp
    public Usuario? Usuario { get; set; }
```

Y agregar, después de `DescripcionCondicion`:

```csharp
    /// <summary>
    /// Datos normalizados que no tienen columna propia (E-03), como JSON:
    /// en ACTUAL, los datos extra de M04; en PRONOSTICO, los días y bloques de M05.
    /// Formato definido en la sección 6 de E-02-E-03-especificacion-tecnica.md.
    /// </summary>
    public string? Detalle { get; set; }
```

Se usa `string` (no `JsonDocument`) para que EF InMemory siga funcionando en las pruebas existentes.

### 2.2 `backend/ClimaPlus.Api/Data/Configurations/ConsultaClimaConfiguration.cs`

**a) Dos CHECK nuevos**, dentro del bloque `builder.ToTable("consulta_clima", t => { ... })`, después de `ck_consulta_clima_mediciones_tipo`:

```csharp
            // E-03: detalle es un objeto JSON cuando existe.
            t.HasCheckConstraint("ck_consulta_clima_detalle_objeto",
                "detalle IS NULL OR jsonb_typeof(detalle) = 'object'");

            // E-03 / RN-BD-43: toda consulta PRONOSTICO guarda su pronóstico en detalle.
            t.HasCheckConstraint("ck_consulta_clima_detalle_pronostico",
                $"tipo_consulta <> '{TiposConsulta.Pronostico}' OR detalle IS NOT NULL");
```

**b) `id_usuario` opcional** – en la propiedad, quitar `.IsRequired()`:

```csharp
        builder.Property(c => c.IdUsuario)
            .HasColumnName("id_usuario")
            .HasColumnType("bigint");
```

**c) Nueva propiedad**, después de `DescripcionCondicion`:

```csharp
        builder.Property(c => c.Detalle)
            .HasColumnName("detalle")
            .HasColumnType("jsonb");
```

**d) Relación con usuario opcional** – agregar `.IsRequired(false)`; el `OnDelete` sigue en `Restrict`:

```csharp
        builder.HasOne(c => c.Usuario)
            .WithMany(u => u.ConsultasClima)
            .HasForeignKey(c => c.IdUsuario)
            .IsRequired(false)
            .HasConstraintName("fk_consulta_clima_usuario")
            .OnDelete(DeleteBehavior.Restrict);
```

No cambia nada más en ese archivo: índices, demás CHECK y nombres se mantienen.

---

## 3. Migraciones (en este orden, una por enmienda)

Desde `backend\ClimaPlus.Api`, en PowerShell.

**Paso 1 – comprobaciones previas en `climaplus_db` (solo lectura):**

```sql
-- Debe devolver 0: si hubiera consultas PRONOSTICO, la CHECK nueva de E-03 no se podría aplicar.
SELECT count(*) AS pronosticos FROM consulta_clima WHERE tipo_consulta = 'PRONOSTICO';

-- Migraciones aplicadas hasta ahora: debe aparecer solo 20261016052608_InitialCreate.
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

**Paso 2 – E-02:** aplicar solo los cambios de `IdUsuario` y `Usuario` (secciones 2.1 y 2.2 b y d), compilar y generar:

```powershell
dotnet build
dotnet ef migrations add ConsultaClimaUsuarioOpcional --output-dir Data/Migrations
dotnet ef migrations script InitialCreate ConsultaClimaUsuarioOpcional -o ..\..\database\migrations\002_ConsultaClimaUsuarioOpcional.sql
```

**Paso 3 – E-03:** aplicar el resto (`Detalle`, sus CHECK y su propiedad), compilar y generar:

```powershell
dotnet build
dotnet ef migrations add ConsultaClimaDetalle --output-dir Data/Migrations
dotnet ef migrations script ConsultaClimaUsuarioOpcional ConsultaClimaDetalle -o ..\..\database\migrations\003_ConsultaClimaDetalle.sql
```

### SQL esperado

`002_ConsultaClimaUsuarioOpcional.sql` debe contener, además de `START TRANSACTION`, `COMMIT` y el `INSERT` en `__EFMigrationsHistory`, únicamente:

```sql
ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;
```

`003_ConsultaClimaDetalle.sql` debe contener, con las mismas tres excepciones, únicamente:

```sql
ALTER TABLE consulta_clima ADD detalle jsonb;
ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_objeto CHECK (detalle IS NULL OR jsonb_typeof(detalle) = 'object');
ALTER TABLE consulta_clima ADD CONSTRAINT ck_consulta_clima_detalle_pronostico CHECK (tipo_consulta <> 'PRONOSTICO' OR detalle IS NOT NULL);
```

**Si cualquiera de los dos scripts incluye otra cosa** (`DROP`, `CREATE TABLE`, cambios en otras tablas o índices), **no aplicarlo**: borrar la migración con `dotnet ef migrations remove` y revisar los cambios de código.

**Paso 4 – aplicar** (solo después de revisar los dos SQL):

```powershell
dotnet ef database update
```

**Paso 5 – verificar:**

```sql
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'consulta_clima' AND column_name IN ('id_usuario', 'detalle');
-- id_usuario | bigint | YES
-- detalle    | jsonb  | YES

SELECT conname FROM pg_constraint
WHERE conrelid = 'consulta_clima'::regclass AND contype = 'c'
ORDER BY conname;
-- 9 filas: las 7 originales + ck_consulta_clima_detalle_objeto + ck_consulta_clima_detalle_pronostico

SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
-- InitialCreate, ConsultaClimaUsuarioOpcional, ConsultaClimaDetalle
```

### Reversión

- `ConsultaClimaDetalle` se revierte sin pérdida de datos de las otras columnas: elimina `detalle` y sus 2 CHECK.
- `ConsultaClimaUsuarioOpcional` **falla** si ya existen consultas anónimas. Revertirla exige borrarlas primero, con pérdida de datos. Solo debe hacerse en desarrollo.

---

## 4. Pruebas SQL (Integrante 1)

Archivo sugerido: `database/scripts/pruebas/E02_E03_consulta_clima.sql`. Todo ocurre dentro de una transacción con `ROLLBACK` final: no deja datos.

```sql
BEGIN;

-- Datos mínimos para las pruebas (se descartan con el ROLLBACK).
INSERT INTO ubicacion (nombre, region, pais, latitud, longitud)
VALUES ('Prueba E02', NULL, 'GT', 89.9999, 179.9999) RETURNING id_ubicacion \gset ubi_

-- 1. E-02: consulta ACTUAL anónima → DEBE ACEPTARSE
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, sensacion_termica,
  humedad, velocidad_viento, codigo_condicion, descripcion_condicion, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'ACTUAL', 22.5, 23.0, 74, 3.3, 802, 'nubes dispersas',
  '{"version":1,"presionHpa":1018}');

-- 2. E-03: PRONOSTICO con detalle → DEBE ACEPTARSE
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', '{"version":1,"dias":[]}');

-- 3. E-03: PRONOSTICO sin detalle → DEBE FALLAR (ck_consulta_clima_detalle_pronostico)
SAVEPOINT p3;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO');
ROLLBACK TO SAVEPOINT p3;

-- 4. E-03: detalle que no es objeto → DEBE FALLAR (ck_consulta_clima_detalle_objeto)
SAVEPOINT p4;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', '[1,2,3]');
ROLLBACK TO SAVEPOINT p4;

-- 5. Regresión: los CHECK de medición siguen activos (humedad 150) → DEBE FALLAR
SAVEPOINT p5;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, sensacion_termica,
  humedad, velocidad_viento, codigo_condicion, descripcion_condicion)
VALUES (NULL, :ubi_id_ubicacion, 'ACTUAL', 22.5, 23.0, 150, 3.3, 802, 'nubes');
ROLLBACK TO SAVEPOINT p5;

-- 6. Regresión: PRONOSTICO con mediciones → DEBE FALLAR (ck_consulta_clima_mediciones_tipo)
SAVEPOINT p6;
INSERT INTO consulta_clima (id_usuario, id_ubicacion, tipo_consulta, temperatura, detalle)
VALUES (NULL, :ubi_id_ubicacion, 'PRONOSTICO', 20, '{"version":1}');
ROLLBACK TO SAVEPOINT p6;

ROLLBACK;
```

Ejecutar con `psql -d climaplus_db -f ...` y conservar la salida como evidencia (`docs/base-datos/evidencias/`). Los casos 3 a 6 deben mostrar el error de la restricción indicada.

RN-BD-42 (una consulta anónima no genera alertas; la alerta y su consulta tienen el mismo usuario) se aplica en el backend y se prueba en la Fase 2H. No requiere cambios en la base de datos.

---

## 5. Documentos que actualiza el Integrante 1 (en el mismo PR)

| Documento | Cambio |
|---|---|
| `docs/enmiendas/E-02-consulta-anonima.md` y `E-03-detalle-jsonb.md` | Registro de cada enmienda (modelo: `E-01`) con motivo, cambios y decisiones afectadas |
| `docs/base-datos/diccionario-datos.md` | `id_usuario`: Permite NULL = Sí, "NULL = visitante sin sesión". Nueva fila `detalle` (JSONB, NULL, 2 CHECK). Texto de la sección 5. Tabla de decisiones: D-03 y D-04 modificadas |
| `docs/base-datos/modelo-relacional.puml` + PNG | `id_usuario : BIGINT NULL`, `detalle : JSONB NULL`, `usuario \|o--o{ consulta_clima`, nota de la leyenda |
| `docs/fase-1/04-modelo-datos.md` | Descripción de `consulta_clima` y cardinalidad usuario–consulta 0..1:N |
| `docs/fase-1/05-reglas-negocio.md` | RN-BD-13 y RN-BD-36 reescritas; nuevas RN-BD-42 y RN-BD-43 |
| `docs/fase-1/09-decisiones-tecnicas.md` | D-03 y D-04 "MODIFICADA por E-03 / E-02"; D-14 "AJUSTADA por D-23"; D-20 a D-27 |
| `docs/fase-1/02-objetivos-alcance.md`, `03-arquitectura.md`, `07-seguridad-inicial.md`, `08-mockups.md` | Ajustes de redacción listados en la sección 6 del análisis |

Texto propuesto para las reglas nuevas:

- **RN-BD-13 (reescrita):** cada consulta de clima actual o pronóstico se registra en `consulta_clima` con su tipo. Si hay sesión, con el usuario; si no, con `id_usuario` NULL. Las consultas anónimas no aparecen en ningún historial personal.
- **RN-BD-36 (reescrita):** una consulta ACTUAL tiene sus 6 mediciones en columnas; una PRONOSTICO no tiene mediciones en columnas y guarda el pronóstico en `detalle`.
- **RN-BD-42 (nueva):** una consulta con `id_usuario` NULL nunca origina alertas; la consulta que origina una alerta pertenece al mismo usuario que la alerta. Se aplica en el backend.
- **RN-BD-43 (nueva):** `detalle` es un objeto JSON con el formato normalizado del backend y es obligatorio en las consultas PRONOSTICO. Nunca contiene la respuesta cruda de OpenWeather ni datos de la API key.

---

## 6. Formato de `detalle` (lo define el backend; informativo para BD)

Toda versión lleva `"version": 1`, para poder cambiar el formato sin migraciones. Las fechas van en UTC (ISO 8601) y las unidades son métricas.

**ACTUAL** (datos extra de M04):

```json
{
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
}
```

**PRONOSTICO** (M05):

```json
{
  "version": 1,
  "zonaHorariaSegundos": -21600,
  "dias": [
    {
      "fecha": "2026-10-27",
      "completo": false,
      "temperaturaMax": 25.0,
      "temperaturaMin": 16.0,
      "codigoCondicion": 802,
      "descripcion": "nubes dispersas",
      "probabilidadLluvia": 20,
      "bloques": [
        {
          "fechaHoraUtc": "2026-10-27T21:00:00Z",
          "temperatura": 23.4,
          "humedad": 70,
          "velocidadViento": 3.1,
          "codigoCondicion": 802,
          "descripcion": "nubes dispersas",
          "probabilidadLluvia": 20,
          "esDeDia": true
        }
      ]
    }
  ]
}
```

---

## 7. Criterios de aceptación del PR

- [ ] Solo cambian `ConsultaClima.cs`, `ConsultaClimaConfiguration.cs`, las 2 migraciones nuevas y su `ModelSnapshot`, los 2 SQL y los documentos de la sección 5
- [ ] `InitialCreate` y `001_InitialCreate.sql` sin cambios (`git diff` vacío para ellos)
- [ ] Los SQL 002 y 003 coinciden con la sección 3
- [ ] `dotnet build` con 0 errores y 0 advertencias
- [ ] `dotnet test` con las 32 pruebas existentes aprobadas
- [ ] Migraciones aplicadas en `climaplus_db` y verificación del paso 5 guardada como evidencia
- [ ] Pruebas SQL de la sección 4 ejecutadas, con su salida guardada
- [ ] PR revisado por el responsable del backend antes de fusionar en `main`

Cuando esté en `main`, el backend continúa con las fases 2E a 2I sobre esa base.
