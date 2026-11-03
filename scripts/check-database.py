#!/usr/bin/env python3
"""Verifica la carga interactiva con psql simulado, sin usar bases reales."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
with tempfile.TemporaryDirectory() as directory:
    root = Path(directory)
    shutil.copytree(ROOT / 'scripts/control', root / 'scripts/control')
    (root / 'database.sql').write_text('CREATE TABLE example (id integer);\n')
    binaries = root / 'bin'
    binaries.mkdir()
    log = root / 'calls.jsonl'
    psql = binaries / 'psql'
    psql.write_text('''#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
assert os.environ['PGHOST'] == 'localhost'
assert os.environ['PGPASSWORD'] == 'temporary-secret'
assert 'temporary-secret' not in ' '.join(sys.argv)
assert '-w' in sys.argv and '-X' in sys.argv
sql = '' if '-f' in sys.argv else sys.stdin.read()
action = 'load' if '-f' in sys.argv else ('exists' if 'SELECT EXISTS' in sql else ('drop' if 'DROP DATABASE' in sql else 'create'))
with open(os.environ['CALL_LOG'], 'a') as output:
    output.write(json.dumps(action) + '\\n')
if os.environ.get('FAIL_ACTION') == action:
    sys.exit(1)
if action == 'exists': print(os.environ['EXISTS'])
if action == 'load': assert '--single-transaction' in sys.argv
''')
    psql.chmod(0o755)
    env = dict(os.environ, PATH=f'{binaries}:{os.environ["PATH"]}', CALL_LOG=str(log))

    def run(exists, confirmations, expected, code=0, fail='', target='isolated_test', mode='load'):
        log.write_text('')
        result = subprocess.run(['bash', 'scripts/control/database.sh', mode], cwd=root,
            env=dict(env, EXISTS=exists, FAIL_ACTION=fail),
            input=f'\n5432\npostgres\n{target}\ntemporary-secret\n{confirmations}',
            text=True, capture_output=True, timeout=5)
        assert result.returncode == code, result.stdout + result.stderr
        assert [json.loads(line) for line in log.read_text().splitlines()] == expected
        assert 'temporary-secret' not in result.stdout + result.stderr
        return result.stdout + result.stderr

    assert 'no existe' in run('f', 's\n', ['exists', 'create', 'load'])
    assert 'Cualquier dato contenido se perderá' in run('t', 's\nisolated_test\n', ['exists', 'drop', 'create', 'load'])
    run('f', 'n\n', ['exists'], code=2)
    run('t', 'n\n', ['exists'], code=2)
    run('t', 's\nwrong_name\n', ['exists'], code=2)
    run('t', '', ['exists'], code=2)
    run('t', 's\nisolated_test\n', ['exists', 'drop', 'create', 'load'], mode='recreate')
    run('f', 's\n', ['exists', 'create', 'load'], mode='recreate')
    assert 'No se pudo consultar' in run('f', 's\n', ['exists'], code=1, fail='exists')
    run('t', 's\nisolated_test\n', ['exists', 'drop'], code=1, fail='drop')
    run('f', 's\n', ['exists', 'create'], code=1, fail='create')
    assert 'Falló la carga' in run('f', 's\n', ['exists', 'create', 'load'], code=1, fail='load')
    run('unexpected', 's\n', ['exists'], code=1)
    for target in ['postgres', 'template0', 'template1', 'bad-name', 'a' * 64, 'db; DROP DATABASE example']:
        run('t', 's\n', [], code=1, target=target)
    (root / 'database.sql').unlink()
    run('f', 's\n', [], code=1)

print('OK: destino ausente/existente, confirmaciones, cancelaciones, fallos y bases protegidas; psql simulado.')
