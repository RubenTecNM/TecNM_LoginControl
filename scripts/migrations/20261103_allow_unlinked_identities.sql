-- Permite registrar correos antes de vincular la identidad del proveedor.
BEGIN;
ALTER TABLE users DROP CONSTRAINT users_google_account_complete;
ALTER TABLE users DROP CONSTRAINT users_microsoft_account_complete;
ALTER TABLE users ADD CONSTRAINT users_google_account_complete
    CHECK (google_subject IS NULL OR google_email IS NOT NULL);
ALTER TABLE users ADD CONSTRAINT users_microsoft_account_complete CHECK (
    (microsoft_email IS NULL AND microsoft_tenant_id IS NULL AND microsoft_object_id IS NULL)
    OR (microsoft_email IS NOT NULL AND ((microsoft_tenant_id IS NULL AND microsoft_object_id IS NULL)
        OR (microsoft_tenant_id IS NOT NULL AND microsoft_object_id IS NOT NULL)))
);
COMMIT;
