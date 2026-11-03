# Arranque Docker TecNM

## Contrato

- `compose.yaml` administra únicamente `frontend` y `backend` en contenedores independientes. PostgreSQL se ejecuta en el sistema anfitrión.
- Frontend Vue se compila en Docker y Nginx lo sirve por HTTPS; `/api` se reenvía al backend.
- Backend .NET se compila en Docker y publica HTTPS. Usa `backend/.env` para la conexión PostgreSQL y la configuración de sesión; `host.docker.internal` resuelve al anfitrión mediante Docker.
- Puertos predeterminados 5173 y 7001, publicados en todas las interfaces disponibles del anfitrión (`0.0.0.0` y, donde Docker lo admita, IPv6). El panel siempre solicita el puerto publicado y propone el guardado; si está ocupado solicita otro y conserva la selección en `.env` de la raíz. Los nombres de contenedores se eligen únicamente al crear servicios en el panel y se guardan como `TECNM_FRONTEND_CONTAINER_NAME` y `TECNM_BACKEND_CONTAINER_NAME`.
- Certificados locales fuera de Git. La conexión de PostgreSQL, sus permisos y su persistencia son responsabilidad de la instalación del anfitrión.
- El panel descrito en [control.spec.md](control.spec.md) administra los contenedores, la carga explícita de SQL y las comprobaciones de estado. Reiniciar contenedores no ejecuta `database.sql`.

## Aceptación

1. El Compose define solamente frontend y backend; PostgreSQL no se inicia en Docker como parte del despliegue.
2. Los contenedores publican las aplicaciones por HTTPS y el backend puede conectarse a PostgreSQL del anfitrión cuando está configurado y autorizado.
3. La carga de `database.sql` se inicia explícitamente y recrear la base exige confirmación; reiniciar contenedores no modifica la base.
4. El panel comprueba puertos y disponibilidad según [control.spec.md](control.spec.md).

Los puertos publicados deben poder recibir conexiones por las interfaces de red del anfitrión, sujeto a la configuración de red y firewall. Antes de crear se comprueba disponibilidad en direcciones comodín IPv4 e IPv6; un puerto ocupado en cualquier interfaz debe rechazarse. La configuración CORS y los certificados no se modifican como parte de este cambio.
