# Requisitos de integración con LoginControl

Estado: contrato con correlación automática del navegador. Las direcciones, claves, códigos y personas de los ejemplos son ficticios.

El usuario inicia sesión únicamente en LoginControl. El servicio externo recibe un código temporal, lo canjea con LoginControl y utiliza la identidad recibida para iniciar su propia sesión. No necesita pedir usuario ni contraseña.

## 1. Datos para registrar el servicio

El equipo del servicio debe proporcionar su nombre y la URL completa que recibirá los accesos. Esa URL se registra como destino del módulo en LoginControl.

Ejemplo:

```text
Nombre: Servicio Social
URL de recepción: https://serviciosocialtecmonclova.com/acceso/
```

LoginControl entregará al equipo estos tres valores:

| Dato | Ejemplo | Uso |
| --- | --- | --- |
| `moduleId` | `22222222-2222-4222-8222-222222222222` | Identificador fijo del servicio. |
| `apiKey` | `API_KEY_DEL_MODULO` | Clave del servicio para autenticar el canje. |
| URL de canje | `https://logintecmonclova.com/api/modules/exchange` | Dirección a la que se devuelve el código. |

`moduleId` y `apiKey` se entregan al registrar el servicio. No llegan en cada navegación del usuario. La URL de canje real se proporciona según el entorno de pruebas o producción.

## 2. Peticiones que recibe el servicio

La misma URL registrada debe aceptar dos llegadas GET: primero `ticket`; después `code` y `state`. No muestran un formulario de login.

### Primera llegada: preparar correlación

El navegador del usuario llega desde LoginControl:

```http
GET /acceso/?ticket=TICKET_TEMPORAL HTTP/1.1
Host: serviciosocialtecmonclova.com
```

Tomar `ticket` de la query string. No es un código canjeable. El servicio genera un `state` aleatorio propio, lo guarda en una cookie temporal y redirige al navegador a LoginControl. `state` admite entre 32 y 128 caracteres ASCII: letras, números, guion y guion bajo. Ejemplo ficticio de 32 caracteres:

```http
HTTP/1.1 302 Found
Set-Cookie: module_state=0123456789abcdef0123456789abcdef; Path=/acceso/; Max-Age=120; HttpOnly; Secure; SameSite=Lax
Referrer-Policy: no-referrer
Location: https://logintecmonclova.com/api/modules/authorize?ticket=TICKET_TEMPORAL&state=0123456789abcdef0123456789abcdef
```

Esta redirección la sigue el navegador, que lleva su sesión central. No sustituirla por una petición del backend. La URL real de `/api/modules/authorize` se entrega junto con la URL de canje. El ticket vence en 60 segundos y solo se autoriza una vez.

LoginControl verifica que la sesión del navegador corresponda al usuario del ticket y que conserve acceso al módulo. Consume el ticket y redirige a la URL registrada con un código nuevo y el mismo `state`. Si no coincide la sesión o falla la autorización, no regresa un código válido.

### Segunda llegada: comprobar correlación

```http
GET /acceso/?code=CODIGO_TEMPORAL&state=0123456789abcdef0123456789abcdef HTTP/1.1
Host: serviciosocialtecmonclova.com
Cookie: module_state=0123456789abcdef0123456789abcdef
```

| Campo | Ubicación | Uso |
| --- | --- | --- |
| `code` | Query string | Código que se canjea en el siguiente paso. |
| `state` | Query string | Debe coincidir exactamente con la cookie temporal propia. |
| `module_state` | Cookie del servicio | Valor creado en la primera llegada; su nombre puede adaptarse. |

El servicio compara `state` con su cookie, exige coincidencia y elimina esa cookie. Si falta alguno o no coincide, rechaza el acceso: no canjea ni crea sesión. La cookie se elimina usando el mismo nombre y Path con `Max-Age=0`.

Después toma `code` de la query string y conserva su valor exacto. El código es opaco, no un JWT. Vence en 60 segundos desde su generación y admite un solo canje exitoso. Ticket y código son strings hexadecimales de 64 caracteres; los textos de los ejemplos son marcadores para sustituir.

Ninguna de estas llegadas incluye API key, ID del usuario, nombres, correos ni JWT central. No hay body ni headers personalizados enviados por LoginControl. La cookie `module_state` la creó el propio servicio.

## 3. Petición que el servicio envía a LoginControl

El backend del servicio debe enviar inmediatamente una petición `POST` a la URL de canje proporcionada. La hace el servicio, no el navegador del usuario.

- Header `Authorization`: palabra `Bearer`, un espacio y la `apiKey` entregada al registrar el servicio.
- Header `Content-Type`: `application/json`.
- Body JSON: `moduleId` del servicio y `code` recibido en la navegación.

Ejemplo HTTP:

```http
POST /api/modules/exchange HTTP/1.1
Host: logintecmonclova.com
Authorization: Bearer API_KEY_DEL_MODULO
Content-Type: application/json
Accept: application/json

{
  "moduleId": "22222222-2222-4222-8222-222222222222",
  "code": "CODIGO_TEMPORAL"
}
```

| Campo | Ubicación | Tipo | Valor que debe enviarse |
| --- | --- | --- | --- |
| API key | Header `Authorization` | String obligatorio | `Bearer API_KEY_DEL_MODULO`. |
| `moduleId` | Body JSON | String UUID obligatorio | ID fijo entregado al registrar el servicio. |
| `code` | Body JSON | String obligatorio | El mismo valor recibido en `?code=...`. |

No se envía `userId`: LoginControl obtiene el usuario asociado al código. Tampoco se envía la cookie de LoginControl ni una clave de firma JWT.

La API key generada es un string hexadecimal de 64 caracteres. Pertenece al servicio y solo participa en esta petición entre backends. No se agrega a la URL de recepción del usuario.

Ejemplo equivalente con curl:

```bash
curl --request POST 'https://logintecmonclova.com/api/modules/exchange' \
  --header 'Authorization: Bearer API_KEY_DEL_MODULO' \
  --header 'Content-Type: application/json' \
  --header 'Accept: application/json' \
  --data '{
    "moduleId": "22222222-2222-4222-8222-222222222222",
    "code": "CODIGO_TEMPORAL"
  }'
```

Para una prueba real, sustituir URL, `moduleId` y `apiKey` por los valores entregados, y `CODIGO_TEMPORAL` por un código recién recibido. Los textos del ejemplo no son credenciales válidas.

## 4. Validaciones y respuesta de LoginControl

Al recibir el canje, LoginControl comprueba:

1. Que la API key autentique al módulo indicado por `moduleId`.
2. Que el módulo esté activo.
3. Que el código exista, corresponda a ese módulo, no haya vencido y no se haya usado.
4. Que el usuario asociado siga existiendo y su asignación al módulo continúe activa.

Si todo es válido, LoginControl marca el código como usado y devuelve los datos del usuario en la respuesta de ese mismo `POST`. No hace otra petición a Servicio Social. Si llegan dos canjes simultáneos con el mismo código, solo uno obtiene la identidad.

Respuesta exitosa:

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: no-store

{
  "user": {
    "id": "11111111-1111-4111-8111-111111111111",
    "firstNames": "Ana María",
    "paternalLastName": "García",
    "maternalLastName": "López",
    "googleEmail": "ana.ejemplo@itsmva.edu.mx",
    "microsoftEmail": "ana.ejemplo@monclova.tecnm.mx"
  }
}
```

| Campo JSON | Tipo | Significado |
| --- | --- | --- |
| `user.id` | String UUID | Identificador del usuario en LoginControl. Usar este ID para reconocerlo en el servicio. |
| `user.firstNames` | String | Nombre o nombres registrados. |
| `user.paternalLastName` | String | Apellido paterno registrado. |
| `user.maternalLastName` | String o `null` | Apellido materno; también puede ser una cadena vacía. |
| `user.googleEmail` | String o `null` | Correo institucional de Google registrado. |
| `user.microsoftEmail` | String o `null` | Correo institucional de Microsoft registrado. |

El usuario puede tener uno o ambos correos. No asumir que los dos campos contienen texto. Los campos provienen del registro local de LoginControl; la respuesta no incluye JWT central, contraseña ni permisos internos del servicio.

## 5. Qué hace el servicio con la respuesta

Si obtiene `200 OK` con el objeto `user`, el servicio toma `user.id` y los datos recibidos e inicia la sesión de ese usuario. Después dirige al navegador a su página de inicio, por ejemplo `https://serviciosocialtecmonclova.com/`, sin conservar el código en la URL.

LoginControl no crea la sesión del servicio ni devuelve una cookie para su dominio. El servicio no debe volver a solicitar un login al usuario para completar este ingreso.

Si el canje falla, no debe iniciar sesión. Los códigos de error para preparar el receptor son:

| Estado | Situación |
| --- | --- |
| `400 Bad Request` | JSON o campos inválidos; código inexistente, vencido, usado o perteneciente a otro módulo. |
| `401 Unauthorized` | API key ausente o inválida para el `moduleId` indicado. |
| `403 Forbidden` | Módulo deshabilitado o usuario sin autorización vigente para ese módulo. |
| `409 Conflict` | Otro cambio coincidió con la operación; iniciar un nuevo intento desde LoginControl. |

Ejemplo de rechazo de código:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/json
Cache-Control: no-store

{
  "message": "No fue posible autorizar el acceso."
}
```

Usar el estado HTTP para decidir éxito o rechazo; el texto es para mostrar el error. Ante error de red, respuesta inesperada o timeout, tampoco se inicia sesión. La página de recepción y sus redirecciones deben usar `Referrer-Policy: no-referrer` y no cargar recursos de terceros antes de limpiar los parámetros.

Para obtener otro código, el usuario vuelve a seleccionar el módulo desde LoginControl. Un código ya consumido no vuelve a entregar identidad, aunque el servicio no haya recibido la respuesta anterior.
