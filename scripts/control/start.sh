#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
service=${1:?Indica frontend o backend}
case "$service" in
  frontend) name=$TECNM_FRONTEND_CONTAINER_NAME;;
  backend) name=$TECNM_BACKEND_CONTAINER_NAME;;
  *) exit 1;;
esac
docker info >/dev/null 2>&1 || { echo 'Docker no está disponible.' >&2; exit 1; }
if ! running=$(docker inspect -f '{{.State.Running}}' "$name" 2>/dev/null); then
  printf 'No existe el contenedor registrado "%s". Usa Crear servicios.\n' "$name" >&2
  exit 1
fi
if [[ $running == true ]]; then
  printf 'El contenedor "%s" ya está activo.\n' "$name"
  exit 0
fi
if [[ $service == backend ]]; then
  bash scripts/control/prepare-services.sh --existing "$service" || exit $?
fi
docker start "$name"
