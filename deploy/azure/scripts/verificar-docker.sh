#!/usr/bin/env bash
# =====================================================================
#  ClimaPlus – Comprobaciones del despliegue con Docker Compose en la VM (solo lectura; no muestra secretos)
#  Uso (desde la carpeta del repositorio):  bash deploy/azure/scripts/verificar-docker.sh
#  Sale con 0 si ClimaPlus está ejecutándose correctamente.
# =====================================================================
set -u
cd "$(dirname "$0")/../../.."
DC=(docker compose --env-file .env.azure -f docker-compose.azure.yml)
DOMINIO="$(grep -E '^DOMINIO=' .env.azure | cut -d= -f2-)"
fallas=0
ok()    { echo "OK    $1"; }
falla() { echo "FALLO $1"; fallas=$((fallas + 1)); }

systemctl is-enabled --quiet docker && ok "Docker arranca con la VM" || falla "docker no está habilitado (sudo systemctl enable docker)"

for s in db api web proxy certbot; do
  id="$("${DC[@]}" ps -q "$s" 2>/dev/null)"
  if [ -z "$id" ]; then falla "contenedor $s no existe"; continue; fi
  estado="$(docker inspect -f '{{.State.Status}} {{if .State.Health}}{{.State.Health.Status}}{{else}}sin-healthcheck{{end}} {{.HostConfig.RestartPolicy.Name}} reinicios={{.RestartCount}}' "$id")"
  case "$estado" in
    running\ healthy\ unless-stopped*|running\ sin-healthcheck\ unless-stopped*) ok "$s: $estado";;
    *) falla "$s: $estado (docker compose logs $s)";;
  esac
done

# Solo el proxy publica puertos, y solo 80 y 443.
publicados="$(docker ps --filter "label=com.docker.compose.project=climaplus" --format '{{.Names}} {{.Ports}}' | grep -- '->' || true)"
otros="$(echo "$publicados" | grep -v 'proxy' | grep -- '->' || true)"
[ -z "$otros" ] && ok "db, api y web sin puertos publicados" || falla "puertos publicados fuera del proxy: $otros"
echo "$publicados" | grep proxy | grep -qE '0\.0\.0\.0:443->443' && ok "proxy publica 80/443" || falla "el proxy no publica 443"
echo "$publicados" | grep -qE ':(5432|8080|3000|5000)->' && falla "hay un puerto interno publicado" || true

r="$(curl -s --max-time 15 "https://$DOMINIO/health")"
[ "$r" = "Healthy" ] && ok "https://$DOMINIO/health: Healthy (certificado válido)" || falla "https://$DOMINIO/health respondió '$r'"
c="$(curl -s -o /dev/null -w '%{http_code} %{redirect_url}' --max-time 10 "http://$DOMINIO/")"
case "$c" in 301\ https://*) ok "HTTP redirige a HTTPS";; *) falla "HTTP no redirige a HTTPS ($c)";; esac
c="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$DOMINIO/dashboard")"
[ "$c" = "200" ] && ok "SPA: /dashboard → index.html" || falla "SPA /dashboard respondió $c"
c="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$DOMINIO/api/auth/me")"
[ "$c" = "401" ] && ok "/api/auth/me sin sesión = 401" || falla "/api/auth/me respondió $c"
c="$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 -X POST -H 'Content-Type: application/json' -d '{}' "https://$DOMINIO/api/lecturas")"
[ "$c" = "401" ] && ok "ESP32: POST /api/lecturas sin X-Device-Key = 401" || falla "POST /api/lecturas sin clave respondió $c"
curl -sI --max-time 10 "https://$DOMINIO/" | grep -qi '^strict-transport-security' && ok "HSTS activo" || falla "falta HSTS"
c="$(curl -sk -o /dev/null -w '%{http_code}' --max-time 5 "https://$(curl -s --max-time 5 -4 ifconfig.me 2>/dev/null || echo 127.0.0.1)/" 2>/dev/null)"
[ "$c" = "000" ] && ok "HTTPS por IP rechazado" || echo "AVISO HTTPS por IP respondió $c"

fin="$(echo | openssl s_client -connect "$DOMINIO:443" -servername "$DOMINIO" 2>/dev/null | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)"
[ -n "$fin" ] && ok "Certificado vigente hasta $fin" || falla "no se pudo leer el certificado"

"${DC[@]}" exec -T api bash -c 'exec 3<>/dev/tcp/smtp.gmail.com/465' 2>/dev/null && ok "La API llega a smtp.gmail.com:465" || falla "la API no llega a smtp.gmail.com:465"
"${DC[@]}" exec -T db sh -c 'wget -q -T 5 -O /dev/null https://example.com' 2>/dev/null && falla "db tiene salida a Internet (red interna mal configurada)" || ok "db aislada (red interna sin salida)"
mig="$("${DC[@]}" exec -T db psql -U postgres -d climaplus_db -XAtc 'SELECT count(*) FROM "__EFMigrationsHistory"' 2>/dev/null)"
[ "$mig" = "5" ] && ok "climaplus_db con 5 migraciones" || falla "migraciones en climaplus_db: '$mig'"
if "${DC[@]}" logs --no-color --since 24h api 2>/dev/null | grep -qiE 'password=|contrase(n|ñ)a[^ ]*=|codigo=[0-9]{6}'; then falla "los logs de la API contienen algo parecido a una contraseña o código"; else ok "logs de la API sin contraseñas ni códigos (24 h)"; fi

echo "Uso de recursos:"; docker stats --no-stream --format '  {{.Name}}: {{.MemUsage}} CPU {{.CPUPerc}}' $("${DC[@]}" ps -q) 2>/dev/null
echo "Disco Docker:"; docker system df 2>/dev/null | sed 's/^/  /'

if [ "$fallas" -eq 0 ]; then echo "RESULTADO: ClimaPlus está ejecutándose correctamente en https://$DOMINIO"; exit 0; fi
echo "RESULTADO: $fallas comprobación(es) fallaron"; exit 1
