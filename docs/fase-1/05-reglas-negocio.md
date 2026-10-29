# Reglas de negocio de base de datos

Las reglas RN-BD-01 a RN-BD-34 provienen del análisis del modelo relacional y conservan su numeración original. RN-BD-35 y RN-BD-36 se agregan únicamente para dejar por escrito D-13 y D-03. RN-BD-37 a RN-BD-41 están en la enmienda E-01. RN-BD-13 y RN-BD-36 se reescriben y RN-BD-42 y RN-BD-43 se agregan con las enmiendas E-02 y E-03.

**Columna "Dónde se aplica":**
- **BD:** restricción en PostgreSQL (PK, FK, UNIQUE, CHECK, DEFAULT).
- **Backend:** validación en los servicios de ASP.NET Core.
- **BD + Backend:** la base de datos garantiza la regla y el backend la valida antes para dar un mensaje claro.

---

## Usuarios

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-01 | El correo de un usuario debe ser único. | BD (UNIQUE) |
| RN-BD-04 | El correo se guarda en minúsculas. | BD (CHECK) + Backend |
| RN-BD-05 | El rol solo puede ser `USUARIO` o `ADMINISTRADOR`. Todo registro nuevo es `USUARIO` y solo un administrador puede cambiar roles. | BD (CHECK, DEFAULT) + Backend |
| RN-BD-06 | Nunca se guarda la contraseña, solo `password_hash` generado con `PasswordHasher` (D-01). | Backend |
| RN-BD-07 | Un usuario con `activo = FALSE` no puede autenticarse. Sus datos históricos se conservan. | Backend |
| RN-BD-08 | Un usuario con consultas, alertas o dispositivos no se elimina físicamente; se desactiva. | BD (RESTRICT) + Backend |
| RN-BD-09 | Debe existir al menos un administrador activo. No se puede desactivar ni degradar al último. | Backend |

## Ubicaciones

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-10 | No pueden existir dos ubicaciones con las mismas coordenadas redondeadas a 4 decimales (D-07). | BD (UNIQUE) |
| RN-BD-11 | La latitud va de -90 a 90 y la longitud de -180 a 180. El país es un código ISO de 2 letras mayúsculas. | BD (CHECK) |
| RN-BD-12 | Antes de crear una ubicación, el backend busca una existente por coordenadas y la reutiliza. | Backend |

## Clima (historial de consultas)

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-13 | Cada consulta de clima actual o pronóstico se registra en `consulta_clima` con su tipo. Si hay sesión, con el usuario; si no, con `id_usuario` NULL. Las consultas anónimas no aparecen en ningún historial personal (E-02, reescrita). | Backend |
| RN-BD-14 | En consultas `ACTUAL`, la humedad va de 0 a 100, la velocidad del viento es ≥ 0 y el código de condición está entre 200 y 899. | BD (CHECK) |
| RN-BD-15 | Todos los valores se guardan en unidades métricas (°C, m/s). La conversión de unidades es responsabilidad de la presentación. | Backend |
| RN-BD-36 | Una consulta `ACTUAL` tiene sus 6 mediciones en columnas; una `PRONOSTICO` no tiene mediciones en columnas y guarda el pronóstico en `detalle` (E-03, reescrita). | BD (CHECK) |
| RN-BD-42 | Una consulta con `id_usuario` NULL nunca origina alertas; la consulta que origina una alerta pertenece al mismo usuario que la alerta (E-02). | Backend |
| RN-BD-43 | `detalle` es un objeto JSON con el formato normalizado del backend y es obligatorio en las consultas `PRONOSTICO`. Nunca contiene la respuesta cruda de OpenWeather ni datos de la API key (E-03). | BD (CHECK) + Backend |

## Favoritos

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-02 | Un usuario no puede guardar dos veces la misma ubicación como favorita. | BD (UNIQUE) + Backend |

## Alertas

### Configuración de reglas

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-16 | El tipo de alerta solo puede ser `LLUVIA`, `TEMPERATURA_ALTA` o `TEMPERATURA_BAJA`. El tipo determina la comparación: ALTA → `>`, BAJA → `<`, LLUVIA → detección de lluvia (D-10). | BD (CHECK) |
| RN-BD-17 | `TEMPERATURA_ALTA` y `TEMPERATURA_BAJA` requieren `valor_umbral`. `LLUVIA` no lo admite. | BD (CHECK) |
| RN-BD-18 | El umbral de temperatura debe estar entre -50 y 60 °C. | BD (CHECK) |
| RN-BD-19 | Solo puede existir una regla por usuario, ubicación (o global) y tipo. | BD (UNIQUE NULLS NOT DISTINCT) |
| RN-BD-20 | Si existen una regla específica de ubicación y una global del mismo tipo, prevalece la específica. | Backend |
| RN-BD-21 | Para una misma ubicación, el umbral de temperatura alta debe ser mayor que el de temperatura baja. | Backend |

### Generación y gestión

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-22 | Solo se generan alertas a partir de reglas con `activa = TRUE` de usuarios activos. | Backend |
| RN-BD-23 | Toda alerta tiene exactamente un origen: una consulta o una lectura. | BD (CHECK) |
| RN-BD-24 | Las alertas de temperatura guardan el valor detectado y el umbral vigente. Las de lluvia no guardan ninguno de los dos. | BD (CHECK) |
| RN-BD-25 | El estado de una alerta solo puede ser `NUEVA` o `LEIDA`. Toda alerta nace como `NUEVA`. | BD (CHECK, DEFAULT) |
| RN-BD-26 | Las alertas son historial: no se editan, salvo el cambio de estado. | Backend |
| RN-BD-27 | No se genera otra alerta del mismo usuario, ubicación y tipo durante los 60 minutos siguientes a la anterior, sin importar su estado (D-09). | Backend |
| RN-BD-33 | Las alertas que origina un ESP32 usan las reglas del dueño del dispositivo para la ubicación del dispositivo. | Backend |
| RN-BD-34 | Una alerta solo puede originarse en una consulta `ACTUAL` o en una lectura del ESP32. Las consultas `PRONOSTICO` nunca generan alertas (D-02). | Backend |
| RN-BD-35 | Toda alerta tiene un `mensaje` de hasta 255 caracteres, generado por el backend al crearla. Es el texto que se muestra al usuario (D-13). | BD (NOT NULL) + Backend |

## Dispositivos

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-28 | La API Key de un dispositivo se muestra una sola vez al crearlo. Solo se guarda su hash SHA-256, que es único. | BD (UNIQUE) + Backend |
| RN-BD-29 | Un dispositivo con `activo = FALSE` no puede enviar lecturas (D-11). | Backend |
| RN-BD-30 | El nombre de un dispositivo es único para cada usuario. | BD (UNIQUE) |

## Lecturas

| ID | Regla | Dónde se aplica |
|---|---|---|
| RN-BD-03 | Una lectura de sensor debe pertenecer a un dispositivo existente. | BD (FK) |
| RN-BD-31 | Una lectura debe contener al menos una medición. La humedad y la luz van de 0 a 100 % y la temperatura de -40 a 85 °C. La lluvia es `BOOLEAN` (D-08). | BD (CHECK) |
| RN-BD-32 | `fecha_lectura` la asigna el servidor. Al guardar una lectura se actualiza `dispositivo.ultima_conexion` en la misma transacción (D-12). | Backend |

---

## Índice numérico

| Rango | Grupo |
|---|---|
| 01, 04–09 | Usuarios |
| 02 | Favoritos |
| 03, 31–32 | Lecturas |
| 10–12 | Ubicaciones |
| 13–15, 36, 42–43 | Clima |
| 16–27, 33–35 | Alertas |
| 28–30 | Dispositivos |
