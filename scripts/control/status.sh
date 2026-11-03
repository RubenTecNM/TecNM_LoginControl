#!/usr/bin/env bash
source "$(dirname "$0")/common.sh"
source "$(dirname "$0")/ui.sh"
status_row() {
  local label=$1 state=$2 detail=$3 color=$warning marker='○'
  if [[ $state == ACTIVO ]]; then color=$success; marker='●'; fi
  printf '  %-18s %s%s %-14s%s  %s%s%s\n' "$label" "$color" "$marker" "$state" "$reset" "$muted" "$detail" "$reset"
}
backend_ready=false
backend_reason='Backend aún no responde'
docker_ready=false
docker info >/dev/null 2>&1 && docker_ready=true
service_status() {
  local label=$1 name=$2 port=$3 path=$4 running http_code
  if [[ $docker_ready != true ]]; then
    status_row "$label" 'SIN COMPROBAR' 'Docker no está disponible'
  elif ! running=$(docker inspect -f '{{.State.Running}}' "$name" 2>/dev/null); then
    status_row "$label" 'SIN CREAR' "$name · usa Crear servicios"
  elif [[ $running != true ]]; then
    status_row "$label" DETENIDO "$name"
  elif ! command -v curl >/dev/null; then
    status_row "$label" 'SIN COMPROBAR' 'Falta curl'
  elif http_code=$(curl -ksS --max-time 3 -o /dev/null -w '%{http_code}' "https://localhost:$port$path" 2>/dev/null); then
    if [[ $http_code == 200 ]]; then
      status_row "$label" ACTIVO "$name :$port"
      [[ $label != Backend ]] || backend_ready=true
    else
      status_row "$label" 'ERROR HTTP' "$name :$port · HTTP $http_code"
      [[ $label != Backend ]] || backend_reason="Health del backend: HTTP $http_code"
    fi
  else
    status_row "$label" 'NO DISPONIBLE' "$name · no responde en :$port"
  fi
}
service_status Frontend "$TECNM_FRONTEND_CONTAINER_NAME" "$TECNM_FRONTEND_PORT" /
service_status Backend "$TECNM_BACKEND_CONTAINER_NAME" "$TECNM_BACKEND_PORT" /api/health
python3 scripts/control/status-db.py
database_status=$?
case $database_status in
  0) status_row PostgreSQL ACTIVO 'Conexión local verificada';;
  2) status_row PostgreSQL 'SIN CONFIGURAR' 'Falta backend/.env';;
  3) status_row PostgreSQL 'SIN CONFIGURAR' 'Revisa la cadena de conexión';;
  4) status_row PostgreSQL 'SIN COMPROBAR' 'Falta el cliente psql';;
  5) status_row PostgreSQL 'SIN COMPROBAR' 'No se puede leer backend/.env';;
  *) status_row PostgreSQL 'SIN CONEXIÓN' 'Revisa credenciales y servicio local';;
esac
if (( database_status == 2 || database_status == 3 )); then
  status_row 'API / PostgreSQL' 'SIN COMPROBAR' 'Configura backend/.env'
elif (( database_status == 5 )); then
  status_row 'API / PostgreSQL' 'SIN COMPROBAR' 'No se puede leer backend/.env'
elif [[ $backend_ready != true ]]; then
  status_row 'API / PostgreSQL' 'SIN COMPROBAR' "$backend_reason"
elif curl -kfsS --max-time 5 "https://localhost:$TECNM_BACKEND_PORT/api/health/database" >/dev/null 2>&1; then
  status_row 'API / PostgreSQL' ACTIVO 'Conexión desde API verificada'
else
  status_row 'API / PostgreSQL' 'SIN CONEXIÓN' 'La API no pudo conectar con la base'
fi
