# Seguridad inicial

Este documento recoge **solo decisiones de diseño**. Ninguna medida está implementada todavía; se aplicarán durante el desarrollo del backend y del frontend.

---

## 1. Autenticación de usuarios

| Decisión | Detalle |
|---|---|
| Autenticación propia (D-01) | Se usa la tabla `usuario`. No se usa ASP.NET Core Identity completo ni sus tablas `AspNet*`. |
| Contraseña almacenada como hash | Nunca se guarda la contraseña en texto plano. La columna `password_hash` guarda el resultado del hash con sal. |
| `PasswordHasher` | Se usa `PasswordHasher<T>` de ASP.NET Core (PBKDF2 con sal aleatoria y varias iteraciones) para generar y verificar el hash. |
| JWT | Al iniciar sesión, el backend emite un token JWT firmado con el id del usuario y su rol, con tiempo de expiración. El frontend lo envía en `Authorization: Bearer`. |
| JWT no se almacena en BD | No existen tablas `token` ni `sesion`. |
| Usuarios inactivos | Un usuario con `activo = FALSE` no puede iniciar sesión, y sus peticiones con un token previo deben rechazarse. |
| Mensajes de login genéricos | Ante credenciales incorrectas se responde "correo o contraseña incorrectos", sin indicar cuál de los dos falló. |

## 2. Autorización por roles

| Recurso | Público | USUARIO | ADMINISTRADOR |
|---|:---:|:---:|:---:|
| Búsqueda de ubicación, clima actual, pronóstico | ✔ | ✔ | ✔ |
| Favoritos, historial, reglas de alerta, alertas propias | — | ✔ | ✔ |
| Dispositivos propios | — | ✔ | ✔ |
| Gestión de usuarios, alertas del sistema, estadísticas | — | — | ✔ |
| Envío de lecturas | — | — | — (solo ESP32 con API Key) |

- La autorización se valida en el backend con políticas por rol. Ocultar botones en el frontend es solo una ayuda visual, no una medida de seguridad.
- Cada servicio comprueba que el recurso solicitado (favorito, alerta, dispositivo) pertenezca al usuario autenticado. Esto evita que alguien acceda a datos ajenos cambiando un id en la URL.

## 3. Validación cliente / servidor

- **Cliente (React):** valida formularios para dar retroalimentación inmediata (campos requeridos, formato de correo, rangos de umbral).
- **Servidor (ASP.NET Core):** valida **siempre** todos los DTO de entrada (anotaciones de validación y reglas en servicios), porque la validación del cliente se puede saltar.
- **Base de datos:** las restricciones `CHECK`, `UNIQUE` y `FK` son la última línea de defensa (ver [`05-reglas-negocio.md`](05-reglas-negocio.md)).

## 4. Inyección SQL y XSS

| Riesgo | Medida de diseño |
|---|---|
| Inyección SQL | Todo acceso a PostgreSQL pasa por Entity Framework Core, que genera consultas parametrizadas. No se concatenan cadenas para construir SQL. |
| XSS | React escapa por defecto el contenido que se muestra en JSX. No se usará `dangerouslySetInnerHTML`. Los textos que llegan de OpenWeather y el `mensaje` de las alertas se muestran como texto plano. |
| Datos externos | Las respuestas de OpenWeather se mapean a DTO propios y se validan antes de guardarse o enviarse al frontend. |

## 5. Dispositivos ESP32

| Decisión | Detalle |
|---|---|
| Autenticación por API Key | Cada dispositivo tiene una clave aleatoria de alta entropía que envía en el encabezado `X-Device-Key`. |
| API Key almacenada como hash | Solo se guarda su hash SHA-256 (`api_key_hash`, único). Como la clave es aleatoria y larga, no requiere sal, y el hash permite buscar el dispositivo directamente. |
| Clave visible una sola vez | La clave se muestra al usuario únicamente al registrar el dispositivo. |
| Dispositivos inactivos | Un dispositivo con `activo = FALSE` no puede enviar lecturas (D-11). |
| Sin acceso a la BD ni a OpenWeather | El ESP32 solo se comunica con la Web API. |

## 6. Secretos y configuración

- La **API Key de OpenWeather** solo existe en la configuración del backend (variables de entorno o *user secrets* en desarrollo). Nunca se envía al frontend ni al ESP32.
- La **clave de firma del JWT** y la **cadena de conexión** de PostgreSQL también son secretos del backend.
- Ningún secreto se sube al repositorio: `.gitignore` excluye archivos `.env` y de configuración local.
- La comunicación entre el navegador y la API debe usar HTTPS.

## 7. Desactivación de cuentas

- **Usuarios:** el administrador puede desactivarlos (`activo = FALSE`). Se conserva su historial y siempre debe quedar al menos un administrador activo (RN-BD-09).
- **Dispositivos:** su dueño puede desactivarlos, lo que revoca de inmediato su API Key.
- No hay borrado físico de registros con historial (RN-BD-08).

## 8. Protección futura de la API meteorológica (D-14)

Como el clima y el pronóstico son públicos (D-04), cualquiera podría consumir la cuota de OpenWeather a través del backend. Durante la implementación del backend se aplicará:

| Medida | Detalle |
|---|---|
| **Rate Limiting** de ASP.NET Core | Límite de peticiones por cliente en las rutas públicas de clima y pronóstico. |
| **Reutilización de consultas recientes** | La caché del clima es `consulta_clima`: una consulta de la misma ubicación de menos de 10 minutos (actual) o 30 minutos (pronóstico) se reutiliza sin llamar a OpenWeather (D-23). |
| **`IMemoryCache`** | Solo para repetir búsquedas de ubicaciones (D-23). |

**No** se usará Redis ni se creará una tabla de caché. Valores aplicados: 30 peticiones por minuto por IP en `/api/clima/*` (D-23).

**Privacidad de las consultas anónimas (E-02):** una fila con `id_usuario` NULL solo guarda la ubicación y los datos meteorológicos. No guarda IP ni ningún identificador del visitante, no aparece en ningún historial personal y no genera alertas.
