# ClimaPlus en una VM de Azure (Ubuntu 24.04): Nginx + ASP.NET Core 8 + PostgreSQL 18

> **Alternativa sin Docker.** La forma recomendada es Docker Compose: [`azure-docker.md`](azure-docker.md).

**Estado:** procedimiento preparado y revisado, **no ejecutado**. ClimaPlus solo se considera desplegado cuando
`deploy/azure/scripts/verificar-vm.sh <DOMINIO>` termina con `RESULTADO: ClimaPlus está ejecutándose correctamente`
en la VM.

| | |
|---|---|
| VM | `vm-climaplus` · Ubuntu Server 24.04 LTS x64 · 2 vCPU / 8 GiB · West US 2 · grupo `rg-climaplus-final` |
| IP pública | `4.154.19.24` |
| NSG | 22 solo desde tu IP; 80 y 443 públicos. **No abrir 5000 ni 5432** |
| Topología | Internet → Nginx (80/443, HTTPS) → API en `127.0.0.1:5000` (systemd) → PostgreSQL 18 en `127.0.0.1:5432` |

Archivos del repositorio para este despliegue:

| Archivo | Destino en la VM |
|---|---|
| `deploy/azure/nginx/climaplus.conf` | `/etc/nginx/sites-available/climaplus` |
| `frontend/nginx/cabeceras-seguridad.conf` (con HSTS activado) | `/etc/nginx/snippets/climaplus-cabeceras.conf` |
| `deploy/azure/systemd/climaplus-api.service` | `/etc/systemd/system/climaplus-api.service` |
| `deploy/azure/climaplus-api.env.example` | `/etc/climaplus/api.env` (con los valores reales; nunca en Git) |
| `deploy/azure/scripts/preparar-artefactos.ps1` | Se ejecuta en Windows: genera `api.tar.gz` y `frontend.tar.gz` |
| `deploy/azure/scripts/verificar-vm.sh` | Se ejecuta en la VM al final |
| `database/scripts/M005_verificacion.sql` | Comparar la base antes (Windows) y después (VM) de copiarla |

## 0. Decisiones previas (requieren tu acción)

1. **Nombre DNS.** Let's Encrypt no emite certificados para una IP y Google Sign-In no acepta IP como origen. En el
   portal: *IP pública de vm-climaplus → Configuración → Etiqueta de nombre DNS*, por ejemplo `climaplus-umg` →
   `climaplus-umg.westus2.cloudapp.azure.com`. En este documento: `<DOMINIO>`.
2. **Google Cloud Console** (si usarás "Continuar con Google"): agrega `https://<DOMINIO>` a *Orígenes autorizados de
   JavaScript* del Client ID.
3. **Claves de producción nuevas** (`Jwt__Clave`, `Codigos__ClaveHmac`, contraseña de `climaplus_app`). Cambiarlas no
   afecta las contraseñas de los usuarios ni las API Keys de los dispositivos (se guardan con hash independiente).
   Solo cierra las sesiones abiertas.

## 1. Preparar artefactos en Windows (PowerShell 5.1)

Desde el commit ya publicado en GitHub:

```powershell
Set-Location "D:\UMG\VIII ciclo II Semestre 2026\Desarrollo web\Proyecto final\ClimaPlus"
powershell -ExecutionPolicy Bypass -File deploy\azure\scripts\preparar-artefactos.ps1 -Dominio <DOMINIO>
```

Genera `Documentos\ClimaPlus-despliegue\<fecha>\` con `api.tar.gz`, `frontend.tar.gz`, `SHA256SUMS` y `version.txt`.
La API se publica dependiente del runtime (`dotnet ClimaPlus.Api.dll`), así que en la VM basta el runtime de ASP.NET
Core 8 y no hace falta el SDK. El script falla si `appsettings.Development.json` llegara a la salida (además, el
`.csproj` ya lo excluye de `dotnet publish`).

**Copia de la base** (API local detenida, para una copia consistente):

```powershell
$env:Path = "C:\Program Files\PostgreSQL\18\bin;" + $env:Path
$d = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "ClimaPlus-despliegue"
pg_dump -h localhost -U postgres -d climaplus_db -Fc -f "$d\climaplus_db-azure.dump"
pg_restore --list "$d\climaplus_db-azure.dump" | Select-String "Dbname|TABLE DATA" | Measure-Object | Select-Object Count
(Get-FileHash "$d\climaplus_db-azure.dump" -Algorithm SHA256).Hash.ToLower()
psql -X -A -t -h localhost -U postgres -d climaplus_db -f database\scripts\M005_verificacion.sql | Out-File -Encoding utf8 "$d\verificacion-windows.txt"
```

La copia contiene datos personales y hashes: solo viaja por `scp`, nunca por Git ni correo, y se borra de la VM al
terminar la restauración.

**Subir** (desde Windows; `$llave` es tu clave privada SSH):

```powershell
$llave = "$HOME\.ssh\vm-climaplus.pem"
$c = "<carpeta generada por preparar-artefactos.ps1>"
scp -i $llave "$c\api.tar.gz" "$c\frontend.tar.gz" "$c\SHA256SUMS" "$d\climaplus_db-azure.dump" azureuser@4.154.19.24:/tmp/
scp -i $llave deploy\azure\nginx\climaplus.conf deploy\azure\systemd\climaplus-api.service deploy\azure\climaplus-api.env.example frontend\nginx\cabeceras-seguridad.conf deploy\azure\scripts\verificar-vm.sh database\scripts\M005_verificacion.sql azureuser@4.154.19.24:/tmp/
ssh -i $llave azureuser@4.154.19.24
```

## 2. Paquetes en la VM

```bash
DOMINIO=climaplus-umg.westus2.cloudapp.azure.com   # tu nombre DNS real (sección 0); se usa en los pasos siguientes
sudo apt update && sudo apt -y upgrade
# Runtime de ASP.NET Core 8 (repositorio de Ubuntu 24.04), Nginx, certbot y utilidades
sudo apt install -y aspnetcore-runtime-8.0 nginx certbot curl
dotnet --list-runtimes          # debe listar Microsoft.AspNetCore.App 8.0.x

# PostgreSQL 18 desde el repositorio oficial PGDG. Ubuntu 24.04 trae la 16, y pg_restore 16 NO puede leer una copia
# hecha con pg_dump 18.
sudo apt install -y postgresql-common
sudo /usr/share/postgresql-common/pgdg/apt.postgresql.org.sh -y
sudo apt install -y postgresql-18
psql --version                  # 18.x
sudo ss -ltnp | grep 5432       # solo 127.0.0.1 / ::1
```

PostgreSQL queda escuchando solo en localhost (valor por defecto) y con autenticación `scram-sha-256` para TCP. No se
cambia `listen_addresses` ni `pg_hba.conf`.

## 3. Restaurar climaplus_db

```bash
cd /tmp && sha256sum climaplus_db-azure.dump        # debe coincidir con el de Windows
sudo -u postgres createuser --pwprompt climaplus_app # contraseña nueva; no superusuario
sudo -u postgres createdb -O climaplus_app -E UTF8 -T template0 climaplus_db
sudo -u postgres pg_restore --exit-on-error --no-owner --no-privileges --role=climaplus_app \
     -d climaplus_db /tmp/climaplus_db-azure.dump
sudo -u postgres psql -X -A -t -d climaplus_db -f /tmp/M005_verificacion.sql > /tmp/verificacion-vm.txt
```

Compara `verificacion-vm.txt` con `verificacion-windows.txt` (por ejemplo, pegando ambos en un comparador local):
las secciones 1 (5 migraciones), 2 (filas), 3 (huellas de datos), 4 (usuarios por rol), 5 y 7 deben ser idénticas.
La 6 puede variar solo en cómo PostgreSQL escribe la restricción (mismo contenido). Después:

```bash
shred -u /tmp/climaplus_db-azure.dump /tmp/verificacion-vm.txt
```

**No** se ejecutan migraciones: la copia ya trae la 001–005 y la API nunca las aplica (si faltara alguna, `/health`
respondería 503).

## 4. API con systemd

```bash
sudo useradd --system --no-create-home --shell /usr/sbin/nologin climaplus
sudo mkdir -p /opt/climaplus/api /etc/climaplus
cd /tmp && sha256sum -c SHA256SUMS
sudo tar -xzf /tmp/api.tar.gz -C /opt/climaplus/api
sudo chown -R root:root /opt/climaplus/api && sudo chmod -R a+rX,go-w /opt/climaplus/api

sudo install -o root -g climaplus -m 640 /tmp/climaplus-api.env.example /etc/climaplus/api.env
sudo sed -i "s/<DOMINIO>/$DOMINIO/g" /etc/climaplus/api.env
sudo nano /etc/climaplus/api.env      # valores reales (REEMPLAZAR); claves nuevas con: openssl rand -base64 48

sudo cp /tmp/climaplus-api.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now climaplus-api
journalctl -u climaplus-api -n 40 --no-pager      # sin "faltan_migraciones"; sin errores de configuración
curl -s -H "Host: localhost" http://127.0.0.1:5000/health   # Healthy
```

La API escucha solo en `127.0.0.1:5000`, como usuario `climaplus` sin privilegios, con el sistema de archivos en solo
lectura salvo `/var/lib/climaplus`.

## 5. Nginx y HTTPS

```bash
# Certificado (Nginx detenido un momento para el desafío HTTP de Let's Encrypt)
sudo systemctl stop nginx
sudo certbot certonly --standalone -d "$DOMINIO" --agree-tos -m <tu correo> --no-eff-email \
     --pre-hook "systemctl stop nginx" --post-hook "systemctl start nginx"
# (los hooks quedan guardados para la renovación automática del temporizador certbot.timer)

# Frontend
sudo mkdir -p /var/www/climaplus/frontend /var/www/certbot
sudo tar -xzf /tmp/frontend.tar.gz -C /var/www/climaplus/frontend
sudo chown -R root:root /var/www/climaplus && sudo chmod -R a+rX,go-w /var/www/climaplus

# Cabeceras de seguridad con HSTS activado
sudo cp /tmp/cabeceras-seguridad.conf /etc/nginx/snippets/climaplus-cabeceras.conf
sudo sed -i 's|^# add_header Strict-Transport-Security|add_header Strict-Transport-Security|' /etc/nginx/snippets/climaplus-cabeceras.conf

# Sitio
sed "s/<DOMINIO>/$DOMINIO/g" /tmp/climaplus.conf | sudo tee /etc/nginx/sites-available/climaplus >/dev/null
sudo ln -sf /etc/nginx/sites-available/climaplus /etc/nginx/sites-enabled/climaplus
sudo rm -f /etc/nginx/sites-enabled/default
sudo nginx -t && sudo systemctl start nginx && sudo systemctl reload nginx
sudo certbot renew --dry-run
```

La configuración de Nginx se probó con Nginx 1.24 (la versión de Ubuntu 24.04): sintaxis correcta, redirección HTTP→HTTPS,
rutas de la SPA a `index.html`, caché de `/assets/`, `/.env` y `/swagger` = 404, reenvío a la API con
`X-Forwarded-For` = IP real (no la que envíe el cliente) y `X-Forwarded-Proto: https`, y rechazo de conexiones HTTPS
dirigidas a la IP o a otro nombre.

## 6. Verificación en la VM

```bash
bash /tmp/verificar-vm.sh "$DOMINIO"
```

Comprueba servicios, puertos (API y PostgreSQL solo en loopback), `/health` local y por HTTPS, redirección, SPA, que
`/api/auth/me` y `POST /api/lecturas` sin credenciales respondan 401, archivos ocultos, HSTS, certificado y su
renovación, y salida a `smtp.gmail.com:465`. Luego, desde el navegador: registro con código, inicio de sesión con
código, recuperación, cambio de contraseña con código, Google (si se configuró) y un panel de administrador.

## 7. ESP32

- Endpoint: `POST https://<DOMINIO>/api/lecturas` con la cabecera `X-Device-Key: cpk_…`. Sin clave o con una clave
  desconocida: 401 antes de leer el cuerpo. Límite: 30 lecturas por minuto por dispositivo (el firmware envía una cada 10 s).
- Las API Keys viajan con la copia de la base (hash SHA-256): el dispositivo registrado localmente sigue siendo válido.
- Solo cambia `secrets.h` (no el firmware): `#define API_URL "https://<DOMINIO>/api/lecturas"`.
- **HTTP no sirve**: Nginx responde 301 y el ESP32 no repite un POST tras una redirección.
- **Comprobado en el código:** el firmware usa `HTTPClient` sin certificado raíz y el núcleo 3.0.7 hace
  `setInsecure()`: cifra pero no valida el servidor. Cambio necesario (requiere autorización):
  [`../hardware/esp32-https.md`](../hardware/esp32-https.md).

## 8. Actualizar una versión

```bash
# Desde Windows: preparar-artefactos.ps1 y scp de los .tar.gz; en la VM:
cd /tmp && sha256sum -c SHA256SUMS
sudo systemctl stop climaplus-api
sudo rm -rf /opt/climaplus/api.anterior && sudo mv /opt/climaplus/api /opt/climaplus/api.anterior && sudo mkdir /opt/climaplus/api
sudo tar -xzf /tmp/api.tar.gz -C /opt/climaplus/api && sudo chmod -R a+rX,go-w /opt/climaplus/api
sudo systemctl start climaplus-api && bash /tmp/verificar-vm.sh "$DOMINIO"
# Si una versión trae migración nueva: respaldo con pg_dump -Fc y su script de aplicación ANTES de iniciar la API.
```

Respaldo programado recomendado (diario, fuera de la carpeta web):
`sudo -u postgres pg_dump -Fc climaplus_db -f /var/backups/climaplus/climaplus_db-$(date +%F).dump`.

## 9. Seguridad y logs

- Públicos solo 80/443 (22 restringido a tu IP en el NSG). La API y PostgreSQL no son accesibles desde fuera.
- Secretos solo en `/etc/climaplus/api.env` (640, root:climaplus). Nunca en `appsettings.Production.json`, que no tiene
  secretos y sí está en Git.
- Logs: `journalctl -u climaplus-api` (la API nunca registra contraseñas, códigos ni tokens; probado con
  `LosLogs_NoContienenContrasenasNiCodigos` y pruebas equivalentes) y `/var/log/nginx/` (registra rutas, no cabeceras
  ni cuerpos: la `X-Device-Key` y el `Authorization` no aparecen).
- La API confía en `X-Forwarded-*` solo desde 127.0.0.1/::1 (`ProxyInverso__ProxiesConocidos`), así que el rate
  limiting y el bloqueo de intentos ven la IP real del cliente.
- Una sola instancia de la API: los topes de intentos y códigos están en memoria; un reinicio los pone en cero.
