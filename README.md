# ClimaPlus

**Panel de clima y alertas ambientales**

[![CI](https://github.com/achenx-arch/ClimaPlus/actions/workflows/ci.yml/badge.svg)](https://github.com/achenx-arch/ClimaPlus/actions/workflows/ci.yml)
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
| Contenedores | Docker y Docker Compose (Nginx para el frontend) |
| CI/CD | GitHub Actions (CI + publicación de imágenes en GHCR) y Dependabot |

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
│   └── esp32/             # Firmware del ESP32 y simulación en Wokwi
├── docs/
│   ├── arquitectura/      # Diagrama de arquitectura (.puml + .png)
│   ├── base-datos/        # Modelo relacional y diccionario de datos
│   ├── mockups/           # Exportaciones de Figma
│   ├── api/               # Contratos de la API y matriz de trazabilidad
│   ├── seguridad/         # Análisis OWASP Top 10 e informe de pruebas
│   ├── despliegue/        # Guía de Docker, CI/CD y despliegue
│   └── fase-1/            # Documentación de planeación y diseño
├── .github/
│   ├── workflows/         # ci.yml (pruebas) y cd.yml (imágenes en GHCR)
│   └── dependabot.yml
├── docker-compose.yml     # Base de datos + API + frontend
├── docker-compose.prod.yml
├── .env.example           # Variables de Docker Compose (copiar como .env)
├── README.md
└── .gitignore
```

## Estado del proyecto

| Fase | Contenido | Estado |
|---|---|---|
| 1 | Planeación, arquitectura, modelo de datos y mockups | Terminada |
| 2–6 | Backend (JWT, clima, favoritos, historial, alertas), frontend React e integración por roles | Terminadas |
| 7 | Dispositivos ESP32: API Key, lecturas, alertas físicas, M11/M12 y firmware | Terminada (falta la prueba con la placa real) |
| 8 | Seguridad OWASP Top 10 y pruebas | Terminada ([`docs/seguridad/owasp-top10.md`](docs/seguridad/owasp-top10.md)) |
| 9 | Docker, GitHub Actions y despliegue | Terminada ([`docs/despliegue/guia-despliegue.md`](docs/despliegue/guia-despliegue.md)) |
| 10 | Entrega: evidencias, documentación final y presentación | En curso |

Pruebas: 509 xUnit (backend, con PostgreSQL real), 105 Vitest (frontend) y 71 SQL (base de datos).

## Ejecución

### Opción 1: todo con Docker (recomendada para probar)

Requisito: Docker Desktop.

```bash
cp .env.example .env          # cambia las claves dentro de .env
docker compose up -d --build
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:8080 (salud: http://localhost:8080/health) |
| ESP32 | `http://<IP de la PC>:8080/api/lecturas` |

Detalles, variables y despliegue en un servidor: [`docs/despliegue/guia-despliegue.md`](docs/despliegue/guia-despliegue.md).

### Opción 2: desarrollo local

Requisitos: .NET 8 SDK, Node.js 22 y PostgreSQL 16.

```bash
# API en http://localhost:5208 (configura antes los User Secrets: ConnectionStrings:ClimaPlusDb, Jwt:Clave, OpenWeather:ApiKey)
dotnet run --project backend/ClimaPlus.Api --launch-profile http

# Frontend en http://localhost:5173
cd frontend && npm install && npm run dev
```

### Pruebas

```bash
export CLIMAPLUS_TEST_DB="Host=localhost;Port=5432;Database=climaplus_test;Username=postgres;Password=TU_CLAVE"
dotnet test backend/ClimaPlus.sln
cd frontend && npm test && npm run lint && npm run build
```
