#!/usr/bin/env python3
"""Comprueba selección y persistencia de nombres sin administrar Docker real."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    shutil.copytree(ROOT / 'scripts/control', root / 'scripts/control')
    binaries = root / 'bin'
    binaries.mkdir()
    docker = binaries / 'docker'
    docker.write_text('''#!/usr/bin/env python3
import os, sys
args = sys.argv[1:]
if args[0] == 'info': sys.exit(1 if os.environ.get('DOCKER_DOWN') else 0)
if args[0] == 'compose':
    assert args[3:6] == ['ps', '--all', '--quiet']
    if args[-1] == 'backend': print('own-id')
    sys.exit(0)
if args[0] == 'start':
    assert args[-1] == 'custom_frontend'
    print('started ' + args[-1])
    sys.exit(0)
if args[0] == 'inspect':
    if args[2] == '{{.State.Running}}':
        if os.environ.get('CONTAINER_MISSING'): sys.exit(1)
        assert args[-1] == os.environ.get('EXPECTED_NAME', 'backend_existing')
        print(os.environ.get('RUNNING', 'true'))
        sys.exit(0)
    if args[-1] == 'occupied': print('foreign-id'); sys.exit(0)
    if args[-1] == 'backend_existing': print('own-id'); sys.exit(0)
    sys.exit(1)
raise AssertionError('Operación inesperada: ' + str(args))
''')
    docker.chmod(0o755)
    env = dict(os.environ, PATH=f'{binaries}:{os.environ["PATH"]}')
    def run(services, text, code=0, **extra):
        result = subprocess.run(['bash', 'scripts/control/configure-containers.sh', *services],
            cwd=root, env=dict(env, **extra), input=text, text=True, capture_output=True, timeout=5)
        assert result.returncode == code, result.stdout + result.stderr
        return result.stdout + result.stderr
    output = run(['backend'], 'bad name\noccupied\nTecNM_LoginControl_Frontend\ncustom_backend\n')
    assert 'Nombre no válido' in output and 'ocupado' in output and 'otro servicio' in output
    settings = root / '.env'
    assert 'TECNM_BACKEND_CONTAINER_NAME=custom_backend' in settings.read_text()
    run(['frontend'], 'custom_frontend\n')
    assert 'TECNM_FRONTEND_CONTAINER_NAME=custom_frontend' in settings.read_text()
    assert 'TECNM_BACKEND_CONTAINER_NAME=custom_backend' in settings.read_text()
    previous = settings.read_bytes()
    run(['frontend', 'backend'], '\n\n')
    assert settings.read_bytes() == previous
    run(['backend', 'frontend'], 'another_backend\n', code=2)
    assert settings.read_bytes() == previous
    run(['backend'], 'new_backend\n', code=1, DOCKER_DOWN='1')
    assert settings.read_bytes() == previous
    invalid = subprocess.run(['python3', 'scripts/control/settings.py', 'save', '5173', '7001', 'same_name', 'same_name'],
        cwd=root, env=env, capture_output=True, timeout=5)
    assert invalid.returncode == 1 and settings.read_bytes() == previous
    run(['backend'], 'backend_existing\n')
    assert 'TECNM_BACKEND_CONTAINER_NAME=backend_existing' in settings.read_text()
    running = subprocess.run(['bash', 'scripts/control/start.sh', 'backend'],
                             cwd=root, env=env, text=True, capture_output=True, timeout=5)
    assert running.returncode == 0 and 'ya está activo' in running.stdout, running.stderr

    previous = settings.read_bytes()
    stopped = subprocess.run(['bash', 'scripts/control/start.sh', 'frontend'], cwd=root,
        env=dict(env, EXPECTED_NAME='custom_frontend', RUNNING='false'), input='',
        text=True, capture_output=True, timeout=5)
    assert stopped.returncode == 0 and 'started custom_frontend' in stopped.stdout, stopped.stderr
    assert settings.read_bytes() == previous
    missing = subprocess.run(['bash', 'scripts/control/start.sh', 'frontend'], cwd=root,
        env=dict(env, CONTAINER_MISSING='1'), input='', text=True, capture_output=True, timeout=5)
    assert missing.returncode == 1 and 'Usa Crear servicios' in missing.stderr
    assert settings.read_bytes() == previous

print('OK: nombres personalizados, conflictos, cancelación, persistencia y reconocimiento del contenedor propio; Docker simulado.')
