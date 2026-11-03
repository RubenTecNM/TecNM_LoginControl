#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
source "$(dirname "$0")/database-common.sh"

printf '\n  LIMPIEZA TOTAL DE CONTENEDORES Y BASE DE DATOS\n'
printf '  Se retirarán los contenedores de frontend y backend de este proyecto.\n'
printf '  La base seleccionada y todos sus datos se eliminarán sin recrearse.\n'
printf '  Las imágenes y archivos de configuración se conservan.\n\n'
docker info >/dev/null 2>&1 || { echo 'Docker no está disponible; no se modificó nada.' >&2; exit 1; }
read_database_target || exit $?
case $exists in
  t) printf '\n  Se eliminará la base "%s" en %s:%s.\n' "$target" "$PGHOST" "$PGPORT";;
  f) printf '\n  La base "%s" no existe en %s:%s; solo se retirarán los contenedores.\n' "$target" "$PGHOST" "$PGPORT";;
  *) echo 'PostgreSQL devolvió un estado inesperado; no se modificó nada.' >&2; exit 1;;
esac
printf '  Contenedores registrados: %s y %s.\n' "$TECNM_FRONTEND_CONTAINER_NAME" "$TECNM_BACKEND_CONTAINER_NAME"
printf '  La eliminación es irreversible y puede quedar incompleta si ocurre un error.\n'
read -r -p 'Escribe ACEPTO para continuar: ' confirm || exit 2
[[ $confirm == ACEPTO ]] || { echo 'Operación cancelada.'; exit 2; }
read -r -p "Escribe el nombre exacto de la base ($target): " confirm || exit 2
[[ $confirm == "$target" ]] || { echo 'Operación cancelada.'; exit 2; }
if ! "${COMPOSE[@]}" rm -sf frontend backend; then
  echo 'No se pudieron retirar todos los contenedores; la base no se eliminó. La limpieza puede estar incompleta.' >&2
  exit 1
fi
if [[ $exists == t ]] && ! drop_database; then
  echo 'Se retiraron los contenedores, pero no se pudo eliminar la base. Revisa conexiones activas y permisos.' >&2
  exit 1
fi
printf '  Limpieza terminada. No se recrearon contenedores ni base de datos.\n'
