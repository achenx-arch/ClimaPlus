# Objetivos y alcance

## Objetivo general

Desarrollar una aplicación web, ClimaPlus, que permita consultar el clima actual y el pronóstico de cualquier ubicación. Los usuarios registrados también podrán guardar favoritos, conservar un historial de consultas y recibir alertas por lluvia o temperatura según sus propios umbrales. La aplicación tendrá un rol administrador y quedará preparada para recibir mediciones de un dispositivo ESP32.

## Objetivos específicos

1. Diseñar una arquitectura por capas (React → ASP.NET Core Web API → EF Core → PostgreSQL) en la que el backend centralice la comunicación con OpenWeather y con el ESP32.
2. Diseñar un modelo relacional normalizado de 8 tablas que soporte usuarios, ubicaciones, favoritos, historial, reglas de alerta, alertas, dispositivos y lecturas.
3. Implementar registro e inicio de sesión con autenticación propia basada en JWT y contraseñas almacenadas como hash, con dos roles: USUARIO y ADMINISTRADOR.
4. Permitir la búsqueda de ubicaciones y la consulta pública del clima actual y del pronóstico mediante el backend.
5. Permitir que el usuario autenticado gestione sus ubicaciones favoritas y consulte su historial de consultas (ACTUAL y PRONÓSTICO).
6. Permitir que el usuario configure reglas de alerta (lluvia, temperatura alta y temperatura baja) y generar alertas cuando una medición cumpla esas reglas.
7. Ofrecer al administrador la gestión de usuarios y la consulta de alertas y estadísticas básicas.
8. Permitir que el usuario registre dispositivos ESP32 que se autentiquen con API Key y envíen lecturas ambientales.

## Alcance funcional

### Usuario público (sin sesión)

Puede:

- buscar una ubicación;
- consultar el clima actual;
- consultar el pronóstico.

Sus consultas **no** se guardan en el historial (D-04).

### Usuario autenticado (rol USUARIO)

Puede:

- consultar el clima actual y el pronóstico (estas consultas sí se registran en su historial);
- guardar y eliminar ubicaciones favoritas;
- consultar su historial de consultas;
- configurar reglas de alerta;
- consultar sus alertas y marcarlas como leídas;
- administrar sus dispositivos ESP32 (registrar, editar, activar o desactivar).

### Administrador (rol ADMINISTRADOR)

Además de lo que puede hacer un usuario autenticado, puede:

- gestionar usuarios (consultar, cambiar rol, activar o desactivar);
- consultar información administrativa;
- consultar las alertas del sistema;
- consultar estadísticas básicas (usuarios, consultas, alertas, ubicaciones más consultadas, dispositivos).

### ESP32 (fase posterior)

Puede:

- autenticarse ante el backend mediante su API Key;
- enviar lecturas ambientales: temperatura, humedad, lluvia y luz.

## Incluido en el proyecto

| Módulo | Funcionalidad |
|---|---|
| Autenticación | Registro, inicio de sesión, JWT, roles USUARIO / ADMINISTRADOR, usuarios activos/inactivos |
| Clima | Búsqueda de ubicación, clima actual y pronóstico de 5 días mediante OpenWeather, consumido solo desde el backend |
| Ubicaciones | Registro y reutilización de las ubicaciones consultadas |
| Favoritos | Agregar, listar y eliminar favoritos |
| Historial | Registro de consultas ACTUAL / PRONÓSTICO de usuarios autenticados y consulta del historial propio |
| Alertas | Reglas por lluvia, temperatura alta y temperatura baja; generación de alertas al consultar clima actual o recibir una lectura del ESP32; anti-duplicado de 60 minutos; historial de alertas |
| Administración | Gestión de usuarios, consulta de alertas y estadísticas básicas |
| Dispositivos | Registro de ESP32 asociados a un usuario y a una ubicación, estado activo/inactivo, última conexión |
| Lecturas físicas | Temperatura, humedad, lluvia, luz y fecha/hora de recepción |
| Calidad | Documentación, control de versiones con Git/GitHub y, en fases posteriores, Docker y GitHub Actions |

## Fuera del alcance

- Mapas interactivos.
- Inteligencia artificial.
- Notificaciones por Telegram o por correo electrónico (las alertas se consultan dentro de la aplicación).
- GPS o geolocalización automática del usuario.
- MQTT (el ESP32 se comunica por HTTP/JSON).
- Redis u otra caché distribuida (D-14 usa `IMemoryCache`).
- Microservicios y Kubernetes.
- Múltiples APIs meteorológicas (solo OpenWeather).
- Funciones sociales (compartir, comentar, seguir usuarios).
- Pagos o suscripciones.
- Almacenar los bloques del pronóstico (D-03).
- Guardar en el historial las consultas de visitantes anónimos (D-04).
