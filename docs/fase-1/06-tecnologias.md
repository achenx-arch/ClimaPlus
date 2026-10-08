# Tecnologías

| Área | Tecnología | Uso en ClimaPlus | Fase de uso |
|---|---|---|---|
| Frontend | **React** | Biblioteca de componentes para construir la SPA | Desarrollo |
| Frontend | **TypeScript** | Tipado estático de componentes, DTO y servicios HTTP | Desarrollo |
| Frontend | **Bootstrap** | Sistema de grilla, componentes y estilos responsive | Desarrollo |
| Backend | **ASP.NET Core Web API** | API REST: seguridad, controladores y servicios | Desarrollo |
| ORM | **Entity Framework Core** (proveedor Npgsql) | Mapeo objeto-relacional, migraciones y consultas parametrizadas | Desarrollo |
| Base de datos | **PostgreSQL 16** | Almacenamiento relacional de las 8 tablas (D-05) | Desarrollo |
| API externa | **OpenWeather** | Geocoding, Current Weather y 5 day / 3 hour Forecast, consumidos solo desde el backend | Desarrollo |
| Hardware | **ESP32** | Placa con BME280, sensor de lluvia, LDR, OLED, LED RGB, buzzer y botón | Fase posterior |
| Control de versiones | **Git** | Historial de cambios del proyecto | Desde Fase 1 |
| Control de versiones | **GitHub** | Repositorio remoto y colaboración | Desde Fase 1 |
| Contenedores | **Docker** | Contenedores para la base de datos, el backend y el frontend | **Fase posterior** |
| CI/CD | **GitHub Actions** | Compilación y pruebas automáticas al subir cambios | **Fase posterior** |
| Documentación | **PlantUML** + Markdown | Diagramas de arquitectura y modelo relacional; documentación técnica | Fase 1 |
| Diseño | **Figma** | Mockups de las pantallas | Fase 1 (pendiente) |

## Por qué se eligieron

- **React + TypeScript:** componentes reutilizables (WeatherCard, AlertCard…) y detección de errores de tipos antes de ejecutar.
- **Bootstrap:** acelera una interfaz limpia y responsive sin diseñar estilos desde cero. La propuesta visual de [`08-mockups.md`](08-mockups.md) está pensada para reproducirse con sus clases.
- **ASP.NET Core Web API:** framework robusto para APIs REST, con autenticación JWT, inyección de dependencias, validación de modelos y Rate Limiting incorporados.
- **Entity Framework Core:** evita SQL manual, genera consultas parametrizadas y permite versionar el esquema con migraciones.
- **PostgreSQL 16:** motor relacional libre que soporta `TIMESTAMPTZ`, `CHECK` y `UNIQUE NULLS NOT DISTINCT`, todos usados en el modelo.
- **OpenWeather:** API meteorológica con plan gratuito y cobertura mundial, que ofrece geocodificación, clima actual y pronóstico.
- **ESP32:** microcontrolador económico con Wi-Fi, adecuado para enviar lecturas por HTTP.

## Notas

- Las versiones exactas de .NET, Node.js y los paquetes se fijarán al iniciar la Fase 2 (implementación). Se recomienda usar las versiones LTS vigentes en ese momento.
- **Docker y GitHub Actions no se implementan en la Fase 1.** Solo se reserva la carpeta `.github/workflows/` en la estructura del repositorio.
- No se usarán Redis, MQTT, microservicios ni otras APIs meteorológicas (ver "Fuera del alcance" en [`02-objetivos-alcance.md`](02-objetivos-alcance.md)).
