#!/usr/bin/env python3
"""Validate coordinated product/runtime versions and independent API/schema domains."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
registry = json.loads((root / 'VERSIONS.json').read_text(encoding='utf-8'))
baseline = registry['baseline']
assert re.fullmatch(r'\d+\.\d+\.\d+', baseline)
expected = {'macclient', 'linuxrelayserver', 'winlite', 'winclient', 'linuxclient'}
assert set(registry['distributions']) == expected
exceptions = registry.get('distributionExceptions', {})
assert set(exceptions) == {'winlite', 'macclient'}
for exception in exceptions.values():
    assert exception['version'] == '0.3.3' and exception['compatibleBaseline'] == baseline == '0.3.2'
    assert (root / exception['documentation']).is_file()
for name, item in registry['distributions'].items():
    if item['status'] == 'planned':
        assert item['version'] is None and item['runtimeVersion'] is None
        continue
    target = exceptions.get(name, {}).get('version', baseline)
    assert item.get('targetVersion') == target
    if name in exceptions: assert item['compatibleBaseline'] == baseline
    assert item['validation032'] in ('pending', 'passed', 'blocked')
    assert re.fullmatch(r'v\d+\.\d+\.\d+-' + name + r'(?:-(?:fix|opt|alpha|beta|rc)[1-9]\d*)?', item['version'])
    source = (root / item['versionSource']).read_text(encoding='utf-8').strip()
    runtime = item['runtimeVersion']
    assert runtime.split('-', 1)[0] == target
    assert item['version'] == 'v' + runtime
    if name == 'linuxrelayserver':
        assert re.search(r"APP_VERSION\s*=\s*['\"]" + re.escape(runtime) + r"['\"]", source)
    else:
        assert source == runtime
win = registry['distributions']['winlite']['runtimeVersion']
win_number = win.split('-', 1)[0]
assert f'Current = "{win}"' in (root / 'Windows/src/Paperang.Core/AppVersion.cs').read_text(encoding='utf-8')
assert f'<InformationalVersion>{win}</InformationalVersion>' in (root / 'Windows/src/Paperang.WinLite/Paperang.WinLite.csproj').read_text(encoding='utf-8')
for doc in ('VERSIONING', 'FEATURE-MATRIX'):
    assert (root / f'doc/{doc}.md').exists() and (root / f'doc/{doc}.zh-CN.md').exists()
assert registry['compatibility']['localApi'] == registry['compatibility']['relayApi'] == 'v2'
assert (root / 'VERSION').read_text().strip() == registry['distributions']['macclient']['runtimeVersion']
print('PASS product/component versions, API baseline and policy/matrix')

for project in ('Paperang.Core', 'Paperang.WinLite'):
    metadata = (root / f'Windows/src/{project}/{project}.csproj').read_text(encoding='utf-8')
    assert f'<Version>{win_number}</Version>' in metadata
    assert f'<AssemblyVersion>{win_number}.0</AssemblyVersion>' in metadata
    assert f'<FileVersion>{win_number}.0</FileVersion>' in metadata
    assert f'<InformationalVersion>{win}</InformationalVersion>' in metadata
installer = (root / 'Windows/installer/WinLite.iss').read_text(encoding='utf-8')
assert f'#define AppVersion "{win}"' in installer
assert f'VersionInfoVersion={win_number}.0' in installer
print('PASS Windows assembly/file/installer numeric versions and distribution suffix')
