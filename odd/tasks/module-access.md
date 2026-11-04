# Acceso centralizado a módulos externos

## Objetivo y alcance autorizado

- Objetivo: planear catálogo, permisos y handshake para iniciar sesión en aplicaciones externas desde LoginControl.
- Autorizado: implementar el plan con subagentes GPT-6.1 Terra medium y verificar en recursos aislados. Migrar bases reales y desplegar quedan fuera del alcance.
- Incluye en el plan: administración exclusiva de `superadmin`, gestión por usuario o módulo, operaciones por lote sobre usuarios existentes y canje de código de una sola vez.
- Fuera de alcance: login externo, permisos internos de aplicaciones, CRUD de usuarios, asignaciones automáticas para altas futuras y logout global.
- Contrato cerrado: ticket preliminar, cookie/state del receptor, confirmación con sesión central y código canjeable; vigencia de 60 segundos por etapa. Logout central no revoca códigos ya emitidos.

## Requisitos y referencias

- Fuente: conversación del 4 de noviembre de 2026. El usuario confirmó que solo `superadmin` administra y que “todos” comprende usuarios actuales, con selección y desactivación por lote.
- [Plan de implementación](../../plans/module-access-plan.md).
- [Diagrama HTML propuesto](../../module-access-flow.html).
- [Alcance actual](../../specs/template.spec.md) y [contrato de sesión vigente](../../specs/session-control.spec.md).
- Spec compartida prevista: `specs/module-access.spec.md`, todavía no creada.

## Tareas

- [x] Explorar esquema, autenticación, frontend, componentes y pruebas existentes.
- [x] Confirmar quién administra y qué significa asignación masiva a todos.
- [x] Preparar plan con fases, archivos afectados, contratos propuestos y aceptación.
- [x] Cerrar correlación del navegador, vigencia y reglas de códigos pendientes con aprobación del usuario.
- [x] Crear spec compartida y actualizar alcance y contrato de sesión antes de modificar comportamiento.
- [x] Implementar y comprobar SQL, migración aditiva y mapeos EF en base temporal.
- [x] Implementar administración y autorización por tipo vigente.
- [x] Implementar emisión, confirmación del navegador, canje atómico e identidad mínima.
- [x] Implementar dashboard y administración por usuario, módulo y lotes.
- [x] Ejecutar pruebas de servicios y controllers con PostgreSQL aislado, incluyendo concurrencia.
- [ ] Verificar receptor externo real y recorrido visual de interfaz.
- [x] Actualizar guía de integración, setup, README y diagrama según contrato final.

## Decisiones

- 2026-11-04: LoginControl es el único login; módulos externos reciben identidad y crean sesiones propias.
- 2026-11-04: Solo `superadmin` administra módulos y asignaciones; comprobar tipo actual en backend.
- 2026-11-04: Administración desde usuario o módulo, con selección múltiple y activación/desactivación por lote.
- 2026-11-04: Asignar a todos afecta una instantánea de usuarios existentes; no incluye altas futuras.
- Propuestas técnicas todavía por formalizar en spec: tres tablas nuevas, API key independiente, código opaco de 60 segundos y consumo atómico.

## Verificación y evidencia

- Inspección de `database.sql`, `SessionService`, `Program.cs`, vista/cliente de sesión, router, componentes y suite existente: la integración aún no está implementada.
- Comprobación documental ejecutada: 12 enlaces locales válidos, ambos archivos con salto final y sin espacios sobrantes; `git diff --check` sin errores. Los archivos nuevos también se revisaron directamente, ya que todavía no están rastreados por Git.
- Implementación: builds backend/frontend aprobados; check-layout aprobado. `bash scripts/test-session.sh` final: 11 pruebas aprobadas, 0 fallidas y 0 omitidas; esquema inicial y migración preservando usuario aprobados. Incluye rol vigente, CSRF, lotes, vencimiento, identidad y canje concurrente. No se modificó ninguna base real.
- Diagrama y guía actualizados al ticket y correlación aprobados por el usuario. La prueba visual con datos simulados fue rechazada por la política automática del navegador (approval policy never); no se presenta como aprobada.

## Estado y siguientes pasos

Implementación con subagentes GPT-6.1 Terra medium y revisión de integración completadas. Falta aplicar la migración explícitamente en el entorno elegido, configurar un módulo y probar su receptor real. La cookie/state del receptor es parte obligatoria de su contrato. No se ejecutó despliegue.
