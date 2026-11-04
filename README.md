# TecNM Campus Monclova — LoginControl

Proyecto derivado del template institucional para el inicio de sesión de usuarios registrados mediante Google o Microsoft. Incluye backend .NET 10 con EF Core/PostgreSQL, frontend Vue 3/TypeScript y componentes visuales TecNM.

## Panel de control local

El panel administra frontend y backend en contenedores y utiliza PostgreSQL instalado en el sistema anfitrión:

```bash
bash control.sh
# El panel crea el .env raíz y ofrece el asistente para backend/.env.
```

La opción «Limpiar contenedores y base de datos» retira los dos contenedores administrados y elimina la base elegida sin recrearlos. Advierte de la pérdida de datos y exige escribir `ACEPTO` y el nombre exacto de la base; conserva las imágenes y la configuración.

Requiere Docker Compose, Python 3, OpenSSL, curl y el cliente `psql`. Al crear backend se elige o crea su `.env`; antes de arrancarlo se comprueba la conexión PostgreSQL; si falla, la operación se detiene. Los puertos publicados escuchan en las interfaces del servidor, sujetos a la red y el firewall. Configurar PostgreSQL para aceptar conexiones desde la red de Docker y autorizar al usuario de aplicación. El panel permite cargar `database.sql` con credenciales introducidas en cada ejecución, consultar estados y recrear contenedores o la base. Recrear la base elimina sus datos y requiere dos confirmaciones. Las opciones “Crear servicios”, “Iniciar servicios” y “Recrear servicios” permiten elegir frontend, backend o ambos. Al crear o recrear un contenedor se solicitan su nombre y el puerto publicado; se guarda junto a los puertos en el `.env` raíz. Iniciar y consultar estados utilizan los nombres guardados, sin pedirlos nuevamente. Recrear elimina los contenedores seleccionados y repite el flujo completo de creación. La navegación es con una tecla, sin Enter; “Ambos” inicia primero el backend. Consulte [la guía de setup](docs/setup.md#panel-con-postgresql-en-el-anfitrión).

## Módulos externos

LoginControl permite registrar aplicaciones externas y asignar sus accesos. Solo `superadmin` administra el catálogo y los permisos, por usuario, módulo o lote de usuarios existentes. Los módulos canjean códigos de un uso desde su backend y crean su sesión propia. Consultar [contrato](specs/module-access.spec.md), [guía de integración](module-integration.md) y [plan](plans/module-access-plan.md). El receptor debe preparar y comprobar su cookie de correlación según la guía; LoginControl verifica la sesión del navegador antes de emitir el código canjeable.

Para bases existentes, revisar la migración aditiva de módulos en `scripts/migrations/` y aplicarla explícitamente antes de usar estas funciones; reconstruir contenedores no actualiza el esquema.

## Base de datos y sesión

El panel consulta si la base PostgreSQL indicada existe: ofrece crearla si falta o reemplazarla tras advertir y confirmar la pérdida de datos si ya existe. Después carga [`database.sql`](database.sql). El esquema incluye usuarios y cuatro tipos iniciales; no registra usuarios. La configuración de proveedores y credenciales se describe en [setup](docs/setup.md#inicio-de-sesión-institucional). Sin ella, el inicio de sesión no está disponible y responde 503; el health permanece disponible.

Editar el SQL no modifica una base existente; esos cambios requieren una migración explícita. Reiniciar o reconstruir contenedores no ejecuta el SQL ni elimina datos.

## Desarrollo y verificación

```bash
# Con el stack activo:
API_URL=https://localhost:7001 bash scripts/smoke-template.sh

# Comprobaciones locales:
python3 scripts/check-control.py
python3 scripts/check-containers.py
python3 scripts/check-recreate.py
python3 scripts/check-database.py
python3 scripts/check-cleanup.py
bash -n control.sh scripts/control/*.sh scripts/smoke-template.sh
python3 -m py_compile scripts/control/*.py
bash scripts/test-session.sh  # PostgreSQL temporal y pruebas de sesión; requiere Docker y .NET
npm --prefix frontend ci
npm --prefix frontend run build
(cd frontend && node scripts/check-layout.mjs)
dotnet build backend/TecNM.Api.csproj
```

Para ejecutar sin Docker, consultar los `.env.example` de backend/frontend. La ejecución manual del backend requiere una conexión PostgreSQL propia.

## Arquitectura y contribución

- [Reglas compartidas de desarrollo](AGENTS.md): punto de entrada desde la raíz; complementa las reglas locales de backend y frontend.
- [Guía de contribución](CONTRIBUTING.md): flujo ODD con SDD, verificaciones por alcance y contribuciones desde un fork.
- [Alcance del proyecto](specs/template.spec.md) y [contrato de sesión](specs/session-control.spec.md).
- [Arquitectura backend](BACKEND_ARCHITECTURE.md) y [reglas de desarrollo backend](backend/AGENTS.md).
- [Arquitectura frontend](FRONTEND_ARCHITECTURE.md) y [reglas de desarrollo frontend](frontend/AGENTS.md).
- [Sistema de diseño](frontend/DESIGN.md) y [componentes compartidos](frontend/src/shared/components/specs/components.spec.md).
- [Layout persistente](frontend/src/app/layouts/mainLayout.spec.md), [tema](frontend/src/shared/composables/useTheme.spec.md), [inicio de sesión](frontend/src/modules/session/specs/session.spec.md) y [página 404](frontend/src/shared/views/specs/notFound.spec.md).
- [Contrato Docker](specs/docker.spec.md) y [health check](specs/health.spec.md).

Aplicar SDD: actualizar la especificación correspondiente antes de modificar comportamiento o contratos. Documentar funcionalidades y criterios de aceptación en `specs/`; mantener los `AGENTS.md` centrados en patrones de diseño y reglas de desarrollo.

Convenciones: objetos PostgreSQL en `snake_case`; código, rutas y archivos en inglés; interfaz, errores y documentación en español de México. Reutilizar componentes antes de crear controles o estilos locales.

Leer primero el `AGENTS.md` raíz y después el de cada área afectada. Para cambios transversales, revisar ambos.
