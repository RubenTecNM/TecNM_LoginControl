# Inicio de sesión institucional

Estado: implementado; las pruebas con proveedores reales requieren configuración y cuentas institucionales. Este contrato corresponde al proyecto derivado `TecNM_LoginControl`.

## Alcance y reglas

Una persona previamente registrada puede iniciar sesión con Google o Microsoft y ver una bienvenida con su nombre y tipo registrados. Cada usuario tiene exactamente un tipo: `superadmin`, `admin`, `docente` o `default`. El tipo no concede permisos ni cambia las funciones disponibles en esta etapa.

No hay autorregistro, administración de usuarios, edición de correos, autorización por tipo, integración con otras aplicaciones ni renovación automática. El alta y corrección de registros son externas al login. No se confía en nombres del proveedor para la bienvenida.

- Google requiere correo registrado y verificado del dominio exacto `itsmva.edu.mx` y organización Workspace autorizada. En el primer acceso se vincula el `sub` validado; después deben coincidir correo y `sub`.
- Microsoft requiere correo registrado del dominio exacto `monclova.tecnm.mx` y tenant autorizado. En el primer acceso se vinculan `tid` y `oid` validados; después deben coincidir correo y ambos identificadores. El tipo de cuenta y el claim `acct` no determinan el acceso.
- No se aceptan subdominios. El dominio no sustituye las comprobaciones de identidad y organización.
- Una identidad ya vinculada no se sustituye durante el inicio de sesión. Un cambio o reasignación de correo, o una cuenta recreada con otro identificador, requiere corregir el registro fuera de este alcance. Si no coincide, se rechaza el acceso. La vinculación inicial es atómica y solo usa claims verificados del proveedor; una identidad ya vinculada a otro usuario se rechaza.
- Para correos se eliminan espacios exteriores y se comparan sin distinguir mayúsculas, también para unicidad por proveedor. Los identificadores externos se comparan exactamente, sin normalización de correo.

## Contrato HTTP propuesto para implementar

Todas las rutas pertenecen al backend. El frontend usa la misma dirección pública mediante el proxy `/api` cuando esté disponible. Las URL de despliegue y callbacks se configuran por entorno.

| Método y ruta | Acceso | Comportamiento |
| --- | --- | --- |
| `GET /api/session/login/{provider}` | Público | Inicia OIDC para `google` o `microsoft` y redirige al proveedor. Otro valor responde `404`. |
| `GET /api/session/callback/{provider}` | Público, correlacionado | Procesa el callback del proveedor, emite la cookie solo si se autoriza y redirige a la aplicación. Los fallos vuelven a la pantalla de inicio con un resultado genérico, sin tokens ni datos personales en la URL. |
| `GET /api/session` | Cookie de sesión | `200` con los datos actuales del usuario; `401` si la sesión no es válida o el usuario ya no existe. |
| `POST /api/session/logout` | Cookie de sesión y protección CSRF mediante origen exacto | Borra la cookie de aplicación y responde `204`; no cierra la sesión del proveedor ni revoca copias del JWT. |

Respuesta de sesión `200`, JSON:

```json
{
  "user": {
    "id": "11111111-1111-4111-8111-111111111111",
    "firstNames": "Ana",
    "paternalLastName": "García",
    "maternalLastName": null,
    "userType": { "code": "docente", "name": "Docente" }
  },
  "expiresAt": "2026-10-29T19:00:00Z"
}
```

`maternalLastName` puede ser `null` o cadena vacía. `expiresAt` es la fecha UTC correspondiente al `exp` del JWT. Las respuestas de sesión y errores de autenticación no deben almacenarse en caché. Fallos no revelan si existe una cuenta ni exponen excepciones. Con credenciales de proveedor ausentes, vacías o con espacios, el login responde `503` y el health público sigue disponible. Los errores de configuración o disponibilidad del proveedor se distinguen de un rechazo de identidad en el servidor, sin mostrar detalles sensibles al usuario.

El backend emite un JWT propio con `sub` igual al UUID interno, `iss`, `aud`, `iat`, `exp` y `user_type` con el código estable. Dura una hora desde su emisión; firma, emisor, audiencia y vencimiento se validan en cada consulta protegida. No contiene correos, nombres, identificadores externos ni secretos. No se introduce en URLs ni es accesible a JavaScript: se transporta en cookie `HttpOnly` y `Secure`. La cookie usa `SameSite=Lax`. El cierre de sesión exige un encabezado `Origin` que coincida con el origen HTTPS del frontend; un origen ausente o diferente responde `403`. Para acceso entre orígenes se permiten credenciales únicamente desde los orígenes CORS configurados.

Si el tipo cambia durante la sesión, el JWT conserva el anterior hasta vencer; la consulta de sesión obtiene el tipo vigente de la base. La verificación de permisos actuales se definirá en una etapa posterior. La eliminación de un usuario se detecta en la siguiente consulta; no hay heartbeat. Al vencer, se exige iniciar sesión de nuevo, aunque el proveedor puede reconocer una sesión propia existente.

## Criterios de aceptación

1. Cada proveedor permite el ingreso de un usuario registrado cuya identidad, correo y organización coinciden; la bienvenida muestra datos locales y tipo actual.
2. Identidad inválida, vencida, no correlacionada, no registrada o con correo no coincidente no produce una cookie de sesión. Se rechazan dominios distintos o subdominios; una cuenta Microsoft invitada puede acceder si cumple las mismas validaciones y su correo está registrado.
3. El JWT contiene solo los claims previstos y vence a la hora; uno alterado, vencido o con emisor o audiencia incorrectos se rechaza.
4. La consulta devuelve `401` para una cookie inválida o un usuario eliminado. El frontend consulta al cargar y al regresar a la pestaña, sin polling, y muestra el vencimiento al cumplirse la hora.
5. Cerrar sesión borra la cookie y regresa al inicio; no afecta al proveedor. Una copia previa del JWT puede seguir siendo válida hasta su vencimiento.
6. Un cambio de tipo aparece en la siguiente consulta, aunque el JWT conserve el valor anterior. Ningún tipo otorga funciones adicionales en esta etapa.
7. Se verifican los flujos con proveedores simulados y base aislada; las pruebas reales dependen de cuentas, configuración y credenciales institucionales.

Detalles de implementación y aceptación: [backend](../backend/Modules/Session/specs/session.spec.md) y [frontend](../frontend/src/modules/session/specs/session.spec.md). Referencia de decisiones: [plan](../plans/session-control-plan.md).
