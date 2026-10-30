# Plan: inicio de sesión institucional

Estado: borrador preparado para revisión; no hay implementación. Actualizado el 29 de octubre de 2026.

## Objetivo y alcance

En el proyecto derivado `TecNM_LoginControl`, permitir que una persona previamente registrada inicie sesión con Google o Microsoft y vea una bienvenida. El backend emite el JWT de sesión firmado con una clave secreta exclusiva del backend.

Esta etapa incluye un tipo por usuario, visible en la bienvenida y presente en el JWT, sin diferencias de funciones ni permisos entre tipos. No incluye autorregistro, edición de correos, administración o carga de usuarios, autorización por tipo, integración con otras aplicaciones ni lógica de negocio. La forma de registrar usuarios es externa a este alcance: el login solo necesita un contrato claro para los datos ya presentes en la base. El plan anterior de login central se conserva como obsoleto en `plans-obsoletes/`; sus propuestas no forman parte del alcance actual.

## Reglas acordadas

- Solo usuarios existentes en la base de datos pueden ingresar.
- Cada usuario puede tener una cuenta institucional de Google, una de Microsoft o ambas; máximo una por proveedor.
- Google: correo institucional del dominio exacto `itsmva.edu.mx`. Microsoft: correo institucional del dominio exacto `monclova.tecnm.mx`. No se admiten subdominios ni cuentas invitadas de Microsoft.
- El backend debe comprobar que el correo presentado corresponde al campo registrado para el proveedor elegido. No se permite cambiarlo mediante el login.
- No usar nombres ni apellidos del proveedor; leerlos del usuario registrado.
- Para Google, registrar previamente y comprobar el identificador estable `sub`, además del correo de Google.
- Para Microsoft, registrar previamente y comprobar el identificador de tenant (`tid`) y el identificador del objeto de usuario (`oid`), además del correo de Microsoft. Configurar y validar el claim opcional `acct`: aceptar solo miembros (`0`) y rechazar invitados, valores desconocidos o ausencia del claim.
- Ante identidad inválida, correo no disponible o no coincidente, o usuario no registrado, rechazar el acceso sin emitir sesión.
- La clave de firma y las credenciales de proveedores permanecen en el backend y fuera de Git. El JWT no contiene la clave.
- El JWT dura una hora, sin renovación automática. Al vencer, el usuario vuelve a iniciar sesión. Sus claims son `sub`, `iss`, `aud`, `iat`, `exp` y `user_type`.

## Modelo propuesto

Se propone una tabla `users` en PostgreSQL y un catálogo `user_types` para los tipos de usuario. No se necesitan tablas de permisos, aplicaciones, contraseñas o sesiones para el alcance actual.

| Columna | Tipo PostgreSQL propuesto | Nulable | Propósito |
| --- | --- | --- | --- |
| `id` | `uuid` | No | Clave primaria e identificador interno del usuario. |
| `user_type_id` | `uuid` | No | Referencia al tipo registrado del usuario. |
| `first_names` | `varchar(150)` | No | Nombre o nombres registrados. |
| `paternal_last_name` | `varchar(100)` | No | Apellido paterno registrado. |
| `maternal_last_name` | `varchar(100)` | Sí | Apellido materno cuando corresponda. |
| `google_email` | `varchar(254)` | Sí | Correo institucional registrado para Google. |
| `google_subject` | `varchar(255)` | Sí | Identificador estable `sub` de la cuenta Google. |
| `microsoft_email` | `varchar(254)` | Sí | Correo institucional registrado para Microsoft. |
| `microsoft_tenant_id` | `uuid` | Sí | Identificador `tid` del tenant Microsoft. |
| `microsoft_object_id` | `uuid` | Sí | Identificador `oid` del usuario dentro del tenant. |

El catálogo `user_types` tendrá un `id` como clave primaria, un `code` único y estable para el JWT y un `name` para mostrar en la bienvenida. `users.user_type_id` sería una clave foránea al catálogo; el tipo exacto de sus identificadores queda por definir.

Restricciones propuestas:

- `id` es clave primaria; nombres obligatorios no pueden quedar vacíos o contener solo espacios. El apellido materno puede omitirse o almacenarse como cadena vacía.
- Al menos uno de los dos correos debe estar presente. Una cuenta Google puede existir sin cuenta Microsoft y viceversa.
- El correo Google y `google_subject` deben estar presentes juntos o ausentes juntos; `google_subject` es único cuando está presente.
- El correo Microsoft, `microsoft_tenant_id` y `microsoft_object_id` deben estar presentes juntos o ausentes juntos.
- Cada correo es único dentro de su proveedor; `(microsoft_tenant_id, microsoft_object_id)` es único cuando está presente. Las restricciones deben permitir múltiples valores nulos.
- Eliminar espacios exteriores al registrar y comparar correos sin distinguir mayúsculas; aplicar la misma regla para su unicidad dentro de cada proveedor. Comparar `google_subject` de forma exacta.
- Requerir los dominios exactos `@itsmva.edu.mx` para Google y `@monclova.tecnm.mx` para Microsoft, sin subdominios. El dominio del correo no sustituye la validación de la organización y la identidad del proveedor.
- Cada usuario tiene exactamente un tipo obligatorio: `superadmin`, `admin`, `docente` o `default`. Estos cuatro tipos se incluyen como datos semilla en la inicialización de la base; el claim `user_type` contiene el código estable en minúsculas.
- No se proponen columnas de estado ni auditoría para esta etapa. La persona que administra la base debe proporcionar registros coherentes y, para Microsoft, una asociación confiable entre correo, tenant y objeto.

El login consume esos datos ya registrados; la herramienta y el procedimiento de alta quedan fuera de este proyecto. Para Google, el registro previo debe asociar de manera confiable el correo con el `sub` de la cuenta.

**Validación de identidad:** Microsoft indica que el claim `email` no debe usarse como base de autorización; el acceso requiere también coincidencia de `tid` y `oid` previamente registrados y tenant permitido. Google indica que `email_verified` por sí solo no demuestra control actual sobre una dirección externa a Gmail o Workspace. Para Google, exigir una cuenta Workspace del dominio acordado, comprobar el claim `hd`, el `sub` registrado y el correo verificado y registrado; rechazar el acceso si el identificador estable o el correo ya no coinciden con el registro. La actualización de registros corresponde a una futura función de control de usuarios, fuera de este alcance. Una dirección puede cambiar o reasignarse, incluso dentro de una organización.

## Flujo técnico propuesto

1. El usuario elige Google o Microsoft; OpenID Connect usa código de autorización, PKCE, `state` y `nonce`.
2. El backend valida la respuesta del proveedor: firma, emisor, audiencia, vencimiento, correlación y organización autorizada.
3. El backend consulta el usuario registrado y comprueba el correo correspondiente. Para Microsoft, comprueba además `tid` y `oid` registrados; para Google, comprueba el `sub` registrado y valida el dominio Workspace y el correo verificado.
4. Si se autoriza, el backend emite JWT propio con una hora de duración y sin renovación automática. Se propone cookie `HttpOnly` y `Secure`, con `SameSite` compatible y protección CSRF para operaciones mutativas; no incluir el token en URLs.
5. El frontend consulta el estado de sesión al backend y muestra una bienvenida igual para todos, con nombre registrado y etiqueta del tipo vigente. El backend valida firma, emisor, audiencia y vencimiento del JWT y consulta al usuario; si ya no existe, rechaza la consulta. El frontend consulta al cargar la página y al volver a la pestaña, sin consultas periódicas; al vencer la hora muestra el vencimiento de la sesión. La eliminación se detecta en la siguiente consulta, no necesariamente mientras la pantalla permanece abierta.
6. Al vencer la hora, se exige iniciar sesión nuevamente. Un botón permite cerrar sesión borrando la cookie; una copia del JWT sigue siendo válida hasta su vencimiento. El cierre no termina la sesión de Google o Microsoft ni exige volver a escribir la contraseña del proveedor.

### Contenido del JWT de aplicación

| Claim | Contenido |
| --- | --- |
| `sub` | `users.id` convertido a texto. |
| `iss` | Identificador configurado del backend emisor. |
| `aud` | Identificador configurado de la aplicación destinataria. |
| `iat` | Instante de emisión. |
| `exp` | Instante de vencimiento, una hora después de la emisión. |
| `user_type` | Código estable del tipo: `superadmin`, `admin`, `docente` o `default`. |

El backend obtiene nombres, apellidos y tipo de la base para la bienvenida. El JWT no incluye clave secreta, correos, nombres ni identificadores externos. Si cambia el tipo en la base, el JWT conserva el tipo emitido hasta su vencimiento; la bienvenida consulta el tipo vigente en la base. La verificación de permisos actuales corresponde a una etapa posterior. La firma permite detectar modificaciones; no cifra el contenido. Las claves y valores de emisor y audiencia se configuran por entorno.

## Pendientes para cerrar el diseño

1. Identificador del tenant de Microsoft y confirmación de la organización Google Workspace. Los identificadores y credenciales se necesitarán al configurar y probar proveedores.
2. Orígenes de frontend y backend para configurar cookies, callbacks y CSRF. Las URLs definitivas se requieren para configurar los proveedores y probar el flujo.

## Etapas

1. Resolver los puntos que afectan identidad y contrato; confirmar entendimiento antes de implementar.
2. Actualizar las specs compartida y locales antes del código; reflejar que este repositorio es un proyecto derivado. Definir flujos, respuestas, fallos y aceptación.
3. Alinear `database.sql`, datos semilla de los cuatro tipos, restricciones y mapeos EF Core. Definir cómo se actualizarán bases existentes: editar el SQL no migra volúmenes existentes.
4. Implementar autenticación en un módulo backend y la vista de inicio y bienvenida en un módulo frontend, siguiendo las reglas locales y reutilizando componentes existentes.
5. Configurar secretos, callbacks y proveedores sin incluir credenciales en Git; actualizar documentación y setup aplicables.
6. Verificar con base aislada e identidades simuladas: ingreso válido por cada proveedor, usuario o correo no registrado, proveedor equivocado, identidad incorrecta, respuesta o JWT inválido/vencido, tipo en el JWT y la bienvenida, y controles del flujo. Comprobar proveedores reales cuando existan credenciales y cuentas de prueba.
7. Ejecutar builds, revisión de layout y verificaciones de contrato y setup correspondientes; documentar resultados y límites. Usar `odd/tasks/` para el seguimiento de implementación si requiere continuidad.

## Referencias

- [Reglas compartidas](../AGENTS.md), [backend](../backend/AGENTS.md), [frontend](../frontend/AGENTS.md) y [contribución](../CONTRIBUTING.md).
- [Setup](../docs/setup.md) y [contrato Docker](../specs/docker.spec.md).
- [Google: OpenID Connect](https://developers.google.com/identity/openid-connect/openid-connect) y [autoridad sobre correo](https://developers.google.com/identity/gsi/web/reference/html-reference).
- [Microsoft: claims opcionales y `acct`](https://learn.microsoft.com/en-us/entra/identity-platform/optional-claims-reference) y [claims de identidad](https://learn.microsoft.com/en-us/entra/identity-platform/id-token-claims-reference) y [riesgo de autorizar por correo](https://learn.microsoft.com/en-us/graph/applications-authenticationbehaviors?tabs=http).
- [OWASP: gestión de sesiones](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).
