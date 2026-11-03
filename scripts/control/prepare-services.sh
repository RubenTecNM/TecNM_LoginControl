#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
if [[ ${1:-} == --existing ]]; then
  shift
  [[ -f backend/.env ]] || { echo 'Falta backend/.env. Usa Crear servicios para configurarlo.' >&2; exit 1; }
else
  bash scripts/control/configure-backend.sh || exit $?
fi
for service in "$@"; do
  if [[ $service == backend ]]; then
    if ! command -v psql >/dev/null; then
      printf '\n  Falta el cliente psql para comprobar la base. Instálalo y vuelve a intentarlo.\n' >&2
      exit 1
    fi
    printf '\n  Comprobando PostgreSQL local con la configuración de backend/.env…\n'
    if ! python3 scripts/control/status-db.py; then
      printf '  No se pudo conectar con PostgreSQL.\n' >&2
      printf '  Comprueba las credenciales de backend/.env, que el servicio esté corriendo\n' >&2
      printf '  y que exista la base configurada. No se modificaron los contenedores.\n' >&2
      exit 1
    fi
    printf '  Conexión a la base de datos verificada.\n'
    break
  fi
done
