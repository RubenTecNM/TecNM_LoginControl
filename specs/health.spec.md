# Health check del backend

## API

| Método y ruta | Acceso | Respuesta |
| --- | --- | --- |
| `GET /api/health` | Público | `200 { "status": "ok" }` |
| `GET /api/health/database` | Público | `200 { "status": "ok" }` si el backend puede conectar; `503 { "status": "error" }` en otro caso |

El health de proceso confirma que la API está disponible; no valida dependencias externas. Debe responder 200 aunque los proveedores OIDC estén ausentes, vacíos o con espacios. La configuración pendiente del login no debe provocar HTTP 500 en este endpoint.

La comprobación de base no expone credenciales ni detalles de errores.
