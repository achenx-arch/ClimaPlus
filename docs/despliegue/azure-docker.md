# ClimaPlus en Azure con Docker Compose (forma recomendada)

**Estado:** propuesta preparada y revisada, **no ejecutada**. ClimaPlus solo se considera desplegado cuando
`deploy/azure/scripts/verificar-docker.sh` termina con `RESULTADO: ClimaPlus está ejecutándose correctamente` en la VM.
La alternativa sin Docker (Nginx + systemd) sigue en [`azure-vm.md`](azure-vm.md).

| | |
|---|---|
| VM | `vm-climaplus` · Ubuntu Server 24.04 LTS x64 · 2 vCPU / 8 GiB / 32 GiB SSD estándar · West US 2 |
| IP pública | `4.154.19.24` (más una etiqueta DNS: ver sección 2) |
| NSG | 22 solo desde tu IP; 80 y 443 públicos. Nada más |

## 1. Arquitectura

```
Internet ──► :80/:443  proxy   (nginx:1.27-alpine, Let's Encrypt, HSTS)          red "publica" 172.30.10.0/24
                         ├──► web   (React compilado, nginx sin privilegios :8080)
                         └──► api   (ASP.NET Core 8 :8080) ──► Gmail 465 / OpenWeather / Google
                                     └──► db (postgres:18-alpine :5432)          red "interna" (sin salida)
              certbot  (renueva cada 12 h; el proxy recarga cada 6 h)
Volúmenes: datos-postgres · letsencrypt · certbot-webroot
```

Solo `proxy` publica puertos (80 y 443). `api`, `web` y `db` no tienen `ports`. `db` está únicamente en una red
`internal`: no tiene salida a Internet ni es alcanzable desde fuera; solo `api` llega a ella.

## 2. Revisión de lo existente y decisiones

| # | Pregunta | Respuesta |
|---|---|---|
| 1 | Servicios definidos / faltantes | `docker-compose.yml` define `db` (postgres:16), `api`, `web` y `mailpit`. Para producción faltan: **proxy HTTPS**, **renovación de certificados**, PostgreSQL **18**, redes separadas, límites de memoria y de logs, healthcheck de la API y respaldos |
| 2 | ¿Desarrollo o producción? | `docker-compose.yml` es de **desarrollo/demostración**: publica la API en `8080` y el frontend en `3000` en todas las interfaces, usa HTTP, Mailpit, PostgreSQL 16 (no restaura copias de la 18) e inicializa la base con las migraciones (incompatible con restaurar `climaplus_db`). `docker-compose.prod.yml` es una superposición para imágenes de GHCR: hereda esos puertos (Compose suma las listas `ports`), y la imagen `web` de GHCR se compila con `http://localhost:8080` salvo que se configure la variable del repositorio. No sirve tal cual para Azure |
| 3 | Qué mantener para Windows | `docker-compose.yml`, `docker-compose.prod.yml`, los dos `Dockerfile`, `frontend/nginx/*`, `.env.example`, `dotnet run` y `npm run dev` **quedan sin cambios**. El `appsettings.Production.json` nuevo no tiene secretos y las variables del Compose local (Mailpit, puerto 1025) lo siguen reemplazando |
| 4 | ¿Archivo separado? | Sí, pero **independiente**: `docker-compose.azure.yml` no extiende al de desarrollo. Así no se heredan puertos, Mailpit ni la inicialización, y el entorno Windows no se toca. `docker-compose.prod.yml` se conserva para el flujo con GHCR |
| 5 | Contenerización | Se reutilizan los `Dockerfile` existentes (multietapa, runtime sin SDK, usuarios sin privilegios; sus `.dockerignore` excluyen `appsettings.Development.json`, `.env*`, pruebas, `bin/obj`). Imágenes construidas **en la VM** desde el commit publicado, con `VITE_API_URL=https://<DOMINIO>` |
| 6 | Persistencia | Volumen `datos-postgres` montado en `/var/lib/postgresql` (cambio de la imagen 18: los datos quedan en `/var/lib/postgresql/18/docker`). Sobrevive a `down`, reinicios y reconstrucciones. **Nunca** `down -v` |
| 7 | Restaurar `climaplus_db` | `pg_dump -Fc` en Windows → `scp` → `restaurar-db-docker.sh`: crea `climaplus_app` (no superusuario), `climaplus_db` (dueño `climaplus_app`), `pg_restore --exit-on-error --no-owner`, verifica 5 migraciones y dueños, y genera la verificación para comparar con Windows. Se detiene si la base ya existe. No aplica migraciones |
| 8 | HTTPS | Etiqueta DNS de Azure + Let's Encrypt. Primer certificado con `certbot --standalone`; renovaciones automáticas por `webroot` cada 12 h; el proxy recarga cada 6 h |
| 9 | Sin exposición directa | Solo 80/443 publicados; API y PostgreSQL sin `ports`; `db` en red `internal`; HTTPS a la IP rechazado (`ssl_reject_handshake`); `AllowedHosts` limitado al dominio |
| 10 | Secretos | En `.env.azure` (permisos 600, ignorado por Git por `.env.*`), pasados como variables de entorno. Nunca en Git, en `appsettings.*.json` ni en las imágenes. Limitación: `docker inspect` muestra las variables a quien tenga acceso de root/Docker en la VM (no se agrega tu usuario al grupo `docker`; se usa `sudo`) |
| 11 | Salud, logs, recuperación | Healthchecks en `db`, `api` (bash a `/health`, la imagen no trae curl), `web` y `proxy`; `restart: unless-stopped`; Docker habilitado al arrancar; logs `json-file` 3 × 10 MB por contenedor; `verificar-docker.sh` |
| 12 | ESP32 | `POST https://<DOMINIO>/api/lecturas` con `X-Device-Key`, a través del proxy. Las API Keys llegan con la copia de la base. HTTP no sirve (301) |
| 13 | Recursos | Sección 9 |
| 14 | GitHub / privado | Sección 10 |

**La API no necesita cambios de código detrás del proxy:** `ProxyInverso__ProxiesConocidos__0=172.30.10.10` (IP fija del
proxy) hace que el rate limiting, el bloqueo de intentos, HSTS y la redirección HTTPS vean la IP y el esquema reales; el
proxy envía una sola entrada `X-Forwarded-For` con la IP del cliente.

## 3. Archivos

| Archivo | Qué es |
|---|---|
| `docker-compose.azure.yml` | Stack de producción completo (nuevo) |
| `deploy/azure/docker/proxy.conf.template` | Nginx público: redirección, HTTPS, `/api` y `/health` → api, resto → web (nuevo) |
| `deploy/azure/docker/azure.env.example` | Plantilla de `.env.azure` sin secretos (nuevo) |
| `deploy/azure/scripts/restaurar-db-docker.sh` | Restauración segura de la copia (nuevo) |
| `deploy/azure/scripts/respaldo-db-docker.sh` | Respaldo diario con SHA-256 y retención (nuevo) |
| `deploy/azure/scripts/verificar-docker.sh` | Comprobación integral, solo lectura (nuevo) |
| `database/scripts/M005_verificacion.sql` | Comparación Windows ↔ Azure (existente) |

Pruebas realizadas en el entorno del asistente (no en Azure): `docker compose config` válido (solo 80/443 publicados,
redes, variables obligatorias); la plantilla del proxy probada en Nginx con backends simulados (redirección, desafío
ACME, `/api/lecturas` con la ruta y la consulta intactas, `X-Forwarded-For` = IP real aunque el cliente envíe otra,
`X-Forwarded-Proto: https`, SPA, HSTS, `/swagger` 404, HTTPS por IP rechazado); `restaurar-db-docker.sh` ejecutado
contra un PostgreSQL local con una copia real de prueba: restauró, verificó 5 migraciones y dueños, la verificación
coincidió con el origen (salvo cómo PostgreSQL escribe la restricción) y en una segunda ejecución se detuvo sin tocar la
base. **No se pudieron construir las imágenes aquí** (sin acceso a Docker Hub ni a MCR): la construcción se comprueba
en la VM, paso 5.

## 4. Antes de empezar (acciones tuyas)

1. **Etiqueta DNS** en la IP pública (*vm-climaplus → IP pública → Configuración → Etiqueta de nombre DNS*), por ejemplo
   `climaplus-umg` → `climaplus-umg.westus2.cloudapp.azure.com`. Let's Encrypt no emite certificados para una IP y
   Google no acepta IP como origen.
2. **Google Cloud Console** (si usas "Continuar con Google"): origen autorizado `https://<DOMINIO>`.
3. **Secretos filtrados en el historial** (auditoría Git): cambiar la contraseña del WiFi y desactivar la API Key
   antigua si sigue activa, **antes** de generar la copia.
4. Los cambios de la fase 7, la 005 y este despliegue **publicados en GitHub** (la VM clona el repositorio público).

## 5. Pasos exactos

### 5.1 Windows: copia de la base (PowerShell 5.1)

API local detenida para una copia consistente. No modifica `climaplus_db`.

```powershell
Set-Location "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
$env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
$d = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "ClimaPlus-despliegue"; New-Item -ItemType Directory -Force $d | Out-Null
pg_dump -h localhost -U postgres -d climaplus_db -Fc -f "$d\climaplus_db-azure.dump"
pg_restore --list "$d\climaplus_db-azure.dump" | Select-String "dbname: climaplus_db"
(Get-FileHash "$d\climaplus_db-azure.dump" -Algorithm SHA256).Hash.ToLower()       # anótalo
$env:PGOPTIONS = "-c default_transaction_read_only=on"
psql -X -A -t -h localhost -U postgres -d climaplus_db -f database\scripts\M005_verificacion.sql | Out-File -Encoding utf8 "$d\verificacion-windows.txt"
Remove-Item Env:PGOPTIONS
scp -i "$HOME\.ssh\vm-climaplus.pem" "$d\climaplus_db-azure.dump" azureuser@4.154.19.24:/tmp/
ssh -i "$HOME\.ssh\vm-climaplus.pem" azureuser@4.154.19.24
```

### 5.2 VM: Docker Engine y Compose (repositorio oficial de Docker)

```bash
sudo apt-get update && sudo apt-get -y upgrade
sudo apt-get install -y ca-certificates curl git openssl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo docker compose version
```

### 5.3 VM: código y secretos

```bash
sudo mkdir -p /opt/climaplus && sudo chown "$USER" /opt/climaplus
git clone https://github.com/achenx-arch/ClimaPlus.git /opt/climaplus/ClimaPlus
cd /opt/climaplus/ClimaPlus
git checkout main            # o el commit/tag aprobado: git checkout <commit>
git log --oneline -1

cp deploy/azure/docker/azure.env.example .env.azure && chmod 600 .env.azure
for i in 1 2 3 4; do openssl rand -base64 48 | tr -d '\n=/+' | cut -c1-48; done   # 4 claves: superusuario, climaplus_app, JWT, HMAC
nano .env.azure              # DOMINIO, CORREO_LETSENCRYPT, contraseñas, claves, SMTP_* (Gmail con contraseña de aplicación)
git status --short           # .env.azure NO debe aparecer (está ignorado)

DC="sudo docker compose --env-file .env.azure -f docker-compose.azure.yml"
$DC config --quiet && echo "configuración válida"
DOMINIO=$(grep '^DOMINIO=' .env.azure | cut -d= -f2-); CORREO=$(grep '^CORREO_LETSENCRYPT=' .env.azure | cut -d= -f2-)
getent hosts "$DOMINIO"      # debe resolver a 4.154.19.24
```

### 5.4 VM: imágenes

```bash
$DC build --pull             # 5–10 min la primera vez (descarga SDK de .NET y Node para compilar)
sudo docker image ls | grep -E 'climaplus|postgres|nginx'
```

### 5.5 VM: restaurar climaplus_db

```bash
sudo bash deploy/azure/scripts/restaurar-db-docker.sh /tmp/climaplus_db-azure.dump <sha256-de-Windows>
```

Compara el `verificacion-azure-*.txt` generado con `verificacion-windows.txt` (secciones 1–5 y 7 idénticas; la 6 solo
puede cambiar en el formato de la restricción). Luego: `shred -u /tmp/climaplus_db-azure.dump`.

### 5.6 VM: certificado

```bash
$DC run --rm -p 80:80 --entrypoint certbot certbot certonly --standalone \
    -d "$DOMINIO" -m "$CORREO" --agree-tos --no-eff-email --non-interactive
```

### 5.7 VM: iniciar todo y verificar

```bash
$DC up -d
$DC ps                        # todos "healthy" (la API tarda ~30 s)
$DC exec certbot certbot renew --dry-run --webroot -w /var/www/certbot
sudo bash deploy/azure/scripts/verificar-docker.sh
```

Desde el navegador (`https://<DOMINIO>`): registro con código, inicio de sesión con código, recuperación, cambio de
contraseña con código, Google (si se configuró), panel de administrador, clima y alertas.

### 5.8 Respaldos diarios

```bash
sudo crontab -e
# 15 3 * * * cd /opt/climaplus/ClimaPlus && bash deploy/azure/scripts/respaldo-db-docker.sh >> /var/log/climaplus-respaldo.log 2>&1
sudo bash deploy/azure/scripts/respaldo-db-docker.sh && sudo ls -la /var/backups/climaplus
```

Los respaldos quedan en la VM (14 días). Copia periódicamente uno a tu equipo con `scp` (contiene datos personales).

## 6. ESP32

- En `secrets.h` (no en el firmware): `#define API_URL "https://<DOMINIO>/api/lecturas"`. La `DEVICE_KEY` registrada
  localmente sigue siendo válida (viaja con la copia de la base, como hash).
- Sin clave o con clave desconocida: 401. Límite: 30 lecturas/minuto por dispositivo (el firmware envía una cada 10 s).
- HTTP no sirve (el proxy responde 301 y el ESP32 no repite el POST).
- **Comprobado en el código:** el firmware actual usa `HTTPClient` sin certificado raíz y el núcleo 3.0.7 hace
  `setInsecure()`: con `https` cifra pero **no valida** el servidor. Antes de producción hay que aplicar el cambio de
  [`../hardware/esp32-https.md`](../hardware/esp32-https.md) (CA de Let's Encrypt; compilado, requiere tu autorización
  porque modifica el firmware principal).

## 7. Operación diaria

| Acción | Comando (desde `/opt/climaplus/ClimaPlus`, con `DC` definido) |
|---|---|
| Estado y salud | `$DC ps` · `sudo bash deploy/azure/scripts/verificar-docker.sh` |
| Logs | `$DC logs -f --tail 100 api` (también `proxy`, `db`, `web`, `certbot`) |
| Reiniciar un servicio | `$DC restart api` |
| Prueba de recuperación | `$DC exec api sh -c 'kill 1'` → Docker lo reinicia solo (`$DC ps`) |
| Reinicio de la VM | `sudo reboot` → al volver, todo arranca solo (`restart: unless-stopped` + Docker habilitado) |
| Detener todo (sin perder datos) | `$DC down` (**nunca** `down -v`: borraría `datos-postgres` y los certificados) |
| Actualizar a una versión nueva | `sudo bash deploy/azure/scripts/respaldo-db-docker.sh && git pull && $DC build --pull && $DC up -d && sudo bash deploy/azure/scripts/verificar-docker.sh` |
| Migración nueva en el futuro | Respaldo + su script SQL dentro de `db` **antes** de `up -d` (la API nunca migra sola; sin la migración `/health` = 503) |
| Espacio | `sudo docker system df` · `sudo docker image prune -f` y `sudo docker builder prune -f` tras actualizar |
| Restaurar un respaldo propio | Igual que 5.5, en una base vacía; reemplazar `climaplus_db` es una decisión explícita |

## 8. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Docker publica puertos saltándose `ufw` | Solo el proxy publica (80/443); el NSG de Azure es el filtro efectivo; `verificar-docker.sh` lo comprueba |
| Pérdida del volumen | Respaldo diario + copia fuera de la VM; nunca `down -v` |
| Topes de intentos en memoria | Una sola instancia de `api`; un reinicio los pone en cero |
| Certificado vencido | Renovación cada 12 h + recarga cada 6 h + aviso por correo de Let's Encrypt; `verificar-docker.sh` muestra la fecha |
| Imágenes `latest` (certbot) o `18-alpine` cambian | Tras el primer despliegue correcto, fijar por digest (`sudo docker image ls --digests`) |
| Gmail bloquea el inicio de sesión desde una IP nueva | Contraseña de aplicación; revisar la alerta de seguridad de la cuenta; `verificar-docker.sh` prueba la salida al 465 |
| ESP32 sin verificación de certificado | Cambio de firmware con CA (requiere autorización) |
| Variables visibles con `docker inspect` | Acceso a la VM solo por SSH con clave y desde tu IP; sin usuarios en el grupo `docker` |

## 9. Consumo aproximado

| Componente | Memoria típica | Límite |
|---|---|---|
| PostgreSQL 18 (`shared_buffers` 256 MB) | 150–350 MB | 1 GB |
| API ASP.NET Core 8 | 150–300 MB | 768 MB |
| web (Nginx) / proxy (Nginx) | 5–15 MB cada uno | 64 MB |
| certbot (dormido) | 20–40 MB | 128 MB |
| Docker + Ubuntu | 500–700 MB | — |
| **Total en funcionamiento** | **≈ 1–1,5 GB de 8 GB** | |
| Construcción (`build`, puntual) | 1,5–2,5 GB | |

| Disco | Tamaño aproximado |
|---|---|
| Ubuntu + Docker | 3–4 GB |
| Imágenes finales (postgres, aspnet + API, nginx ×2, certbot) | ≈ 0,8–1 GB |
| Caché de construcción (SDK .NET, Node, node_modules) | ≈ 1,5–2 GB (se libera con `docker builder prune`) |
| Base de datos | < 100 MB hoy; el ESP32 cada 10 s agrega ≈ 8.600 lecturas/día (≈ 1 GB/año con índices) |
| Logs de contenedores | máx. 30 MB por contenedor |
| Respaldos (14 días) | decenas de MB hoy, crece con las lecturas |
| **Total** | **≈ 7–9 GB de 32 GB**; holgura para años de lecturas |

## 10. Qué va a GitHub y qué queda privado

| A GitHub | Solo en la VM o en tu equipo (nunca en Git) |
|---|---|
| `docker-compose.azure.yml`, `deploy/azure/**` (plantillas `.example`, configuración de Nginx, scripts), `docs/despliegue/azure-docker.md`, `azure-vm.md`, `appsettings.Production.json` (sin secretos) | `.env.azure` (contraseñas, claves JWT/HMAC, Gmail), certificados y claves de Let's Encrypt (volumen `letsencrypt`), copias `.dump`, `verificacion-*.txt`, respaldos de `/var/backups/climaplus`, tu clave SSH `.pem`, `secrets.h` del ESP32 |
