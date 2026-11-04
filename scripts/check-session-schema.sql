-- Ejecutar únicamente contra una base de prueba inicializada con database.sql.
BEGIN;
CREATE FUNCTION pg_temp.expect_rejected(statement text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    BEGIN
        EXECUTE statement;
    EXCEPTION WHEN integrity_constraint_violation THEN
        RETURN;
    END;
    RAISE EXCEPTION 'La base aceptó una operación inválida: %', statement;
END;
$$;

DO $$
BEGIN
    IF (SELECT count(*) FROM user_types) <> 4 OR
       (SELECT count(*) FROM user_types WHERE code IN ('superadmin', 'admin', 'docente', 'default')) <> 4 THEN
        RAISE EXCEPTION 'Los tipos iniciales no coinciden';
    END IF;
END;
$$;

INSERT INTO users (id, user_type_id, first_names, paternal_last_name, maternal_last_name, google_email, google_subject)
VALUES ('10000000-0000-4000-8000-000000000001', '00000000-0000-4000-8000-000000000003', 'Ana', 'García', '', 'Ana@itsmva.edu.mx', 'subject-A');
INSERT INTO users (id, user_type_id, first_names, paternal_last_name, microsoft_email, microsoft_tenant_id, microsoft_object_id)
VALUES ('10000000-0000-4000-8000-000000000002', '00000000-0000-4000-8000-000000000004', 'Luis', 'Pérez', 'luis@monclova.tecnm.mx', '20000000-0000-4000-8000-000000000001', '30000000-0000-4000-8000-000000000001');
INSERT INTO users (id, user_type_id, first_names, paternal_last_name, google_email, google_subject)
VALUES ('10000000-0000-4000-8000-000000000003', '00000000-0000-4000-8000-000000000004', 'Eva', 'López', 'eva@itsmva.edu.mx', 'subject-a');

SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','ana@itsmva.edu.mx','new-subject')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@itsmva.edu.mx','subject-A')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@sub.itsmva.edu.mx','other')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez',' other@itsmva.edu.mx','other')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,microsoft_email,microsoft_tenant_id,microsoft_object_id) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@monclova.tecnm.mx','20000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000001')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,microsoft_email,microsoft_tenant_id) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@monclova.tecnm.mx','20000000-0000-4000-8000-000000000001')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','   ','Pérez','other@itsmva.edu.mx','other')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000099','Ana','Pérez','other@itsmva.edu.mx','other')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,microsoft_email,microsoft_tenant_id,microsoft_object_id) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','LUIS@monclova.tecnm.mx','20000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000002')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,microsoft_email,microsoft_tenant_id,microsoft_object_id) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@sub.monclova.tecnm.mx','20000000-0000-4000-8000-000000000001','30000000-0000-4000-8000-000000000002')$sql$);
SELECT pg_temp.expect_rejected($sql$INSERT INTO users (id,user_type_id,first_names,paternal_last_name,google_email,google_subject) VALUES ('10000000-0000-4000-8000-000000000004','00000000-0000-4000-8000-000000000003','Ana','Pérez','other@itsmva.edu.mx','   ')$sql$);
ROLLBACK;
