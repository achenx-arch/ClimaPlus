# ClimaPlus – Informe de pruebas · Fase 8

**Fecha:** 2026-10-29 · **Rama:** `feature/fase8-seguridad`

## Resultado general

| Suite | Herramienta | Aprobadas | Fallidas | Omitidas |
|---|---|---|---|---|
| Backend | xUnit (.NET 8), API completa en memoria + PostgreSQL 16 real en base aislada `_test` | **507** | 0 | 0 |
| Frontend | Vitest 5 + Testing Library | **105** | 0 | 0 |
| Base de datos | Scripts SQL con transacción y ROLLBACK (8 archivos) | **71** | 0 | — |
| **Total** | | **683** | **0** | **0** |

Además: `npm run lint` sin errores, `npm run build` correcto, `npm audit` sin vulnerabilidades y 0 paquetes NuGet
vulnerables. Las cifras salen de la ejecución en Linux con PostgreSQL 16; falta confirmarlas en el equipo de desarrollo
(Windows).

## Cómo ejecutarlas

```bash
# Backend (con la API detenida). La base debe terminar en _test.
export CLIMAPLUS_TEST_DB="Host=localhost;Port=5432;Database=climaplus_test;Username=postgres;Password=TU_CLAVE"
dotnet test backend/ClimaPlus.sln

# Frontend
cd frontend && npm test && npm run lint && npm run build

# Base de datos: en pgAdmin, Query Tool sobre climaplus_test, ejecutar cada archivo de database/scripts/pruebas/
```

## Pruebas nuevas de la Fase 8

| Archivo | Pruebas | Qué comprueban |
|---|---|---|
| `backend/.../Seguridad/CabecerasSeguridadTests.cs` | 9 | Cabeceras de seguridad en respuestas 200, 401, 404 y 500; `no-store`; sin cabecera `Server`; CSP de Swagger; 413 con cuerpos mayores a 1 MB; 500 sin detalles internos; `WeatherForecast` eliminado; HSTS de 1 año |
| `backend/.../Seguridad/AutenticacionAvanzadaTests.cs` | 9 | Bloqueo tras 5 fallos (aunque luego la contraseña sea correcta), solo para esa cuenta, también para correos inexistentes, reinicio al acertar, liberación a los 15 minutos, log sin contraseña; revocación de todas las sesiones al cambiar la contraseña; token sin sello rechazado; token sin datos personales |
| `backend/.../Seguridad/AutenticacionAvanzadaTests.cs` (`LimiteGeneralTests`) | 1 | Una ruta con sesión sin límite propio también recibe 429 al superar el límite general |
| `frontend/src/test/modulos.test.tsx` | 1 | Al cambiar la contraseña, el frontend cierra la sesión y lleva al login |

## Pruebas manuales realizadas

| Prueba | Resultado |
|---|---|
| API en Kestrel (Producción): `curl -D -` a `/api/auth/login` | Todas las cabeceras presentes, sin `Server` |
| Cuerpo de 2 MB a `/api/auth/login` | 413 |
| `/swagger` en Producción | No disponible (401) |
| Frontend compilado en Chromium: inicio de sesión y dashboard | 0 violaciones de CSP; iconos y estilos cargan |
| Cambio de contraseña en Chromium | Lleva al login con el aviso "inicia sesión de nuevo con tu nueva contraseña" |
| Seis inicios de sesión fallidos seguidos en Chromium | A partir del sexto: 429 con mensaje para el usuario |

## Evidencias sugeridas para el informe final

1. Salida de `dotnet test` con 507 aprobadas.
2. Salida de `npm test` con 105 aprobadas y de `npm audit` con 0 vulnerabilidades.
3. Cabeceras de seguridad en las herramientas de desarrollo del navegador (pestaña Red → respuesta de `/api/auth/login`).
4. Mensaje de bloqueo después de 5 intentos fallidos.
5. Login con el aviso después de cambiar la contraseña.
