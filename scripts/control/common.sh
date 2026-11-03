#!/usr/bin/env bash
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$ROOT"
COMPOSE=(docker compose -f compose.yaml)
load_settings() {
  local values
  values=$(python3 "$ROOT/scripts/control/settings.py" load) || return
  read -r TECNM_FRONTEND_PORT TECNM_BACKEND_PORT TECNM_FRONTEND_CONTAINER_NAME TECNM_BACKEND_CONTAINER_NAME <<< "$values"
  export TECNM_FRONTEND_PORT TECNM_BACKEND_PORT TECNM_FRONTEND_CONTAINER_NAME TECNM_BACKEND_CONTAINER_NAME
}
save_settings() {
  python3 "$ROOT/scripts/control/settings.py" save "$TECNM_FRONTEND_PORT" "$TECNM_BACKEND_PORT" "$TECNM_FRONTEND_CONTAINER_NAME" "$TECNM_BACKEND_CONTAINER_NAME"
}
load_settings || exit 1
certificates() {
  if [[ ! -s .docker/certs/localhost.crt || ! -s .docker/certs/localhost.key ]]; then
    mkdir -p .docker/certs
    openssl req -x509 -newkey rsa:2048 -nodes -days 365 -keyout .docker/certs/localhost.key -out .docker/certs/localhost.crt -subj '/CN=localhost' -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1' >/dev/null 2>&1
    chmod 644 .docker/certs/localhost.key
  fi
}
