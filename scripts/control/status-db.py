#!/usr/bin/env python3
"""Códigos: 0 conectado, 1 fallo, 2 sin .env, 3 inválido, 4 sin psql, 5 sin acceso."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


def check():
    path = Path(__file__).resolve().parents[2] / 'backend/.env'
    try:
        lines = path.read_text().splitlines()
    except FileNotFoundError:
        return 2
    except (OSError, UnicodeError):
        return 5
    try:
        value = next(line.split('=', 1)[1].strip() for line in lines
                     if line.strip().startswith('ConnectionStrings__DefaultConnection='))
        if value.startswith('"'):
            try:
                value = json.loads(value).replace('$$', '$')
            except ValueError:
                value = value.strip('"')
        else:
            value = value.strip("'")
        parts = {}
        pattern = r'(?:^|;)\s*([^=;]+)=\s*(?:"((?:[^"]|"")*)"|([^;]*))\s*(?=;|$)'
        for match in re.finditer(pattern, value):
            key, quoted, plain = match.groups()
            parts[key.strip().lower()] = quoted.replace('""', '"') if quoted is not None else plain.strip()
        host = parts.get('host', '')
        database = parts.get('database', '')
        username = parts.get('username', parts.get('user id', ''))
        port = parts.get('port', '5432')
        if not host or not database or not username or not port.isascii() or not port.isdecimal() or not 1 <= int(port) <= 65535:
            return 3
    except (StopIteration, ValueError, TypeError):
        return 3
    if not shutil.which('psql'):
        return 4
    if host == 'host.docker.internal':
        host = 'localhost'
    env = os.environ.copy()
    env.update(PGHOST=host, PGPORT=port, PGDATABASE=database, PGUSER=username,
               PGPASSWORD=parts.get('password', ''), PGCONNECT_TIMEOUT='3')
    try:
        result = subprocess.run(['psql', '-X', '-w', '-tAc', 'SELECT 1=1'], env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True, timeout=5)
        return 0 if result.returncode == 0 and result.stdout.strip() == 't' else 1
    except (OSError, subprocess.TimeoutExpired):
        return 1


if __name__ == '__main__':
    sys.exit(check())
