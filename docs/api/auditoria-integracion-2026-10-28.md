# Auditoría de integración backend – frontend (2026-10-28)

**Rama de trabajo:** `feature/backend`, en `3999088` (igual que `main`, ya incluye el frontend de `feature/frontend-base-react`), con los cambios de backend de las fases 2E–2H, E-02/E-03 y alertas generadas **sin commit**.
**Base de datos:** `climaplus_db` con `InitialCreate`, `ConsultaClimaUsuarioOpcional` y `ConsultaClimaDetalle` aplicadas (confirmado por ACHX).
**Última ejecución confirmada:** 231 pruebas aprobadas, 14 omitidas (PostgreSQL), 0 fallidas.

## 1. Estado del repositorio

| Elemento | Estado |
|---|---|
| Ramas | `main` = `feature/backend` = `3999088`; remotas `origin/feature/backend` (antigua) y `origin/feature/frontend-base-react` |
| Cambios del merge `3999088` | Solo `frontend/` (60 archivos). No tocó `backend/`, `docs/` ni `database/` |
| Cambios locales sin commit | Backend 2E–2H, alertas generadas, E-02/E-03 (código, migraciones 002/003, scripts, documentación) |
| Migraciones | `InitialCreate`, `002` y `003`; snapshot sin cambios pendientes (prueba automática y `has-pending-model-changes`) |
| Archivos de plantilla | `WeatherForecastController.cs` y `WeatherForecast.cs` siguen en el proyecto (expuestos en `/WeatherForecast` con autenticación). Se recomienda quitarlos con autorización |

## 2. Funcionalidades (backend)

| Funcionalidad (Variante 6 / RF) | Pantallas | Estado |
|---|---|---|
| Registro, login, JWT, roles, perfil (RF-01–03, 31–33) | M02, M03, M16, M17 | **Implementado y verificado** |
| Búsqueda de ubicaciones (RF-04, RF-07) | M01, M04 | **Implementado y verificado** (OpenWeather real) |
| Clima actual guardado antes de mostrar (RF-05) | M04 | **Implementado en este bloque, sin verificar** |
| Pronóstico de 5 días guardado (RF-06) | M05 | **Implementado en este bloque, sin verificar** |
| Favoritos (RF-08–10) | M06, M07 | **Implementado y verificado** |
| Historial (RF-11) | M08 | **Pendiente** (Bloque 3, 2G) |
| Reglas de alerta (RF-12–15) | M10 | **Implementado y verificado** |
| Motor de alertas (RF-16) | M04, M09 | **Pendiente** (Bloque 4, 2H) |
| Alertas generadas y contador (RF-17, 18, 29) | M09, M15, navbar | **Implementado y verificado** (vacías hasta que exista el motor) |
| Dashboard de usuario (RF-19) | M06 | **Parcial**: favoritos y contador listos; faltan dispositivos |
| Dispositivos y lecturas ESP32 (RF-20–24) | M11, M12 | **Pendiente** |
| Estadísticas (RF-25) | M13 | **Pendiente** |
| Gestión de usuarios (RF-26–28) | M14 | **Pendiente** |
| Protección de rutas públicas (RF-30) | — | **Implementado y verificado** (rate limiting y caché) |

## 3. Frontend (estado en `main`)

| Pantalla | Archivo | Consume la API | Datos actuales |
|---|---|---|---|
| M01 Inicio | `Home.tsx` | No | Demostración |
| M02 Login | `Login.tsx` | `POST /api/auth/login` | Real, pero **no guarda el token ni navega** (muestra un `alert`) |
| M03 Registro | `Register.tsx` | `POST /api/auth/register` | Real |
| M04 Clima actual | `CurrentWeather.tsx` | No | 3 ciudades fijas; favoritos e historial en `localStorage` |
| M05 Pronóstico | `Forecast.tsx` | No | Demostración |
| M06 Dashboard | `Dashboard.tsx` | No | Demostración |
| M07 Favoritos | `Favorites.tsx` | No | `localStorage` |
| M08 Historial | `History.tsx` | No | `localStorage` |
| M09 Alertas | `Alerts.tsx` | No | `localStorage` |
| M10 Config. alertas | `AlertSettings.tsx` | No | `localStorage` |
| M11, M12 Dispositivos | `Devices.tsx`, `DeviceDetails.tsx` | No | `localStorage` |
| M13–M15 Administración | `AdminDashboard.tsx`, `AdminUsers.tsx`, `AdminAlerts.tsx` | No | Demostración |
| M16, M17 Perfil | `UserProfile.tsx` | No | `localStorage` |
| (sin mockup) Recuperar contraseña | `RecoverPassword.tsx` | No | Solo valida el formulario |

## 4. Incompatibilidades detectadas

| # | Problema | Impacto | Propuesta (sin modificar el frontend en este bloque) |
|---|---|---|---|
| I-1 | `authService.ts` usa `http://localhost:5000` si no hay `VITE_API_URL`; el backend escucha en `http://localhost:5208` (`launchSettings.json`, perfil `http`) | Login y registro fallan con "No se pudo conectar" | Crear `frontend/.env.development` con `VITE_API_URL=http://localhost:5208` (archivo del Integrante 3; no contiene secretos) |
| I-2 | El login no guarda el token ni el usuario, y no redirige según el rol | Ninguna pantalla privada puede llamar a la API | Contexto de sesión en memoria (D-18) y redirección USUARIO → M06, ADMINISTRADOR → M13 |
| I-3 | Todas las pantallas, salvo login y registro, usan datos de demostración o `localStorage` | Los datos no llegan a PostgreSQL | Servicios por módulo según los contratos de `docs/api/` |
| I-4 | M04 muestra "índice UV" | OpenWeather gratuito no lo ofrece (C2) | Quitarlo o mostrar "No disponible" |
| I-5 | El viento se muestra en km/h; la API lo entrega en m/s | Valores incorrectos si se pintan tal cual | Convertir en el frontend (× 3,6) |
| I-6 | M04 y M05 buscan por nombre de ciudad en la URL (`?ciudad=`) | La API usa `idUbicacion` (D-20) | Navegar con `?idUbicacion=` tras la búsqueda |
| I-7 | `RecoverPassword.tsx` no tiene mockup ni requisito aprobado | Fuera del alcance | Mantenerlo deshabilitado o aprobarlo como enmienda |
| I-8 | Perfil `https` redirige HTTP → HTTPS (`UseHttpsRedirection`) | Un preflight CORS redirigido falla en el navegador | Usar el perfil `http` (5208) en desarrollo |
| I-9 | CORS permite solo `http://localhost:5173` | Correcto para `vite` por defecto; otro puerto sería rechazado | Sin cambio |

El frontend es del Integrante 3: estas propuestas se le entregan como guía; no se modificó ningún archivo de `frontend/`.

## 5. Próximos bloques

1. **Bloque 2 (2E)** – clima actual y pronóstico: implementado, pendiente de verificar en Windows.
2. **Bloque 3 (2G)** – historial (M08).
3. **Bloque 4 (2H)** – motor de alertas.
4. **Bloque 5 (2I)** – usuarios, dispositivos y lecturas, estadísticas e integración del frontend.
