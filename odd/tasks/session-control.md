# Inicio de sesión institucional

## Objetivo y alcance autorizado

Implementar el inicio de sesión de usuarios registrados con Google o Microsoft, la sesión propia y la bienvenida con tipo de usuario. Se excluyen el alta de usuarios, permisos y la administración de sesiones de los proveedores.

## Requisitos y referencias

- [Contrato compartido](../../specs/session-control.spec.md), [backend](../../backend/Modules/Session/specs/session.spec.md) y [frontend](../../frontend/src/modules/session/specs/session.spec.md).
- [Plan y decisiones](../../plans/session-control-plan.md).
- Probar con recursos aislados; las pruebas de extremo a extremo con proveedores reales requieren su configuración y cuentas institucionales.

## Tareas

- [x] Explorar el proyecto y especificar el contrato.
- [x] Implementar y comprobar esquema, semillas y mapeos en base aislada.
- [x] Implementar autenticación y sesión backend; comprobar reglas de identidad y JWT de forma aislada.
- [x] Implementar interfaz de sesión y comprobarla con API simulada.
- [x] Alinear setup y documentación.
- [ ] Verificar callback OIDC exitoso con un proveedor simulado, incluidos firma, vencimiento, nonce y reutilización de código.
- [ ] Verificar flujos completos con ambos proveedores reales cuando existan configuración y cuentas de prueba.

## Decisiones

Las decisiones de identidad, tipos y sesión están en el plan y las specs. El SQL de inicialización no migra volúmenes existentes. El cierre comprueba el origen y no revoca copias del JWT.

## Verificación y evidencia

- `bash scripts/test-session.sh`: inicialización, restricciones SQL y 2 pruebas de sesión aprobadas con PostgreSQL efímero.
- `npm --prefix frontend run build` y `node scripts/check-layout.mjs`: aprobados. Interfaz comprobada con API simulada, incluyendo sesión, errores, cierre, vencimiento, tema oscuro y ancho 375 px.
- `python3 scripts/check-dev-ports.py`, `python3 scripts/check-setup.py`, sintaxis de scripts y `docker compose config --quiet` con contraseña de prueba: aprobados. Las pruebas de setup simulan Docker.
- Detrás de Nginx HTTPS aislado, el inicio Google produjo callback público, PKCE, state y nonce; un callback con state inválido se rechazó sin cookie. Se comprobaron HTTP de health, sesión anónima, proveedor no configurado, proveedor desconocido y CSRF sin Origin.
- No se probó un callback OIDC exitoso ni el flujo real con proveedores. No se realizó una revisión completa de accesibilidad.

## Estado y siguientes pasos

Implementación disponible para revisión. Para las pruebas reales se requieren credenciales, configuración de proveedores, organizaciones, URL y usuarios de prueba institucionales. Las bases existentes requieren una actualización explícita revisada; no hay migración automática.
