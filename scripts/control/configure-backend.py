#!/usr/bin/env python3
"""Crea configuración de backend para Docker sin exponer secretos en argumentos."""
import base64
import getpass
import json
import os
from pathlib import Path
import secrets
import sys
import tempfile
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]


def ask(label, default='', secret=False, allow_empty=False):
    while True:
        prompt = f'  {label}' + (f' [{default}]' if default else '') + ': '
        value = getpass.getpass(prompt) if secret and sys.stdin.isatty() else input(prompt)
        value = value if secret else value.strip()
        value = value or default
        if (value or allow_empty) and not any(ord(character) < 32 for character in value):
            return value
        print('  Ingresa un valor válido; este campo es obligatorio.')


def connection_value(value):
    return '"' + value.replace('"', '""') + '"'


def dotenv_value(value):
    # Compose interpola cadenas dobles; $$ conserva un dólar literal.
    return json.dumps(value.replace('$', '$$'), ensure_ascii=False)


def main():
    target = ROOT / 'backend/.env'
    replacing = '--replace' in sys.argv[1:]
    if target.exists() and not replacing:
        print('  backend/.env ya existe; se conserva la configuración.')
        return 0
    print('\n  ASISTENTE / Configuración de la aplicación para Docker')
    print('  Estos datos se guardarán en backend/.env. Antes de iniciar el backend se comprobará la conexión.')
    print('  PostgreSQL está instalado en el sistema anfitrión, fuera de Docker.')
    print('  Por defecto se usará localhost:5432. Presiona Enter para aceptar cada valor.')
    print('  La comprobación previa se realiza desde el anfitrión mediante localhost.')
    print('  Usa las credenciales de la aplicación, no las de administración.\n')
    host = ask('Servidor PostgreSQL local', 'localhost')
    if host.lower() in ('localhost', '127.0.0.1', '::1'):
        print('  Para acceder desde el backend en Docker se guardará host.docker.internal.')
        host = 'host.docker.internal'
    while True:
        port = ask('Puerto PostgreSQL', '5432')
        if port.isascii() and port.isdecimal() and 1 <= int(port) <= 65535:
            break
        print('  Usa un puerto entre 1 y 65535.')
    database = ask('Base de datos', 'tecnm_template')
    username = ask('Usuario de la aplicación', 'tecnm_app')
    password = ask('Contraseña PostgreSQL (oculta)', secret=True, allow_empty=True)
    while True:
        origin = ask('Origen HTTPS del frontend', f'https://localhost:{os.environ.get("TECNM_FRONTEND_PORT", "5173")}')
        try:
            url = urlsplit(origin)
            valid = (url.scheme == 'https' and url.hostname and url.path in ('', '/')
                     and not url.query and not url.fragment and not url.username and not url.password)
            valid = valid and (url.port is None or 1 <= url.port <= 65535)
        except ValueError:
            valid = False
        if valid:
            break
        print('  Ingresa un origen HTTPS válido, sin ruta, usuario ni parámetros.')
    print('\n  Se guardará la conexión y se generará una clave de firma de sesión.')
    print('  Google y Microsoft quedan pendientes en backend/.env; el login no estará disponible.')
    if replacing:
        print('  Al guardar se reemplazará el archivo actual, incluidas las credenciales de proveedores.')
    if input('  ¿Guardar y continuar? [s/N]: ').strip().lower() != 's':
        print('  Configuración cancelada; backend/.env se conserva como estaba.')
        return 2
    values = {
        'ConnectionStrings__DefaultConnection': ';'.join(f'{key}={connection_value(value)}' for key, value in
            [('Host', host), ('Port', port), ('Database', database), ('Username', username), ('Password', password)]),
        'Session__FrontendUrl': origin.rstrip('/'),
        'Session__Issuer': 'tecnm-logincontrol',
        'Session__Audience': 'tecnm-logincontrol',
        'Session__SigningKey': base64.b64encode(secrets.token_bytes(32)).decode(),
        'Session__Google__ClientId': '', 'Session__Google__ClientSecret': '',
        'Session__Microsoft__ClientId': '', 'Session__Microsoft__ClientSecret': '',
        'Session__Microsoft__TenantId': '',
    }
    content = '# Generado por el asistente para Docker. No subir a Git.\n'
    content += '\n'.join(f'{key}={dotenv_value(value)}' for key, value in values.items()) + '\n'
    temporary_path = None
    try:
        with tempfile.NamedTemporaryFile(mode='w', dir=target.parent, prefix='.env.', delete=False) as output:
            temporary_path = Path(output.name)
            output.write(content)
        if replacing:
            os.replace(temporary_path, target)
        else:
            os.link(temporary_path, target)
    finally:
        if temporary_path is not None:
            temporary_path.unlink(missing_ok=True)
    print('  backend/.env creado. Continuando con los servicios…')
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (EOFError, KeyboardInterrupt):
        print('\n  Asistente cancelado; vuelve a intentarlo cuando estés listo.')
        sys.exit(2)
    except OSError:
        print('  No se pudo crear backend/.env; revisa permisos o si ya existe.', file=sys.stderr)
        sys.exit(1)
