#!/usr/bin/env python3
"""Host-local opt1 factory reset, preserving exactly one explicitly selected client key."""
import argparse
import fcntl
import grp
import hashlib
import json
import os
from pathlib import Path
import pwd
import re
import shutil
import sqlite3
import subprocess
import sys
import time

B=Path('/home/elt/paperang')
UNIT=Path('/etc/systemd/system/paperang-relay.service')
SITE=Path('/etc/nginx/sites-available/paperang')
EXPECTED='0.3.1-linuxrelayserver-opt1'

def run(*args,check=True,input=None):
    return subprocess.run(list(map(str,args)),check=check,text=True,capture_output=True,input=input)

def link(path,target):
    temporary=path.with_name(path.name+'.new');temporary.unlink(missing_ok=True)
    temporary.symlink_to(target);os.replace(temporary,path)

def folder(path,user='root',mode=0o750):
    path.mkdir(parents=True,exist_ok=True);shutil.chown(path,user=user,group=user);path.chmod(mode)

def public_health():
    for _ in range(12):
        result=run('curl','--noproxy','*','-fsS','--max-time','10','https://etel.patronum.uk/v2/public/status',check=False)
        try:
            if not result.returncode and json.loads(result.stdout).get('version')==EXPECTED:return
        except ValueError:pass
        time.sleep(1)
    raise RuntimeError('Public HTTPS failed; no old data has been erased')

def health():
    for _ in range(40):
        result=run('curl','--noproxy','*','-fsS','--max-time','2','http://127.0.0.1:28177/v2/public/status',check=False)
        try:
            if not result.returncode and json.loads(result.stdout).get('version')==EXPECTED:return
        except ValueError:pass
        time.sleep(.25)
    raise RuntimeError('Fresh backend health failed')

def preserve(config,identifier,fingerprint):
    with sqlite3.connect('file:'+config['database']+'?mode=ro',uri=True) as db:
        db.row_factory=sqlite3.Row
        row=db.execute('SELECT * FROM credentials WHERE id=?',(identifier,)).fetchone()
        if not row or row['revoked'] or row['role']!='C' or row['kind']!='publickey':
            raise RuntimeError('Expected the selected active Windows public-key C identity')
        identity=db.execute('SELECT * FROM identities WHERE id=?',(row['user_id'],)).fetchone()
        if not identity or identity['revoked']:raise RuntimeError('Selected identity is revoked')
        if hashlib.sha256(row['public_key'].encode()).hexdigest()!=fingerprint:
            raise RuntimeError('Windows public-key fingerprint changed; nothing deleted')
        device=db.execute('SELECT * FROM devices WHERE id=?',(row['device'],)).fetchone()
        status=json.loads(device['status']) if device else {}
        if status.get('_relay_key_id')!=identifier or 'winlite' not in status.get('appVersion','').lower():
            raise RuntimeError('Selected key is not the latest Windows receiver identity')
        if status.get('busy') or db.execute("SELECT 1 FROM jobs WHERE state IN ('claimed','printing') LIMIT 1").fetchone():
            raise RuntimeError('Drain active prints before resetting')
        # Only this credential's still-valid short sessions avoid breaking a cached Windows token.
        sessions=[dict(r) for r in db.execute('SELECT * FROM sessions WHERE credential_id=? AND expires>?',(identifier,time.time()))]
        counts={table:db.execute('SELECT count(*) FROM '+table).fetchone()[0] for table in ['credentials','identities','jobs']}
        owner=dict(identity);owner.update(name='Windows client',anonymous=0,revoked=0)
        return dict(credential=dict(row),identity=owner,sessions=sessions),counts

def tree_hashes(root):
    return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest()
            for p in root.rglob('*') if p.is_file() and '__pycache__' not in p.parts}

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path,required=True)
    parser.add_argument('--keep-id',required=True)
    parser.add_argument('--public-key-sha256',required=True)
    parser.add_argument('--confirm-reset',action='store_true')
    args=parser.parse_args()
    if os.geteuid()!=0:raise SystemExit('Run with sudo')
    if not args.confirm_reset:raise SystemExit('This removes old users, jobs, logs and backups; --confirm-reset is required')
    if B.is_symlink() or B.resolve()!=B:raise RuntimeError('Unexpected deployment root')
    os.umask(0o077)
    # The lock survives cleanup of the obsolete deploy/ tree.
    lock=open(B/'workspace/reset.lock','a');fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    owner_lock=open('/run/paperang-owner-upgrade.lock','a');fcntl.flock(owner_lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    legacy_lock=open(B/'deploy/maintenance.lock','a');fcntl.flock(legacy_lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
    package=args.package.resolve();manifest=json.loads((package/'manifest.json').read_text())
    for relative,digest in manifest['files'].items():
        path=package/relative
        if path.is_symlink() or hashlib.sha256(path.read_bytes()).hexdigest()!=digest:raise RuntimeError('Package hash mismatch: '+relative)
    oldconfig=json.loads((B/'config/config.json').read_text())
    if oldconfig['bind']!='127.0.0.1' or oldconfig['port']!=28177 or oldconfig['database']!=str(B/'data/relay.sqlite3'):
        raise RuntimeError('Unexpected production paths')
    if oldconfig.get('execution_paused'):raise RuntimeError('An owner cutover is paused; resolve it first')
    selection,oldcounts=preserve(oldconfig,args.keep_id,args.public_key_sha256)
    version='opt1-'+manifest['source'][:12]
    target=B/'releases'/version;venv=B/'venvs'/version
    if target.exists() or venv.exists():raise RuntimeError('Candidate exists; inspect previous attempt first')
    if shutil.disk_usage(B).free<700*1024*1024:raise RuntimeError('Need 700 MiB free')
    run('/usr/bin/python3','-m','venv',venv)
    run(venv/'bin/python','-m','pip','install','--no-cache-dir','--no-index','--find-links',package/'wheelhouse','-r',package/'requirements-lock.txt')
    run(venv/'bin/python','-m','pip','check')
    shutil.copytree(package/'Server',target)
    for root in [target,venv]:
        for directory,dirs,files in os.walk(root):
            Path(directory).chmod(0o755);os.chown(directory,0,0)
            for name in files:
                path=Path(directory)/name
                if path.is_symlink():continue
                os.chown(path,0,0);path.chmod(0o755 if os.access(path,os.X_OK) else 0o644)
    oldunit=UNIT.read_bytes();oldsite=SITE.read_bytes();oldtarget=(B/'current').resolve();oldvenv=(B/'venvs/current').resolve()
    if oldsite.count(b'location / {')!=1:raise RuntimeError('Unexpected Nginx site')
    recovery=B/('.reset-recovery-'+str(time.time_ns()));folder(recovery,mode=0o700)
    moved=[];published=False;reset_complete=False
    run('nginx','-t')
    try:
        SITE.write_bytes(oldsite.replace(b'location / {',b'location / {\n return 503; # fresh opt1 deployment',1));SITE.chmod(0o644)
        run('nginx','-t');run('systemctl','reload','nginx')
        run('systemctl','stop','paperang-relay')
        if run('systemctl','show','paperang-relay','-p','MainPID','--value').stdout.strip()!='0':raise RuntimeError('Old process did not stop')
        selection,oldcounts=preserve(oldconfig,args.keep_id,args.public_key_sha256)
        realip=(B/'config/cloudflare-realip.conf').read_bytes()
        acl=(B/'deploy/original-home.acl').read_bytes() if (B/'deploy/original-home.acl').exists() else None
        resources=(B/'docs/cloudflare-resources.json').read_bytes() if (B/'docs/cloudflare-resources.json').exists() else None
        for name in ['data','config','logs']:
            (B/name).rename(recovery/name);moved.append(name)
        folder(B/'data','paperang-relay');folder(B/'data/nginx-temp','www-data')
        run('setfacl','-m','u:www-data:--x',B/'data')
        folder(B/'config');shutil.chown(B/'config',user='root',group='paperang-relay')
        config=json.loads((target/'config.example.json').read_text())
        config.update(bind='127.0.0.1',port=28177,database=str(B/'data/relay.sqlite3'))
        for name in ['device_id','trusted_proxies','allow_anonymous','retention_days','anonymous_max_length','anonymous_hourly_limit','anonymous_hourly_chars','authenticated_max_length','authenticated_hourly_limit','authenticated_hourly_chars','queue_limit','global_hourly_limit','global_hourly_chars']:
            if name in oldconfig:config[name]=oldconfig[name]
        (B/'config/config.json').write_text(json.dumps(config,indent=2)+'\n')
        shutil.chown(B/'config/config.json',user='root',group='paperang-relay');(B/'config/config.json').chmod(0o640)
        (B/'config/cloudflare-realip.conf').write_bytes(realip);(B/'config/cloudflare-realip.conf').chmod(0o644)
        if resources:
            (B/'config/cloudflare-resources.json').write_bytes(resources);(B/'config/cloudflare-resources.json').chmod(0o600)
        folder(B/'logs');run('setfacl','-m','u:paperang-relay:--x,u:www-data:--x',B/'logs')
        folder(B/'logs/service','paperang-relay');folder(B/'logs/nginx','www-data')
        importer='''import sys,json,sqlite3
sys.path.insert(0,sys.argv[1]);from relay import Store
s=Store(json.load(open(sys.argv[2])));keep=json.load(sys.stdin)
with sqlite3.connect(s.db) as db:
 for table,value in [('identities',keep['identity']),('credentials',keep['credential'])]:
  columns=list(value);db.execute('INSERT INTO '+table+'('+','.join(columns)+') VALUES('+','.join('?' for _ in columns)+')',[value[c] for c in columns])
 for row in keep['sessions']:
  columns=list(row);db.execute('INSERT INTO sessions('+','.join(columns)+') VALUES('+','.join('?' for _ in columns)+')',[row[c] for c in columns])
 assert db.execute('SELECT count(*) FROM credentials').fetchone()[0]==1
 assert db.execute('SELECT count(*) FROM identities').fetchone()[0]==1
 assert db.execute('SELECT count(*) FROM jobs').fetchone()[0]==0
 assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
'''
        run('runuser','-u','paperang-relay','--',venv/'bin/python','-B','-c',importer,target,B/'config/config.json',input=json.dumps(selection))
        # Store creates an empty pre-migration backup on a new DB; no old rows belong here.
        for file in (B/'data').glob('*.pre-v0.3.0-*.bak'):file.unlink()
        (B/'data/.bootstrapped').touch(mode=0o600);shutil.chown(B/'data/.bootstrapped',user='paperang-relay',group='paperang-relay')
        newunit=re.sub(r'^ExecStart=.+$',f'ExecStart={venv}/bin/python -B {B}/current/relay.py --config {B}/config/config.json serve',oldunit.decode(),flags=re.M)
        if newunit==oldunit.decode():raise RuntimeError('Unit did not change')
        UNIT.unlink();UNIT.write_text(newunit);UNIT.chmod(0o644)
        link(B/'current',target);link(B/'venvs/current',venv)
        run('systemctl','daemon-reload');run('systemctl','enable','paperang-relay');run('systemctl','start','paperang-relay');health()
        run('systemctl','restart','paperang-relay');health()
        console=json.loads(run(venv/'bin/python','-B',target/'manage.py','--root',B,'--check').stdout)
        if console['credentials']!=1:raise RuntimeError('Console worker sees unexpected credential count')
        SITE.write_bytes(oldsite);run('nginx','-t');run('systemctl','reload','nginx');published=True
        public_health()
        # No automatic data rollback once the public endpoint can accept new jobs.
        # Destruction below is explicitly authorized by --confirm-reset.
        reset_complete=True
    except Exception:
        if not published:
            run('systemctl','stop','paperang-relay',check=False)
            for name in reversed(moved):
                if (B/name).exists():shutil.rmtree(B/name)
                (recovery/name).rename(B/name)
            UNIT.write_bytes(oldunit);UNIT.chmod(0o644);link(B/'current',oldtarget);link(B/'venvs/current',oldvenv)
            run('systemctl','daemon-reload',check=False);run('systemctl','start','paperang-relay',check=False)
            SITE.write_bytes(oldsite);run('nginx','-t',check=False);run('systemctl','reload','nginx',check=False)
            if recovery.exists():shutil.rmtree(recovery)
        else:
            print('New backend is live; cleanup postponed. Old data is private in '+str(recovery),file=sys.stderr)
        raise
    if reset_complete:
        # Delete exact application-owned directories only; TLS and shared nginx stay intact.
        for name in ['backups','updates','source','docs','scripts','deploy','admin-exports']:
            path=B/name
            if path.is_symlink():path.unlink()
            elif path.exists():shutil.rmtree(path)
        for parent,kept in [(B/'releases',target),(B/'venvs',venv)]:
            for path in parent.iterdir():
                if path.name=='current' or path==kept:continue
                if path.is_symlink() or path.is_file():path.unlink()
                else:shutil.rmtree(path)
        shutil.rmtree(recovery)
        # Install only current lifecycle/management entrypoints and fresh documentation.
        shutil.copy2(target/'manage.py',B/'manage.py');os.chown(B/'manage.py',0,0);(B/'manage.py').chmod(0o755)
        folder(B/'deploy',mode=0o755)
        shutil.copy2(UNIT,B/'deploy/paperang-relay.service');shutil.copy2(SITE,B/'deploy/nginx.conf')
        if acl:(B/'deploy/original-home.acl').write_bytes(acl)
        folder(B/'docs',mode=0o755)
        shutil.copy2(target/'ADMIN-CONSOLE.md',B/'docs/ADMIN-CONSOLE.md')
        (B/'README.md').write_text('# Paperang opt1\n\nRuntime: '+EXPECTED+'\n\n`sudo python3 /home/elt/paperang/manage.py` starts the local terminal GUI. See docs/ADMIN-CONSOLE.md.\n')
        with sqlite3.connect(B/'data/relay.sqlite3') as db:
            remaining={table:db.execute('SELECT count(*) FROM '+table).fetchone()[0] for table in ['credentials','identities','jobs','sessions','audit_events','keys']}
            kept=db.execute('SELECT public_key FROM credentials WHERE id=?',(args.keep_id,)).fetchone()
            if not kept or hashlib.sha256(kept[0].encode()).hexdigest()!=args.public_key_sha256:raise RuntimeError('Preserved Windows key verification failed')
        report=dict(version=EXPECTED,source=manifest['source'],retained_credential_id=args.keep_id,
                    database_counts=remaining,retained_windows_key_verified=True,
                    public_key_sha256=args.public_key_sha256,removed_old_counts=oldcounts,
                    old_logs_backups_and_releases_removed=True,public_https=True,cold_restart=True,console_worker_checked=True,
                    previous_history='deleted by explicit factory-reset request',retention_days=config['retention_days'])
        (B/'docs/deployment.json').write_text(json.dumps(report,indent=2)+'\n');(B/'docs/deployment.json').chmod(0o644)
        # Release nginx references to unlinked old access/error logs.
        run('nginx','-s','reopen')
        run('systemctl','reset-failed','paperang-relay',check=False)
        # Keep only sanitized current code/validation workspace. No old production rows or key exports.
        (B/'workspace/current-identity-audit.json').unlink(missing_ok=True)
        (B/'workspace/inspect-current.py').unlink(missing_ok=True)
        (B/'reset-opt1.py').unlink(missing_ok=True)
        if package==B/'.opt1-package':shutil.rmtree(package)
        print(json.dumps(report,indent=2))

if __name__=='__main__':
    try:main()
    except subprocess.CalledProcessError as error:raise SystemExit(str(error.cmd[0])+': '+error.stderr[-1200:])
