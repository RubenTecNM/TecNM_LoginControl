-- Migración aditiva explícita; no ejecutar como prueba sobre bases reales.
BEGIN;
CREATE TABLE modules (
    id uuid PRIMARY KEY,
    name varchar(150) NOT NULL CHECK (name ~ '[^[:space:]]'),
    entry_url text NOT NULL,
    api_key_hash bytea NOT NULL CHECK (octet_length(api_key_hash) = 32),
    is_active boolean NOT NULL
);
CREATE TABLE user_modules (
    user_id uuid NOT NULL REFERENCES users(id),
    module_id uuid NOT NULL REFERENCES modules(id),
    is_active boolean NOT NULL,
    PRIMARY KEY (user_id, module_id)
);
CREATE INDEX user_modules_module_id_idx ON user_modules (module_id);
CREATE TABLE auth_tokens (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL REFERENCES users(id),
    module_id uuid NOT NULL REFERENCES modules(id),
    token_hash bytea NOT NULL UNIQUE CHECK (octet_length(token_hash) = 32),
    created_at timestamptz NOT NULL,
    expires_at timestamptz NOT NULL,
    browser_confirmed boolean NOT NULL DEFAULT false,
    used_at timestamptz,
    CHECK (expires_at > created_at)
);
COMMIT;
