# Inicio de sesión y bienvenida

Estado: implementado; la prueba con proveedores reales está pendiente de configuración institucional. Ver [contrato compartido](../../../../../specs/session-control.spec.md).

## Flujo

- La página inicial ofrece iniciar sesión con Google o Microsoft. Cada acción inicia el flujo en el backend; el frontend no recibe tokens del proveedor ni el JWT en una URL.
- Presentar el acceso con una introducción institucional y una tarjeta de proveedores inspiradas en el diseño de Figma. No mostrar campos de correo ni contraseña. La bienvenida autenticada es sencilla y no incluye módulos ni datos de ejemplo.
- Tras autenticarse, consultar `GET /api/session` y mostrar una bienvenida igual para todos, con nombre registrado y etiqueta del tipo actual. No mostrar funciones ni permisos diferentes por tipo.
- Consultar al cargar la página y al regresar a la pestaña; no ejecutar heartbeat. Con el vencimiento recibido, actualizar la interfaz cuando se cumpla la hora y solicitar iniciar sesión de nuevo. Al reabrir o volver a la pestaña se consulta el estado real al backend.
- Si la consulta responde `401`, mostrar inicio de sesión; si hay un error de red o del servidor, informar en español y permitir reintentar sin afirmar que el usuario no existe. Un fallo de autenticación muestra un mensaje genérico.
- Ofrecer cierre de sesión mediante `POST /api/session/logout`, con protección CSRF conforme al contrato backend. Al completarse, regresar al inicio. Evitar envíos duplicados y mostrar el estado de la operación.
- Usar componentes compartidos y `UiBasePage`, textos en español de México, navegación por teclado, foco visible, temas claro y oscuro y diseño responsive desde 375 px.

## Aceptación

Los dos proveedores pueden iniciarse y el resultado exitoso muestra nombres y tipo de la base; los rechazos no revelan si el usuario existe. La interfaz consulta al cargar y al volver a la pestaña, maneja `401`, errores de red y vencimiento sin heartbeat. El cierre regresa al inicio sin afectar el proveedor. Verificar teclado, foco, ambos temas, responsive y los comandos de build y layout indicados en las reglas frontend.
