#!/usr/bin/env python3
"""Install a pinned console-only update without restarting/resetting the relay."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile

ROOT=Path('/home/elt/paperang')

def atomic(path,content,mode):
    fd,temporary=tempfile.mkstemp(dir=path.parent,prefix='.console-update-')
    try:
        os.fchmod(fd,mode)
        with os.fdopen(fd,'wb') as file:
            file.write(content);file.flush();os.fsync(file.fileno())
        os.replace(temporary,path)
    finally:Path(temporary).unlink(missing_ok=True)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package',type=Path,required=True)
    args=parser.parse_args()
    if os.geteuid()!=0:raise SystemExit('Run with sudo; close existing console instances first')
    os.umask(0o077)
    with (ROOT/'workspace/reset.lock').open('a') as lock:
        try:fcntl.flock(lock,fcntl.LOCK_EX|fcntl.LOCK_NB)
        except BlockingIOError:raise SystemExit('Close the open terminal console, then run the update again')
        manifest=json.loads((args.package/'manifest.json').read_text())
        content={name:(args.package/name).read_bytes() for name in ['manage.py','ADMIN-CONSOLE.md']}
        for name,value in content.items():
            if hashlib.sha256(value).hexdigest()!=manifest['files'][name]:raise ValueError('Console package hash mismatch: '+name)
        targets={ROOT/'manage.py':('manage.py',0o755),ROOT/'docs/ADMIN-CONSOLE.md':('ADMIN-CONSOLE.md',0o644)}
        old={path:path.read_bytes() for path in targets}
        python=ROOT/'venvs/current/bin/python'
        try:
            for path,(name,mode) in targets.items():atomic(path,content[name],mode)
            # Exercise root-only atomic config writing with identical bytes: no policy change/restart.
            check='''import importlib.util,json,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('console',sys.argv[1]);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
path=Path(sys.argv[2]);original=path.read_bytes();editor=m.Configuration(path,None)
editor.validate(original,json.loads(original));editor.write(original)
assert path.read_bytes()==original and path.stat().st_mode&0o777==0o640
'''
            subprocess.run([str(python),'-B','-c',check,str(ROOT/'manage.py'),str(ROOT/'config/config.json')],check=True,capture_output=True,text=True)
            result=subprocess.run([str(python),'-B',str(ROOT/'manage.py'),'--check'],check=True,capture_output=True,text=True)
            status=json.loads(result.stdout)
        except Exception:
            for path,(_,mode) in targets.items():atomic(path,old[path],mode)
            raise
        report_path=ROOT/'docs/deployment.json';report=json.loads(report_path.read_text())
        report.update(console_source=manifest['source'],console_sha256=manifest['files']['manage.py'],configuration_editor=True,
                      configuration_atomic_write_checked=True,console_worker_status=status)
        atomic(report_path,(json.dumps(report,indent=2)+'\n').encode(),0o644)
        print(json.dumps(dict(console_source=manifest['source'],configuration_editor=True,relay_restarted=False,
                             data_reset=False,worker_status=status),indent=2))

if __name__=='__main__':main()
