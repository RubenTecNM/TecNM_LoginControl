#!/usr/bin/env bash
# Presentación compartida del panel y el resumen de estado.
reset='' bold='' muted='' accent='' success='' warning='' danger=''
if [[ -t 1 && ${TERM:-dumb} != dumb && ! ${NO_COLOR+x} ]]; then
  reset=$'\033[0m' bold=$'\033[1m' muted=$'\033[90m'
  accent=$'\033[36m' success=$'\033[32m' warning=$'\033[33m' danger=$'\033[31m'
fi
separator() {
  local width=${COLUMNS:-68}
  [[ $width =~ ^[0-9]+$ ]] || width=68
  (( width > 78 )) && width=78
  printf '%s' "$muted"
  printf '%*s' "$width" '' | tr ' ' '-'
  printf '%s\n' "$reset"
}
heading() {
  if [[ -t 1 && ${TERM:-dumb} != dumb ]]; then printf '\033[2J\033[H'; fi
  printf '  %sTECNM%s / LOGIN CONTROL\n' "$bold$accent" "$reset"
  printf '  %s%s%s\n' "$muted" "$1" "$reset"
  separator
}
menu_item() { printf '  %s[%s]%s  %s%s%s\n' "$accent$bold" "$1" "$reset" "${3:-}" "$2" "$reset"; }
read_key() {
  printf '\n  %s%s%s ' "$bold" "$1" "$reset"
  if [[ -t 0 ]]; then
    IFS= read -r -s -n 1 "$2" || return 1
    printf '\n'
  else
    IFS= read -r "$2" || return 1
  fi
}
pause_panel() {
  [[ -t 0 ]] || return 0
  local ignored
  read_key 'Presiona una tecla para volver.' ignored || exit 0
}
