#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
service=${1:?Indica frontend o backend}
case "$service" in frontend) var=TECNM_FRONTEND_PORT; name_var=TECNM_FRONTEND_CONTAINER_NAME;; backend) var=TECNM_BACKEND_PORT; name_var=TECNM_BACKEND_CONTAINER_NAME;; *) exit 1;; esac
if [[ ${2:-} != --prepared && ${2:-} != --ports-prepared ]]; then
  bash scripts/control/prepare-services.sh "$service" || exit $?
  bash scripts/control/configure-containers.sh "$service" || exit $?
  load_settings
fi
# A stopped/recreated service cannot occupy its own port during the check.
if [[ $(docker inspect -f '{{.State.Running}}' "${!name_var}" 2>/dev/null || :) == true ]]; then
  echo "El contenedor $service todavía existe y está activo. Usa recrear para eliminarlo y crearlo de nuevo." >&2; exit 1
fi
if [[ ${2:-} == --ports-prepared ]]; then
  bash scripts/control/configure-port.sh "$service" --verify || exit $?
else
  bash scripts/control/configure-port.sh "$service" || exit $?
  load_settings
fi
certificates
"${COMPOSE[@]}" up -d --build --no-deps "$service"
