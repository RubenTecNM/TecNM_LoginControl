# Plan de implementación: acceso a módulos externos

Estado: implementado y verificado en recursos aislados; revisión visual y prueba con un receptor real pendientes. Ver seguimiento ODD. Actualizado el 4 de noviembre de 2026.

## Objetivo y alcance autorizado

LoginControl será el único punto de inicio de sesión. Una persona autenticada seleccionará una aplicación externa, por ejemplo Servicio Social, y su backend iniciará una sesión propia después de canjear un código temporal con LoginControl. Los módulos no tendrán formulario de login ni credenciales de usuarios.

Esta entrega comprende catálogo de módulos, asignaciones entre usuarios y módulos, dashboard de accesos, administración exclusiva de `superadmin`, canje de códigos y una guía para los equipos externos. Se prepara el plan; este documento no autoriza ejecutar migraciones, modificar bases reales ni desplegar.

Quedan fuera de alcance el autorregistro y la edición de usuarios, permisos internos de las aplicaciones externas, sesiones compartidas entre dominios, logout global y construcción de aplicaciones de los compañeros. La administración de asignaciones sí incluye consultar usuarios existentes.

## Decisiones confirmadas

- LoginControl conserva los usuarios y entrega identidad solamente en la respuesta entre backends.
- Cada módulo es una aplicación independiente, en otro dominio o ruta pública HTTPS.
- Un usuario puede tener varios módulos y un módulo puede tener varios usuarios.
- Solo `superadmin` registra y configura módulos y administra asignaciones. `admin`, `docente` y `default` no administran.
- Los permisos se gestionan desde dos perspectivas: módulos de un usuario y usuarios de un módulo.
- Se permite seleccionar varios usuarios, agregar a todos los usuarios existentes y activar o desactivar asignaciones por lote.
- “Todos” significa usuarios existentes al ejecutar la operación. No crea una regla para usuarios futuros.
- La asignación permite intentar el acceso. Los permisos de negocio pertenecen al módulo externo.
- Cada módulo usa una clave de integración independiente de la firma JWT de LoginControl.
- El módulo devuelve el código temporal al canjearlo. `userId + moduleId` no sustituyen esa comprobación.
- Deshabilitar una asignación o módulo impide nuevos accesos y canjes pendientes; no cierra sesiones externas ya emitidas.

## Estado previo comprobado al planear

- [El contrato de sesión](../specs/session-control.spec.md) cubre Google/Microsoft, bienvenida y logout. Todavía excluye integración externa y permisos por tipo.
- [El SQL](../database.sql) contiene `users` y `user_types`. Los usuarios tienen UUID, nombres, apellidos y hasta dos correos institucionales.
- [SessionService](../backend/Modules/Session/SessionService.cs) valida la cookie y devuelve el usuario y tipo vigentes desde PostgreSQL. El tipo contenido en el JWT puede estar desactualizado.
- [Program.cs](../backend/Program.cs) no registra políticas de autorización; no bastaría agregar un atributo de autorización a los nuevos controllers.
- [La vista de sesión](../frontend/src/modules/session/views/SessionView.vue) reúne login y bienvenida. No existe un dashboard de módulos ni un panel administrativo.
- Hay componentes institucionales reutilizables y [pruebas con PostgreSQL aislado](../scripts/test-session.sh). No existe un ejecutor de pruebas frontend configurado.
- [El diagrama HTML](../module-access-flow.html) representa el intercambio propuesto; no demuestra funcionalidad implementada.

## Modelo de datos propuesto

Conservar `users` y `user_types`. Agregar tres tablas y sus mapeos EF Core, siguiendo `snake_case` en PostgreSQL.

| Tabla | Columnas principales | Reglas |
| --- | --- | --- |
| `modules` | `id uuid`, `name varchar(150)`, `entry_url text`, `api_key_hash bytea`, `is_active boolean` | PK `id`; nombre no vacío; URL HTTPS absoluta validada por backend; secreto aleatorio de integración almacenado como hash SHA-256. |
| `user_modules` | `user_id uuid`, `module_id uuid`, `is_active boolean` | PK compuesta; FK a usuarios y módulos; sin fila o con fila inactiva significa sin acceso. |
| `auth_tokens` | `id uuid`, `user_id uuid`, `module_id uuid`, `token_hash bytea`, `created_at timestamptz`, `expires_at timestamptz`, `used_at timestamptz NULL`, `browser_confirmed boolean` | PK `id`; hash único; FK a usuario y módulo; vencimiento posterior a creación; consumo condicional de una sola vez. |

Agregar índice por `module_id` en `user_modules` para consultar usuarios de un módulo. No imponer unicidad al nombre como identificador: la integración usa el UUID.

Propuesta técnica: código y API key generados con 32 bytes de aleatoriedad criptográfica y codificados para transporte. Código con vigencia inicial de 60 segundos, sin JWT ni información personal. Mostrar la API key una sola vez al crearla o reemplazarla; los listados nunca la devuelven. Un reemplazo invalida la clave anterior.

Las asignaciones usan activación y desactivación, sin borrado físico en las pantallas. Para las nuevas FK, proponer borrado restringido y evitar cascadas sobre datos existentes. Documentar una eliminación controlada si posteriormente se necesita borrar usuarios.

Actualizar `database.sql` para instalaciones nuevas y preparar un script independiente en `scripts/migrations/` para bases existentes. No ejecutar el script como parte de este trabajo de planeación ni asumir que reconstruir contenedores lo aplicará.

## Contrato HTTP inicial (ver ajuste de correlación al cierre)

Todas las rutas siguientes son propuestas. Separar cookies del usuario y credenciales del módulo. Las respuestas con códigos, claves o identidad deben usar `Cache-Control: no-store`.

### Dashboard y handshake

| Método y ruta | Acceso | Resultado |
| --- | --- | --- |
| `GET /api/modules` | Cookie válida | Lista únicamente módulos activos con asignación activa del usuario vigente. Sin claves. |
| `POST /api/modules/{id}/launch` | Cookie válida y origen exacto permitido | Verifica sesión y permisos, registra un intento y devuelve la URL registrada con ticket preliminar. |
| `POST /api/modules/exchange` | API key del módulo en `Authorization: Bearer ...` | Recibe `{ moduleId, code }`; valida y consume el código; devuelve identidad local. No usa la cookie de un usuario como credencial del módulo. |

La emisión obtiene el ID de usuario de la sesión validada. El frontend no escoge un `userId` ni una URL de redirección arbitraria. La API key nunca sale hacia el navegador. La vuelta entre backends no necesita ampliar CORS para los dominios de los módulos.

La respuesta de canje tendrá `user.id`, `firstNames`, `paternalLastName`, `maternalLastName`, `googleEmail` y `microsoftEmail`. Mantener correos y apellido materno nulables conforme al registro. No entregar identificadores del proveedor, JWT central ni tipo de usuario como permiso externo.

Definir en la spec respuestas para entrada inválida (`400`), sesión o clave inválida (`401`), operación administrativa no permitida (`403`) y recurso ausente (`404`). Los rechazos de código deben ser genéricos y no revelar usuario, vencimiento o uso previo a clientes no autenticados.

### Administración exclusiva de superadmin

| Método y ruta | Resultado propuesto |
| --- | --- |
| `GET /api/admin/modules` | Consulta catálogo, incluidos módulos inactivos. |
| `POST /api/admin/modules` | Registra módulo y entrega su clave una sola vez. |
| `PATCH /api/admin/modules/{id}` | Cambia nombre, URL registrada o estado. |
| `POST /api/admin/modules/{id}/key` | Reemplaza la clave y muestra la nueva una sola vez. |
| `GET /api/admin/users?search=...` | Busca usuarios existentes para administrar asignaciones. |
| `GET /api/admin/users/{userId}/modules` | Consulta módulos asignados y disponibles de una persona. |
| `PUT /api/admin/users/{userId}/modules/{moduleId}` | Crea o actualiza una asignación con `{ isActive }`. |
| `GET /api/admin/modules/{moduleId}/users` | Consulta usuarios y estado de asignación desde un módulo. |
| `PUT /api/admin/modules/{moduleId}/users` | Aplica activación o desactivación a usuarios seleccionados o a todos los existentes. |

La operación masiva recibirá un modo explícito (`selected` o `all`), `userIds` solo en modo `selected` e `isActive`. No interpretar una lista vacía como “todos”. Para activar, insertar faltantes y reactivar existentes; para desactivar, modificar únicamente asignaciones existentes. No alterar asignaciones de otros módulos.

Validar toda la selección antes de escribir; IDs desconocidos rechazan el lote completo. Eliminar IDs repetidos. Aplicar una transacción y responder cantidades de filas creadas, cambiadas y sin cambios. Repetir una operación no crea duplicados. Resolver conflictos concurrentes usando la PK compuesta y transacciones EF, sin SQL embebido.

“Todos” toma una instantánea de usuarios dentro de la operación. Un alta concurrente posterior no recibe acceso automático. La interfaz debe nombrar el alcance y pedir confirmación antes de aplicar una acción a todos los usuarios. Esa confirmación pertenece a la aplicación terminada, no es una autorización para ejecutar este plan.

## Secuencia de implementación

### 1. Cerrar contrato y actualizar specs

- Crear `specs/module-access.spec.md` como contrato compartido y specs locales en backend y frontend para detalles de cada área.
- Actualizar `specs/template.spec.md` y `specs/session-control.spec.md`: incorporar integración y autorización de `superadmin` sin presentar propuestas como implementadas.
- Precisar JSON, errores, nulabilidad, selección masiva, autorización, vencimiento y comportamiento de reintentos.
- Implementar la correlación automática aprobada descrita al final del plan para evitar login CSRF y sustitución de códigos. El código de un solo uso no resuelve por sí solo esa correlación. La solución debe conservar el único login central; una ruta técnica de preparación del módulo no sería un formulario de login. Este punto debe quedar resuelto antes de cerrar el contrato del handshake.
- Determinar si cerrar la sesión central invalida intentos emitidos. Propuesta mínima: los códigos ya emitidos conservan sus 60 segundos, salvo que se desactive el módulo o la asignación. No prometer revocación inexistente.
- Ajustar el diagrama al contrato cerrado y escribir ejemplos saneados para el equipo externo.

**Salida:** contrato revisable que distingue login existente, permisos centrales y sesión externa.

### 2. Agregar persistencia

- Crear entidades y configuraciones de `Module`, `UserModule` y `AuthToken` en `backend/Data/`; aprovechar el descubrimiento de configuraciones existente.
- Actualizar esquema inicial y preparar migración aditiva, sin cambiar ni volver a insertar usuarios o tipos existentes.
- Probar esquema nuevo y migración en PostgreSQL temporal, incluidos índices, FK, hashes únicos y PK de asignación.

**Salida:** SQL y EF coinciden; migración conserva usuarios y relaciones existentes.

### 3. Implementar autorización y administración backend

- Crear la slice `backend/Modules/Modules/` con controllers delgados, servicio y DTOs. Registrar servicio en `Program.cs`.
- Reutilizar validación de sesión y origen exacto de `SessionService`. Verificar `superadmin` contra el tipo actual de la base en cada operación administrativa, incluidos listados y reemplazo de claves.
- Aplicar protección CSRF en operaciones administrativas y lanzamiento mediante cookie. No confiar en ocultar botones o en el tipo guardado en un JWT anterior.
- Implementar catálogo, claves, búsqueda de usuarios y asignaciones desde ambos contextos, con transacciones para lotes e idempotencia de actualizaciones.
- Mantener esta autorización acotada; no introducir un sistema genérico de roles o permisos ni un Repository.

**Salida:** un usuario que deje de ser `superadmin` pierde la administración en su siguiente petición, aunque conserve la cookie.

### 4. Implementar emisión y canje

- Listar accesos vigentes del usuario autenticado y validar nuevamente al emitir el código.
- Generar y guardar hash del código; construir destino exclusivamente desde la URL registrada.
- Autenticar la API key de forma segura y comprobar que el código corresponde a ese mismo módulo.
- Ejecutar validación de permisos, consumo condicional y lectura de identidad dentro de una transacción coherente. Evaluar aislamiento serializable para coordinar desactivaciones concurrentes y consumo; manejar conflictos sin entregar identidad antes del commit.
- Consumir con `ExecuteUpdateAsync` condicional y exigir una fila afectada. Implementar con LINQ/EF, sin `raw SQL`.
- No aceptar códigos usados, vencidos ni de otro módulo. Si dos peticiones canjean el mismo código, una sola entrega identidad.
- Si la respuesta se pierde después del consumo, el módulo no reutiliza el código: se requiere un nuevo lanzamiento desde LoginControl.
- No registrar claves, códigos ni respuestas personales. Documentar limpieza de query string, `Referrer-Policy: no-referrer` y ausencia de recursos de terceros en la página externa de recepción.

**Salida:** la identidad solo se entrega al backend autenticado del módulo y después de consumir un intento válido.

### 5. Agregar dashboard y pantallas administrativas

- Crear slice `frontend/src/modules/modules/` con cliente sobre Axios compartido, tipos, componentes y rutas cuando hagan falta.
- Integrar la lista de accesos en la bienvenida autenticada actual. Mantener login, logout, vencimiento y consulta al regresar a la pestaña.
- Renderizar módulos activos asignados y estado vacío. Al seleccionar, bloquear doble clic mientras se emite el intento y navegar al destino recibido.
- Incluir administración solo para `superadmin`: catálogo de módulos, formulario de registro/edición, visualización única de clave y reemplazo explícito.
- Incluir vista por usuario para habilitar o deshabilitar sus módulos.
- Incluir vista por módulo con búsqueda, selección múltiple, activación/desactivación de seleccionados y acción explícita para todos los usuarios existentes. Mostrar resultado del lote y conservar estado ante errores.
- Reutilizar `UiBasePage`, `UiCard`, `UiButton`, `UiTable`, `UiField`, `UiToggle`, `UiDialog`, `UiAlert` y `UiToast`. Mantener español de México, teclado, foco y temas claro/oscuro desde 375 px.
- Tratar `401` como sesión inválida y `403` como permiso perdido. La seguridad depende del backend.

**Salida:** navegación central y administración desde ambas perspectivas, sin formularios de login externos.

### 6. Verificar integración y documentar entrega

- Extender el proyecto de pruebas existente con casos de módulos y asignaciones. Reutilizar PostgreSQL aislado; comprobar concurrencia con contextos independientes.
- Adaptar `scripts/test-session.sh` y agregar comprobaciones del esquema cuando sea necesario. Las nuevas pruebas deben ejecutarse realmente desde el script, no quedar fuera del proyecto.
- Crear guía en `docs/` para equipos externos: registro del módulo, almacenamiento de clave, recepción del código, correlación del navegador, canje, cookies de sesión propias, errores y limpieza de URL.
- Simular un backend externo en pruebas; no implementar el sistema de Servicio Social ni depender de su disponibilidad para comprobar el contrato.
- Actualizar README, setup, referencias de arquitectura afectadas y diagrama. No cambiar Compose salvo que el contrato cerrado requiera configuración adicional justificada.

**Salida:** evidencia reproducible y guía suficiente para que un compañero implemente su receptor sin otro login.

## Matriz de aceptación

| Caso | Resultado verificable |
| --- | --- |
| Usuario con asignación activa | Ve el módulo y puede emitir un intento. |
| Usuario sin asignación o módulo inactivo | No ve el acceso; llamada directa no emite código. |
| Usuario suplanta `userId` o destino | Backend usa usuario de sesión y URL registrada. |
| Canje correcto | Devuelve solo identidad prevista; almacena `used_at`. |
| Código repetido, vencido, inexistente o de otro módulo | No devuelve identidad. |
| Dos canjes simultáneos | Exactamente uno obtiene identidad. |
| Asignación o módulo desactivados antes del canje | Se rechaza el intento aunque el código no haya vencido. |
| Carrera entre desactivación y canje | Orden transaccional definido y comprobado; no validar permisos en una consulta desconectada del consumo. |
| Clave inválida, ausente o reemplazada | Se rechaza sin exponer datos de usuario. |
| Tipo `admin`, `docente` o `default` | No puede administrar ni con llamadas HTTP directas. |
| Superadmin cambia de tipo durante sesión | Siguiente petición administrativa rechazada. |
| Administración por usuario y por módulo | Ambas vistas muestran y actualizan la misma asignación. |
| Activación masiva repetida | Sin filas duplicadas; conserva permisos de otros módulos. |
| Lista vacía o IDs desconocidos | No se convierte en operación global ni produce un lote parcial. |
| Activar a todos y registrar usuario después | El nuevo usuario no recibe la asignación anterior. |
| Desactivar seleccionados o todos | Impide futuros ingresos de los afectados; sesiones externas existentes no se revocan. |
| Reenvío de URL de acceso a otro navegador | La correlación definida impide iniciar una sesión con identidad del atacante. |
| Sesión central vencida o petición con origen inválido | No se emite código ni se modifica administración. |
| Secreto o código en listados, URL equivocada, logs o caché | Verificación falla; no se acepta la entrega. |
| Cambio de módulos | Login institucional y logout existentes conservan su comportamiento. |

## Verificaciones previstas

Estos comandos corresponden a la implementación futura; no se ejecutan para validar este plan documental:

```bash
dotnet build backend/TecNM.Api.csproj
bash scripts/test-session.sh
npm --prefix frontend run build
(cd frontend && node scripts/check-layout.mjs)
git diff --check
```

El build no sustituye pruebas HTTP. Agregar comprobaciones de rutas, cookies, CSRF, errores y canje con cliente externo simulado. Validar UI mediante recorridos de usuario en 375 px y escritorio, claro/oscuro y teclado. No añadir un framework frontend solo para este alcance si las comprobaciones existentes y los recorridos bastan.

La carga de SQL y la migración se ensayan exclusivamente en bases temporales. La aplicación en una base real queda fuera de las verificaciones rutinarias.

## Ajustes cerrados durante implementación

- Aprobado por el usuario: correlación automática sin otro formulario de login. El lanzamiento entrega un ticket preliminar. El receptor crea cookie/state y redirige el navegador a `/api/modules/authorize`. LoginControl exige sesión del mismo usuario, consume ticket y devuelve código confirmado y state. El receptor compara cookie/state antes del canje.
- Tickets y códigos duran 60 segundos cada uno. Códigos canjeados no se repiten aunque la respuesta se pierda.
- Logout central no revoca intentos ya emitidos; desactivar permisos sí impide su canje. Cambiar URL o clave invalida intentos pendientes.
- Este apartado y la spec compartida sustituyen los puntos abiertos de correlación del plan inicial. El detalle de HTTP vigente está en la guía transferible.

Seguimiento de tareas y evidencia: [odd/tasks/module-access.md](../odd/tasks/module-access.md).
