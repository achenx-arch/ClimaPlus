#!/usr/bin/env bash
# =====================================================================
#  ClimaPlus – Restaura en el contenedor "db" una copia de climaplus_db (pg_dump -Fc de PostgreSQL 18, migraciones
#  001–005). Crea el usuario climaplus_app (sin privilegios de superusuario) como dueño de la base.
#  Se DETIENE sin tocar nada si climaplus_db ya existe en el contenedor (nunca sobrescribe).
#  No ejecuta migraciones: la copia ya las trae.
#  Uso (desde la carpeta del repositorio en la VM):
#    bash deploy/azure/scripts/restaurar-db-docker.sh /ruta/climaplus_db-azure.dump [sha256-esperado]
# =====================================================================
set -euo pipefail
DUMP="${1:-}"; ESPERADO="${2:-}"
cd "$(dirname "$0")/../../.."
[ -f docker-compose.azure.yml ] || { echo "Ejecuta desde el repositorio de ClimaPlus."; exit 1; }
[ -f .env.azure ] || { echo "Falta .env.azure"; exit 1; }
[ -n "$DUMP" ] && [ -f "$DUMP" ] || { echo "Uso: $0 <copia.dump> [sha256]"; exit 1; }
DC=(docker compose --env-file .env.azure -f docker-compose.azure.yml)

# Contraseña de climaplus_app desde .env.azure (sin "source": no se interpreta como shell ni se muestra).
CLIMAPLUS_DB_PASSWORD="$(grep -E '^CLIMAPLUS_DB_PASSWORD=' .env.azure | head -1 | cut -d= -f2-)"
[ -n "$CLIMAPLUS_DB_PASSWORD" ] && [ "$CLIMAPLUS_DB_PASSWORD" != "REEMPLAZAR" ] || { echo "Define CLIMAPLUS_DB_PASSWORD en .env.azure"; exit 1; }
export CLIMAPLUS_DB_PASSWORD

HASH="$(sha256sum "$DUMP" | cut -d' ' -f1)"
echo "1. Copia: $DUMP ($(du -h "$DUMP" | cut -f1)) SHA-256 $HASH"
if [ -n "$ESPERADO" ] && [ "${ESPERADO,,}" != "$HASH" ]; then echo "DETENIDO: el SHA-256 no coincide con el de Windows."; exit 1; fi

echo "2. Iniciando solo PostgreSQL"
"${DC[@]}" up -d db
for i in $(seq 1 30); do
  "${DC[@]}" exec -T db pg_isready -U postgres -d postgres >/dev/null 2>&1 && break
  sleep 2
  [ "$i" -eq 30 ] && { echo "DETENIDO: PostgreSQL no respondió."; exit 1; }
done
VERSION="$("${DC[@]}" exec -T db psql -U postgres -d postgres -XAtc "SHOW server_version_num")"
[ "${VERSION:0:2}" = "18" ] || { echo "DETENIDO: el contenedor no es PostgreSQL 18 ($VERSION)."; exit 1; }

EXISTE="$("${DC[@]}" exec -T db psql -U postgres -d postgres -XAtc "SELECT count(*) FROM pg_database WHERE datname = 'climaplus_db'")"
[ "$EXISTE" = "0" ] || { echo "DETENIDO: climaplus_db ya existe en el contenedor. No se sobrescribe."; exit 1; }

echo "3. Usuario climaplus_app y base climaplus_db"
# La contraseña viaja por variable de entorno y psql la interpola desde stdin (no aparece en la línea de comandos).
"${DC[@]}" exec -T -e CLIMAPLUS_DB_PASSWORD db sh -c 'psql -U postgres -d postgres -X -v ON_ERROR_STOP=1 -v pw="$CLIMAPLUS_DB_PASSWORD"' <<'SQL'
SELECT 'CREATE ROLE climaplus_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE'
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'climaplus_app') \gexec
ALTER ROLE climaplus_app PASSWORD :'pw';
CREATE DATABASE climaplus_db OWNER climaplus_app ENCODING 'UTF8' TEMPLATE template0;
REVOKE ALL ON DATABASE climaplus_db FROM PUBLIC;
GRANT CONNECT, TEMPORARY ON DATABASE climaplus_db TO climaplus_app;
SQL

echo "4. Restaurando (pg_restore --exit-on-error, dueño climaplus_app)"
"${DC[@]}" cp "$DUMP" db:/tmp/restaurar.dump
if ! "${DC[@]}" exec -T db pg_restore -U postgres -d climaplus_db --exit-on-error --no-owner --no-privileges --role=climaplus_app /tmp/restaurar.dump; then
  "${DC[@]}" exec -T db rm -f /tmp/restaurar.dump
  echo "DETENIDO: la restauración falló. Revisa el mensaje; para reintentar: DROP DATABASE climaplus_db dentro del contenedor (solo ahí)."
  exit 1
fi
"${DC[@]}" exec -T db rm -f /tmp/restaurar.dump

echo "5. Verificación (solo lectura)"
"${DC[@]}" cp database/scripts/M005_verificacion.sql db:/tmp/m005.sql
SALIDA="verificacion-azure-$(date +%Y%m%d-%H%M%S).txt"
"${DC[@]}" exec -T db sh -c 'PGOPTIONS="-c default_transaction_read_only=on" psql -U postgres -d climaplus_db -X -A -t -v ON_ERROR_STOP=1 -f /tmp/m005.sql' > "$SALIDA"
"${DC[@]}" exec -T db rm -f /tmp/m005.sql
chmod 600 "$SALIDA"
MIG="$(awk '/^1\. /{f=1;next} /^[0-9]+\. /{f=0} f' "$SALIDA" | grep -c . || true)"
grep -q "20261113150000_CambioPasswordConCodigo" "$SALIDA" && [ "$MIG" = "5" ] || { echo "DETENIDO: la copia no tiene las 5 migraciones ($MIG). Ver $SALIDA"; exit 1; }
DUENO="$("${DC[@]}" exec -T db psql -U postgres -d climaplus_db -XAtc "SELECT count(*) FROM pg_tables WHERE schemaname = 'public' AND tableowner <> 'climaplus_app'")"
[ "$DUENO" = "0" ] || { echo "DETENIDO: hay $DUENO tablas que no pertenecen a climaplus_app."; exit 1; }
echo "   5 migraciones (001–005); todas las tablas pertenecen a climaplus_app."
awk '/^2\. /{f=1;next} /^[0-9]+\. /{f=0} f' "$SALIDA" | sed 's/^/   /'
echo "RESULTADO: CORRECTO. Compara $SALIDA con verificacion-windows.txt (secciones 1–5 y 7 idénticas)."
echo "Después borra la copia: shred -u \"$DUMP\""
