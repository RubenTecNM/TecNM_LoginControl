#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/common.sh"
case ${1:-load} in
  load) printf '\n  Cargar database.sql en PostgreSQL local\n';;
  recreate) printf '\n  Recrear base de datos en PostgreSQL local\n';;
  *) printf 'Operación no válida.\n' >&2; exit 1;;
esac
[[ -f database.sql && -r database.sql ]] || { echo 'No se puede leer database.sql.' >&2; exit 1; }
command -v psql >/dev/null || { echo 'Falta el cliente psql.' >&2; exit 1; }
read -r -p 'Servidor PostgreSQL [localhost]: ' PGHOST; PGHOST=${PGHOST:-localhost}
read -r -p 'Puerto PostgreSQL [5432]: ' PGPORT; PGPORT=${PGPORT:-5432}
[[ $PGPORT =~ ^[0-9]{1,5}$ ]] && (( 10#$PGPORT >= 1 && 10#$PGPORT <= 65535 )) || { echo 'Puerto no válido.' >&2; exit 1; }
read -r -p 'Usuario PostgreSQL: ' PGUSER
read -r -p 'Nombre de la base de datos destino (snake_case): ' target
[[ -n $PGUSER && $target =~ ^[a-z_][a-z0-9_]*$ && ${#target} -le 63 ]] || { echo 'Usuario obligatorio y nombre de base no válido (snake_case, máximo 63 caracteres).' >&2; exit 1; }
case $target in postgres|template0|template1) echo 'No se permite reemplazar bases de sistema.' >&2; exit 1;; esac
read -r -s -p 'Contraseña PostgreSQL: ' PGPASSWORD; echo
export PGHOST PGPORT PGUSER PGPASSWORD
export PGCONNECT_TIMEOUT=5
PSQL=(psql -X -w -v ON_ERROR_STOP=1)
if ! exists=$("${PSQL[@]}" -At -d postgres -v db="$target" <<'SQL'
SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db');
SQL
); then
  echo 'No se pudo consultar PostgreSQL. Comprueba credenciales y que el servicio esté corriendo.' >&2
  exit 1
fi
case $exists in
  t)
    printf 'La base "%s" ya existe. ¿Deseas continuar? Cualquier dato contenido se perderá.\n' "$target"
    read -r -p '¿Eliminarla y cargar database.sql desde cero? [s/N]: ' confirm || exit 2
    [[ $confirm == s || $confirm == S ]] || { echo 'Operación cancelada.'; exit 2; }
    read -r -p "Escribe el nombre exacto de la base ($target): " confirm || exit 2
    [[ $confirm == "$target" ]] || { echo 'Operación cancelada.'; exit 2; }
    "${PSQL[@]}" -d postgres -v db="$target" <<'SQL'
SELECT format('DROP DATABASE %I', :'db') \gexec
SQL
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
