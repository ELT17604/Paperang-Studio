#!/usr/bin/env python3
"""Validate 0.3.2 targets separately from unverified platform runtime labels."""
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
    assert item.get('targetVersion') == baseline
    assert item['validation032'] in ('pending', 'passed', 'blocked')
    assert re.fullmatch(r'v\d+\.\d+\.\d+-' + name + r'(?:-(?:fix|opt|alpha|beta|rc)[1-9]\d*)?', item['version'])
    source = (root / item['versionSource']).read_text(encoding='utf-8').strip()
    runtime = item['runtimeVersion']
    assert item['version'] == 'v' + runtime
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
assert registry['compatibility']['localApi'] == registry['compatibility']['relayApi'] == 'v2'
assert (root / 'VERSION').read_text().strip() == baseline + '-macclient'
print('PASS product targets, historical platform runtimes, API baseline and policy/matrix')

for project in ('Paperang.Core', 'Paperang.WinLite'):
    metadata = (root / f'Windows/src/{project}/{project}.csproj').read_text(encoding='utf-8')
    assert f'<Version>{baseline}</Version>' in metadata
    assert f'<AssemblyVersion>{baseline}.0</AssemblyVersion>' in metadata
    assert f'<FileVersion>{baseline}.0</FileVersion>' in metadata
    assert f'<InformationalVersion>{win}</InformationalVersion>' in metadata
installer = (root / 'Windows/installer/WinLite.iss').read_text(encoding='utf-8')
assert f'#define AppVersion "{win}"' in installer
assert f'VersionInfoVersion={baseline}.0' in installer
print('PASS Windows assembly/file/installer numeric versions and distribution suffix')
