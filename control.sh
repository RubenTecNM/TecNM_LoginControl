#!/usr/bin/env bash
set -u
ROOT=$(cd "$(dirname "$0")" && pwd)
cd "$ROOT"
source scripts/control/common.sh
source scripts/control/ui.sh

select_services() {
  local option
  while true; do
    heading "$1 / Seleccionar servicios"
    printf '  ¿Qué servicios deseas %s?\n\n' "$2"
    menu_item 1 'Frontend'
    menu_item 2 'Backend'
    menu_item 3 'Ambos (backend y frontend)'
    separator
    menu_item 0 'Volver'
    read_key 'Selecciona una tecla:' option || exit 0
    case "$option" in
      1) services=(frontend); return 0;;
      2) services=(backend); return 0;;
      3) services=(backend frontend); return 0;;
      0) return 1;;
      *) printf '  %sOpción no válida. Usa 1, 2, 3 o 0.%s\n' "$warning" "$reset"; pause_panel;;
    esac
  done
}

manage_services() {
  local mode=$1 service
  if [[ $mode == start ]]; then
    for service in "${services[@]}"; do
      bash scripts/control/start.sh "$service" || return
    done
    return 0
  fi
  local -a previous_names=() previous_settings=("$TECNM_FRONTEND_PORT" "$TECNM_BACKEND_PORT" "$TECNM_FRONTEND_CONTAINER_NAME" "$TECNM_BACKEND_CONTAINER_NAME")
  for service in "${services[@]}"; do
    case $service in
      frontend) previous_names+=("$TECNM_FRONTEND_CONTAINER_NAME");;
      backend) previous_names+=("$TECNM_BACKEND_CONTAINER_NAME");;
    esac
  done
  if [[ $mode == recreate ]]; then
    local confirm name
    printf '\n'
    for name in "${previous_names[@]}"; do
      printf '  %sSe eliminará el contenedor "%s" para crearlo de nuevo.%s\n' "$warning" "$name" "$reset"
    done
    read_key '¿Estás de acuerdo? [s/N]:' confirm || return 2
    [[ $confirm == s || $confirm == S ]] || return 2
  fi
  bash scripts/control/prepare-services.sh "${services[@]}" || return
  bash scripts/control/configure-containers.sh "${services[@]}" || return
  load_settings || return
  if [[ $mode == recreate ]]; then
    local index=0
    for service in "${services[@]}"; do
      if bash scripts/control/configure-port.sh "$service" --recreate "${previous_names[index]}"; then
        :
      else
        local result=$?
        TECNM_FRONTEND_PORT=${previous_settings[0]} TECNM_BACKEND_PORT=${previous_settings[1]}
        TECNM_FRONTEND_CONTAINER_NAME=${previous_settings[2]} TECNM_BACKEND_CONTAINER_NAME=${previous_settings[3]}
        save_settings || return 1
        return "$result"
      fi
      ((index+=1))
    done
    load_settings || return
    printf '\n  Eliminando contenedores para volver a crearlos: %s\n' "${services[*]}"
    "${COMPOSE[@]}" rm -sf "${services[@]}" || return
    # El nombre registrado también puede existir sin etiquetas de este Compose.
    for service in "${services[@]}"; do
      case $service in
        frontend) previous_names+=("$TECNM_FRONTEND_CONTAINER_NAME");;
        backend) previous_names+=("$TECNM_BACKEND_CONTAINER_NAME");;
      esac
    done
    for name in "${previous_names[@]}"; do
      if docker inspect -f '{{.Id}}' "$name" >/dev/null 2>&1; then
        docker rm -f "$name" || return
        if docker inspect -f '{{.Id}}' "$name" >/dev/null 2>&1; then
          printf 'No se eliminó el contenedor "%s"; se detuvo la recreación.\n' "$name" >&2
          return 1
        fi
      fi
    done
  fi
  for service in "${services[@]}"; do
    if [[ $mode == recreate ]]; then
      bash scripts/control/create.sh "$service" --ports-prepared || return
    else
      bash scripts/control/create.sh "$service" --prepared || return
    fi
    load_settings || return 1
  done
}

run_action() {
  local result
  "$@"
  result=$?
  if (( result == 0 )); then
    printf '\n  %sAcción finalizada.%s\n' "$success" "$reset"
  elif (( result == 2 )); then
    printf '\n  Operación cancelada. Puedes volver a intentarlo más tarde.\n'
  else
    printf '\n  %sLa acción no se completó (código %s). Revisa el mensaje anterior.%s\n' "$danger" "$result" "$reset"
  fi
  pause_panel
}

while true; do
  heading 'Panel de servicios / PostgreSQL en el anfitrión'
  printf '  %sESTADO ACTUAL%s\n' "$bold" "$reset"
  bash scripts/control/status.sh
  separator
  printf '  %sSERVICIOS%s\n' "$muted" "$reset"
  menu_item 1 'Crear servicios'
  menu_item 2 'Iniciar servicios'
  menu_item 3 'Recrear servicios' "$warning"
  printf '  %sBASE DE DATOS%s\n' "$muted" "$reset"
  menu_item 4 'Cargar base de datos'
  menu_item 5 'Recrear base de datos · elimina datos' "$danger"
  printf '  %sHERRAMIENTAS%s\n' "$muted" "$reset"
  menu_item 6 'Detener contenedores' "$warning"
  menu_item 7 'Ver registros'
  menu_item 8 'Limpiar contenedores y base de datos · elimina datos' "$danger"
  menu_item R 'Actualizar estado'
  separator
  menu_item 0 'Salir'
  read_key 'Selecciona una tecla (sin Enter):' option || exit 0
  case "$option" in
    1) if select_services 'Crear servicios' crear; then run_action manage_services create; fi;;
    2) if select_services 'Iniciar servicios' iniciar; then run_action manage_services start; fi;;
    3) if select_services 'Recrear servicios' recrear; then run_action manage_services recreate; fi;;
    4) run_action bash scripts/control/database.sh load;;
    5) run_action bash scripts/control/database.sh recreate;;
    6) run_action "${COMPOSE[@]}" stop;;
    7)
      printf '\n  Registros en vivo · Ctrl+C para volver al panel.\n\n'
      trap ':' INT
      "${COMPOSE[@]}" logs --tail=100 -f
      trap - INT
      pause_panel;;
    8) run_action bash scripts/control/cleanup.sh;;
    r|R) :;;
    0) printf '\n  Hasta luego.\n'; exit 0;;
    *) printf '\n  %sOpción no válida.%s\n' "$warning" "$reset"; pause_panel;;
  esac
  load_settings || exit 1
done
