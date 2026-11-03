# Panel de control local

## Limpieza conjunta

La opción 8 consulta el destino PostgreSQL y exige escribir `ACEPTO` y el nombre exacto de la base antes de retirar los contenedores de Compose y eliminar la base. Se conservan imágenes y archivos de configuración; un fallo posterior al retiro de contenedores se informa como limpieza incompleta. La conexión y la eliminación de PostgreSQL se comparten con el script de carga mediante `database-common.sh`.

`check-cleanup.py` cubre confirmaciones, cancelación, base ausente, protección de bases de sistema y fallos con recursos simulados. Una prueba con dos contenedores y una base PostgreSQL temporales verificó que cancelar conserva los recursos y confirmar los elimina.

## Revisión de opciones y separación de puertos

Se separó la selección y validación de puertos de la creación. En recreación se solicita antes del borrado, se admite el puerto del contenedor anterior y se comprueba otra vez después de retirarlo. La cancelación durante la selección conserva los contenedores. La disponibilidad puede cambiar entre ambas comprobaciones; si ocurre, se detiene la creación.

Las pruebas automatizadas cubren navegación, configuración, nombres, puertos, recreación y operaciones SQL con dependencias simuladas. Se verificaron además creación, cancelación y reemplazo de una base PostgreSQL temporal real. El ciclo de Docker se ensayó en un proyecto Compose temporal independiente con una imagen ligera, no con las imágenes de la aplicación.

## Objetivo y alcance autorizado

Administrar frontend y backend en Docker y PostgreSQL en el anfitrión mediante un panel interactivo, con verificaciones y confirmación para recrear la base.

## Requisitos y referencias

[Contrato del panel](../../specs/control.spec.md), [health](../../specs/health.spec.md) y [setup](../../docs/setup.md).

## Decisiones

El panel usa el Compose principal para las aplicaciones; la conexión del backend se configura en `backend/.env`. La recreación de base requiere dos confirmaciones y credenciales introducidas al momento.

## Verificación y evidencia

Aprobados `dotnet build backend/TecNM.Api.csproj --no-restore`, sintaxis Bash, compilación Python, `git diff --check`, apertura y salida del panel, y `docker compose config` con un archivo de ejemplo aislado; el Compose declara solo backend y frontend. No se probaron contenedores del panel ni una conexión PostgreSQL real: falta `backend/.env`. Las operaciones destructivas y bases existentes no se ejecutan como prueba rutinaria.

## Mejora de navegación

Se agruparon inicio y recreación en submenús con frontend, backend, ambos y volver. La lectura en terminal usa una tecla sin Enter. Se añadieron grupos visuales, estados con etiquetas y colores opcionales, y resultado visible tras cada acción. La opción ambos inicia backend antes de frontend y se detiene ante un fallo.

Verificados `python3 scripts/check-control.py` (acciones simuladas y pseudoterminal sin Enter), sintaxis Bash y `git diff --check`. No se ejecutaron acciones sobre contenedores o bases reales para esta mejora.

## Persistencia de puertos en la raíz

Los puertos ahora se guardan en `.env` y se documentan en `.env.example`. Se recupera `.docker/control.env` si todavía falta el archivo raíz. Se conservan las demás variables y comentarios, y no se ejecuta el contenido del archivo. `check-control.py` verifica persistencia entre procesos, recuperación de la configuración anterior, conservación de ajustes, permisos y rechazo de valores inválidos.

## Asistente y conexión previa

Antes de iniciar o recrear servicios se permite crear `backend/.env`, usar el existente o reemplazarlo después de confirmar. Los secretos se ocultan y el archivo se guarda con permisos privados. Para backend se comprueba la conexión PostgreSQL antes de cualquier operación de contenedores. Una conexión fallida impide recrear; cancelar conserva la configuración anterior. Los proveedores de sesión quedan pendientes de configuración explícita.

`check-control.py` cubre creación, cancelación, reemplazo, conservación del archivo, caracteres especiales, lectura de credenciales y bloqueo de recreación ante conexión fallida con acciones simuladas. No se ejecutaron operaciones sobre contenedores o bases reales.

## Carga con selección de destino

La carga consulta si existe la base indicada. Si falta, ofrece crearla; si existe, avisa de la pérdida de datos y exige confirmar y escribir el nombre antes de reemplazarla. Consulta fallida, cancelación o fallo al crear/eliminar detienen la carga. Se protegen las bases de sistema y se valida el nombre snake_case.

Aprobados `python3 scripts/check-database.py` (psql simulado), `python3 scripts/check-control.py`, sintaxis Bash y `git diff --check`. Las pruebas verifican ambas rutas, cancelación, fallos y rechazo de nombres protegidos. Además se ejecutó `database.sh` con `database.sql` real contra PostgreSQL temporal aislado en Docker: creación, cancelación que conserva datos y reemplazo confirmado aprobados. El contenedor temporal se eliminó al finalizar; no se modificaron bases preexistentes.

## Estados con contexto de configuración

Se reprodujo que, sin `backend/.env`, el resumen mostraba PostgreSQL como no disponible sin indicar el motivo. El comprobador ahora distingue archivo ausente, conexión incompleta, cliente ausente, archivo ilegible y fallo real de conexión. El resumen muestra la causa y omite la petición API/base mientras falte configuración o el backend no responda.

`python3 scripts/check-control.py` verifica la regresión con recursos simulados, además de conexión fallida y API disponible/no disponible; se aprobaron sintaxis Bash, compilación Python y `git diff --check`. No se realizaron comprobaciones sobre bases reales para este ajuste.

## Nombres configurables de contenedores

El panel solicita el nombre de cada servicio antes de crear o recrear. Guarda nombres y puertos en el `.env` raíz; Compose y la detección del contenedor activo usan esos nombres. La configuración de backend permanece separada. Se validan nombres inválidos, duplicados y ocupados por otros contenedores, y se completan las selecciones antes de retirar servicios. El helper de configuración se renombró de `ports.py` a `settings.py` para reflejar el alcance actual.

Verificación: pruebas de nombres y persistencia con Docker simulado, navegación, carga SQL simulada, configuración Compose aislada, sintaxis Bash y compilación Python. No se crean contenedores reales para probar este cambio.

## Separación entre crear e iniciar

Se agregó Crear servicios como acción independiente. Solo la creación ofrece configuración y selección de nombres. Iniciar consume los nombres del `.env` raíz y arranca el contenedor existente, sin construir ni preguntar datos. Recrear conserva los nombres. El estado inspecciona esos nombres antes de verificar HTTP y diferencia sin crear, detenido y en ejecución.

Verificados navegación sin Enter, creación, inicio sin preguntas, recreación sin selección de nombres, nombres personalizados y estados con Docker simulado; sintaxis Bash, compilación Python y diff sin errores de formato. No se operaron contenedores reales para este ajuste.

## Recreación completa

Recrear vuelve al flujo completo de creación, con configuración y selección de nombres antes de eliminar. Elimina los servicios de Compose y cualquier contenedor que persista con el nombre registrado; verifica que el anterior ya no exista antes de construir. Crear ya no devuelve éxito si permanece un contenedor activo. La preparación se completa antes del borrado para conservar los servicios si se cancela.

Aprobados `check-control.py`, `check-containers.py`, `check-recreate.py`, sintaxis Bash y `git diff --check`. La regresión cubre contenedores en ejecución que no reconoce Compose, frontend/backend/ambos, eliminación fallida y eliminación que reporta éxito pero conserva el contenedor. Se usa Docker simulado; no se modificaron contenedores reales.

## Confirmación antes de recrear contenedores

Se muestran los nombres registrados de los contenedores seleccionados y se pide confirmación explícita (`s/N`) antes de ejecutar asistentes o modificar configuración. Cancelar conserva configuración y contenedores. Las pruebas de recreación verifican frontend, backend y ambos, confirmación con `s`/`S`, rechazo, entrada vacía y respuesta inválida con Docker simulado.

## Puerto publicado y health con proveedores pendientes

Crear y recrear ahora solicitan siempre el puerto publicado, ofreciendo el guardado como predeterminado. Se verifican valores inválidos y ocupados, se permite aceptar el valor con Enter y se persiste la selección en `.env` raíz; iniciar no pregunta puertos.

Se reprodujo HTTP 500 al solicitar health con ClientId vacío mediante una prueba HTTP. Se evita registrar proveedores OIDC sin ClientId/ClientSecret válidos; el health sigue en 200 y el login pendiente devuelve 503. El panel distingue ERROR HTTP con código de una falta de respuesta y muestra la causa cuando no comprueba API/base.

Verificados `check-control.py`, `check-recreate.py` (puerto elegido, valor predeterminado, ocupado/inválido y cancelación), `check-containers.py`, `check-database.py`, build backend y sintaxis Bash. `bash scripts/test-session.sh` aprobó 5 casos, incluidos tres casos HTTP con proveedores ausentes/vacíos/espacios, usando PostgreSQL aislado y Data Protection efímera. La prueba HTTP de credenciales vacías fallaba antes del cambio y pasa después. El contenedor actual del usuario no se recreó: todavía responde HTTP 500 con la imagen anterior y requiere recreación desde el panel para aplicar la corrección.

## Puertos publicados en las interfaces del anfitrión

Compose publica frontend y backend en las interfaces del servidor; PostgreSQL continúa en el anfitrión. La validación de puertos comprueba direcciones comodín IPv4 e IPv6, por lo que rechaza ocupaciones en direcciones diferentes de localhost. La configuración CORS no se modifica.

Verificados `check-recreate.py` con Docker simulado y puertos realmente ocupados en otra dirección IPv4 y en IPv6 cuando está disponible, sintaxis Bash, `docker compose config` con configuración aislada y `git diff --check`. No se recrearon contenedores existentes ni se comprobó el acceso desde otro equipo.
