#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
docker info >/dev/null 2>&1 || { echo 'Docker no está disponible; comprueba el servicio.' >&2; exit 1; }
for service in "$@"; do
  case $service in
    frontend) name_var=TECNM_FRONTEND_CONTAINER_NAME; other_var=TECNM_BACKEND_CONTAINER_NAME;;
    backend) name_var=TECNM_BACKEND_CONTAINER_NAME; other_var=TECNM_FRONTEND_CONTAINER_NAME;;
    *) exit 1;;
  esac
  own_id=$("${COMPOSE[@]}" ps --all --quiet "$service")
  while true; do
    read -r -p "Nombre del contenedor $service [${!name_var}]: " name || exit 2
    name=${name:-${!name_var}}
    if [[ ! $name =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]+$ ]]; then
      echo 'Nombre no válido. Usa letras, números, puntos, guiones o guiones bajos (mínimo 2 caracteres).'
      continue
    fi
    if [[ $name == "${!other_var}" ]]; then
      echo 'Ese nombre está configurado para el otro servicio. Elige otro.'
      continue
    fi
    existing_id=$(docker inspect -f '{{.Id}}' "$name" 2>/dev/null || :)
    if [[ -n $existing_id && $existing_id != "$own_id" ]]; then
      echo 'Ese nombre está ocupado por otro contenedor. Elige otro.'
      continue
    fi
    printf -v "$name_var" '%s' "$name"
    export "$name_var"
    break
  done
done
save_settings
printf 'Nombres guardados en el .env raíz.\n'
