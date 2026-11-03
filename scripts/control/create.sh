#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
service=${1:?Indica frontend o backend}
case "$service" in frontend) var=TECNM_FRONTEND_PORT; name_var=TECNM_FRONTEND_CONTAINER_NAME;; backend) var=TECNM_BACKEND_PORT; name_var=TECNM_BACKEND_CONTAINER_NAME;; *) exit 1;; esac
if [[ ${2:-} != --prepared ]]; then
  bash scripts/control/prepare-services.sh "$service" || exit $?
  bash scripts/control/configure-containers.sh "$service" || exit $?
  load_settings
fi
port=${!var}
# A stopped/recreated service cannot occupy its own port during the check.
if [[ $(docker inspect -f '{{.State.Running}}' "${!name_var}" 2>/dev/null || :) == true ]]; then
  echo "El contenedor $service todavía existe y está activo. Usa recrear para eliminarlo y crearlo de nuevo." >&2; exit 1
fi
read -r -p "Puerto publicado para $service en el anfitrión [$port]: " selected_port || exit 2
port=${selected_port:-$port}
while ! python3 - "$port" <<'PY'
import errno, socket, sys
try:
    port = int(sys.argv[1])
    assert 1 <= port <= 65535
    sockets = []
    for family, address in ((socket.AF_INET, '0.0.0.0'), (socket.AF_INET6, '::')):
        try:
            s = socket.socket(family)
        except OSError as error:
            if family == socket.AF_INET6 and error.errno in (errno.EAFNOSUPPORT, errno.EPROTONOSUPPORT):
                continue
            raise
        if family == socket.AF_INET6:
            s.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
        sockets.append(s)
        try:
            s.bind((address, port))
        except OSError as error:
            if family == socket.AF_INET6 and error.errno in (errno.EAFNOSUPPORT, errno.EADDRNOTAVAIL):
                continue
            raise
except (ValueError, OSError, AssertionError):
    sys.exit(1)
PY
do
  printf 'El puerto %s no está disponible o no es válido. Ingresa otro: ' "$port"
  read -r port || exit 1
done
printf -v "$var" '%s' "$port"
export "$var"
save_settings
certificates
"${COMPOSE[@]}" up -d --build --no-deps "$service"
