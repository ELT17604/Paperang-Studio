"""Real macOS loopback HTTP checks; no BLE, credentials or production state."""
import argparse, base64, json, os, socket, subprocess, tempfile, time, urllib.request, urllib.error
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--app',required=True);args=ap.parse_args()
 with tempfile.TemporaryDirectory(prefix='paperang032-local-') as tmp:
  p=Path(tmp)/'secret';p.write_text(os.urandom(32).hex());p.chmod(0o600)
  with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
  proc=subprocess.Popen([str(Path(args.app).resolve()/'Contents/MacOS/PaperangLab'),'--api-offline','--api','--api-port',str(port),'--api-token-file',str(p)],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
  opener=urllib.request.build_opener(urllib.request.ProxyHandler({}));count=0
  def req(method,path,body=None,token=True,ctype='application/json',extra=None):
   nonlocal count
   time.sleep(.065)
   h={'Content-Type':ctype}
   if token:h['Authorization']='Bearer '+p.read_text()
   if extra:h.update(extra)
   data=None if body is None else (body if isinstance(body,bytes) else json.dumps(body).encode())
   q=urllib.request.Request(f'http://127.0.0.1:{port}'+path,data=data,method=method,headers=h)
   try:r=opener.open(q,timeout=15)
   except urllib.error.HTTPError as e:r=e
   with r:
    raw=r.read();value=json.loads(raw) if raw else None;count+=1;return r.code,value
  try:
   for _ in range(100):
    if proc.poll() is not None:raise RuntimeError(proc.stderr.read().decode())
    try:status,value=req('GET','/v2/status');break
    except urllib.error.URLError:time.sleep(.1)
   else:raise AssertionError('listener never ready')
   assert status==200 and value['apiVersion']=='v2' and value['appVersion']=='0.3.2' and not value['connected']
   schema=json.loads((ROOT/'doc/openapi.json').read_text())
   for path,ops in schema['paths'].items():
    if '{id}' in path:continue
    if 'get' in ops:
     status,value=req('GET',path);assert status==200,(path,status,value)
   assert req('GET','/v2/openapi')[1]==schema, 'bundled OpenAPI differs from source'
   for path in ['/v1','/v1/status','/v1/jobs','/v1/jobs/anything/cancel','/v1/render','/v1/unknown']:
    for method in ['GET','POST','OPTIONS']:
     status,value=req(method,path,b'invalid' if method=='POST' else None,ctype='text/plain',extra={'Origin':'http://forbidden.example'} if method=='OPTIONS' else None)
     expected=403 if method=='OPTIONS' else 410
     assert status==expected,(method,path,status,value)
     if status==410:assert value['error']['code']=='api_v1_retired'
   assert req('OPTIONS','/v1/jobs',extra={'Origin':'http://localhost:3000'})[0]==410
   assert req('OPTIONS','/v2/jobs',extra={'Origin':'http://localhost:3000'})[0]==204
   for method in ['GET','POST']:
    status,value=req(method,'/v1/jobs',{} if method=='POST' else None,token=False);assert status==403
   for extra in [{'Origin':'http://forbidden.example'},{'Host':'evil.example'}]:assert req('GET','/v2/status',extra=extra)[0]==403
   assert req('GET','/v2/status',token=False)[0]==403
   for path,body,code in [('/v2/render',{'text':'PS032 offline render 文字','includeRaw':True},200),('/v2/render',{'text':'x','imageBase64':'bad'},400),('/v2/render',{'imageBase64':'bad'},400),('/v2/render',{'text':'x','settings':{'unknown':1}},400),('/v2/jobs',{'text':'PS032','allowPaper':True},403),('/v2/feed',{'lines':1,'allowPaper':True},403),('/v2/scan',{},409),('/v2/disconnect',{},200),('/v2/query',{'command':255},400),('/v2/unknown',{},404)]:
    status,value=req('POST',path,body);assert status==code,(path,status,value)
    if path=='/v2/render' and code==200:assert value['width']==576 and len(base64.b64decode(value['rawBase64']))==72*value['height']
    if code>=400:assert isinstance(value['error']['code'],str) and isinstance(value['error']['message'],str)
   assert req('POST','/v2/render',b'{')[0]==400
   assert req('POST','/v2/render',{},ctype='text/plain')[0]==415
   assert req('GET','/v2/jobs/00000000-0000-0000-0000-000000000000')[0]==404
   print(f'PASS {count} actual HTTP requests: local v2 routes, v1 retirement, auth/Host/Origin, JSON/errors, consent and 576-dot rendering; no BLE access')
  finally:
   proc.terminate()
   try:proc.wait(timeout=5)
   except subprocess.TimeoutExpired:proc.kill();proc.wait()
if __name__=='__main__':main()
