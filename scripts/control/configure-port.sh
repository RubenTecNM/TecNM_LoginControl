#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
service=${1:?Indica frontend o backend}
case "$service" in frontend) var=TECNM_FRONTEND_PORT; name_var=TECNM_FRONTEND_CONTAINER_NAME;; backend) var=TECNM_BACKEND_PORT; name_var=TECNM_BACKEND_CONTAINER_NAME;; *) exit 1;; esac
port=${!var}
if [[ ${2:-} != --verify ]]; then
  read -r -p "Puerto publicado para $service en el anfitrión [$port]: " selected_port || exit 2
  port=${selected_port:-$port}
fi
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
  if [[ ${2:-} == --verify ]]; then
    printf 'El puerto %s ya no está disponible; no se creó el contenedor.\n' "$port" >&2
    exit 1
  fi
  if [[ ${2:-} == --recreate ]] && docker port "${3:?Falta el nombre anterior}" 2>/dev/null | awk -v port="$port" '$NF ~ (":" port "$") {found=1} END {exit !found}'; then
    break
  fi
  printf 'El puerto %s no está disponible o no es válido. Ingresa otro: ' "$port"
  read -r port || exit 2
done
[[ ${2:-} != --verify ]] || exit 0
printf -v "$var" '%s' "$port"
export "$var"
save_settings
