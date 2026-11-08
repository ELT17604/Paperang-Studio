#!/usr/bin/env python3
"""Operator-run, pinned owner cutover. Never receives a private key or raw API token."""
import argparse
import base64
import contextlib
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import sqlite3
import subprocess
import tarfile
import tempfile
import time
import urllib.request
import uuid


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--commit',required=True);p.add_argument('--archive-sha256',required=True)
    p.add_argument('--public-key-b64',required=True);p.add_argument('--credential-id',required=True)
    p.add_argument('--service',default='paperang-relay.service')
    a=p.parse_args()
    if os.name!='posix' or os.geteuid()!=0:raise SystemExit('Run as root on the existing Linux relay host')
    import fcntl
    lock_fd=os.open('/run/paperang-owner-upgrade.lock',os.O_CREAT|os.O_RDWR,0o600)
    try:fcntl.flock(lock_fd,fcntl.LOCK_EX|fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(lock_fd);raise SystemExit('Another owner cutover is still running; do not start a second updater')
    if not re.fullmatch(r'[a-f0-9]{40}',a.commit) or not re.fullmatch(r'[a-f0-9]{64}',a.archive_sha256):raise SystemExit('Invalid pinned revision/hash')
    if not re.fullmatch(r'paperang-relay(?:\.service)?',a.service):raise SystemExit('Unexpected service')
    uuid.UUID(a.credential_id)
    pem=base64.b64decode(a.public_key_b64,validate=True).decode()
    if len(pem)>8192 or 'PRIVATE' in pem:raise SystemExit('Only the public PEM is accepted')
    def run(args,**kw):return subprocess.run(args,check=True,text=True,**kw)
    def prop(name):return subprocess.check_output(['systemctl','show',a.service,'--property='+name,'--value'],text=True).strip()
    source=Path(prop('WorkingDirectory')).resolve()
    executable=prop('ExecStart')
    match=re.search(r'path=([^;]+)\s*;',executable)
    argv=re.search(r'argv\[\]=([^;]+)\s*;',executable)
    if not match or not argv:raise SystemExit('Cannot identify the service interpreter/arguments')
    python=match.group(1).strip();args=shlex.split(argv.group(1).strip())
    if '--config' not in args:raise SystemExit('Cannot identify the service config')
    config=Path(args[args.index('--config')+1]).resolve()
    if not (source/'relay.py').is_file() or not config.is_file():raise SystemExit('Expected existing relay files')
    user=prop('User')
    if not re.fullmatch(r'[a-z_][a-z0-9_-]*',user):raise SystemExit('Expected a named service account')
    original=config.read_bytes();c=json.loads(original);database=Path(c['database'])
    if not database.is_absolute():database=source/database
    database=database.resolve()
    req=urllib.request.Request('https://codeload.github.com/ELT17604/Paperang-Studio/tar.gz/'+a.commit,headers={'User-Agent':'Paperang-Owner-Upgrade/0.3.1'})
    with urllib.request.urlopen(req,timeout=30) as response:archive=response.read(8*1024*1024+1)
    if len(archive)>8*1024*1024 or hashlib.sha256(archive).hexdigest()!=a.archive_sha256:raise SystemExit('Archive hash/size check failed')
    backup=database.parent/('owner-cutover-'+time.strftime('%Y%m%dT%H%M%SZ',time.gmtime()))
    backup.mkdir(mode=0o700)
    with tempfile.TemporaryDirectory(prefix='paperang-owner-source-') as tmp:
        staging=Path(tmp);staging.chmod(0o755)
        with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
            for item in tar.getmembers():
                pieces=Path(item.name).parts
                if len(pieces)<3 or pieces[1]!='Server' or not item.isfile():continue
                relative=Path(*pieces[2:])
                if relative.is_absolute() or '..' in relative.parts or item.size>1024*1024:raise SystemExit('Unsafe release archive member')
                target=staging/relative;target.parent.mkdir(parents=True,exist_ok=True)
                with tar.extractfile(item) as content:target.write_bytes(content.read())
        run([python,'-m','unittest','discover','-s',str(staging),'-p','test_*.py'],env={**os.environ,'PYTHONDONTWRITEBYTECODE':'1'})
        files=['relay.py','security.py','api_v2.py','web/app.js','web/index.html','web/style.css']
        for name in files:
            target=backup/name;target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(source/name,target)
        (backup/'config.json').write_bytes(original);(backup/'config.json').chmod(0o600)
        run(['systemctl','stop',a.service])
        try:
            with contextlib.closing(sqlite3.connect(str(database))) as db:
                if db.execute("SELECT count(*) FROM jobs WHERE state IN ('claimed','printing')").fetchone()[0]:
                    raise RuntimeError('Active print claims exist; let them settle before cutover')
                saved=backup/'database.sqlite3';saved.touch(mode=0o600)
                with contextlib.closing(sqlite3.connect(str(saved))) as dest:db.backup(dest)
            for name in files:shutil.copy2(staging/name,source/name)
            c['execution_paused']=True;config.write_text(json.dumps(c,indent=2)+'\n')
            code="import json,sys;from relay import Store; s=Store(json.load(open(sys.argv[1]))); r=s.api.auth.public_key_add('Windows owner',sys.argv[2],sys.argv[3]); print('Registered public credential '+r['id'])"
            run(['runuser','-u',user,'--','env','PYTHONDONTWRITEBYTECODE=1','PYTHONPATH='+str(source),python,'-c',code,str(config),pem,a.credential_id],cwd=source)
            run(['systemctl','start',a.service])
            print('READY: owner proof may now be verified from Windows. Execution is paused until old credentials are retired.',flush=True)
        except Exception:
            for name in files:shutil.copy2(backup/name,source/name)
            config.write_bytes(original);run(['systemctl','start',a.service]);raise
        # The Windows owner must prove its private key and explicitly retire others.
        # This short-lived operator command performs no unattended permanent task.
        deadline=time.monotonic()+900
        while time.monotonic()<deadline:
            with contextlib.closing(sqlite3.connect(str(database))) as db:
                row=db.execute("SELECT kind,revoked FROM credentials WHERE id=?",(a.credential_id,)).fetchone()
                retired=db.execute("SELECT 1 FROM audit_events WHERE action='retire_all_other_credentials' AND target=? AND at>? LIMIT 1",(a.credential_id,time.time()-900)).fetchone()
            if row==('publickey',0) and retired:break
            time.sleep(2)
        else:raise SystemExit('Timed out waiting for Windows proof and retirement; service remains paused, backup at '+str(backup))
        resumed=json.loads(original)
        # A repeated, already-proven cutover must not preserve a previous cutover's pause.
        if resumed.pop('execution_paused',None) is not None:config.write_text(json.dumps(resumed,indent=2)+'\n')
        else:config.write_bytes(original)
        run(['systemctl','restart',a.service])
        for folder in {database.parent,config.parent,source}:
            for name in ('initial-credentials.jsonl','initial-credentials.keys.json','credentials.keys.json','recovered-credentials.keys.json'):
                candidate=folder/name
                if candidate.is_file() and not candidate.is_symlink():candidate.unlink()
        print('SUCCESS: owner paired, old credentials revoked, known legacy credential exports removed; execution resumed.',flush=True)
        print('Private rollback backup: '+str(backup),flush=True)
    os.close(lock_fd)


if __name__=='__main__':main()
