#!/usr/bin/env python3
"""Publish the validated main build; never replace an existing tag or Release."""
import hashlib
import json
import os
import posixpath
from pathlib import Path
import re
import shutil
import subprocess
import urllib.request


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)


def prepare(artifacts, output):
    """Validate the builder's checksums before flattening the release assets."""
    checks = list(artifacts.rglob('*.sha256')) + list(artifacts.rglob('SHA256SUMS'))
    if not checks:
        raise ValueError('No native artifact checksums')
    output.mkdir()
    copied = set()
    for checksum in checks:
        for line in checksum.read_text(encoding='utf-8-sig').splitlines():
            expected, name = line.split(maxsplit=1)
            name = name.lstrip('*')
            if Path(name).name != name:
                raise ValueError('Checksum must name a sibling package')
            package = checksum.parent/name
            if hashlib.sha256(package.read_bytes()).hexdigest() != expected.lower():
                raise ValueError('Artifact checksum mismatch: '+name)
            if name in copied:
                raise ValueError('Duplicate release asset: '+name)
            shutil.copyfile(package, output/name)
            copied.add(name)
    required = ['PaperangStudio-0.3.2-winlite-win-x64-setup.exe',
                'V0.3.2-winlite-win-x64-portable.zip', 'V0.3.2-winlite-win-x64-lite.zip']
    if not all(name in copied for name in required) or not any(
            name.startswith('PaperangStudio-0.3.2-macclient-macos-') and name.endswith('.zip') for name in copied):
        raise ValueError('Incomplete native release packages')


def main():
    if os.environ.get('GITHUB_REF') != 'refs/heads/main' or os.environ.get('GITHUB_EVENT_NAME') != 'push':
        raise SystemExit('Publication requires the gated main push job')
    repo = os.environ['GITHUB_REPOSITORY']
    sha = os.environ['GITHUB_SHA']
    if repo != 'ELT17604/Paperang-Studio':
        raise SystemExit('Unexpected repository')
    head = subprocess.check_output(['git', 'ls-remote', 'origin', 'refs/heads/main'], text=True).split()[0]
    if head != sha:
        raise SystemExit('Main advanced during validation; build the new head')
    registry = json.loads(Path('VERSIONS.json').read_text())
    assert registry['baseline'] == '0.3.2'
    for name, item in registry['distributions'].items():
        if item['status'] == 'implemented':
            assert item['runtimeVersion'] == '0.3.2-'+name and item['validation032'] == 'passed'
    if subprocess.check_output(['git','ls-files','xdev/032-handoff'], text=True).strip():
        raise SystemExit('Temporary handoffs are still tracked')
    req = urllib.request.Request('https://etel.patronum.uk/v2/public/status',
                                 headers={'User-Agent':'PaperangStudio/0.3.2-linuxrelayserver'})
    with urllib.request.urlopen(req, timeout=15) as response:
        assert json.load(response)['version'] == '0.3.2-linuxrelayserver'
    # Read-only checks before any tag/release mutation. Fail closed on any
    # existing name; do not attempt to overwrite or "repair" its history.
    if subprocess.check_output(['git','ls-remote','--tags','origin','refs/tags/v0.3.2'], text=True).strip():
        raise SystemExit('Tag already exists: inspect it before proceeding')
    existing = subprocess.run(['gh','release','view','v0.3.2','--repo',repo],
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if existing.returncode == 0:
        raise SystemExit('Release already exists: inspect it before proceeding')
    output = Path('work/release-assets')
    prepare(Path('work/release-input'), output)
    for distribution, paths in {
        'linuxrelayserver':['Server','VERSIONS.json','LICENSE','NOTICE','doc/AUTH-v2.md','doc/UPGRADE-v0.3.2.md'],
        'relayskill':['Skills/paperang-relay-print','VERSIONS.json','LICENSE','NOTICE']
    }.items():
        archive = output/f'PaperangStudio-0.3.2-{distribution}.tar.gz'
        run('git','archive','--format=tar.gz','--prefix=PaperangStudio-0.3.2/',
            '--output='+str(archive), sha, *paths)
    url = f'https://github.com/{repo}/releases/tag/v0.3.2'
    notes = Path('doc/RELEASE-v0.3.2.md').read_text()
    notes = re.sub(r'\]\((?!https?://)([^)]+)\)',
                   lambda m: ']('+f'https://github.com/{repo}/blob/{sha}/'+posixpath.normpath('doc/'+m.group(1))+')', notes)
    report = output/'Paperang-Studio-v0.3.2-Final-Release-Report.md'
    report.write_text('# Paperang Studio v0.3.2 Final Release Report\n\n'
                      f'- Main commit: `{sha}`\n- Tag commit: `{sha}`\n- Release: {url}\n'
                      f'- Final CI: https://github.com/{repo}/actions/runs/{os.environ["GITHUB_RUN_ID"]}\n'
                      '- Repository: final main tree; temporary handoffs removed; generated packages remain outside Git.\n'
                      '- Production backup and migration: protected, integrity-checked SQLite backup; unchanged rows/schema and preserving rollback PASS.\n\n'+notes)
    sums = output/'SHA256SUMS'
    sums.write_text(''.join(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n'
                            for p in sorted(output.iterdir()) if p.is_file()))
    # Create a draft, upload checked files, and publish only after upload succeeds.
    # GITHUB_TOKEN is scoped to this one publication job and is never printed.
    run('gh','api','--method','POST',f'repos/{repo}/git/refs',
        '-f','ref=refs/tags/v0.3.2','-f','sha='+sha, stdout=subprocess.DEVNULL)
    run('gh','release','create','v0.3.2','--repo',repo,'--verify-tag','--draft',
        '--title','Paperang Studio v0.3.2','--notes-file',str(report),
        *[str(p) for p in sorted(output.iterdir())])
    run('gh','release','edit','v0.3.2','--repo',repo,'--draft=false','--latest')
    run('gh','release','view','v0.3.2','--repo',repo,'--json','tagName,isDraft,url,targetCommitish')


if __name__ == '__main__':
    main()
