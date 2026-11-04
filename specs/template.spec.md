# Proyecto derivado TecNM LoginControl

## Alcance

Proyecto derivado del template institucional con .NET 10, EF Core, PostgreSQL, Vue 3, TypeScript y componentes TecNM. Su funcionalidad de inicio de sesión se define en [session-control.spec.md](session-control.spec.md); la integración con aplicaciones externas y administración de accesos se define en [module-access.spec.md](module-access.spec.md); el health check público permanece definido en [health.spec.md](health.spec.md).

La aplicación conserva el layout, tema y componentes compartidos del template. `database.sql` inicializa el esquema del proyecto mediante carga explícita en PostgreSQL del anfitrión; no migra bases existentes. El setup conserva HTTPS, comprobación de puertos, contenedores separados y persistencia.

## Aceptación

1. Frontend y backend compilan; el health check responde conforme a su spec.
2. La autenticación y bienvenida cumplen el contrato y los criterios de sesión.
3. El SQL inicializa el esquema y catálogo de tipos en una base preparada; reiniciar contenedores no lo ejecuta ni borra datos existentes.
4. Las reglas, documentación y configuración describen el proyecto derivado y distinguen verificaciones reales de pendientes.
