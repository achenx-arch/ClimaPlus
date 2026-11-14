#!/usr/bin/env bash
# =====================================================================
#  ClimaPlus – Comprobaciones en la VM de Azure (solo lectura: no cambia nada)
#  Uso:  bash verificar-vm.sh <DOMINIO>
#  Sale con 0 si todo está correcto. No muestra secretos.
# =====================================================================
set -u
DOMINIO="${1:-}"
if [ -z "$DOMINIO" ]; then echo "Uso: bash verificar-vm.sh <DOMINIO>"; exit 2; fi
fallas=0
ok()    { echo "OK    $1"; }
falla() { echo "FALLO $1"; fallas=$((fallas + 1)); }

for s in postgresql climaplus-api nginx; do
  if systemctl is-active --quiet "$s"; then ok "servicio $s activo"; else falla "servicio $s no está activo (journalctl -u $s -n 50)"; fi
done

# Puertos: la API y PostgreSQL solo en loopback; públicos solo 80 y 443 (y 22).
escucha=$(ss -ltnH 2>/dev/null)
if echo "$escucha" | grep -qE '127\.0\.0\.1:5000\b'; then ok "API escucha solo en 127.0.0.1:5000"; else falla "la API no escucha en 127.0.0.1:5000"; fi
if echo "$escucha" | grep -E ':5000\b' | grep -vqE '127\.0\.0\.1:5000'; then falla "la API escucha en una interfaz pública"; fi
if echo "$escucha" | grep -E ':5432\b' | grep -vqE '(127\.0\.0\.1|\[::1\]):5432'; then falla "PostgreSQL escucha fuera de localhost"; else ok "PostgreSQL solo en localhost"; fi

r=$(curl -s --max-time 10 -H "Host: localhost" http://127.0.0.1:5000/health)
[ "$r" = "Healthy" ] && ok "/health local: Healthy" || falla "/health local respondió '$r' (¿base de datos o migraciones?)"

r=$(curl -s --max-time 15 "https://$DOMINIO/health")
[ "$r" = "Healthy" ] && ok "https://$DOMINIO/health: Healthy (certificado válido)" || falla "https://$DOMINIO/health respondió '$r'"

c=$(curl -s -o /dev/null -w '%{http_code} %{redirect_url}' --max-time 10 "http://$DOMINIO/")
case "$c" in 301\ https://*) ok "HTTP redirige a HTTPS";; *) falla "HTTP no redirige a HTTPS ($c)";; esac

c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$DOMINIO/dashboard")
[ "$c" = "200" ] && ok "SPA: /dashboard devuelve index.html" || falla "SPA: /dashboard respondió $c"

c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$DOMINIO/api/auth/me")
[ "$c" = "401" ] && ok "API protegida: /api/auth/me sin sesión = 401" || falla "/api/auth/me respondió $c"

c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 -X POST -H 'Content-Type: application/json' -d '{}' "https://$DOMINIO/api/lecturas")
[ "$c" = "401" ] && ok "Sensores: POST /api/lecturas sin X-Device-Key = 401" || falla "POST /api/lecturas sin clave respondió $c"

c=$(curl -s -o /dev/null -w '%{http_code}' --max-time 10 "https://$DOMINIO/.env")
[ "$c" = "404" ] && ok "Archivos ocultos no se sirven" || falla "/.env respondió $c"

h=$(curl -sI --max-time 10 "https://$DOMINIO/" | tr -d '\r')
echo "$h" | grep -qi '^strict-transport-security:' && ok "HSTS en el frontend" || falla "falta Strict-Transport-Security en el frontend (snippet climaplus-cabeceras.conf)"

fin=$(echo | openssl s_client -connect "$DOMINIO:443" -servername "$DOMINIO" 2>/dev/null | openssl x509 -noout -enddate 2>/dev/null | cut -d= -f2)
[ -n "$fin" ] && ok "Certificado vigente hasta: $fin" || falla "no se pudo leer el certificado"
systemctl list-timers 2>/dev/null | grep -q certbot && ok "Renovación automática de certbot programada" || falla "no hay temporizador de certbot"

if timeout 10 bash -c 'exec 3<>/dev/tcp/smtp.gmail.com/465' 2>/dev/null; then ok "Salida a smtp.gmail.com:465 permitida"; else falla "no hay salida a smtp.gmail.com:465"; fi

if [ "$fallas" -eq 0 ]; then echo "RESULTADO: ClimaPlus está ejecutándose correctamente en $DOMINIO"; exit 0; fi
echo "RESULTADO: $fallas comprobación(es) fallaron"; exit 1
