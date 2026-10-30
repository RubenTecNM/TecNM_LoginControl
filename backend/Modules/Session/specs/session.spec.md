# Módulo backend de sesión

Estado: implementado; la prueba con proveedores reales está pendiente de configuración institucional. El contrato compartido es [inicio de sesión institucional](../../../../specs/session-control.spec.md).

## Identidad y sesión

- Usar OpenID Connect con código de autorización, PKCE, `state` y `nonce`. Validar firma, emisor, audiencia, expiración, correlación y organización antes de consultar y autorizar al usuario. Rechazar callbacks repetidos o no correlacionados.
- Google: exigir `email_verified`, `hd` del Workspace autorizado, correo de dominio exacto y coincidencia tanto del correo como del `sub` registrado. `sub` se trata como identificador opaco y se compara exactamente.
- Microsoft: validar tenant permitido, `tid`, `oid` y correo registrado. Configurar el claim opcional `acct` y aceptar únicamente el valor de miembro `0`; rechazar invitado, ausencia o valor desconocido. No usar el correo como única prueba de identidad. Si falta el correo requerido, rechazar el acceso.
- El backend firma el JWT propio con clave exclusiva del backend y valida firma, algoritmo permitido, emisor, audiencia y vencimiento. Configurar y proteger clave, credenciales y valores por entorno; no registrar tokens, códigos de autorización ni secretos en logs; el proxy tampoco registra consultas de `/api/`. No exponer errores internos.
- Emitir cookie de sesión solo tras todas las comprobaciones. Proteger el cierre de sesión y cualquier operación mutativa contra CSRF; `state` y `nonce` protegen la correlación del flujo OIDC. Configurar CORS con orígenes explícitos cuando se usen credenciales entre orígenes.
- La consulta de sesión valida JWT y busca el usuario y tipo actual en la base. Si no existe, responde `401`; no confunde fallos de base con una identidad inexistente. El cierre borra la cookie con los atributos necesarios para eliminarla y no revoca el token ni termina la sesión del proveedor.

## Persistencia

`database.sql` inicializa las siguientes tablas en un volumen nuevo; su modificación no migra volúmenes existentes. El mecanismo para actualizar bases existentes debe definirse antes de aplicarlo; no se aplican migraciones ni cambios automáticos a bases reales como prueba.

- `user_types`: `id` UUID, clave primaria; `code` único y estable; `name` no vacío para mostrar. Inicializar los códigos `superadmin`, `admin`, `docente`, `default`, con nombres visibles `Superadministrador`, `Administrador`, `Docente`, `Predeterminado`. Un código no cambia al modificar la etiqueta.
- `users`: `id` UUID primario; `user_type_id` UUID obligatorio con clave foránea a `user_types`; `first_names` varchar(150) y `paternal_last_name` varchar(100), obligatorios y no vacíos ni solo espacios; `maternal_last_name` varchar(100), nullable y admite cadena vacía; `google_email` y `microsoft_email` varchar(254), nullable; `google_subject` varchar(255), nullable; `microsoft_tenant_id` y `microsoft_object_id` UUID, nullable.
- Al menos una cuenta de proveedor debe estar presente. Correo Google y `google_subject` están ambos presentes o ausentes; correo Microsoft, tenant y objeto están los tres presentes o ausentes. Los identificadores y correos presentes no son vacíos ni solo espacios.
- Cada correo es único por proveedor según la regla compartida, con múltiples nulos permitidos. `google_subject` es único con comparación exacta; el par Microsoft `(microsoft_tenant_id, microsoft_object_id)` es único. Verificar restricciones también al insertar datos fuera de la aplicación.
- Los correos almacenados deben estar normalizados según la regla compartida y pertenecer al dominio exacto del proveedor. Las asociaciones entre correo e identidad son responsabilidad de quien registra usuarios.
- Mantener entidades y mapeos EF Core alineados con SQL; consultas asíncronas sin SQL embebido. Los DTO de sesión no exponen entidades ni identificadores externos.

## Verificación

Construir el backend y comprobar el contrato compartido con base aislada e identidades simuladas. Cubrir ambos proveedores, cuentas no registradas, correos e identificadores incorrectos, correo ausente, invitado o `acct` ausente, dominio y organización incorrectos, firma y claims inválidos, correlación, repetición de callback, vencimiento, CSRF y cierre de sesión. Comprobar restricciones, datos semilla y unicidad en un volumen nuevo aislado, así como cambio de tipo y eliminación del usuario. Documentar por separado la prueba con proveedores reales cuando se disponga de configuración y cuentas de prueba.
