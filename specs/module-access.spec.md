# Acceso a módulos externos

Estado: implementado; pruebas en recursos aislados. La verificación con un servicio externo real y la revisión visual están pendientes. Contrato del proyecto derivado LoginControl.

## Alcance

LoginControl es el único inicio de sesión. Las aplicaciones externas reciben un código opaco temporal, lo canjean desde su backend y crean su sesión propia con la identidad devuelta. Cada usuario puede tener varios módulos y cada módulo varios usuarios.

Solo `superadmin`, comprobado contra la base en cada petición, administra módulos y asignaciones. Los demás tipos pueden acceder únicamente a módulos activos con asignación activa. Ser superadmin no sustituye una asignación para acceder a una aplicación.

## Persistencia

Agregar `modules` (UUID, nombre, URL HTTPS de recepción, hash de API key, activo), `user_modules` (PK usuario/módulo, activo) y `auth_tokens` (UUID, usuario, módulo, hash único, creación, vencimiento, uso y `browser_confirmed`). Conservar usuarios y tipos actuales. Claves y códigos usan 32 bytes criptográficos; guardar SHA-256 y no el secreto original. Código con vigencia de 60 segundos y un solo uso.

El esquema inicial y los mapeos EF deben coincidir. La migración para una base existente es aditiva, explícita y se prueba únicamente en una base aislada. Reiniciar contenedores no migra datos.

## HTTP

Rutas relativas al origen público HTTPS de LoginControl. Respuestas con `Cache-Control: no-store`.

| Método y ruta | Entrada y salida |
| --- | --- |
| `GET /api/modules` | Cookie: lista de módulos activos asignados `{id,name,entryUrl,isActive}`. |
| `POST /api/modules/{id}/launch` | Cookie y origen exacto: `{url}` hacia URL registrada con query `ticket`. Usuario obtenido de sesión, nunca de entrada. |
| `GET /api/modules/authorize` | Cookie, query `{ticket,state}`: confirma navegador, consume ticket y redirige al módulo con `{code,state}`. |
| `POST /api/modules/exchange` | Header `Authorization: Bearer API_KEY`, JSON `{moduleId,code}`; devuelve `{user:{id,firstNames,paternalLastName,maternalLastName,googleEmail,microsoftEmail}}`. |
| `GET /api/admin/modules` | Solo superadmin: catálogo, sin claves. |
| `POST /api/admin/modules` | `{name,entryUrl,isActive}`; devuelve `{module,apiKey}` una sola vez. |
| `PATCH /api/admin/modules/{id}` | `{name,entryUrl,isActive}`; modifica módulo. |
| `POST /api/admin/modules/{id}/key` | Reemplaza clave; devuelve `{apiKey}` una sola vez. La anterior deja de funcionar. |
| `GET /api/admin/users?search=...` | Usuarios existentes con UUID, nombres y correos, sin identidades de proveedor. |
| `GET /api/admin/users/{userId}/modules` | Módulos con `isActive` de asignación y `moduleActive` del catálogo. |
| `PUT /api/admin/users/{userId}/modules/{moduleId}` | `{isActive}`; crea o actualiza asignación. |
| `GET /api/admin/modules/{moduleId}/users` | Usuarios con estado `isActive` de asignación. |
| `PUT /api/admin/modules/{moduleId}/users` | `{mode,userIds,isActive}`; resultado `{created,changed,unchanged}`. |

Mutaciones con cookie requieren origen exacto configurado; canje autentica módulo, no cookie. No ampliar CORS para peticiones entre backends. Campos de identidad nulables: apellido materno y ambos correos; al menos un correo está registrado. No entregar tipo de usuario, JWT ni identificadores externos de proveedor.

Errores: `400` entrada o código inválidos (incluye usado, vencido o de otro módulo), `401` sesión o API key inválidas, `403` autorización, origen, módulo o asignación deshabilitados, `404` recurso administrativo ausente. Errores de canje no entregan identidad. Fallos transitorios deben controlarse sin exponer excepciones ni secretos.

## Asignaciones particulares y masivas

Administrar desde un usuario o desde un módulo modifica las mismas filas. Sin asignación equivale a acceso denegado. Las actualizaciones son idempotentes y no borran usuarios.

Lotes usan `mode=selected` con lista explícita no vacía de UUID, o `mode=all` sin selección. Una lista vacía nunca significa todos. Validar todos los IDs antes de escribir y deduplicar selección. Activar inserta faltantes y reactiva inactivos; desactivar solo modifica existentes. Contar filas creadas, cambiadas y sin cambios. No modificar asignaciones de otros módulos.

Todos comprende una instantánea de usuarios existentes al ejecutar la operación; altas posteriores no reciben acceso automáticamente. El lote es transaccional, sin resultados parciales, y la PK impide duplicados concurrentes. La interfaz confirma operaciones globales y muestra sus resultados.

## Emisión y canje

La emisión comprueba sesión, existencia del usuario, módulo y asignación activos. El navegador solo recibe la URL registrada con código, sin API key ni identidad. La URL registrada no puede incluir usuario/contraseña, fragmentos ni parámetros `code`, `ticket` o `state` preexistentes.

El canje autentica la API key del módulo, comprueba vigencia, correspondencia, no uso y autorización actual. Consumo y lectura de identidad forman una transacción coherente con las desactivaciones concurrentes. Solo se entrega identidad después de confirmar transacción. Dos canjes simultáneos: exactamente uno obtiene datos. Respuesta perdida después del consumo exige nuevo lanzamiento; no se vuelve a entregar identidad.

Desactivar módulo o asignación bloquea canjes pendientes. Cerrar sesión central no revoca códigos ya emitidos antes de sus 60 segundos. Cambios de URL y clave requieren nuevo intento. Sesiones propias de módulos no se revocan mediante estas acciones.

## Correlación automática del navegador

El usuario aprobó este ajuste antes de la entrega. `launch` emite un ticket preliminar y devuelve la URL registrada con `ticket`, no con `code`. `auth_tokens.browser_confirmed` distingue tickets (`false`) de códigos canjeables (`true`); valor por defecto `false`.

Al recibir `ticket`, el módulo genera un `state` aleatorio URL-safe de 32 a 128 caracteres, lo guarda en una cookie propia de correlación y redirige el navegador a `GET /api/modules/authorize?ticket=...&state=...`. LoginControl exige sesión válida del mismo usuario del ticket, permisos actuales y ticket sin usar ni vencer. Consume el ticket y genera código confirmado en una transacción serializable. Devuelve `302` a la URL registrada con `code` y el mismo `state`. No acepta destinos del cliente.

El receptor compara `state` recibido con su cookie y elimina esa correlación antes de canjear. Sin coincidencia no hace canje ni crea sesión. El canje rechaza tickets preliminares. Un ticket de otra persona no se autoriza aunque sea válido. El usuario no ve otro login; sin sesión central, se rechaza para que vuelva a LoginControl.

Validar `state` con caracteres ASCII alfanuméricos, guion y guion bajo; límites 32..128. El receptor usa una cookie HttpOnly, Secure, SameSite=Lax con vigencia breve. No cargar recursos externos en recepción; usar Referrer-Policy: no-referrer en redirecciones y limpiar parámetros al terminar. Estos requisitos forman parte del contrato del receptor, que LoginControl no puede imponer sobre otro dominio.

## Aceptación

- Dashboard lista solo módulos activos asignados; llamada directa sin permiso no emite código.
- Solo superadmin vigente administra; cambio de tipo se refleja en la siguiente petición, aunque el JWT conserve otro tipo.
- Código correcto entrega exactamente identidad prevista; inválido, usado, vencido o cruzado no entrega datos.
- Canje concurrente consume una vez; desactivación concurrente tiene orden transaccional definido.
- Clave ausente, incorrecta o reemplazada se rechaza; secretos nunca aparecen en listados.
- Asignaciones por usuario, módulo y lote son consistentes; repetir no duplica filas.
- Alta posterior a asignar todos no recibe acceso; IDs desconocidos rechazan lote entero.
- Login, logout y vencimiento institucional conservan su comportamiento.
- Pruebas con PostgreSQL aislado y compilaciones de ambas áreas; build no sustituye comprobaciones funcionales.

Guía transferible: [module-integration.md](../module-integration.md). Plan: [module-access-plan.md](../plans/module-access-plan.md).
