#!/usr/bin/env python3
"""Prueba el panel con acciones simuladas y una pseudoterminal aislada."""
import json
import os
from pathlib import Path
import pty
import select
import shutil
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    shutil.copy(ROOT / 'control.sh', root / 'control.sh')
    shutil.copytree(ROOT / 'scripts/control', root / 'scripts/control')
    binaries = root / 'bin'
    binaries.mkdir()
    log = root / 'actions.log'
    env = dict(os.environ, PATH=f'{binaries}:{os.environ["PATH"]}', ACTION_LOG=str(log), TERM='xterm')
    def ports(*arguments, succeeds=True):
        if arguments[0] == 'save':
            arguments = (*arguments, 'TecNM_LoginControl_Frontend', 'TecNM_LoginControl_Backend')
        result = subprocess.run(['python3', 'scripts/control/settings.py', *arguments], cwd=root,
                                env=env, text=True, capture_output=True, timeout=5)
        assert (result.returncode == 0) == succeeds, result.stderr
        return ' '.join(result.stdout.split()[:2])

    legacy = root / '.docker/control.env'
    legacy.parent.mkdir()
    legacy.write_text('TECNM_FRONTEND_PORT=5188\nTECNM_BACKEND_PORT=7088\n')
    assert ports('load') == '5188 7088'
    settings = root / '.env'
    settings.write_text(settings.read_text() + '# Conservar comentario\nOTHER_SETTING=example\n')
    ports('save', '5199', '7099')
    assert ports('load') == '5199 7099'
    assert 'OTHER_SETTING=example' in settings.read_text()
    assert '# Conservar comentario' in settings.read_text()
    assert settings.stat().st_mode & 0o777 == 0o600
    settings.write_text('TECNM_FRONTEND_PORT=$(touch injected)\n')
    ports('load', succeeds=False)
    assert not (root / 'injected').exists()
    settings.write_text('TECNM_FRONTEND_PORT=65536\n')
    ports('load', succeeds=False)
    ports('save', '5173', '7001')

    (root / 'backend').mkdir()
    backend_env = root / 'backend/.env'

    def wizard(text):
        return subprocess.run(['bash', 'scripts/control/configure-backend.sh'], cwd=root, env=env,
                              input=text, text=True, capture_output=True, timeout=5)

    assert wizard('0\n').returncode == 2
    assert not backend_env.exists()
    fields = '\n0\n5432\n\n\nwizard-secret;$\\"\n\n'
    assert wizard('1\n' + fields + 'n\n').returncode == 2
    assert not backend_env.exists()
    assert wizard('1\n').returncode == 2
    assert not backend_env.exists()
    created = wizard('1\n' + fields + 's\n')
    assert created.returncode == 0, created.stderr
    assert 'Servidor PostgreSQL local [localhost]:' in created.stdout
    assert 'Puerto PostgreSQL [5432]:' in created.stdout
    assert 'wizard-secret' not in created.stdout + created.stderr
    assert backend_env.stat().st_mode & 0o777 == 0o600
    saved = backend_env.read_bytes()
    assert wizard('1\n').returncode == 0
    assert backend_env.read_bytes() == saved
    assert wizard('2\n' + fields + 'n\n').returncode == 2
    assert backend_env.read_bytes() == saved
    assert wizard('2\n').returncode == 2
    assert backend_env.read_bytes() == saved
    defaults = '\n\n\n\nwizard-secret;$\\"\n\n'
    assert wizard('2\n' + defaults + 's\n').returncode == 0
    assert backend_env.read_bytes() != saved
    saved = backend_env.read_bytes()
    empty_password = wizard('2\n\n\n\nroot\n\n\ns\n')
    assert empty_password.returncode == 0, empty_password.stderr
    assert 'este campo es obligatorio' not in empty_password.stdout
    connection_line = next(line for line in backend_env.read_text().splitlines()
                           if line.startswith('ConnectionStrings__DefaultConnection='))
    connection = json.loads(connection_line.split('=', 1)[1])
    assert 'Username="root";Password=""' in connection
    backend_env.write_bytes(saved)
    # La comprobación SQL debe recuperar también contraseñas con delimitadores.
    psql = binaries / 'psql'
    psql.write_text('#!/usr/bin/env python3\nimport os, sys\nif os.environ.get("FAIL_DATABASE"): sys.exit(1)\nassert os.environ["PGHOST"] == "localhost"\nassert os.environ["PGPASSWORD"] == ' +
                    repr('wizard-secret;$\\"') + '\nprint("t")\n')
    psql.chmod(0o755)
    checked = subprocess.run(['python3', 'scripts/control/status-db.py'], cwd=root, env=env, timeout=5)
    assert checked.returncode == 0

    status_docker = binaries / 'docker'
    status_docker.write_text('''#!/usr/bin/env python3
import os, sys
if sys.argv[1] == 'info': sys.exit(0)
if sys.argv[1] == 'inspect':
    with open(os.environ['INSPECT_LOG'], 'a') as output: output.write(sys.argv[-1] + '\\n')
    state = os.environ.get('CONTAINER_STATE', 'true')
    if state == 'missing': sys.exit(1)
    print(state)
    sys.exit(0)
raise AssertionError(sys.argv)
''')
    status_docker.chmod(0o755)
    inspect_log = root / 'inspected.log'
    env['INSPECT_LOG'] = str(inspect_log)

    # El estado diferencia falta de configuración de una conexión fallida.
    curl = binaries / 'curl'
    curl.write_text('''#!/usr/bin/env python3
import os, sys
url = sys.argv[-1]
if url.endswith('/api/health/database'):
    with open(os.environ['API_LOG'], 'a') as output: output.write('checked\\n')
    ready = os.environ.get('API_READY') == '1'
elif url.endswith('/api/health'):
    if os.environ.get('BACKEND_HTTP_STATUS'):
        print(os.environ['BACKEND_HTTP_STATUS'])
        sys.exit(0)
    ready = os.environ.get('BACKEND_READY') == '1'
else:
    ready = os.environ.get('FRONTEND_READY') == '1'
if ready and '-w' in sys.argv: print('200')
sys.exit(0 if ready else 1)
''')
    curl.chmod(0o755)
    api_log = root / 'api-checks.log'
    env['API_LOG'] = str(api_log)

    def status(**variables):
        api_log.write_text('')
        result = subprocess.run(['bash', 'scripts/control/status.sh'], cwd=root,
            env=dict(env, **variables), text=True, capture_output=True, timeout=5)
        assert result.returncode == 0, result.stderr
        assert 'wizard-secret' not in result.stdout + result.stderr
        return {line.strip().split('  ')[0]: line for line in result.stdout.splitlines()}

    backend_env.unlink()
    rows = status()
    assert 'SIN CONFIGURAR' in rows['PostgreSQL'] and 'Falta backend/.env' in rows['PostgreSQL']
    assert 'SIN COMPROBAR' in rows['API / PostgreSQL']
    assert not api_log.read_text()
    backend_env.write_text('ConnectionStrings__DefaultConnection=\n')
    rows = status()
    assert 'SIN CONFIGURAR' in rows['PostgreSQL'] and 'cadena de conexión' in rows['PostgreSQL']
    assert not api_log.read_text()
    backend_env.write_bytes(saved)
    rows = status(FAIL_DATABASE='1')
    assert 'SIN CONEXIÓN' in rows['PostgreSQL']
    assert 'SIN COMPROBAR' in rows['API / PostgreSQL'] and 'Backend aún no responde' in rows['API / PostgreSQL']
    assert not api_log.read_text()
    rows = status(BACKEND_READY='1')
    assert 'ACTIVO' in rows['PostgreSQL']
    assert 'SIN CONEXIÓN' in rows['API / PostgreSQL'] and api_log.read_text()
    rows = status(BACKEND_HTTP_STATUS='500')
    assert 'ERROR HTTP' in rows['Backend'] and 'HTTP 500' in rows['Backend']
    assert 'no responde' not in rows['Backend']
    assert 'HTTP 500' in rows['API / PostgreSQL']
    rows = status(BACKEND_READY='1', API_READY='1')
    assert 'ACTIVO' in rows['API / PostgreSQL']
    rows = status(CONTAINER_STATE='false')
    assert 'DETENIDO' in rows['Backend']
    rows = status(CONTAINER_STATE='missing')
    assert 'SIN CREAR' in rows['Backend']
    subprocess.run(['python3', 'scripts/control/settings.py', 'save', '5173', '7001', 'saved_frontend', 'saved_backend'],
                   cwd=root, env=env, check=True)
    inspect_log.write_text('')
    rows = status()
    assert inspect_log.read_text().splitlines() == ['saved_frontend', 'saved_backend']
    ports('save', '5173', '7001')
    missing_client = subprocess.run(['python3', '-c',
        "import runpy; from unittest.mock import patch; module = runpy.run_path('scripts/control/status-db.py'); "
        "context = patch('shutil.which', return_value=None); context.start(); assert module['check']() == 4"],
        cwd=root, env=env, timeout=5)
    assert missing_client.returncode == 0

    for name, body in {
        'configure-containers.sh': 'exit 0',
        'configure-port.sh': 'exit 0',
        'create.sh': 'echo "create $1" >> "$ACTION_LOG"; [[ ${FAIL_SERVICE:-} != "$1" ]]',
        'start.sh': 'echo "start $1" >> "$ACTION_LOG"; [[ ${FAIL_SERVICE:-} != "$1" ]]',
        'database.sh': 'echo "database $1" >> "$ACTION_LOG"',
        'cleanup.sh': 'echo cleanup >> "$ACTION_LOG"',
        'status.sh': 'echo "ESTADO SIMULADO"',
    }.items():
        (root / 'scripts/control' / name).write_text('#!/usr/bin/env bash\n' + body + '\n')
    docker = binaries / 'docker'
    docker.write_text('#!/usr/bin/env bash\n[[ $1 != inspect ]] || exit 1\nprintf "%s\\n" "$*" >> "$ACTION_LOG"\n')
    docker.chmod(0o755)

    def run(keys, expected, fail=''):
        log.write_text('')
        result = subprocess.run(['bash', 'control.sh'], cwd=root, env=dict(env, FAIL_SERVICE=fail),
                                input=keys, text=True, capture_output=True, timeout=5)
        assert result.returncode == 0, result.stderr
        assert log.read_text().splitlines() == expected, log.read_text()
        assert '\x1b' not in result.stdout, 'La salida redirigida debe estar libre de ANSI.'
        return result.stdout + result.stderr

    backend_env.unlink()
    cancelled = run('1\n3\n0\n0\n', [])
    assert 'Operación cancelada' in cancelled
    assert not backend_env.exists()
    backend_env.write_bytes(saved)

    env['FAIL_DATABASE'] = '1'
    disconnected = run('3\n3\ns\n1\n0\n', [])
    assert 'No se pudo conectar con PostgreSQL' in disconnected
    env.pop('FAIL_DATABASE')

    run('3\n1\nn\n0\n', [])
    run('3\n3\n\n0\n', [])
    run('1\n1\n1\n0\n', ['create frontend'])
    run('1\n2\n1\n0\n', ['create backend'])
    run('1\n3\n1\n0\n', ['create backend', 'create frontend'])
    run('2\n1\n0\n', ['start frontend'])
    run('2\n2\n0\n', ['start backend'])
    run('2\n3\n0\n', ['start backend', 'start frontend'])
    for choice, names in [('1', ['frontend']), ('2', ['backend']), ('3', ['backend', 'frontend'])]:
        run(f'3\n{choice}\ns\n1\n0\n', ['compose -f compose.yaml rm -sf ' + ' '.join(names)] +
            ['create ' + name for name in names])
    run('1\n0\n2\n0\n3\n0\nR\nx\n0\n', [])
    run('1\nx\n2\n1\n0\n', ['create backend'])
    run('4\n5\n6\n7\n8\n0\n', ['database load', 'database recreate',
        'compose -f compose.yaml stop', 'compose -f compose.yaml logs --tail=100 -f', 'cleanup'])
    output = run('1\n3\n1\n0\n', ['create backend'], fail='backend')
    assert 'La acción no se completó' in output
    result = subprocess.run(['bash', 'control.sh'], cwd=root, env=env, input='',
                            text=True, capture_output=True, timeout=5)
    assert result.returncode == 0

    # Se envían teclas sueltas solo después de que aparece cada pantalla.
    log.write_text('')
    master, slave = pty.openpty()
    process = subprocess.Popen(['bash', 'control.sh'], cwd=root, env=env, stdin=slave,
                               stdout=slave, stderr=slave, start_new_session=True)
    os.close(slave)
    pending = b''

    def expect(value):
        global pending
        needle = value.encode()
        deadline = time.monotonic() + 5
        while needle not in pending:
            if time.monotonic() >= deadline:
                raise AssertionError(f'No apareció {value!r}: {pending!r}')
            ready, _, _ = select.select([master], [], [], 0.1)
            if ready:
                pending += os.read(master, 65536)
        pending = pending.split(needle, 1)[1]

    try:
        expect('Selecciona una tecla (sin Enter):')
        os.write(master, b'1')
        expect('Crear servicios / Seleccionar servicios')
        expect('Selecciona una tecla:')
        os.write(master, b'3')
        expect('Se detectó que backend/.env ya existe.')
        expect('Selecciona una tecla:')
        os.write(master, b'1')
        expect('Acción finalizada.')
        expect('Presiona una tecla para volver.')
        os.write(master, b'v')
        expect('Selecciona una tecla (sin Enter):')
        os.write(master, b'0')
        process.wait(timeout=5)
        assert process.returncode == 0
        assert log.read_text().splitlines() == ['create backend', 'create frontend']
    finally:
        if process.poll() is None:
            process.kill()
            process.wait()
        os.close(master)

print('OK: estados contextuales, asistente, conexión previa, puertos persistentes, migración, submenús, retorno, acciones, orden, fallos y selección sin Enter; recursos simulados.')
