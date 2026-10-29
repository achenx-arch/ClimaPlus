# Integración React ↔ API ClimaPlus

**Fecha:** 2026-10-29 · **Rama:** `main` (sin commit) · **Responsable:** Integrante 2
**Alcance:** conectar las pantallas de `frontend/` con los endpoints reales del backend. Sin cambios en el backend, la base de datos, las migraciones ni el diseño de las pantallas.

## Configuración

| Tema | Valor |
|---|---|
| URL de la API | `VITE_API_URL`; si no está definida se usa `http://localhost:5208`. Para otra URL, copia `frontend/.env.example` como `frontend/.env.development.local` |
| CORS | El backend permite `http://localhost:5173` (`appsettings.json` → `Cors:OrigenesPermitidos`). Abre el frontend en `localhost`, no en `127.0.0.1` |
| Claves | React no tiene ni pide claves de OpenWeather: todo pasa por el backend |

## Arquitectura

- `src/services/apiClient.ts`: cliente único. Añade `Authorization: Bearer` solo en rutas privadas (o públicas con sesión). Convierte `application/problem+json` en `ApiError` (`status`, `errores` por campo, `Retry-After`). Un 500 nunca muestra detalles internos. Ante un 401 con token, cierra la sesión. No escribe en consola.
- `src/services/*Service.ts`: una función por endpoint, con los nombres JSON reales (`src/types/api.ts`).
- `src/context/AuthProvider.tsx`: sesión (sección 7 de `contrato-auth-perfil.md`).
  - El token vive en memoria.
  - Con "Recordarme", el token y `expiraEn` también se guardan en `sessionStorage`; nunca se usa `localStorage` ni se guarda la contraseña.
  - Al recargar, la sesión se valida con `GET /api/auth/me`.
  - La sesión se cierra sola al llegar a `expiraEn` y ante cualquier 401.
- `src/context/NotificacionesProvider.tsx`:
  - campana con `GET /api/alertas/contador` (al iniciar sesión, cada 60 s y al volver a la pestaña);
  - toasts "Nueva alerta: …" a partir de `alertasGeneradas`, deduplicados por `idAlerta`;
  - toasts de confirmación.
- `src/components/RutaProtegida.tsx`:
  - M06–M12 y M16 requieren sesión;
  - M13–M15 y M17 requieren `ADMINISTRADOR`.
  
  Ocultar rutas es solo ayuda visual: el backend decide.
- `src/hooks/useConsulta.ts`: carga con `AbortController`, ignora respuestas tardías y no usa `setState` síncrono en efectos.
- Las consultas de clima comparten la petición en curso. Así, StrictMode o un doble clic no registran dos consultas ni duplican alertas.

## Pantallas y endpoints

| Pantalla | Endpoints | Notas |
|---|---|---|
| M01 Inicio | — | La búsqueda abre M04 con `?q=`. La tarjeta de ejemplo sigue siendo ilustrativa |
| M02 Login | `POST /api/auth/login` | 401 "Correo o contraseña incorrectos", 403 cuenta desactivada, 429 bloquea el botón `Retry-After` s. USUARIO → M06, ADMINISTRADOR → M13 |
| M03 Registro | `POST /api/auth/register` | Errores por campo (400/409). Éxito → M02 con "Cuenta creada, inicia sesión" |
| M04 Clima actual | `GET /api/clima/ubicaciones`, `GET /api/clima/actual`, `GET`/`POST /api/favoritos` | URL `?q=` (búsqueda) y `?idUbicacion=`. Viento en m/s; sin índice UV (C2). Toast por cada `alertasGeneradas` |
| M05 Pronóstico | `GET /api/clima/ubicaciones`, `GET /api/clima/pronostico` | Días de la API; al elegir un día se ven sus bloques de 3 h en hora local |
| M06 Dashboard | `GET /api/favoritos`, `GET /api/alertas/contador`, `GET /api/alertas` | "Actualizar clima" consulta cada favorito. Dispositivos: "—" |
| M07 Favoritos | `GET`/`POST`/`DELETE /api/favoritos`, `GET /api/clima/ubicaciones` | Escribir filtra; "Buscar" busca ubicaciones nuevas para agregarlas |
| M08 Historial | `GET /api/historial`, `GET /api/historial/{id}` | Filtros tipo, ubicación y día (`desde` incluido, `hasta` excluido); 20 por página; "—" en PRONOSTICO; detalle y "Consultar de nuevo". Se quitaron "Limpiar historial" y el borrado de filas (la API es de solo lectura) |
| M09 Alertas | `GET /api/alertas`, `PATCH /api/alertas/{id}/estado`, `GET /api/configuraciones-alerta` | Filtros Todas/Nuevas/Leídas en el servidor; paginación |
| M10 Configuración | `GET`/`POST`/`PUT`/`PATCH`/`DELETE /api/configuraciones-alerta` | Ubicación: "Todas mis ubicaciones" (global) o un favorito. Se añadió "Eliminar" |
| M15 Alertas del sistema | `GET /api/admin/alertas`, `GET /api/admin/alertas/{id}` | Filtros de estado, tipo y usuario en el servidor; ubicación sobre la página cargada. Sin "Marcar como leída" (solo lectura) |
| M16/M17 Perfil | `GET`/`PUT /api/usuarios/me`, `PUT /api/usuarios/me/password` | Actualiza el nombre en la navbar |

## Sigue simulado (sin backend)

- M11/M12 Dispositivos (ESP32, fase siguiente).
- M13 Panel administrativo (RF-25).
- M14 Gestión de usuarios (RF-26 a RF-28).
- Recuperar contraseña (sin requisito aprobado).

## Pruebas

`npm test` (Vitest + Testing Library + jsdom; 70 casos):

- **Cliente HTTP:** URL, Bearer, 400/401/403/404/409/429/500/502–504, sin conexión, cancelación, sin token en consola.
- **Autenticación y rutas protegidas.**
- **Pantallas:** M04, M05, M07, M08, M09, M10, M15 y M16.

`fetch` se sustituye con respuestas copiadas de los contratos: no se llama al backend real.

## Riesgos conocidos

- Con StrictMode en desarrollo, la sesión restaurada pide `/api/auth/me` dos veces (la primera se cancela). No pasa en producción.
- Los botones "Cerrar sesión" de los menús laterales de M09, M10 y M11 conservan el estilo gris del diseño original, aunque ya funcionan.
- El filtro de ubicación de M08 ofrece los favoritos y las ubicaciones de la página cargada: no hay un endpoint de "ubicaciones consultadas".
