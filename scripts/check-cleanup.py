#!/usr/bin/env python3
"""Verifica confirmaciones y fallos de limpieza con Docker y PostgreSQL simulados."""
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
    log = root / 'actions.log'
    (binaries / 'docker').write_text('''#!/usr/bin/env bash
if [[ $1 == info ]]; then exit "${FAIL_INFO:-0}"; fi
printf '%s\\n' containers >> "$ACTION_LOG"
exit "${FAIL_CONTAINERS:-0}"
''')
    (binaries / 'psql').write_text('''#!/usr/bin/env bash
sql=$(cat)
if [[ $sql == *'SELECT EXISTS'* ]]; then
  echo exists >> "$ACTION_LOG"
  [[ ${FAIL_QUERY:-0} == 0 ]] || exit 1
  echo "$EXISTS"
else
  echo drop >> "$ACTION_LOG"
  exit "${FAIL_DROP:-0}"
fi
''')
    for binary in binaries.iterdir():
        binary.chmod(0o755)
    env = dict(os.environ, PATH=f'{binaries}:{os.environ["PATH"]}', ACTION_LOG=str(log), EXISTS='t')

    def run(confirmations, expected, code=0, target='isolated_test', **settings):
        log.write_text('')
        result = subprocess.run(['bash', 'scripts/control/cleanup.sh'], cwd=root, env=dict(env, **settings),
            input=f'\n\npostgres\n{target}\nsecret\n{confirmations}', text=True, capture_output=True, timeout=5)
        assert result.returncode == code, result.stdout + result.stderr
        assert log.read_text().splitlines() == expected, log.read_text()
        assert 'secret' not in result.stdout + result.stderr
        return result.stdout + result.stderr

    for confirmation in ('', 'no\n', 'acepto\n', 'ACEPTO\n', 'ACEPTO\nwrong\n'):
        run(confirmation, ['exists'], code=2)
    run('ACEPTO\nisolated_test\n', ['exists', 'containers', 'drop'])
    assert 'no existe' in run('ACEPTO\nisolated_test\n', ['exists', 'containers'], EXISTS='f')
    run('ACEPTO\nisolated_test\n', ['exists'], code=1, FAIL_QUERY='1')
    run('ACEPTO\nisolated_test\n', ['exists'], code=1, EXISTS='unexpected')
    run('ACEPTO\nisolated_test\n', ['exists', 'containers'], code=1, FAIL_CONTAINERS='1')
    assert 'no se pudo eliminar la base' in run('ACEPTO\nisolated_test\n', ['exists', 'containers', 'drop'], code=1, FAIL_DROP='1')
    run('ACEPTO\nisolated_test\n', [], code=1, FAIL_INFO='1')
    for target in ('postgres', 'template0', 'template1', 'invalid-name'):
        run('', [], code=1, target=target)
print('OK: limpieza, confirmaciones, base ausente, protección y fallos; Docker y psql simulados.')
