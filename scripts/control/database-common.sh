#!/usr/bin/env bash
# Conexión y consulta compartidas por las operaciones PostgreSQL del panel.
read_database_target() {
  command -v psql >/dev/null || { echo 'Falta el cliente psql.' >&2; return 1; }
  read -r -p 'Servidor PostgreSQL [localhost]: ' PGHOST || return 2; PGHOST=${PGHOST:-localhost}
  read -r -p 'Puerto PostgreSQL [5432]: ' PGPORT || return 2; PGPORT=${PGPORT:-5432}
  [[ $PGPORT =~ ^[0-9]{1,5}$ ]] && (( 10#$PGPORT >= 1 && 10#$PGPORT <= 65535 )) || { echo 'Puerto no válido.' >&2; return 1; }
  read -r -p 'Usuario PostgreSQL: ' PGUSER || return 2
  read -r -p 'Nombre de la base de datos destino (snake_case): ' target || return 2
  [[ -n $PGUSER && $target =~ ^[a-z_][a-z0-9_]*$ && ${#target} -le 63 ]] || { echo 'Usuario obligatorio y nombre de base no válido (snake_case, máximo 63 caracteres).' >&2; return 1; }
  case $target in postgres|template0|template1) echo 'No se permite modificar bases de sistema.' >&2; return 1;; esac
  read -r -s -p 'Contraseña PostgreSQL: ' PGPASSWORD || return 2; echo
  export PGHOST PGPORT PGUSER PGPASSWORD
  export PGCONNECT_TIMEOUT=5
  PSQL=(psql -X -w -v ON_ERROR_STOP=1)
  if ! exists=$("${PSQL[@]}" -At -d postgres -v db="$target" <<'SQL'
SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db');
SQL
  ); then
    echo 'No se pudo consultar PostgreSQL. Comprueba credenciales y que el servicio esté corriendo.' >&2
    return 1
  fi
}

drop_database() {
  "${PSQL[@]}" -d postgres -v db="$target" <<'SQL'
SELECT format('DROP DATABASE %I', :'db') \gexec
SQL
}
