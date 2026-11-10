#!/usr/bin/env python3
"""Pinned, data-preserving upgrade for the existing /home/elt/paperang host.

No credential creation, database restore, reset or automatic print submission.
"""
import argparse
import ast
import contextlib
import fcntl
import hashlib
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import sys
import time
import urllib.request


def snapshot(database, destination):
    destination.touch(mode=0o600)
    with contextlib.closing(sqlite3.connect(database)) as src:
        with contextlib.closing(sqlite3.connect(destination)) as dst:
            src.backup(dst)
            if dst.execute('PRAGMA integrity_check').fetchone()[0] != 'ok':
                raise RuntimeError('Backup integrity check failed')


def fingerprint(database):
    """Private in-memory comparison; never publish data or credential digests."""
    with contextlib.closing(sqlite3.connect(database)) as db:
        return hashlib.sha256('\n'.join(db.iterdump()).encode()).hexdigest()


def idle(database):
    with contextlib.closing(sqlite3.connect(database)) as db:
        if db.execute("SELECT 1 FROM jobs WHERE state IN ('claimed','printing') LIMIT 1").fetchone():
            raise RuntimeError('Active print: wait for completion; nothing will be retried')


def link(target, path):
    temporary = path.with_name(path.name + '.next')
    temporary.symlink_to(target)
    temporary.replace(path)


def runtime(source):
    for node in ast.parse((source/'relay.py').read_text()).body:
        if isinstance(node, ast.Assign) and any(isinstance(n, ast.Name) and n.id == 'APP_VERSION' for n in node.targets):
            return ast.literal_eval(node.value)
    raise ValueError('Source has no APP_VERSION')


class Deployment:
    def __init__(self, root, package):
        self.root, self.package = Path(root), Path(package).resolve()

    def control(self, action):
        subprocess.run(['systemctl', action, 'paperang-relay.service'], check=True)

    def stop_idle(self, database):
        # Hold the same SQLite writer lock used by claim creation until the
        # service is stopped. A new receiver cannot claim between check/stop.
        with contextlib.closing(sqlite3.connect(database, timeout=10)) as db:
            db.execute('BEGIN IMMEDIATE')
            try:
                if db.execute("SELECT 1 FROM jobs WHERE state IN ('claimed','printing') LIMIT 1").fetchone():
                    raise RuntimeError('Active print: leave service running and retry after completion')
                self.control('stop')
            finally:
                db.rollback()

    def healthy(self, version):
        c = json.loads((self.root/'config/config.json').read_text())
        opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
        for attempt in range(20):
            try:
                with opener.open(f"http://127.0.0.1:{c['port']}/v2/public/status", timeout=2) as response:
                    if json.load(response)['version'] == version:
                        return
            except (OSError, ValueError):
                pass
            time.sleep(.25)
        raise RuntimeError('Local version/health check failed')

    def public_health(self, version):
        request = urllib.request.Request('https://etel.patronum.uk/v2/public/status',
                                         headers={'User-Agent':'PaperangStudio/'+version})
        with urllib.request.urlopen(request, timeout=15) as response:
            if json.load(response)['version'] != version:
                raise RuntimeError('Public version check failed')

    def rehearse(self, directory, source, python, database, config):
        trial = directory/'rehearsal.sqlite3'
        snapshot(database, trial)
        before = fingerprint(trial)
        trial_config = directory/'rehearsal.json'
        trial_config.write_text(json.dumps({**config, 'database':str(trial)}))
        trial_config.chmod(0o600)
        code = "import json,sys;from relay import Store;Store(json.load(open(sys.argv[1])))"
        for implementation in (source, (self.root/'current').resolve()):
            subprocess.run([str(python), '-B', '-c', code, str(trial_config)],
                           env={**os.environ, 'PYTHONPATH':str(implementation)}, check=True)
            if fingerprint(trial) != before:
                raise RuntimeError('Upgrade/rollback rehearsal changed persistent rows or schema')
        trial.unlink()
        trial_config.unlink()

    def apply(self):
        b = self.root
        manifest = json.loads((self.package/'manifest.json').read_text())
        source = self.package/'Server'
        for name, expected in manifest['files'].items():
            if Path(name).is_absolute() or '..' in Path(name).parts:
                raise ValueError('Invalid manifest path')
            if hashlib.sha256((source/name).read_bytes()).hexdigest() != expected:
                raise ValueError('Package checksum failed: '+name)
        config = json.loads((b/'config/config.json').read_text())
        database = Path(config['database'])
        previous = (b/'current').resolve()
        previous_version = runtime(previous)
        python = b/'venvs/current/bin/python'
        # This release changes no dependencies or persistent schema. Refuse to
        # silently reuse an incompatible environment for a future release.
        if (source/'requirements.txt').read_bytes() != (previous/'requirements.txt').read_bytes():
            raise RuntimeError('Dependency change requires a separately tested environment')
        idle(database)
        stamp = time.strftime('%Y%m%dT%H%M%SZ', time.gmtime()) + '-' + str(time.time_ns())
        backup = b/'backups'/stamp
        backup.mkdir(parents=True, mode=0o700)
        backup.chmod(0o700)
        print('Protected backup: '+str(backup), flush=True)
        self.rehearse(backup, source, python, database, config)
        release = b/'releases'/('0.3.2-'+manifest['source'][:12])
        if release.exists():
            raise RuntimeError('Release directory already exists; inspect before repeating')
        shutil.copytree(source, release)
        for path in [release, *release.rglob('*')]:
            os.chown(path, 0, 0)
            path.chmod(0o755 if path.is_dir() else 0o644)
        unit = Path('/etc/systemd/system/paperang-relay.service')
        if unit.exists():
            shutil.copy2(unit, backup/'paperang-relay.service')
        for name in ['config', 'certs', 'deploy', 'admin-exports']:
            if (b/name).exists():
                shutil.copytree(b/name, backup/name, symlinks=True)
        shutil.copy2(b/'manage.py', backup/'manage.py')
        if (b/'docs/deployment.json').exists():
            shutil.copy2(b/'docs/deployment.json', backup/'deployment.json')
        nginx = Path('/etc/nginx/sites-available/paperang')
        if nginx.exists():
            shutil.copy2(nginx, backup/'nginx.conf')
        self.stop_idle(database)
        try:
            idle(database)  # Catch a claim created between preflight and stop.
            snapshot(database, backup/'database.sqlite3')
            # Preserve additional state/templates without copying an open SQLite
            # file. The service is stopped; the database uses backup() above.
            excluded = {database.name, database.name+'-wal', database.name+'-shm'}
            shutil.copytree(b/'data', backup/'data',
                            ignore=lambda directory, names: excluded if Path(directory) == database.parent else [])
            record = dict(source=manifest['source'], version=manifest['version'],
                          previous=str(previous), previous_version=previous_version, database=str(database), backup=str(backup),
                          rollback='program-only; live database is never restored', deployed_at=stamp)
            (backup/'upgrade.json').write_text(json.dumps(record, indent=2)+'\n')
            link(release, b/'current')
            shutil.copy2(source/'manage.py', b/'manage.py')
            (b/'manage.py').chmod(0o755)
            self.control('start')
            self.healthy(manifest['version'])
            self.public_health(manifest['version'])
        except BaseException:
            # The rehearsal proved old code reads the new database unchanged.
            # Keep all writes accepted since upgrade; never restore the snapshot.
            self.stop_idle(database)
            link(previous, b/'current')
            shutil.copy2(backup/'manage.py', b/'manage.py')
            self.control('start')
            self.healthy(previous_version)
            print('Rollback healthy; current database retained. Backup: '+str(backup), flush=True)
            raise
        (b/'docs/deployment.json').write_text(json.dumps(record, indent=2)+'\n')
        print(json.dumps({'installed':manifest['version'], 'backup':str(backup),
                          'data_preserved':True, 'local_health':True}, indent=2))

    def rollback(self, backup):
        b = self.root
        record = json.loads((backup/'upgrade.json').read_text())
        database = Path(record['database'])
        idle(database)
        self.stop_idle(database)
        try:
            idle(database)
            # Preserve an additional consistent copy of ALL post-upgrade data.
            snapshot(database, backup/('pre-rollback-'+str(time.time_ns())+'.sqlite3'))
            link(Path(record['previous']), b/'current')
            shutil.copy2(backup/'manage.py', b/'manage.py')
        finally:
            self.control('start')
        self.healthy(record['previous_version'])
        if (backup/'deployment.json').exists():
            shutil.copy2(backup/'deployment.json', b/'docs/deployment.json')
        print('Program rolled back; current database and all post-upgrade data retained')


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--package', type=Path, required=True)
    p.add_argument('--rollback', type=Path)
    a = p.parse_args()
    if os.geteuid() != 0:
        p.error('Run with sudo')
    b = Path('/home/elt/paperang')
    # Share the existing console/reset lock so configuration edits cannot race.
    with (b/'workspace/reset.lock').open('a') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        deployment = Deployment(b, a.package)
        if a.rollback:
            deployment.rollback(a.rollback)
        else:
            deployment.apply()


if __name__ == '__main__':
    main()
