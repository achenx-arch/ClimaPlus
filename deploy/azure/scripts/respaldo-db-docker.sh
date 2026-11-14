#!/usr/bin/env bash
# =====================================================================
#  ClimaPlus – Respaldo diario de climaplus_db desde el contenedor "db" (pg_dump -Fc), con SHA-256 y retención.
#  Uso:  bash deploy/azure/scripts/respaldo-db-docker.sh [carpeta] [días]
#  Cron (03:15 todos los días):
#    15 3 * * * cd /opt/climaplus/ClimaPlus && bash deploy/azure/scripts/respaldo-db-docker.sh >> /var/log/climaplus-respaldo.log 2>&1
# =====================================================================
set -euo pipefail
DESTINO="${1:-/var/backups/climaplus}"; DIAS="${2:-14}"
cd "$(dirname "$0")/../../.."
DC=(docker compose --env-file .env.azure -f docker-compose.azure.yml)
umask 077
mkdir -p "$DESTINO"
ARCHIVO="$DESTINO/climaplus_db-$(date +%Y%m%d-%H%M%S).dump"
"${DC[@]}" exec -T db pg_dump -U postgres -d climaplus_db -Fc -f /tmp/respaldo.dump
"${DC[@]}" cp db:/tmp/respaldo.dump "$ARCHIVO"
"${DC[@]}" exec -T db rm -f /tmp/respaldo.dump
[ -s "$ARCHIVO" ] || { echo "$(date -Is) FALLO: respaldo vacío"; exit 1; }
# Valida que el archivo se pueda leer (índice de pg_restore) con las herramientas del propio contenedor.
"${DC[@]}" exec -T db sh -c 'cat > /tmp/v.dump && pg_restore --list /tmp/v.dump >/dev/null; r=$?; rm -f /tmp/v.dump; exit $r' < "$ARCHIVO" \
  || { echo "$(date -Is) FALLO: el respaldo no se puede leer"; exit 1; }
sha256sum "$ARCHIVO" > "$ARCHIVO.sha256"
find "$DESTINO" -name 'climaplus_db-*.dump*' -mtime +"$DIAS" -delete
echo "$(date -Is) OK $ARCHIVO ($(du -h "$ARCHIVO" | cut -f1))"
