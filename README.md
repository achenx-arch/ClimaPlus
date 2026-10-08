# ClimaPlus

**Panel de clima y alertas ambientales**
Proyecto final del curso **Desarrollo Web** – Universidad Mariano Gálvez de Guatemala.

## Descripción

ClimaPlus es una aplicación web para consultar el clima actual y el pronóstico de cualquier ubicación. Los usuarios registrados pueden guardar ubicaciones favoritas, revisar su historial de consultas y recibir alertas por lluvia, temperatura alta o temperatura baja según sus propios umbrales. Incluye un rol administrador y, en una fase posterior, la integración con un dispositivo ESP32 que envía mediciones ambientales.

Toda la comunicación pasa por el backend: el frontend no consume OpenWeather ni la base de datos directamente, y el ESP32 tampoco accede a la base de datos.

## Objetivo

Desarrollar una aplicación web por capas (React → ASP.NET Core Web API → EF Core → PostgreSQL) que permita consultar información meteorológica, gestionar favoritos, historial y alertas personalizadas, y que quede preparada para recibir lecturas de un ESP32. Ver [`docs/fase-1/02-objetivos-alcance.md`](docs/fase-1/02-objetivos-alcance.md).

## Funcionalidades principales

| Rol | Funcionalidades |
|---|---|
| Público | Buscar ubicaciones, consultar clima actual y pronóstico de 5 días |
| Usuario | Lo anterior, más favoritos, historial (actual / pronóstico), reglas de alerta, alertas y gestión de sus dispositivos ESP32 |
| Administrador | Gestión de usuarios, consulta de alertas del sistema y estadísticas básicas |
| ESP32 | Autenticación con API Key y envío de lecturas (temperatura, humedad, lluvia, luz) |

## Tecnologías

| Área | Tecnología |
|---|---|
| Frontend | React, TypeScript, Bootstrap |
| Backend | ASP.NET Core Web API |
| ORM | Entity Framework Core |
| Base de datos | PostgreSQL 16 |
| API meteorológica | OpenWeather |
| Hardware | ESP32 |
| Control de versiones | Git, GitHub |
| Contenedores | Docker *(fase posterior)* |
| CI/CD | GitHub Actions *(fase posterior)* |

Detalle en [`docs/fase-1/06-tecnologias.md`](docs/fase-1/06-tecnologias.md).

## Arquitectura

```
Usuario → React → ASP.NET Core Web API → Services → EF Core → PostgreSQL
                          │
                          └→ OpenWeather API
ESP32 ──HTTP/JSON + API Key──→ ASP.NET Core Web API
```

- Diagrama: [`docs/arquitectura/arquitectura-climaplus.puml`](docs/arquitectura/arquitectura-climaplus.puml) ([PNG](docs/arquitectura/arquitectura-climaplus.png))
- Explicación: [`docs/fase-1/03-arquitectura.md`](docs/fase-1/03-arquitectura.md)
- Modelo de datos: [`docs/fase-1/04-modelo-datos.md`](docs/fase-1/04-modelo-datos.md) y [`docs/base-datos/diccionario-datos.md`](docs/base-datos/diccionario-datos.md)

## Estructura del repositorio

```
ClimaPlus/
├── frontend/              # Aplicación React (Fase 2)
├── backend/               # ASP.NET Core Web API (Fase 2)
├── database/
│   ├── migrations/        # Migraciones de EF Core (Fase 2)
│   └── scripts/           # Scripts auxiliares de base de datos
├── hardware/
│   └── esp32/             # Firmware del ESP32 (fase posterior)
├── docs/
│   ├── arquitectura/      # Diagrama de arquitectura (.puml + .png)
│   ├── base-datos/        # Modelo relacional y diccionario de datos
│   ├── mockups/           # Exportaciones de Figma
│   └── fase-1/            # Documentación de planeación y diseño
├── .github/
│   └── workflows/         # GitHub Actions (fase posterior)
├── README.md
└── .gitignore
```

## Estado del proyecto

**FASE 1 – Planeación y diseño inicial**

| Elemento | Estado |
|---|---|
| Arquitectura | Terminada |
| Modelo relacional y diccionario de datos | Terminados (v1.0) |
| Documentación de Fase 1 | Organizada |
| Mockups en Figma | Pendientes |
| Repositorio en GitHub | Pendiente |

Checklist completo: [`docs/fase-1/10-checklist-fase1.md`](docs/fase-1/10-checklist-fase1.md).

## Ejecución

Todavía no implementada. Las instrucciones de instalación y ejecución se agregarán cuando exista código del frontend y del backend.
