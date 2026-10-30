# Setup de desarrollo

## Requisitos y opciones

Ejecutar desde cualquier directorio:

```bash
bash scripts/setup-dev.sh
bash scripts/setup-dev.sh --frontend-port 5180 --backend-port 7010 --project-name tecnm-mi-servicio
bash scripts/setup-dev.sh --check-ports
```

Requiere Docker con Compose y daemon activo, Python 3 y OpenSSL. Frontend y backend se compilan dentro de Docker.

| Opción | Predeterminado | Función |
| --- | --- | --- |
| `--frontend-port` | `5173` | Puerto HTTPS local de Nginx |
| `--backend-port` | `7001` | Puerto HTTPS local de la API |
| `--project-name` | `tecnm-template` | Nombre de proyecto Compose, contenedores y volumen |
| `--check-ports` | Desactivado | Solo comprueba puertos; no crea archivos ni cambia contenedores |

Los valores persistidos en `.env` sustituyen los predeterminados. El script preserva configuración ajena y contraseña existente. Si falta `TECNM_DB_PASSWORD`, genera una contraseña aleatoria.

Puertos distintos, entre 1 y 65535, publicados solo en `127.0.0.1`. Un conflicto propone alternativa; permite elegir en terminal y termina con código 2 si no hay interacción. No detiene procesos ajenos.

## Qué hace

1. Verifica que `database.sql` en raíz sea un archivo regular, salvo en modo `--check-ports`.
2. Comprueba herramientas, Docker y puertos IPv4/IPv6; acepta puertos de sus propios contenedores.
3. Guarda `.env` y genera o reutiliza el certificado autofirmado en `.docker/certs/`.
4. Construye imágenes y vuelve a comprobar puertos.
5. Inicia PostgreSQL y espera su health check; después inicia backend y frontend.
6. Muestra nombres y URLs de acceso.

El navegador puede pedir aceptar el certificado local. Producción requiere su configuración de certificados, red y credenciales.

## Contrato de `database.sql`

El archivo **debe estar en la raíz del repositorio**. Compose lo monta de solo lectura en `/docker-entrypoint-initdb.d/01-schema.sql`. El inicializador de la imagen PostgreSQL lo ejecuta con el usuario `tecnm_app` sobre la base `tecnm_template` cuando el directorio de datos está vacío.

El SQL del proyecto crea las tablas de usuarios y tipos de usuario y los cuatro tipos iniciales. No crea usuarios; los registros deben proporcionarse externamente con identidades verificadas.

- Definir tablas, relaciones, índices, restricciones y vistas propios; usar `snake_case`.
- Mantener el SQL alineado con entidades y configuraciones de `backend/Data/`.
- Las semillas de tipos forman parte de `database.sql`; no se cargan usuarios ni semillas adicionales.
- No aplica migraciones EF, `EnsureCreated` ni SQL adicional desde backend o script.
- Para bases existentes se requiere una migración explícita revisada; no existe una migración automática en este proyecto.

## Persistencia y cambios de esquema

El volumen lógico `postgres_data` se llama `${COMPOSE_PROJECT_NAME}_postgres_data`; por defecto, `tecnm-template_postgres_data`.

- Primer inicio con volumen nuevo: se ejecuta `database.sql`.
- Reinicio, reconstrucción o repetición del setup con volumen inicializado: no vuelve a ejecutar SQL y conserva datos.
- Modificar `database.sql` no actualiza una base existente. Aplicar migraciones explícitas según el proyecto.
- Incluso si el SQL solo tenía comentarios, ese volumen ya está inicializado. Añadir tablas después requiere una migración o un volumen nuevo independiente.
- Cambiar `TECNM_DB_PASSWORD` en `.env` no actualiza la contraseña almacenada en PostgreSQL de un volumen existente; debe coordinarse el cambio.

```bash
docker compose stop
# Reiniciar con configuración existente:
docker compose up -d --wait
# Reconstruir tras cambiar fuentes:
bash scripts/setup-dev.sh
# Administrar la base desde el contenedor:
docker compose exec db psql -U tecnm_app -d tecnm_template
```

`docker compose down` conserva el volumen. La opción `-v` elimina sus datos; no usarla para actualizar esquemas.

Para ensayar otra inicialización sin tocar el volumen anterior, usar un nombre de proyecto y puertos libres diferentes:

```bash
bash scripts/setup-dev.sh --project-name tecnm-prueba --frontend-port 5180 --backend-port 7010
```

## Errores de SQL

Un fallo en `database.sql` puede dejar una inicialización parcial. Si PostgreSQL no alcanza su health check, el setup reporta el error y conserva el volumen. No revierte, vuelve a aplicar ni repara el SQL automáticamente. Consultar `docker compose logs db`, corregir el script y resolver explícitamente el estado de la base. Para repetir una prueba desde cero puede usarse otro proyecto con volumen nuevo.

Una base ya inicializada puede arrancar tras un reinicio automático o en un intento posterior aunque el SQL previo haya fallado. El health check solo verifica disponibilidad de PostgreSQL; el proyecto debe comprobar su esquema y adoptar migraciones controladas.

## Inicio de sesión institucional

Copiar los valores de sesión de `.env.example` al `.env` de la raíz para Compose, o usar `backend/.env.example` para ejecución manual. Nunca guardar secretos en Git. Generar una clave exclusiva de al menos 32 bytes codificada en base64 con `openssl rand -base64 32`; configurar también emisor y audiencia estables. Sin configuración completa del proveedor y de la firma, el login responde `503`; el health check sigue disponible.

`SESSION_FRONTEND_URL` es el origen HTTPS público exacto (por ejemplo `https://localhost:5173`), sin ruta. Si se cambia el puerto frontend y no se fija esta variable, Compose usa ese puerto. Registrar en los proveedores las URL públicas `https://localhost:5173/api/session/callback/google` y `https://localhost:5173/api/session/callback/microsoft`, ajustando origen y puerto al entorno. No usar las URL internas del contenedor. Se requieren las credenciales de cada aplicación y el UUID del tenant Microsoft; configurar en Microsoft Entra los claims opcionales `acct` y `email` en el ID token. Google debe emitir `hd`, `email_verified`, correo y `sub`. Solo se admiten las organizaciones y dominios de la spec.

No se incluyen usuarios semilla ni una herramienta de alta. Cada registro debe tener un tipo y al menos una identidad vinculada de forma confiable, con el correo normalizado y los identificadores requeridos por proveedor. Una base ya inicializada necesita una actualización de esquema explícita y revisada antes de usar esta versión; el setup no la aplica. Para ensayos, usar un proyecto y volumen nuevos aislados.

## Verificación

```bash
python3 scripts/check-dev-ports.py
python3 scripts/check-setup.py
bash scripts/test-session.sh  # PostgreSQL temporal y pruebas backend; requiere Docker y .NET
API_URL=https://localhost:7001 bash scripts/smoke-template.sh
```

La prueba HTTP requiere curl y Python 3. Verifica la respuesta del health check y un `404` para una ruta de API no registrada.
