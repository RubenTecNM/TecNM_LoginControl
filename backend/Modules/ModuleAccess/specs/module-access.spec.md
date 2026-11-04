# Backend de acceso a módulos

El contrato HTTP, las reglas de persistencia y los criterios de aceptación se definen en [la spec compartida](../../../../specs/module-access.spec.md). Las consultas y mutaciones usan EF Core; los secretos se almacenan únicamente como hashes. La autorización administrativa consulta el tipo de usuario vigente en cada petición.
