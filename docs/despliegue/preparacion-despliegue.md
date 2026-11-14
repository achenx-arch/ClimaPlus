# ClimaPlus – Preparación para ejecutar en el equipo de destino

**Fecha:** 2026-11-13 · Estado: **listo para ejecutar, no desplegado**. Nada de este documento abre puertos, cambia el
firewall ni publica secretos; esas acciones requieren autorización específica del responsable. La aplicación solo se
considera desplegada cuando `F7_verificar_api.ps1` pasa **en el equipo de destino**.

**Despliegue en la VM de Azure (Ubuntu 24.04):** recomendado con Docker Compose, [`azure-docker.md`](azure-docker.md); alternativa con Nginx + systemd, [`azure-vm.md`](azure-vm.md).

Hay dos formas de ejecutarla. Para la presentación y el montaje del hardware, la **A** (Windows, sin Docker) es la que
ya funciona en tu equipo; la **B** (Docker) es la del servidor.

## 1. Componentes

| Componente | Versión | Puerto (por defecto) | Notas |
|---|---|---|---|
| Backend ASP.NET Core | .NET 8 (`global.json`) | 5208 (`dotnet run --launch-profile http`) · 8080 (Docker) | `/health` público; Swagger solo en `Development` |
| Frontend React/Vite | Node 22 | 5173 (`npm run dev`) · 3000 (Docker/Nginx) | La URL de la API se fija al compilar (`VITE_API_URL`) |
| PostgreSQL | 18 (Windows) · 16 (Docker) | 5432 · sin puerto externo en Docker | Migraciones 001 a 005 |
| Correo | Gmail SMTP 465 TLS (ya probado) · Mailpit en desarrollo | — | Sin correo nadie puede entrar (login en dos pasos) |
| ESP32 | DevKit + DHT11 + LCD + LDR + MH-RD | Envía a la API (5208 o 8080) | `X-Device-Key`; no cambia con esta versión |

## 2. Variables de entorno y secretos

Nunca en Git ni en capturas. En Windows (forma A) van en **secretos de usuario** (`dotnet user-secrets`, fuera del
repositorio); en Docker (forma B) en `.env` (ignorado por Git). Ya configurados en tu equipo: no los cambies.

| Clave (user-secrets) | Variable en Docker (`.env`) | Obligatoria | Uso |
|---|---|---|---|
| `ConnectionStrings:ClimaPlusDb` | `POSTGRES_USER`, `POSTGRES_PASSWORD` | Sí | Base `climaplus_db` |
| `Jwt:Clave` | `JWT_CLAVE` | Sí | 32+ caracteres aleatorios |
| `Codigos:ClaveHmac` | `CODIGOS_CLAVE_HMAC` | Sí | 32+ caracteres, distinta de `Jwt:Clave` |
| `Correo:Host`, `Puerto`, `Seguridad`, `Usuario`, `Password`, `Remitente` | `SMTP_HOST`, `SMTP_PORT`, `SMTP_SEGURIDAD`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM` | Sí | Gmail: `smtp.gmail.com`, 465, `SslAlConectar`, contraseña de aplicación |
| `OpenWeather:ApiKey` | `OPENWEATHER_API_KEY` | No | Sin ella, clima y pronóstico responden 503 |
| `Cors:OrigenesPermitidos:0` | `FRONTEND_URL` | Sí fuera de localhost | Origen exacto del frontend |
| `Google:ClientId` | `GOOGLE_CLIENT_ID` | No | Vacío = "Continuar con Google" desactivado |
| `ProxyInverso:RedesConocidas:0` | `PROXY_RED_CONOCIDA` | Solo con proxy | Para ver la IP real del cliente |
| `AdminInicial:*` | `ADMIN_*` | No | Ya existe el administrador real: déjalas vacías |

Comprobación sin mostrar valores: `dotnet user-secrets list --project backend\ClimaPlus.Api | ForEach-Object { ($_ -split ' = ')[0] }`.
Variables de entorno que **no** deben existir en el equipo de destino salvo que se quieran a propósito:
`ConnectionStrings__ClimaPlusDb` apuntando a otra base, `PGHOSTADDR`, `PGSERVICE` (F7_aplicar_005.ps1 lo comprueba).

## 3. Forma A: Windows, sin Docker (equipo actual)

```powershell
$repo = "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
Set-Location $repo

# 1) Base de datos: PostgreSQL 18 iniciado (servicio "postgresql-x64-18") y migraciones 001 a 005 en climaplus_db
Get-Service postgresql* | Select-Object Name, Status
# (la 005 se aplica con docs/despliegue/migracion-005.md)

# 2) API (consola 1)
dotnet build backend\ClimaPlus.sln -c Release
dotnet run --project backend\ClimaPlus.Api --launch-profile http        # http://localhost:5208

# 3) Frontend (consola 2)
Set-Location frontend
npm run dev                                                             # http://localhost:5173

# 4) Verificación (consola 3)
Set-Location $repo
powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_verificar_api.ps1 -UrlApi http://localhost:5208 -UrlFrontend http://localhost:5173
```

Para servir el frontend compilado en lugar del servidor de desarrollo: `npm run build` y `npx vite preview --port 5173`
(la URL de la API sale de `frontend\.env.development.local` / `VITE_API_URL` al compilar).

**ESP32 en la misma red:** la API con el perfil `http` escucha solo en `localhost`. Para que el ESP32 llegue a la PC
hace falta escuchar en la IP de la red local y permitir el puerto en el Firewall de Windows. **Ambas cosas requieren tu
autorización**; cuando la des, los comandos son:

```powershell
# Escuchar en todas las interfaces de la red local (solo mientras dure la prueba)
dotnet run --project backend\ClimaPlus.Api --launch-profile http -- --urls "http://0.0.0.0:5208"
# Regla de firewall SOLO para la red privada y el puerto 5208 (PowerShell como administrador)
New-NetFirewallRule -DisplayName "ClimaPlus API 5208 (red privada)" -Direction Inbound -Protocol TCP -LocalPort 5208 -Profile Private -Action Allow
# Quitarla al terminar
Remove-NetFirewallRule -DisplayName "ClimaPlus API 5208 (red privada)"
```

En `secrets.h` del ESP32: `API_URL "http://<IP de la PC>:5208/api/lecturas"` (no se modifica el firmware principal).

## 4. Forma B: Docker / servidor

Procedimiento completo en [`guia-despliegue.md`](guia-despliegue.md). Resumen:

```powershell
Copy-Item .env.example .env      # y completa las claves (nunca las de desarrollo)
docker compose up -d --build     # local, con Mailpit
docker compose ps
powershell -ExecutionPolicy Bypass -File docs\herramientas\F7_verificar_api.ps1 -UrlApi http://localhost:8080 -UrlFrontend http://localhost:3000
```

En el servidor: `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d` (exige `SMTP_*`), con un proxy
HTTPS delante. Volumen nuevo: la base se crea con 001 a 005; volumen existente: [`migracion-005.md`](migracion-005.md),
sección 8.

## 5. CORS y HTTPS

- La API solo acepta el origen configurado en `Cors:OrigenesPermitidos` (`http://localhost:5173` en desarrollo,
  `FRONTEND_URL` en Docker). Si el frontend se abre con otra URL (IP de la red, dominio), hay que agregar **ese origen
  exacto**; nunca `*`.
- Fuera de `Development`, la API activa HSTS y redirige a HTTPS. En el servidor, el HTTPS lo pone el proxy
  (Caddy/Nginx con Let's Encrypt) y `ProxyInverso:RedesConocidas` debe contener la red del proxy.
- Para la demostración local en HTTP no hace falta certificado.

## 6. Orden de arranque y verificación

1. PostgreSQL → 2. API (esperar el log de inicio sin migraciones pendientes) → 3. frontend → 4. `F7_verificar_api.ps1`
→ 5. prueba manual: registro con código, inicio de sesión con código, recuperación, cambio de contraseña con código,
clima, favoritos, alertas y, con el hardware, una lectura del ESP32 (201) visible en el panel.

## 7. Recomendaciones de seguridad para producción

- Claves nuevas y aleatorias en producción (`JWT_CLAVE`, `CODIGOS_CLAVE_HMAC`, contraseña de PostgreSQL); la contraseña
  de aplicación de Gmail solo en `.env`/secretos y revocable desde la cuenta de Google.
- PostgreSQL sin puerto expuesto a Internet; usuario de la base distinto de `postgres` para la API.
- Solo 80/443 abiertos en el servidor; el ESP32 por HTTPS cuando haya dominio (riesgo R-5 de `owasp-top10.md`).
- Respaldos programados (`pg_dump -Fc`) guardados fuera del repositorio y probados con una restauración.
- Una sola instancia de la API (los topes de intentos están en memoria) o, si se escala, Redis.
- Revisar `docs/despliegue/notas-version-autenticacion.md` antes de cada versión (las sesiones se cierran al cambiar
  la contraseña o el sello).

## 8. Qué falta para considerarla desplegada

| Pendiente | Quién | Cómo se comprueba |
|---|---|---|
| Compilar y pasar las pruebas en tu equipo | Tú | `dotnet build` 0 errores; `dotnet test` 0 fallidas, 0 omitidas de PostgreSQL |
| 005 en `climaplus_test` y luego en `climaplus_db` | Tú, con `F7_aplicar_005.ps1` | `RESULTADO: CORRECTO` en ambos `resumen.txt` |
| API y frontend en ejecución en el equipo de destino | Tú | `F7_verificar_api.ps1` → `RESULTADO: servicios correctos` |
| Acceso del ESP32 por la red local | Requiere tu autorización (escuchar en la red + regla de firewall privada) | Lectura 201 en el log y en el panel |
| Servidor con HTTPS y dominio (si se publica fuera de la PC) | Requiere autorización | `https://…/health` → `Healthy` |
