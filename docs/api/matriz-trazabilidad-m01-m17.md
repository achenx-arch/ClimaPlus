# Matriz de trazabilidad M01–M17 (backend)

**Fecha:** 2026-10-29 (actualizada con el backend de dispositivos y lecturas, Fase 7) · **Rama:** `feature/backend` · **Responsable:** Integrante 2 (backend)
**Fuentes:** `docs/fase-1/08-mockups.md` (M01–M15), `docs/enmiendas/E-01-perfil-usuario-m16-m17.md` (M16–M17), requisitos RF-01 a RF-30 de la auditoría del 2026-10-25, RF-31 a RF-33 de E-01, y el código actual de `backend/ClimaPlus.Api/Controllers`.

## Leyenda de estados

| Estado | Significado |
|---|---|
| **Verificado** | Implementado, compilado y con pruebas aprobadas en Windows; probado a mano cuando se indica |
| **Implementado, sin verificar** | Código entregado; falta la compilación y las pruebas en Windows |
| **Preparado** | DTO, servicios o interfaces listos y probados, pero sin endpoint, porque depende de E-02/E-03 |
| **No implementado** | Sin código |

Último resultado de pruebas confirmado: **308 aprobadas, 14 omitidas (PostgreSQL), 0 fallidas** (historial 2G, 2026-10-28, más Swagger). Las migraciones 002 y 003 están aplicadas en `climaplus_db`. **2026-10-29:** el motor 2H quedó verificado (**400 aprobadas, 0 omitidas** en Windows con `climaplus_test`). La Fase 7 (dispositivos y lecturas) da **488 aprobadas, 0 omitidas** en Linux con PostgreSQL 16; falta confirmarlo en Windows. El motor de alertas (2H, RF-16) está implementado sin verificar: se esperan **383 aprobadas y 17 omitidas** (400 en total).

## Resumen por pantalla

| Pantalla | Rol | Requisitos | Estado del backend | Bloqueo principal |
|---|---|---|---|---|
| M01 Inicio público | Público | RF-04 | Verificado | — |
| M02 Iniciar sesión | Público | RF-02, RF-03 | Verificado | — |
| M03 Registro | Público | RF-01 | Verificado | — |
| M04 Clima actual | Público / Usuario | RF-05, RF-07, RF-09, RF-16 | Búsqueda, favoritos y clima actual verificados; alertas (RF-16) y `alertasGeneradas` implementados sin verificar | — |
| M05 Pronóstico | Público / Usuario | RF-06, RF-09 | Verificado | — |
| M06 Dashboard usuario | Usuario | RF-19 (reutiliza RF-08, 17, 20) | Parcial: favoritos verificados, contador de alertas sin verificar; dispositivos no implementados | Dispositivos; datos reales de consultas |
| M07 Favoritos | Usuario | RF-08, RF-09, RF-10 | Verificado | — (`ultimaConsulta` será `null` hasta E-02/E-03) |
| M08 Historial | Usuario / Administrador (su cuenta) | RF-11 | Verificado (308 pruebas y Swagger) | — |
| M09 Alertas | Usuario | RF-17, RF-18 | Verificado (221 pruebas); alertas reales con el motor 2H, sin verificar | — |
| M10 Configuración de alertas | Usuario | RF-12 a RF-15 | Verificado (182 pruebas, 7 escenarios manuales) | — |
| M11 Mis dispositivos | Usuario | RF-20, RF-21, RF-22 | Backend verificado (488 pruebas en Windows); frontend conectado a la API real, sin verificar en Windows (`test/dispositivos.test.tsx`) | — |
| M12 Detalle dispositivo | Usuario | RF-22, RF-23 | Backend verificado (488 pruebas en Windows); frontend conectado a la API real, sin verificar en Windows (`test/dispositivos.test.tsx`) | — |
| M13 Dashboard administrador | Administrador | RF-25 | No implementado | Datos de consultas (E-02/E-03) y dispositivos |
| M14 Gestión de usuarios | Administrador | RF-26, RF-27, RF-28 | No implementado | Ninguno técnico (usa la tabla `usuario`) |
| M15 Alertas del sistema | Administrador | RF-29 | Verificado (221 pruebas); alertas reales con el motor 2H, sin verificar | — |
| M16 Mi perfil (usuario) | Usuario | RF-31, RF-32, RF-33 | Verificado (Fase 2D) | — |
| M17 Mi perfil (administrador) | Administrador | RF-31, RF-32, RF-33 | Verificado (Fase 2D) | — |

## Detalle por endpoint

| Pantalla | Requisito | Método y ruta | Rol | Estado | Pruebas | Dependencias / notas |
|---|---|---|---|---|---|---|
| M03 | RF-01 Registro | `POST /api/auth/register` | Público | Verificado | `AutenticacionTests`, `SeguridadEntradasTests` | La auditoría proponía `/registro`; quedó `/register` (`contrato-auth-perfil.md`) |
| M02 | RF-02 Inicio de sesión | `POST /api/auth/login` | Público | Verificado | `AutenticacionTests`, `RateLimitingTests` | Rate limiting de 5 por minuto |
| Todas | RF-03 Sesión y rol | `GET /api/auth/me` + JWT, roles y política por defecto | Autenticado | Verificado | `AutenticacionTests` | — |
| M01, M04 | RF-04 Buscar ubicación | `GET /api/clima/ubicaciones?q=&limite=` | Público | Verificado (con OpenWeather real) | `Clima/UbicacionesTests` | INC-01: Swagger UI no interpreta el patrón de `q` (no es un fallo del backend) |
| — | RF-07 Reutilizar ubicaciones | Interno (`UbicacionService`) | — | Verificado | `Clima/UbicacionesTests` | La concurrencia real solo se prueba con PostgreSQL |
| M04 | RF-05 Clima actual | `GET /api/clima/actual?idUbicacion=` | Público | Verificado (con OpenWeather real) | `Clima/ClimaActualTests`, `Clima/ClimaTransformadorTests` | Campo nuevo `alertasGeneradas` (2H), sin verificar |
| M05 | RF-06 Pronóstico 5 días | `GET /api/clima/pronostico?idUbicacion=` | Público | Verificado (con OpenWeather real) | `Clima/PronosticoTests`, `Clima/ClimaTransformadorTests` | — |
| M07, M06 | RF-08 Listar favoritos | `GET /api/favoritos` | Autenticado | Verificado | `Favoritos/FavoritosTests` | `ultimaConsulta` es `null` hasta que se registren consultas |
| M07 | — | `GET /api/favoritos/{id}` | Autenticado | Verificado | `Favoritos/FavoritosTests` | Soporte del `Location` del POST |
| M04, M05, M07 | RF-09 Agregar favorito | `POST /api/favoritos` | Autenticado | Verificado (manual: 409 en duplicado) | `Favoritos/FavoritosTests` | — |
| M07 | RF-10 Eliminar favorito | `DELETE /api/favoritos/{id}` | Autenticado | Verificado (manual: 204 y 404) | `Favoritos/FavoritosTests` | — |
| M08 | RF-11 Historial | `GET /api/historial?tipo=&idUbicacion=&desde=&hasta=&pagina=` | Autenticado | Verificado | `Historial/HistorialTests` | Solo consultas propias; anónimas excluidas (`contrato-historial.md`) |
| M08 | RF-11 Detalle de consulta | `GET /api/historial/{idConsulta}` | Autenticado | Verificado | `Historial/HistorialTests` | Ajena, anónima o inexistente: 404 idéntico |
| M10 | RF-12 Listar reglas | `GET /api/configuraciones-alerta` | Autenticado | Verificado | `Alertas/ConfiguracionesAlertaTests` | — |
| M10 | — | `GET /api/configuraciones-alerta/{id}` | Autenticado | Verificado | ídem | — |
| M10 | RF-13 Crear regla | `POST /api/configuraciones-alerta` | Autenticado | Verificado | ídem | La auditoría proponía 400 para alta ≤ baja; quedó 409 (conflicto con otra regla), validado por el equipo |
| M10 | RF-14 Editar regla | `PUT /api/configuraciones-alerta/{id}` | Autenticado | Verificado | ídem | — |
| M10 | RF-14 Activar/pausar | `PATCH /api/configuraciones-alerta/{id}/estado` | Autenticado | Verificado | ídem | — |
| M10 | RF-15 Eliminar regla | `DELETE /api/configuraciones-alerta/{id}` | Autenticado | Verificado | ídem | — |
| M04 (toast), M09 | RF-16 Generar alertas (origen consulta) | Dentro de `GET /api/clima/actual` con sesión; respuesta `alertasGeneradas` | Autenticado | Implementado, sin verificar | `Alertas/EvaluadorAlertasTests`, `Alertas/MotorAlertasTests`, `Datos/MotorAlertasPostgreSqlTests` | Transacción + `pg_advisory_xact_lock`; sin migraciones. Origen ESP32 implementado en la Fase 7 (RF-24) |
| M09, M06, navbar | RF-17 Listar mis alertas | `GET /api/alertas?estado=&tipo=&pagina=` | Autenticado | Verificado | `Alertas/AlertasTests`, `Alertas/MotorAlertasTests` | Con alertas reales desde 2H |
| navbar, M06 | RF-17 Contador | `GET /api/alertas/contador` | Autenticado | Verificado | ídem | Sube con cada alerta del motor (2H) |
| M09 | RF-18 Marcar como leída | `PATCH /api/alertas/{id}/estado` | Autenticado | Verificado | ídem | La auditoría proponía `/leida`; se usó `/estado` según la autorización de esta fase |
| M06 | RF-19 Dashboard usuario | Reutiliza RF-08, RF-17 y RF-20 | Autenticado | Parcial | — | Falta RF-20 (dispositivos) |
| M11, M06 | RF-20 Listar dispositivos | `GET /api/dispositivos` | Autenticado | Implementado, sin verificar | `Dispositivos/DispositivosTests`, `Datos/LecturasPostgreSqlTests` | `enLinea` = lectura en los últimos 5 min (DP-F7-1) |
| M11 | RF-21 Registrar dispositivo | `POST /api/dispositivos` | Autenticado | Implementado, sin verificar | `Dispositivos/DispositivosTests`, `Dispositivos/ApiKeyDispositivoTests` | API Key mostrada una vez; solo hash SHA-256 (RN-BD-28) |
| M11, M12 | RF-22 Editar y activar/desactivar | `PUT /api/dispositivos/{id}`, `PATCH /api/dispositivos/{id}/estado` | Autenticado | Implementado, sin verificar | `Dispositivos/DispositivosTests` | Desactivar revoca la API Key (D-11) |
| M12 | RF-23 Detalle, lecturas y alertas | `GET /api/dispositivos/{id}`, `GET /api/dispositivos/{id}/lecturas`, `GET /api/dispositivos/{id}/alertas` | Autenticado (dueño) | Implementado, sin verificar | `Dispositivos/DispositivosTests`, `Dispositivos/LecturasTests` | Paginación de 20 |
| — | RF-24 Recibir lecturas del ESP32 | `POST /api/lecturas` con `X-Device-Key` | Dispositivo | Implementado, sin verificar | `Dispositivos/LecturasTests`, `Alertas/EvaluadorAlertasLecturaTests`, `Datos/LecturasPostgreSqlTests` | Genera alertas (RN-BD-33); rate limiting 30/min por dispositivo |
| M13 | RF-25 Estadísticas | `GET /api/admin/estadisticas?periodo=` | Administrador | No implementado | — | Consultas por periodo y top 5 dependen de E-02/E-03; dispositivos |
| M14 | RF-26 Listar y filtrar usuarios | `GET /api/admin/usuarios` | Administrador | No implementado | — | Sin bloqueo técnico; paginación de 20 |
| M14 | RF-27 Cambiar rol | `PATCH /api/admin/usuarios/{id}/rol` | Administrador | No implementado | — | RN-BD-09: no degradar al último administrador activo |
| M14 | RF-28 Activar/desactivar usuario | `PATCH /api/admin/usuarios/{id}/estado` | Administrador | No implementado | — | RN-BD-09; RN-BD-08 (sin borrado físico) |
| M15 | RF-29 Alertas del sistema | `GET /api/admin/alertas?tipo=&estado=&origen=&desde=&hasta=&idUsuario=&pagina=` | Administrador | Verificado | `Alertas/AlertasTests` | Con alertas reales desde 2H; buscar usuario por nombre/correo vendrá con RF-26 |
| M15 | RF-29 Detalle | `GET /api/admin/alertas/{id}` | Administrador | Verificado | ídem | — |
| — | RF-30 Proteger rutas públicas | Rate limiting de `/api/clima/*` (30/min) e `IMemoryCache` de búsquedas | — | Verificado | `ClimaRateLimitingTests` | — |
| M16, M17 | RF-31 Ver perfil | `GET /api/usuarios/me` | Autenticado | Verificado | `PerfilTests` | — |
| M16, M17 | RF-32 Editar nombre y correo | `PUT /api/usuarios/me` | Autenticado | Verificado | `PerfilTests` | RN-BD-37, RN-BD-38 |
| M16, M17 | RF-33 Cambiar contraseña | `PUT /api/usuarios/me/password` | Autenticado | Verificado | `PerfilTests` | Rate limiting de autenticación |

## Pendientes agrupados por dependencia

| Grupo | Requisitos | Puede empezar ya | Depende de |
|---|---|---|---|
| Clima persistido | RF-05, RF-06 | Verificado (2E) | — |
| Historial | RF-11 | Verificado (2G) | — |
| Motor de alertas | RF-16, RF-24 | Implementado para ambos orígenes (consulta 2H, lectura Fase 7) | — |
| Gestión de usuarios | RF-26, RF-27, RF-28 | **Sí**: usa solo la tabla `usuario` | — |
| Dispositivos | RF-20 a RF-23 | Backend y pantallas M11/M12 implementados (Fase 7) | Verificar M11/M12 en Windows |
| Lecturas ESP32 | RF-24 | Backend y firmware implementados (Fase 7, `hardware/esp32`) | Prueba con la placa real |
| Estadísticas | RF-25 | Sí (usuarios, consultas y alertas); dispositivos cuando exista RF-20 | — |

## Funciones pendientes por tipo

- **Dispositivos ESP32:** RF-20 a RF-24 con backend implementado en la Fase 7 (`contrato-dispositivos-lecturas.md`). Pantallas M11/M12 conectadas a la API y firmware en `hardware/esp32` (compila con el paquete esp32 3.0.7). Falta la prueba con la placa real.
- **Administración:** RF-26 a RF-28 (M14). Nada implementado; RF-29 (M15) implementado sin verificar.
- **Estadísticas:** RF-25 (M13). Nada implementado.
- **Usuario:** RF-16 (motor) implementado sin verificar; falta su origen ESP32. RF-19 (M06) parcial.

## Incidencias abiertas

- **INC-01** (`incidencias-pendientes.md`): Swagger UI y el patrón Unicode de `q`. Afecta solo a la documentación interactiva.
