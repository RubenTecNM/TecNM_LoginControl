#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
case ${1:-load} in
  load) printf '\n  Cargar database.sql en PostgreSQL local\n';;
  recreate) printf '\n  Recrear base de datos en PostgreSQL local\n';;
  *) printf 'Operación no válida.\n' >&2; exit 1;;
esac
[[ -f database.sql && -r database.sql ]] || { echo 'No se puede leer database.sql.' >&2; exit 1; }
source "$(dirname "$0")/database-common.sh"
read_database_target || exit $?
case $exists in
  t)
    printf 'La base "%s" ya existe. ¿Deseas continuar? Cualquier dato contenido se perderá.\n' "$target"
    read -r -p '¿Eliminarla y cargar database.sql desde cero? [s/N]: ' confirm || exit 2
    [[ $confirm == s || $confirm == S ]] || { echo 'Operación cancelada.'; exit 2; }
    read -r -p "Escribe el nombre exacto de la base ($target): " confirm || exit 2
    [[ $confirm == "$target" ]] || { echo 'Operación cancelada.'; exit 2; }
    drop_database
    ;;
  f)
    printf 'La base "%s" no existe. ¿Deseas crearla?\n' "$target"
    read -r -p '¿Crear y cargar database.sql? [s/N]: ' confirm || exit 2
    [[ $confirm == s || $confirm == S ]] || { echo 'Operación cancelada.'; exit 2; }
    ;;
  *) echo 'PostgreSQL devolvió un estado inesperado; se canceló la operación.' >&2; exit 1;;
esac
"${PSQL[@]}" -d postgres -v db="$target" <<'SQL'
SELECT format('CREATE DATABASE %I', :'db') \gexec
SQL
if ! "${PSQL[@]}" --single-transaction -d "$target" -f database.sql; then
  printf 'Falló la carga de database.sql. La base nueva permanece sin el esquema; el borrado previo no puede revertirse.\n' >&2
  exit 1
fi
printf 'SQL aplicado en %s.\n' "$target"
