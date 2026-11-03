#!/usr/bin/env python3
"""Lee y conserva únicamente los ajustes de contenedores y puertos del panel."""
import os
from pathlib import Path
import re
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
SETTINGS = ROOT / '.env'
DEFAULTS = {'TECNM_FRONTEND_PORT': '5173', 'TECNM_BACKEND_PORT': '7001',
            'TECNM_FRONTEND_CONTAINER_NAME': 'TecNM_LoginControl_Frontend',
            'TECNM_BACKEND_CONTAINER_NAME': 'TecNM_LoginControl_Backend'}


def validate(key, value):
    if key.endswith('_CONTAINER_NAME'):
        if not re.fullmatch(r'[a-zA-Z0-9][a-zA-Z0-9_.-]+', value):
            raise ValueError(f'Nombre de contenedor no válido en {key}.')
        return value
    if not re.fullmatch(r'[0-9]{1,5}', value) or not 1 <= int(value) <= 65535:
        raise ValueError(f'Puerto no válido en {key}; usa un número entre 1 y 65535.')
    return str(int(value))


def load():
    values = {key: validate(key, os.environ.get(key, default)) for key, default in DEFAULTS.items()}
    source = SETTINGS if SETTINGS.exists() else ROOT / '.docker/control.env'
    if source.exists():
        for line in source.read_text().splitlines():
            key, separator, value = line.partition('=')
            key = key.strip().removeprefix('export ')
            if separator and key in values:
                match = re.fullmatch(r'''\s*(?:"([^"]*)"|'([^']*)'|([^\s#]+))\s*(?:#.*)?''', value)
                if not match:
                    raise ValueError(f'Valor no válido en {key}.')
                values[key] = validate(key, next(part for part in match.groups() if part is not None))
    if values['TECNM_FRONTEND_CONTAINER_NAME'] == values['TECNM_BACKEND_CONTAINER_NAME']:
        raise ValueError('Los contenedores deben tener nombres diferentes.')
    if not SETTINGS.exists():
        save(values)
    return values


def save(values):
    values = {key: validate(key, values[key]) for key in DEFAULTS}
    if values['TECNM_FRONTEND_CONTAINER_NAME'] == values['TECNM_BACKEND_CONTAINER_NAME']:
        raise ValueError('Los contenedores deben tener nombres diferentes.')
    lines = SETTINGS.read_text().splitlines() if SETTINGS.exists() else []
    remaining = dict(values)
    output = []
    for line in lines:
        key = line.partition('=')[0].strip().removeprefix('export ')
        if key in values:
            if key in remaining:
                output.append(f'{key}={remaining.pop(key)}')
        else:
            output.append(line)
    output.extend(f'{key}={value}' for key, value in remaining.items())
    with tempfile.NamedTemporaryFile(mode='w', dir=ROOT, prefix='.env.', delete=False) as temporary:
        temporary.write('\n'.join(output) + '\n')
    os.replace(temporary.name, SETTINGS)


if __name__ == '__main__':
    try:
        if sys.argv[1] == 'save':
            save(dict(zip(DEFAULTS, sys.argv[2:], strict=True)))
        else:
            print(' '.join(load().values()))
    except (OSError, ValueError) as error:
        print(f'No se pudo guardar o leer la configuración: {error}', file=sys.stderr)
        sys.exit(1)
