#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
source "$(dirname "$0")/ui.sh"
while true; do
  heading 'Configuración / Backend'
  if [[ -f backend/.env ]]; then
    printf '  Se detectó que backend/.env ya existe.\n\n'
    menu_item 1 'Continuar con el archivo existente'
    menu_item 2 'Crear uno nuevo con el asistente'
    menu_item 0 'Cancelar y volver'
    read_key 'Selecciona una tecla:' option || exit 2
    case "$option" in
      1) exit 0;;
      2) exec python3 scripts/control/configure-backend.py --replace;;
      0) exit 2;;
      *) printf '  Opción no válida.\n';;
    esac
  else
    printf '  No tienes configurado backend/.env.\n'
    printf '  Es necesario para crear los contenedores.\n\n'
    menu_item 1 'Crear backend/.env con el asistente'
    menu_item 0 'Configurar por mi cuenta y volver más tarde'
    read_key 'Selecciona una tecla:' option || exit 2
    case "$option" in
      1) exec python3 scripts/control/configure-backend.py;;
      0) printf '\n  Usa backend/.env.example como referencia y vuelve a intentarlo.\n'; exit 2;;
      *) printf '  Opción no válida.\n';;
    esac
  fi
done
