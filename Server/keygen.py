#!/usr/bin/env python3
"""Generate a relay credential offline; import it into the server with key-import."""
import argparse,json,os,re,secrets
from pathlib import Path

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--name',required=True);p.add_argument('--role',choices=['submit','device','admin'],default='submit');p.add_argument('--device',default='p2');p.add_argument('--out',required=True)
    a=p.parse_args()
    if not 1<=len(a.name)<=80 or not re.fullmatch(r'[A-Za-z0-9_-]{1,64}',a.device):p.error('Invalid name or device ID')
    out=Path(a.out).expanduser();out.parent.mkdir(parents=True,exist_ok=True)
    value=dict(name=a.name,role=a.role,device=a.device,token='psr_'+secrets.token_urlsafe(32))
    fd=os.open(out,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600)
    with os.fdopen(fd,'w') as f:json.dump(value,f,indent=2);f.write('\n')
    print(f'Created private {a.role} key file: {out}. Import on server; secret was not printed.')
if __name__=='__main__':main()
