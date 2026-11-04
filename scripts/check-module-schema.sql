-- Ejecutar únicamente en una base de prueba aislada.
BEGIN;
CREATE FUNCTION pg_temp.expect_rejected(statement text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN EXECUTE statement;
    EXCEPTION WHEN integrity_constraint_violation THEN RETURN;
    END;
    RAISE EXCEPTION 'La base aceptó una operación inválida: %', statement;
END;
$$;
INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email)
VALUES ('10000000-0000-4000-8000-000000000001','00000000-0000-4000-8000-000000000004','Prueba','Módulo','module@itsmva.edu.mx');
INSERT INTO modules (id,name,entry_url,api_key_hash,is_active)
VALUES ('20000000-0000-4000-8000-000000000001','Módulo','https://example.test/receive',decode(repeat('ab',32),'hex'),true);
INSERT INTO user_modules VALUES ('10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',true);
INSERT INTO auth_tokens (id,user_id,module_id,token_hash,created_at,expires_at)
VALUES ('30000000-0000-4000-8000-000000000001','10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',decode(repeat('cd',32),'hex'),now(),now()+interval '60 seconds');
SELECT pg_temp.expect_rejected($sql$INSERT INTO user_modules VALUES ('10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',true)$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO user_modules VALUES ('10000000-0000-4000-8000-000000000099','20000000-0000-4000-8000-000000000001',true)$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO user_modules VALUES ('10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000099',true)$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO auth_tokens (id,user_id,module_id,token_hash,created_at,expires_at) VALUES ('30000000-0000-4000-8000-000000000002','10000000-0000-4000-8000-000000000001','20000000-0000-4000-8000-000000000001',decode(repeat('cd',32),'hex'),now(),now())$sql$);
ROLLBACK;
