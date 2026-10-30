# ClimaPlus – Docker, CI/CD y despliegue · Fase 9

**Fecha:** 2026-10-30 · **Rama:** `feature/fase9-devops`

## Resumen

ClimaPlus se ejecuta completo con un solo comando (`docker compose up`) en tres contenedores: PostgreSQL, la API y el
frontend servido con Nginx. GitHub Actions prueba cada Pull Request (backend, base de datos, frontend y los contenedores)
y, cuando `main` pasa todas las pruebas, publica las imágenes en GitHub Container Registry listas para desplegar.

```
                     ┌──────────── docker compose ─────────────┐
 Navegador ── :3000 ─┤ web  (Nginx sin privilegios, SPA React) │
 Navegador ── :8080 ─┤ api  (ASP.NET Core 8, usuario "app")    ├── OpenWeather
 ESP32 ────── :8080 ─┤                                          │
                     │ db   (PostgreSQL 16, sin puerto externo) │
                     └──────────────────────────────────────────┘
```

| Archivo | Para qué |
|---|---|
| `backend/Dockerfile` | Imagen de la API en dos etapas (SDK → runtime), usuario sin privilegios, puerto 8080 |
| `frontend/Dockerfile` | Compila React con Node 22 y lo sirve con `nginx-unprivileged` en el puerto 8080 |
| `frontend/nginx/` | Rutas de React, caché de recursos, cabeceras de seguridad, bloqueo de archivos ocultos |
| `docker-compose.yml` | Base de datos + API + frontend para correr todo localmente |
| `docker-compose.prod.yml` | Usa las imágenes publicadas en GHCR en lugar de construirlas |
| `.env.example` | Plantilla de variables (contraseñas y claves); se copia como `.env`, que no se sube |
| `.github/workflows/ci.yml` | Integración continua: 4 trabajos de pruebas |
| `.github/workflows/cd.yml` | Entrega continua: publica las imágenes en GHCR |
| `.github/dependabot.yml` | Actualizaciones semanales de NuGet, npm, Actions y Docker |

## 1. Ejecutar todo con Docker

Requisito: **Docker Desktop** (Windows: con WSL 2).

```bash
cp .env.example .env
# Edita .env: POSTGRES_PASSWORD, JWT_CLAVE (32+ caracteres aleatorios) y OPENWEATHER_API_KEY
docker compose up -d --build
docker compose ps
```

| Servicio | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:8080 |
| Salud de la API | http://localhost:8080/health → `Healthy` |

- La **primera vez**, PostgreSQL ejecuta automáticamente `database/migrations/001`, `002` y `003`.
- Los datos quedan en el volumen `climaplus_datos-postgres` y sobreviven a `docker compose down`.
- `docker compose down -v` **borra** la base de datos de Docker (no toca tu PostgreSQL local ni `climaplus_db`).
- Registros: `docker compose logs -f api`.
- Si defines `ADMIN_CORREO` y `ADMIN_PASSWORD` en `.env`, se crea el administrador inicial al arrancar.

### Variables (`.env`)

| Variable | Obligatoria | Uso |
|---|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` | Sí | Usuario de la base de datos dentro de Docker |
| `JWT_CLAVE` | Sí | Firma de los JWT. 32 caracteres o más, aleatoria; la API no arranca con una más corta |
| `OPENWEATHER_API_KEY` | No | Sin ella, clima y pronóstico responden 503 |
| `FRONTEND_URL` | No | Origen permitido por CORS (por defecto `http://localhost:3000`) |
| `API_PUBLIC_URL` | No | URL de la API que usa el navegador; se fija al construir el frontend |
| `ADMIN_NOMBRE` / `ADMIN_CORREO` / `ADMIN_PASSWORD` | No | Administrador inicial |

Si cambias `API_PUBLIC_URL`, reconstruye el frontend: `docker compose build web`.

### ESP32 con Docker
En `secrets.h` del firmware usa `API_URL "http://<IP de tu PC>:8080/api/lecturas"` y permite el puerto **8080** en el
Firewall de Windows. El resto es igual que en `hardware/esp32/README.md`.

## 2. Integración continua (`ci.yml`)

Se ejecuta en cada Pull Request hacia `main`, en cada push a `main` y a mano desde la pestaña **Actions**.

| Trabajo | Qué hace | Falla si… |
|---|---|---|
| **backend** | Restaura, compila en Release y corre las pruebas xUnit contra un PostgreSQL 16 de servicio (`climaplus_test`). Revisa paquetes NuGet vulnerables. Guarda los resultados (`.trx`) | Alguna prueba falla o se omite; hay un paquete vulnerable |
| **base-datos** | Aplica las migraciones 001–003 en una base vacía y ejecuta las 71 pruebas SQL y el script E-02/E-03 | Hay un `FAIL`, menos de 71 `PASS` o una restricción esperada no rechaza |
| **frontend** | `npm ci`, lint, Vitest, build y `npm audit --audit-level=high` | Cualquiera de los pasos falla |
| **docker** | Construye las dos imágenes, levanta los tres contenedores con secretos generados en el momento, espera `/health` y hace una prueba de humo (registro, login, cabeceras de seguridad y rutas de React en Nginx) | Algo no arranca o no responde como se espera |

El trabajo **docker** solo corre si los otros tres pasaron.

**Recomendado en GitHub** (Settings → Branches → Add rule para `main`): exigir Pull Request y que pasen los checks
`Backend`, `Base de datos`, `Frontend` y `Docker Compose`. Así nada llega a `main` sin pruebas.

## 3. Entrega continua (`cd.yml`)

Cuando la CI de `main` termina bien, se publican:

- `ghcr.io/achenx-arch/climaplus-api:latest` y `:sha-<commit>`
- `ghcr.io/achenx-arch/climaplus-web:latest` y `:sha-<commit>`

No requiere secretos: usa el `GITHUB_TOKEN` del workflow. La URL pública de la API para el frontend se toma de la
variable del repositorio `API_PUBLIC_URL` (Settings → Secrets and variables → Actions → **Variables**). Las imágenes se
ven en el perfil de GitHub → **Packages**. La primera vez quedan privadas; para descargarlas sin iniciar sesión, cambia
su visibilidad a pública en la configuración del paquete.

## 4. Despliegue en un servidor

Sirve cualquier VPS o máquina con Linux y Docker (por ejemplo una VM de la universidad, DigitalOcean, Azure o AWS).

1. Copia al servidor `docker-compose.yml`, `docker-compose.prod.yml`, `.env.example` y la carpeta `database/migrations`.
2. Crea `.env` con valores reales de producción:
   - `JWT_CLAVE` y `POSTGRES_PASSWORD` nuevos y aleatorios (nunca los de desarrollo).
   - `FRONTEND_URL=https://<tu-dominio>` y, en GitHub, la variable `API_PUBLIC_URL=https://api.<tu-dominio>`.
3. Descarga y arranca las imágenes publicadas:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.prod.yml pull
   docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   ```

4. Pon delante un **proxy con HTTPS** (Caddy, Nginx o Traefik con certificado de Let's Encrypt) que envíe
   `https://<tu-dominio>` al puerto 3000 y `https://api.<tu-dominio>` al 8080. Con HTTPS, la API envía HSTS
   automáticamente; en el frontend, activa la línea de `Strict-Transport-Security` de `frontend/nginx/cabeceras-seguridad.conf`.
5. Cierra en el firewall del servidor todo excepto 80 y 443.
6. Actualizar a una versión nueva: repite el paso 3 (o fija `VERSION=sha-<commit>` en `.env` para volver a una anterior).

### Respaldo de la base de datos

```bash
docker compose exec db pg_dump -U climaplus -d climaplus_db -Fc > respaldo-$(date +%F).dump
# Restaurar en una base vacía:
docker compose exec -T db pg_restore -U climaplus -d climaplus_db --clean < respaldo-AAAA-MM-DD.dump
```

## 5. Seguridad del despliegue (continuación de la Fase 8)

| Medida | Dónde |
|---|---|
| Contenedores sin usuario root (API: `app`; frontend: `nginx`) | Dockerfiles |
| PostgreSQL sin puerto expuesto fuera de Docker | `docker-compose.yml` |
| Secretos solo en `.env` (ignorado por Git) o en variables del servidor; nunca en las imágenes | `.env.example`, `.dockerignore` |
| `appsettings.Development.json` y los proyectos de prueba no entran en la imagen | `backend/.dockerignore` |
| Nginx sin versión (`server_tokens off`), cabeceras de seguridad, `frame-ancestors 'none'` y archivos ocultos bloqueados | `frontend/nginx/` |
| Swagger desactivado (la imagen corre en `Production`) | `Program.cs` |
| Paquetes vulnerables bloquean la CI; Dependabot propone actualizaciones | `ci.yml`, `dependabot.yml` |
| `/health` público pero sin detalles internos | `VerificacionSalud.cs` |

Con esto quedan atendidos los riesgos residuales R-6 (cabeceras del frontend como cabeceras HTTP), R-7 (escaneo
automático de dependencias) y, al desplegar con el proxy HTTPS, R-5 (ESP32 por HTTPS) de
[`../seguridad/owasp-top10.md`](../seguridad/owasp-top10.md).

### Limitaciones conocidas
- Detrás de un proxy, la API ve la IP del proxy; los límites por IP se comparten entre todos los clientes. Si se
  despliega con proxy, conviene habilitar `ForwardedHeaders` con la IP del proxy como confiable.
- Sin HTTPS (por ejemplo con `docker compose` local), la API registra el aviso "Failed to determine the https port for
  redirect": es esperado, porque el HTTPS lo pone el proxy.
- El rate limiting y el bloqueo por cuenta están en memoria: con varias réplicas de la API se necesitaría Redis.

## 6. Verificación de esta fase

| Comprobación | Resultado |
|---|---|
| `hadolint` en los dos Dockerfiles | Sin advertencias |
| `actionlint` + `shellcheck` en los workflows | Sin advertencias |
| `docker compose config` (con y sin `docker-compose.prod.yml`) | Válido; sin `.env` falla con un mensaje claro |
| `nginx -t` con la configuración real y el build de React | Correcto; rutas de React → 200, `/.env` → 404, cabeceras presentes, `/assets` con caché de 1 año |
| Base vacía + migraciones 001–003 (igual que `docker-entrypoint-initdb.d`) | Aplicadas sin error |
| API publicada en `Production`, puerto 8080, configurada solo con variables de entorno | `/health` 200 (503 sin base), registro, login, administrador inicial, lectura del ESP32 201, CORS solo para `localhost:3000` |
| Pasos del trabajo `base-datos` ejecutados localmente | 71 pruebas SQL aprobadas, E-02/E-03 correcto |
| Pruebas | 509 xUnit · 105 Vitest · 71 SQL |

No se pudieron construir las imágenes en el entorno de desarrollo del asistente (sin acceso a Docker Hub ni a MCR):
la primera construcción real ocurre con `docker compose up --build` en la PC del equipo y en el trabajo **docker** de la CI.
