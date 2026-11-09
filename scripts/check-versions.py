#!/usr/bin/env python3
"""Validate distribution labels without changing deployed legacy runtime aliases."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
registry = json.loads((root / 'VERSIONS.json').read_text(encoding='utf-8'))
baseline = registry['baseline']
assert re.fullmatch(r'\d+\.\d+\.\d+', baseline)
expected = {'macclient', 'linuxrelayserver', 'winlite', 'relayskill', 'winclient', 'linuxclient'}
assert set(registry['distributions']) == expected
for name, item in registry['distributions'].items():
    if item['status'] == 'planned':
        assert item['version'] is None and item['runtimeVersion'] is None
        continue
    assert re.fullmatch(r'v' + re.escape(baseline) + '-' + name + r'(?:-(?:fix|opt|alpha|beta|rc)[1-9]\d*)?', item['version'])
    source = (root / item['versionSource']).read_text(encoding='utf-8').strip()
    runtime = item['runtimeVersion']
    assert runtime.split('-')[0] == baseline
    if name == 'linuxrelayserver':
        assert re.search(r"APP_VERSION\s*=\s*['\"]" + re.escape(runtime) + r"['\"]", source)
    else:
        assert source == runtime
win = registry['distributions']['winlite']['runtimeVersion']
assert f'Current = "{win}"' in (root / 'Windows/src/Paperang.Core/AppVersion.cs').read_text(encoding='utf-8')
assert f'<InformationalVersion>{win}</InformationalVersion>' in (root / 'Windows/src/Paperang.WinLite/Paperang.WinLite.csproj').read_text(encoding='utf-8')
for doc in ('VERSIONING', 'FEATURE-MATRIX'):
    assert (root / f'doc/{doc}.md').exists() and (root / f'doc/{doc}.zh-CN.md').exists()
skill = registry['distributions']['relayskill']['runtimeVersion']
assert f"__version__ = '{skill}'" in (root / 'Skills/paperang-relay-print/scripts/relay_print.py').read_text(encoding='utf-8')
print('PASS distribution naming, runtime aliases, baseline and bilingual policy/matrix')
