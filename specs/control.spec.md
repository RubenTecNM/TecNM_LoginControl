# Panel de control local

El panel `control.sh` administra contenedores independientes de frontend y backend; PostgreSQL corre en el anfitrión. El backend usa `backend/.env` para su conexión y Docker resuelve `host.docker.internal` hacia el anfitrión. El servidor PostgreSQL debe aceptar conexiones desde Docker y autorizar al usuario configurado.

Crear o recrear pregunta siempre qué puerto se publicará en el anfitrión, ofreciendo el valor guardado como predeterminado (5173 frontend, 7001 backend). Enter acepta ese valor; una selección ocupada o inválida se vuelve a solicitar. El puerto se publica en todas las interfaces disponibles del anfitrión y se comprueba con direcciones comodín IPv4 e IPv6 antes de crear. El puerto seleccionado se guarda en el `.env` raíz y no modifica el puerto interno del contenedor. Los certificados locales de desarrollo se generan en `.docker/certs`. El estado comprueba HTTP del frontend y health del backend, la conexión PostgreSQL con las credenciales de `backend/.env` y el endpoint de conectividad del backend.

La carga de `database.sql` solicita conexión y contraseña de PostgreSQL sin guardarlas y aplica el archivo en la base indicada. La recreación elimina la base indicada y todos sus datos solo tras advertencia y confirmación explícita; después crea la base y aplica el SQL. No se ejecutan migraciones automáticamente. Los contenedores pueden reconstruirse por separado.

## Navegación del panel

- Menú principal: crear servicios, iniciar servicios, recrear servicios, cargar base de datos, recrear base de datos, detener contenedores, registros, actualizar estado y salir.
- Crear, iniciar y recrear abren un submenú con frontend, backend, ambos y volver. Ambos inicia primero backend y después frontend; si falla un paso se detiene la operación y se muestra el fallo.
- En una terminal, las opciones se ejecutan al presionar su tecla, sin Enter. Los campos de texto y las confirmaciones de borrado siguen requiriendo Enter. Con entrada redirigida, las opciones se leen por línea.
- El panel distingue encabezado, estados y grupos de acciones. Usa colores solo en una terminal compatible y respeta `NO_COLOR`; los estados incluyen texto legible sin color. Los submenús siempre permiten volver con `0`.
- Tras una operación se conserva la salida y se permite regresar con una tecla. Los errores permanecen visibles; actualizar estado vuelve a ejecutar las comprobaciones existentes.

## Verificación de navegación

`python3 scripts/check-control.py` prueba la selección y retorno, el orden de ambos servicios, los fallos y la lectura sin Enter con una pseudoterminal. Usa un proyecto temporal y acciones simuladas; no administra contenedores ni bases reales.

## Persistencia de puertos

El `.env` de la raíz guarda `TECNM_FRONTEND_PORT` y `TECNM_BACKEND_PORT`. El panel lo crea si falta y reutiliza los valores al iniciar, recrear y consultar estados. `.env.example` documenta los valores predeterminados. Los ajustes ajenos se conservan al guardar puertos; el archivo nunca se ejecuta como código. Si todavía no existe `.env`, se recuperan los puertos de `.docker/control.env` cuando esté presente. Las credenciales de la aplicación permanecen en `backend/.env`.

## Asistente de configuración del backend

Al crear servicios, si falta `backend/.env`, el panel ofrece crear el archivo con un asistente o configurarlo por cuenta propia y volver. La elección usa una tecla; los campos del asistente requieren Enter. El asistente explica que PostgreSQL está instalado en el anfitrión y que `host.docker.internal` permite acceder desde Docker a ese servidor local. Ofrece `localhost` y `5432` como valores predeterminados de servidor y puerto, aceptados con Enter; guarda `host.docker.internal` para que Docker acceda al anfitrión. Solicita dirección, puerto, base, usuario y contraseña de PostgreSQL de la aplicación, y origen HTTPS del frontend. La contraseña se oculta y no aparece en el resumen. Se conserva la entrada de contraseña sin imponer longitud mínima; PostgreSQL determina si autentica. Genera una clave de firma aleatoria y deja los proveedores pendientes con aviso explícito de que el login no estará disponible hasta configurarlos.

El archivo se crea únicamente al terminar y confirmar el guardado, con permisos `0600`, y no reemplaza archivos existentes sin la selección y confirmación del usuario. Cancelar o interrumpir antes de confirmar no crea un archivo parcial ni inicia o retira contenedores. Una vez guardado, continúa la acción elegida. El asistente no prueba conexiones ni crea usuarios o bases; guarda la configuración para Docker. Las credenciales administrativas para cargar SQL siguen siendo independientes.

Antes de crear, iniciar o recrear backend (también con “Ambos”), se usa `backend/.env`; el asistente se ofrece durante la creación y la recreación y después se intenta `SELECT 1=1` con sus credenciales. Una conexión fallida o un cliente `psql` ausente impide iniciar o retirar contenedores. Se informa que deben revisarse credenciales, servicio PostgreSQL y existencia de la base. No se crea ni se modifica la base de forma automática. La comprobación se realiza desde el anfitrión y no sustituye la comprobación backend → base del panel.

Al crear servicios, si `backend/.env` existe, se anuncia y se permite usarlo, iniciar el asistente para crear uno nuevo o cancelar. El reemplazo se confirma al guardar y es atómico; cancelar conserva el archivo anterior. Esta selección se realiza una sola vez por acción del panel, incluso al elegir ambos servicios.

## Carga y destino de SQL

“Cargar base de datos” y “Recrear base de datos” solicitan un nombre `snake_case` de hasta 63 caracteres y consultan su existencia en el PostgreSQL local con las credenciales administrativas introducidas para esa ejecución. Un fallo de conexión o consulta detiene la operación; no se interpreta como base inexistente. Se rechazan `postgres`, `template0` y `template1`.

- Si no existe, se informa y se pregunta si se desea crearla; sin confirmación no se crea ni se carga SQL.
- Si existe, se informa “Ya existe. ¿Deseas continuar? Cualquier dato contenido se perderá”. Se requiere confirmar y escribir el nombre exacto antes de eliminar y crear nuevamente esa base.
- Tras crear la base se aplica `database.sql` en una transacción. Si falla eliminar o crear, no se intenta cargar SQL. Si falla la carga, la nueva base queda vacía; el borrado anterior no puede revertirse.
- El archivo SQL y el cliente `psql` se verifican antes de operaciones sobre la base. No se detienen conexiones activas ni se modifican usuarios o permisos de aplicación automáticamente.

## Estados con contexto

El resumen distingue una comprobación fallida de una que no puede ejecutarse. PostgreSQL muestra `SIN CONFIGURAR · Falta backend/.env` cuando falta el archivo, `SIN CONFIGURAR · Revisa la cadena de conexión` cuando faltan valores necesarios, y `SIN COMPROBAR` si falta `psql` o no se puede leer el archivo. Solo muestra fallo de conexión después de intentarlo con configuración válida. No infiere que una base no existe a partir de un fallo ni muestra credenciales.

API / PostgreSQL muestra `SIN COMPROBAR` si falta configuración o si el backend no responde; solo solicita su endpoint de base cuando el backend responde y hay configuración. Las comprobaciones HTTP indican si falta `curl`. La creación previa de una base no prueba la conexión con el usuario de aplicación del backend.

## Nombres de contenedores

Al crear o repetir la creación desde Recrear se solicita el nombre de cada servicio seleccionado con el valor guardado como predeterminado. Se guardan `TECNM_FRONTEND_CONTAINER_NAME` y `TECNM_BACKEND_CONTAINER_NAME` en el `.env` raíz, junto a los puertos; las credenciales siguen en `backend/.env`. Los nombres predeterminados son `TecNM_LoginControl_Frontend` y `TecNM_LoginControl_Backend`.

Los nombres deben ser válidos para Docker, distintos entre ambos servicios y no estar ocupados por un contenedor ajeno al servicio. Ante un conflicto se solicita otro. Iniciar y consultar estados consumen los nombres registrados en el `.env` raíz y no solicitan nombres. Iniciar arranca el contenedor existente, sin construir ni crearlo; si falta, indica usar Crear servicios. Recrear vuelve al flujo completo de creación: prepara configuración y nombres, elimina los contenedores seleccionados y construye contenedores nuevos. Los nombres registrados se ofrecen como predeterminados. Se comprueba que el contenedor anterior se haya eliminado antes de continuar; un fallo de eliminación impide la creación. Cancelar la preparación conserva los contenedores. El estado inspecciona el contenedor por su nombre y distingue sin crear, detenido y en ejecución antes de verificar HTTP. Compose usa los nombres guardados; el proxy conserva el nombre interno del servicio `backend`.

## Confirmación de recreación

Antes de abrir los asistentes o modificar configuración para recrear, el panel muestra los nombres registrados de los contenedores seleccionados y pregunta `¿Estás de acuerdo? [s/N]`. Con ambos servicios muestra los dos nombres. Solo `s` o `S` confirma; cualquier otra respuesta o fin de entrada cancela sin ejecutar asistentes, borrar, crear ni guardar configuración. La selección respeta la lectura de una tecla del panel.

Un contenedor en ejecución con un health que devuelve HTTP 500 se muestra como `ERROR HTTP` con su código; “no responde” se reserva para fallos de conexión o timeout. API / PostgreSQL indica el error de health que impide comprobarla.

## Casos de uso y resultados esperados

Esta tabla resume el comportamiento acordado. Las comprobaciones se realizan sin exponer secretos; los casos de creación y eliminación de datos se ensayan con recursos aislados.

| Caso | Acción | Resultado esperado |
| --- | --- | --- |
| Se abre el panel | Ejecutar `bash control.sh` | Se muestran los estados y las acciones disponibles; en terminal se seleccionan opciones con una tecla. |
| Falta el `.env` raíz | Abrir el panel | Se crea con valores predeterminados o se recuperan los puertos anteriores disponibles. |
| Se actualiza el estado | Elegir `R` | Se vuelven a consultar los estados con los nombres y puertos registrados. |
| Se elige una opción inválida | Presionar una tecla no reconocida | Se informa el error y se puede volver a elegir. |
| Se selecciona un submenú | Elegir crear, iniciar o recrear servicios | Se puede elegir frontend, backend, ambos o volver; con ambos se procesa primero backend. |
| Se crean servicios sin `backend/.env` | Elegir crear frontend, backend o ambos | Se ofrece el asistente o configurar el archivo por cuenta propia y volver. |
| Ya existe `backend/.env` | Iniciar el flujo de creación o recreación | Se ofrece usarlo, crear uno nuevo o cancelar; cancelar conserva el archivo existente. |
| Se completa el asistente | Capturar datos y confirmar | Se guarda `backend/.env` con permisos privados y se comprueba PostgreSQL antes de crear backend. Las credenciales de proveedores pendientes dejan el login no disponible. |
| La conexión previa de backend falla | Intentar crear, iniciar o recrear backend | Se informa el fallo y se detiene la operación antes de retirar o iniciar contenedores. |
| Se elige un nombre de contenedor | Crear o recrear un servicio | Se propone el nombre guardado; uno inválido, duplicado u ocupado por otro contenedor se vuelve a solicitar. |
| Se selecciona un puerto publicado | Crear o recrear un servicio | Se pregunta el puerto del anfitrión, con el guardado como predeterminado; Enter lo acepta y un puerto inválido u ocupado se vuelve a solicitar. |
| Se confirma nombre y puerto | Completar la creación | Las selecciones se guardan en el `.env` raíz y Compose las usa para crear el contenedor. |
| El servicio ya está creado | Elegir iniciar | Se utiliza el nombre guardado y se inicia el contenedor detenido sin pedir un nombre nuevo. |
| Se intenta iniciar un contenedor inexistente | Elegir iniciar | Se informa que no existe y se indica usar Crear servicios. |
| Se intenta iniciar un contenedor activo | Elegir iniciar | Se informa que ya está activo. |
| Se solicita recrear | Elegir frontend, backend o ambos | Se muestran los nombres que se eliminarán y se solicita confirmación antes de abrir asistentes o modificar configuración. |
| Se cancela la recreación | Responder distinto de `s`/`S` o terminar la entrada | No se ejecutan asistentes ni se eliminan o crean contenedores. |
| Se confirma la recreación | Confirmar y completar la preparación | Se eliminan los contenedores seleccionados y se crean de nuevo; si la eliminación falla, no se continúa con la creación. |
| Se detienen los servicios | Elegir detener contenedores | Se detienen los servicios administrados por Compose. |
| Se consultan registros | Elegir ver registros | Se muestran registros en tiempo real; `Ctrl+C` permite volver al panel. |
| Se solicita cargar o recrear la base | Capturar credenciales y nombre de destino | Se consulta su existencia en PostgreSQL; las credenciales se usan solo durante la operación. |
| La base destino no existe | Confirmar su creación | Se crea y después se aplica `database.sql`; al cancelar no se modifica. |
| La base destino existe | Confirmar pérdida de datos y escribir su nombre exacto | Se elimina, se crea de nuevo y se carga el SQL; sin ambas confirmaciones no se modifica. |
| Falla la consulta de existencia | Intentar cargar o recrear la base | Se informa el fallo y se detiene la operación sin asumir que la base falta. |
| Falla la carga después de reemplazar la base | Aplicar `database.sql` | Se informa el error; la carga transaccional se revierte, pero el borrado anterior no se recupera. |
| Falta `backend/.env` | Consultar estados | PostgreSQL indica `SIN CONFIGURAR` y explica que falta el archivo; API / PostgreSQL queda `SIN COMPROBAR`. |
| Un contenedor no existe o está detenido | Consultar estados | Se muestra `SIN CREAR` o `DETENIDO` para el nombre registrado, respectivamente. |
| Un contenedor está ejecutándose | Consultar estados | Se verifica su respuesta HTTP; `200` muestra `ACTIVO`, un error HTTP muestra su código y una conexión fallida indica que no responde. |
| Backend aún no responde | Consultar estados | API / PostgreSQL queda `SIN COMPROBAR`; no se infiere que PostgreSQL esté caído. |
| Backend y configuración están disponibles | Consultar API / PostgreSQL | Se solicita `/api/health/database` y se informa si la conexión desde la API funciona. |
| Se sale del panel | Elegir `0` | El panel termina. |

| Un puerto está ocupado en cualquier interfaz del anfitrión | Intentar publicarlo | Se informa el conflicto y se solicita otro, aunque la ocupación no sea en localhost. |
