# Setup de desarrollo

## Panel con PostgreSQL en el anfitrión

Ejecutar `bash control.sh` desde la raíz. Instalar Docker Compose, Python 3, OpenSSL, curl y `psql`. Al crear servicios o repetir su creación desde Recrear, el panel permite crear `backend/.env` con un asistente. Si el archivo existe, permite usarlo, crear uno nuevo o cancelar. También puede configurarse manualmente a partir de `backend/.env.example` con `Host=host.docker.internal`, base, usuario y contraseña de aplicación. PostgreSQL debe escuchar en una interfaz accesible desde Docker y permitir esa conexión en `pg_hba.conf`; `localhost` dentro del contenedor apunta al contenedor mismo. Ajustar además la configuración de sesión del backend.

El panel publica los puertos en las interfaces del servidor para que sean accesibles desde otros equipos cuando la red y el firewall lo permitan. La comprobación rechaza puertos ocupados en cualquier interfaz IPv4 o IPv6. El certificado local generado solo identifica `localhost`; el acceso HTTPS mediante otra dirección requiere un certificado adecuado. La configuración CORS existente no se modifica.

El panel usa `compose.yaml`, nombres predeterminados `TecNM_LoginControl_Backend` y `TecNM_LoginControl_Frontend`, HTTPS local y puertos 7001 y 5173. Al crear o recrear siempre pregunta el puerto publicado en el anfitrión, con el valor guardado como predeterminado. Enter lo acepta; si está ocupado o no es válido pide otro. El puerto interno del contenedor no cambia. Al crear o recrear solicita el nombre de cada contenedor; Enter conserva el valor mostrado. Valida nombres distintos y conflictos con otros contenedores. Los puertos y nombres se guardan en el `.env` de la raíz, que el panel crea si falta; `.env.example` documenta los valores iniciales. Iniciar y consultar estado reutilizan esos ajustes sin preguntar nombres. Iniciar arranca el contenedor ya existente; si falta, indica usar Crear servicios. Antes de recrear muestra los nombres de los contenedores que se eliminarán y pregunta si estás de acuerdo (`s/N`). Cancelar conserva contenedores y configuración, sin abrir los asistentes. Recrear repite el flujo completo de creación y ofrece los nombres guardados como valores predeterminados. Después de completar la configuración elimina los contenedores seleccionados, verifica su eliminación y construye otros nuevos. Un fallo de eliminación detiene el proceso; cancelar la preparación conserva los contenedores anteriores. Si existe la configuración anterior en `.docker/control.env` y falta `.env`, se recuperan esos puertos. “Crear servicios”, “Iniciar servicios” y “Recrear servicios” abren un submenú con frontend, backend, ambos y volver. La opción “Ambos” inicia primero el backend, porque Nginx necesita resolverlo al iniciar. En terminal, presionar el número ejecuta la opción sin Enter; `0` permite volver o salir. Los datos de conexión y las confirmaciones de borrado siguen requiriendo Enter. El estado del SQL desde el anfitrión convierte `host.docker.internal` a `localhost`; si PostgreSQL escucha en otra dirección, configurar esa dirección accesible tanto desde el anfitrión como desde Docker.

La carga solicita credenciales de PostgreSQL por separado y no las guarda. Se ingresa el nombre de destino en `snake_case` y se consulta si existe. Si falta, ofrece crearla; si existe, avisa que se perderán todos sus datos y pide confirmar y escribir el nombre exacto antes de reemplazarla. Recrear usa las mismas comprobaciones y confirmaciones. Se necesitan permisos para consultar, crear y, al reemplazar, eliminar la base; conexiones activas pueden impedir el borrado. Si falla la carga después de recrear, la base permanece creada sin el esquema; el borrado anterior no puede revertirse. No se crean automáticamente el usuario de aplicación ni sus permisos. Nunca usar estas operaciones como migración de datos existentes.

## Interpretación de estados

Crear la base no permite comprobar la conexión con el usuario de aplicación hasta configurar `backend/.env`. PostgreSQL indica `SIN CONFIGURAR` si falta ese archivo o la cadena de conexión está incompleta; `SIN COMPROBAR` si falta `psql` o no puede leer el archivo; y `SIN CONEXIÓN` si el intento con configuración válida falla. El resumen muestra el motivo sin credenciales.

`API / PostgreSQL` permanece `SIN COMPROBAR` hasta que haya configuración y el backend responda. Un backend detenido no implica un fallo de PostgreSQL. El estado de cada servicio consulta su nombre registrado en el `.env` raíz: `SIN CREAR` si no existe y `DETENIDO` si está apagado. Para un contenedor en ejecución verifica HTTP; un código de error como 500 se muestra como `ERROR HTTP` con el código, mientras que “no responde” indica fallo de conexión o timeout; también indica cuando falta `curl` o Docker no está disponible.

## Asistente y comprobación previa

El asistente pide servidor PostgreSQL, puerto, base de datos, usuario de aplicación, contraseña oculta y origen HTTPS del frontend. Servidor y puerto ofrecen `localhost` y `5432` por defecto: Enter acepta cada valor. Para Docker se guarda `host.docker.internal`, que apunta al mismo anfitrión. Los campos requieren Enter; genera una clave de firma y deja las credenciales de Google y Microsoft pendientes para completarlas en `backend/.env`. El inicio de sesión no estará disponible hasta configurarlas.

El archivo para Docker se guarda con permisos `0600` después de confirmar. Al crear uno nuevo se reemplaza toda la configuración anterior; cancelar conserva el archivo existente. Las credenciales guardadas son de la aplicación y no sustituyen las credenciales administrativas que solicita la carga de SQL.

Para backend o ambos, se comprueba con el archivo existente; durante la creación o recreación se permite elegirlo o crearlo. La comprobación ejecuta `SELECT 1=1` desde el anfitrión. Si falla, se solicita revisar las credenciales, el servicio y la existencia de la base; no se inicia ni se retira ningún contenedor. La comprobación no inicializa el esquema ni crea la base y no garantiza por sí misma el acceso desde Docker; el estado “API / PostgreSQL” comprueba esa ruta con el backend activo.

## Contrato de `database.sql`

El archivo de la raíz crea las tablas de usuarios y tipos de usuario y los cuatro tipos iniciales. No crea usuarios. El panel lo aplica únicamente cuando se solicita la carga o recreación; no aplica migraciones EF ni `EnsureCreated`. Editar el archivo no actualiza una base existente: requiere una migración explícita revisada.

El certificado local es autofirmado y el navegador puede pedir aceptarlo. Producción requiere su propia configuración de certificados, red y credenciales.

## Inicio de sesión institucional

Configurar los valores de sesión de `backend/.env.example` en `backend/.env`, tanto para Compose como para ejecución manual. Nunca guardar secretos en Git. Generar una clave exclusiva de al menos 32 bytes codificada en base64 con `openssl rand -base64 32`; configurar también emisor y audiencia estables. Sin configuración completa del proveedor y de la firma, el login responde `503`; el health check sigue disponible.

`Session__FrontendUrl` es el origen HTTPS público exacto (por ejemplo `https://localhost:5173`), sin ruta. Actualizar esta variable cuando cambie el puerto frontend. Registrar en los proveedores las URL públicas `https://localhost:5173/api/session/callback/google` y `https://localhost:5173/api/session/callback/microsoft`, ajustando origen y puerto al entorno. No usar las URL internas del contenedor. Se requieren las credenciales de cada aplicación y el UUID del tenant Microsoft; configurar en Microsoft Entra los claims opcionales `acct` y `email` en el ID token. Google debe emitir `hd`, `email_verified`, correo y `sub`. Solo se admiten las organizaciones y dominios de la spec.

No se incluyen usuarios semilla ni una herramienta de alta. Cada registro debe tener un tipo y al menos una identidad vinculada de forma confiable, con el correo normalizado y los identificadores requeridos por proveedor. Una base ya inicializada necesita una actualización de esquema explícita y revisada antes de usar esta versión; el setup no la aplica. Para ensayos, usar una base aislada.

## Verificación

```bash
python3 scripts/check-control.py
python3 scripts/check-containers.py
python3 scripts/check-recreate.py
python3 scripts/check-database.py
bash -n control.sh scripts/control/*.sh scripts/smoke-template.sh
python3 -m py_compile scripts/control/*.py
bash scripts/test-session.sh  # PostgreSQL temporal y pruebas backend; requiere Docker y .NET
API_URL=https://localhost:7001 bash scripts/smoke-template.sh
```

La prueba HTTP requiere curl y Python 3. Verifica la respuesta del health check y un `404` para una ruta de API no registrada.
