-- Inicialización de LoginControl para una base PostgreSQL nueva.
-- No se reaplica a volúmenes existentes ni sustituye una migración.
BEGIN;

CREATE TABLE user_types (
    id uuid PRIMARY KEY,
    code varchar(32) NOT NULL UNIQUE,
    name varchar(100) NOT NULL,
    CONSTRAINT user_types_code_not_blank CHECK (code ~ '[^[:space:]]'),
    CONSTRAINT user_types_name_not_blank CHECK (name ~ '[^[:space:]]')
);

INSERT INTO user_types (id, code, name) VALUES
    ('00000000-0000-4000-8000-000000000001', 'superadmin', 'Superadministrador'),
    ('00000000-0000-4000-8000-000000000002', 'admin', 'Administrador'),
    ('00000000-0000-4000-8000-000000000003', 'docente', 'Docente'),
    ('00000000-0000-4000-8000-000000000004', 'default', 'Predeterminado');

CREATE TABLE users (
    id uuid PRIMARY KEY,
    user_type_id uuid NOT NULL REFERENCES user_types (id),
    first_names varchar(150) NOT NULL,
    paternal_last_name varchar(100) NOT NULL,
    maternal_last_name varchar(100),
    google_email varchar(254),
    google_subject varchar(255) COLLATE "C",
    microsoft_email varchar(254),
    microsoft_tenant_id uuid,
    microsoft_object_id uuid,
    CONSTRAINT users_first_names_not_blank CHECK (first_names ~ '[^[:space:]]'),
    CONSTRAINT users_paternal_last_name_not_blank CHECK (paternal_last_name ~ '[^[:space:]]'),
    CONSTRAINT users_provider_required CHECK (google_email IS NOT NULL OR microsoft_email IS NOT NULL),
    CONSTRAINT users_google_account_complete CHECK (google_subject IS NULL OR google_email IS NOT NULL),
    CONSTRAINT users_microsoft_account_complete CHECK (
        (microsoft_email IS NULL AND microsoft_tenant_id IS NULL AND microsoft_object_id IS NULL)
        OR (microsoft_email IS NOT NULL AND ((microsoft_tenant_id IS NULL AND microsoft_object_id IS NULL)
            OR (microsoft_tenant_id IS NOT NULL AND microsoft_object_id IS NOT NULL)))
    ),
    CONSTRAINT users_google_email_domain CHECK (
        google_email IS NULL OR google_email ~* '^[^[:space:]@]+@itsmva[.]edu[.]mx$'
    ),
    CONSTRAINT users_microsoft_email_domain CHECK (
        microsoft_email IS NULL OR microsoft_email ~* '^[^[:space:]@]+@monclova[.]tecnm[.]mx$'
    ),
    CONSTRAINT users_google_subject_not_blank CHECK (google_subject IS NULL OR google_subject ~ '[^[:space:]]'),
    CONSTRAINT users_google_subject_unique UNIQUE (google_subject),
    CONSTRAINT users_microsoft_identity_unique UNIQUE (microsoft_tenant_id, microsoft_object_id)
);

CREATE UNIQUE INDEX users_google_email_unique ON users (lower(google_email));
CREATE UNIQUE INDEX users_microsoft_email_unique ON users (lower(microsoft_email));

COMMIT;
