#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

# El contenedor y su base son exclusivos de esta prueba; no se usa el volumen del proyecto.
test_container=$(docker run -d --rm -e POSTGRES_PASSWORD=isolated-test-password -e POSTGRES_DB=isolated_test -p 127.0.0.1::5432 postgres:15)
trap 'docker stop "$test_container" >/dev/null 2>&1 || true' EXIT
for attempt in {1..30}; do
    if docker exec "$test_container" pg_isready -U postgres -d isolated_test >/dev/null 2>&1; then break; fi
    if [ "$attempt" -eq 30 ]; then echo 'PostgreSQL aislado no estuvo disponible.' >&2; exit 1; fi
    sleep 1
done
docker exec -i "$test_container" psql -U postgres -d isolated_test -v ON_ERROR_STOP=1 < database.sql >/dev/null
docker exec -i "$test_container" psql -U postgres -d isolated_test -v ON_ERROR_STOP=1 < scripts/check-session-schema.sql >/dev/null
docker exec -i "$test_container" psql -U postgres -d isolated_test -v ON_ERROR_STOP=1 < scripts/check-module-schema.sql >/dev/null
# Verificar la migración sobre un esquema anterior y conservar un usuario existente.
docker exec "$test_container" createdb -U postgres migration_test
{ sed '/^CREATE TABLE modules (/,$d' database.sql; printf '%s\n' COMMIT\;; } | docker exec -i "$test_container" psql -U postgres -d migration_test -v ON_ERROR_STOP=1 >/dev/null
printf '%s\n' "INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email) VALUES ('40000000-0000-4000-8000-000000000001','00000000-0000-4000-8000-000000000004','Usuario','Existente','sentinel@itsmva.edu.mx');" | docker exec -i "$test_container" psql -U postgres -d migration_test -v ON_ERROR_STOP=1 >/dev/null
docker exec -i "$test_container" psql -U postgres -d migration_test -v ON_ERROR_STOP=1 < scripts/migrations/20261104_module_access.sql >/dev/null
docker exec -i "$test_container" psql -U postgres -d migration_test -v ON_ERROR_STOP=1 < scripts/check-module-schema.sql >/dev/null
docker exec "$test_container" psql -U postgres -d migration_test -v ON_ERROR_STOP=1 -c "DO \$\$ BEGIN IF NOT EXISTS (SELECT 1 FROM users WHERE id='40000000-0000-4000-8000-000000000001') THEN RAISE EXCEPTION 'Usuario previo perdido'; END IF; END \$\$;" >/dev/null
test_port=$(docker port "$test_container" 5432/tcp | awk -F: 'NR==1 { print $NF }')
SESSION_TEST_DATABASE="Host=127.0.0.1;Port=$test_port;Database=isolated_test;Username=postgres;Password=isolated-test-password" \
    dotnet test tests/TecNM.Session.Tests/TecNM.Session.Tests.csproj
