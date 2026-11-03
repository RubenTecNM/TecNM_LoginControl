#!/usr/bin/env python3
"""Regresión: recrear elimina contenedores por nombre y ejecuta creación real con Docker simulado."""
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    shutil.copy(ROOT / 'control.sh', root / 'control.sh')
    shutil.copytree(ROOT / 'scripts/control', root / 'scripts/control')
    binaries = root / 'bin'
    binaries.mkdir()
    log = root / 'actions.log'
    state = root / 'containers.json'
    env = dict(os.environ, PATH=f'{binaries}:{os.environ["PATH"]}', ACTION_LOG=str(log), STATE_FILE=str(state))
    for name, body in {
        'prepare-services.sh': 'echo prepare >> "$ACTION_LOG"; exit "${PREPARE_RESULT:-0}"',
        'configure-containers.sh': 'echo names >> "$ACTION_LOG"; if [[ ${CHANGE_NAME:-} ]]; then python3 scripts/control/settings.py save "$TECNM_FRONTEND_PORT" "$TECNM_BACKEND_PORT" changed_frontend "$TECNM_BACKEND_CONTAINER_NAME"; fi; exit "${NAMES_RESULT:-0}"',
        'status.sh': 'echo "ESTADO SIMULADO"',
    }.items():
        (root / 'scripts/control' / name).write_text('#!/usr/bin/env bash\n' + body + '\n')
    certs = root / '.docker/certs'
    certs.mkdir(parents=True)
    (certs / 'localhost.crt').write_text('simulated')
    (certs / 'localhost.key').write_text('simulated')
    # Usar puertos libres para que la creación llegue a Docker sin interacción extra.
    sockets = [socket.socket(), socket.socket()]
    for listener in sockets: listener.bind(('127.0.0.1', 0))
    ports = [str(listener.getsockname()[1]) for listener in sockets]
    for listener in sockets: listener.close()
    front, back = 'recorded_frontend', 'recorded_backend'
    subprocess.run(['python3', 'scripts/control/settings.py', 'save', *ports, front, back], cwd=root, check=True)
    docker = binaries / 'docker'
    docker.write_text('''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args = sys.argv[1:]
path = Path(os.environ['STATE_FILE'])
containers = json.loads(path.read_text())
def record(value):
    with open(os.environ['ACTION_LOG'], 'a') as output: output.write(value + '\\n')
if args[0] == 'inspect':
    name = args[-1]
    if name not in containers: sys.exit(1)
    print('true' if args[2] == '{{.State.Running}}' else 'existing-id')
    sys.exit(0)
if args[0] == 'rm':
    name = args[-1]
    record('remove ' + name)
    if os.environ.get('FAIL_REMOVE'): sys.exit(1)
    if not os.environ.get('KEEP_CONTAINER'):
        containers.pop(name, None)
        path.write_text(json.dumps(containers))
    sys.exit(0)
if args[0] == 'compose' and args[3] == 'rm':
    record('compose-rm ' + ' '.join(args[5:]))
    # Caso reproducible: Compose no identifica el contenedor por sus etiquetas.
    sys.exit(0)
if args[0] == 'compose' and args[3] == 'up':
    service = args[-1]
    name = os.environ['TECNM_' + service.upper() + '_CONTAINER_NAME']
    if os.environ.get('EXPECTED_PORT'):
        assert os.environ['TECNM_' + service.upper() + '_PORT'] == os.environ['EXPECTED_PORT']
    assert name not in containers, 'Se intentó crear sin eliminar el anterior.'
    record('build ' + name)
    containers[name] = True
    path.write_text(json.dumps(containers))
    sys.exit(0)
raise AssertionError(args)
''')
    docker.chmod(0o755)

    def run(choice, confirm='s', **extra):
        state.write_text(json.dumps({front: True, back: True}))
        log.write_text('')
        result = subprocess.run(['bash', 'control.sh'], cwd=root, env=dict(env, **extra),
            input=f'3\n{choice}\n' + (f'{confirm}\n' + '\n' * (2 if choice == '3' else 1) + '0\n' if confirm is not None else ''), text=True, capture_output=True, timeout=10)
        assert result.returncode == 0, result.stderr
        return log.read_text().splitlines(), result.stdout + result.stderr

    for choice, names, services in [('1', [front], 'frontend'), ('2', [back], 'backend'),
                                    ('3', [back, front], 'backend frontend')]:
        actions, output = run(choice)
        assert actions == ['prepare', 'names', 'compose-rm ' + services] + ['remove ' + name for name in names] + ['build ' + name for name in names], actions
        assert 'ya está activo' not in output
        assert '¿Estás de acuerdo?' in output
        for name in names:
            assert f'Se eliminará el contenedor "{name}"' in output
        assert set(json.loads(state.read_text())) == {front, back}
    previous_settings = (root / '.env').read_bytes()
    for choice in ['1', '2', '3']:
        for confirm in ['n', '', 'x', None]:
            actions, output = run(choice, confirm=confirm)
            assert actions == [], actions
            assert 'Operación cancelada' in output
            assert json.loads(state.read_text()) == {front: True, back: True}
            assert (root / '.env').read_bytes() == previous_settings
    state.write_text(json.dumps({front: True, back: True}))
    log.write_text('')
    cancelled_port = subprocess.run(['bash', 'control.sh'], cwd=root, env=dict(env, CHANGE_NAME='1'),
        input='3\n1\ns\n', text=True, capture_output=True, timeout=10)
    assert 'Operación cancelada' in cancelled_port.stdout
    assert log.read_text().splitlines() == ['prepare', 'names']
    assert json.loads(state.read_text()) == {front: True, back: True}
    assert (root / '.env').read_bytes() == previous_settings
    actions, _ = run('1', confirm='S')
    assert 'build ' + front in actions
    for extra in [dict(FAIL_REMOVE='1'), dict(KEEP_CONTAINER='1'), dict(PREPARE_RESULT='1'), dict(NAMES_RESULT='2')]:
        actions, output = run('1', **extra)
        assert not any(action.startswith('build ') for action in actions), actions
        assert 'Acción finalizada' not in output
    state.write_text(json.dumps({front: True}))
    active = subprocess.run(['bash', 'scripts/control/create.sh', 'frontend', '--prepared'], cwd=root,
                           env=env, text=True, capture_output=True, timeout=5)
    assert active.returncode == 1, 'Crear no debe informar éxito si sigue el contenedor anterior activo.'

    state.write_text('{}')
    before_cancel = (root / '.env').read_bytes()
    cancelled = subprocess.run(['bash', 'scripts/control/create.sh', 'frontend', '--prepared'],
        cwd=root, env=env, input='', text=True, capture_output=True, timeout=5)
    assert cancelled.returncode == 2
    assert (root / '.env').read_bytes() == before_cancel
    with socket.socket() as busy, socket.socket() as free:
        busy.bind(('127.0.0.2', 0))
        free.bind(('127.0.0.1', 0))
        busy_port = str(busy.getsockname()[1])
        chosen_port = str(free.getsockname()[1])
        free.close()
        selected = subprocess.run(['bash', 'scripts/control/create.sh', 'frontend', '--prepared'],
            cwd=root, env=dict(env, EXPECTED_PORT=chosen_port),
            input=f'invalid\n{busy_port}\n{chosen_port}\n', text=True, capture_output=True, timeout=5)
        assert selected.returncode == 0, selected.stdout + selected.stderr
        assert 'no está disponible' in selected.stdout
        assert f'TECNM_FRONTEND_PORT={chosen_port}' in (root / '.env').read_text()

    try:
        ipv6_busy = socket.socket(socket.AF_INET6)
        ipv6_busy.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
        ipv6_busy.bind(('::1', 0))
    except OSError:
        if 'ipv6_busy' in locals(): ipv6_busy.close()
    else:
        with ipv6_busy, socket.socket() as free:
            state.write_text('{}')
            free.bind(('127.0.0.1', 0))
            ipv6_port = str(ipv6_busy.getsockname()[1])
            chosen_port = str(free.getsockname()[1])
            free.close()
            selected = subprocess.run(['bash', 'scripts/control/create.sh', 'frontend', '--prepared'],
                cwd=root, env=dict(env, EXPECTED_PORT=chosen_port),
                input=f'{ipv6_port}\n{chosen_port}\n', text=True, capture_output=True, timeout=5)
            assert selected.returncode == 0, selected.stdout + selected.stderr
            assert 'no está disponible' in selected.stdout

print('OK: confirmación/cancelación, recreación completa individual/ambos, contenedores sin etiquetas y fallos que impiden crear; Docker simulado.')
