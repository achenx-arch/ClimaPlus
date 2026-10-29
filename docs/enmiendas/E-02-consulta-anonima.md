# Enmienda E-02 – Consultas anónimas en `consulta_clima`

| Campo | Valor |
|---|---|
| Fecha | 2026-10-28 |
| Estado | **Aprobada** (ACHX, 2026-10-27). **Implementada en código, migración sin aplicar.** Revisión pendiente del Integrante 1 (base de datos) |
| Implementa | ACHX, temporalmente en lugar del Integrante 1 |
| Fase | 2E-DB (desbloquea 2E, 2G y el motor de alertas de 2H) |
| Especificación | [`E-02-E-03-especificacion-tecnica.md`](E-02-E-03-especificacion-tecnica.md) |
| Migración | `20261028062000_ConsultaClimaUsuarioOpcional` · `database/migrations/002_ConsultaClimaUsuarioOpcional.sql` |
| Decisiones afectadas | D-04 **MODIFICADA**; D-14 **AJUSTADA** por D-23 |
| Reglas | RN-BD-13 reescrita; RN-BD-42 nueva |

## 1. Motivo

La guía oficial del proyecto (sección 4, p. 3) exige que los datos de fuentes externas se procesen en el backend y **se almacenen en la base de datos antes de mostrarse**. D-04 decía lo contrario para los visitantes: sus consultas se atendían sin guardarse. Como el clima y el pronóstico son públicos (D-04, D-20), la única forma de cumplir la guía sin obligar a iniciar sesión es guardar también las consultas anónimas.

## 2. Cambio

| Elemento | Antes | Con E-02 |
|---|---|---|
| `consulta_clima.id_usuario` | `BIGINT NOT NULL` | `BIGINT NULL`. NULL = visitante sin sesión |
| `fk_consulta_clima_usuario` | `ON DELETE RESTRICT` | Sin cambio (una FK admite NULL) |
| CHECK, índices y demás tablas | — | Sin cambio |
| Entidad `ConsultaClima` | `long IdUsuario`, `Usuario Usuario` | `long? IdUsuario`, `Usuario? Usuario` |
| `ConsultaClimaConfiguration` | Columna obligatoria | Columna opcional y relación `IsRequired(false)` |

SQL de la migración: `ALTER TABLE consulta_clima ALTER COLUMN id_usuario DROP NOT NULL;`

## 3. Reglas

- **RN-BD-13 (reescrita):** cada consulta de clima actual o pronóstico se registra en `consulta_clima` con su tipo. Si hay sesión, con el usuario; si no, con `id_usuario` NULL. Las consultas anónimas no aparecen en ningún historial personal.
- **RN-BD-42 (nueva):** una consulta con `id_usuario` NULL nunca origina alertas; la consulta que origina una alerta pertenece al mismo usuario que la alerta. Se aplica en el backend (no se puede expresar con un CHECK porque relaciona dos tablas).

## 4. Efectos

- **Historial (M08):** filtra por el usuario del token; las filas anónimas no aparecen en el historial de nadie.
- **Favoritos (M07):** `ultimaConsulta` solo considera consultas del propio usuario (comprobado con pruebas).
- **Privacidad:** una fila anónima no guarda IP ni ningún identificador del visitante.
- **Volumen:** lo controlan el rate limiting de 30 peticiones por minuto por IP y la reutilización de consultas recientes (D-23).

## 5. Reversión

`Down` vuelve a poner `NOT NULL` **sin valor por defecto**: si ya existen consultas anónimas, falla y no modifica nada. Revertir exigiría borrar antes esas filas, con pérdida de datos; solo se hace en desarrollo.

## 6. Documentos actualizados

`docs/base-datos/diccionario-datos.md`, `docs/base-datos/modelo-relacional.puml` y `.png`, `docs/fase-1/02-objetivos-alcance.md`, `03-arquitectura.md`, `04-modelo-datos.md`, `05-reglas-negocio.md`, `07-seguridad-inicial.md`, `08-mockups.md` y `09-decisiones-tecnicas.md`.
