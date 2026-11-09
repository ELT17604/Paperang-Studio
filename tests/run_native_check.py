import sys,tempfile,threading,json,os,socket,uuid,subprocess,base64,hashlib
from pathlib import Path
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Server'))
from relay import Store,Server
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives import serialization
with tempfile.TemporaryDirectory(prefix='paperang-mac-v031-') as tmp:
 tmp=Path(tmp)
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
 base=f'http://127.0.0.1:{port}/';store=Store({'database':str(tmp/'db.sqlite'),'port':port,'authenticated_hourly_limit':500,'authenticated_hourly_chars':100000,'anonymous_hourly_limit':500})
 auth=store.api.auth
 private=rsa.generate_private_key(public_exponent=65537,key_size=3072);public=private.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode()
 entry=auth.public_key_add('fixture owner',public)
 def write(name,value):
  path=tmp/name;fd=os.open(path,os.O_WRONLY|os.O_CREAT|os.O_EXCL,0o600)
  with os.fdopen(fd,'w') as f:json.dump(value,f)
  return str(path)
 owner=dict(server=base,id=entry['id'],privateKeyPem=private.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.PKCS8,serialization.NoEncryption()).decode());ownerpath=write('owner.json',owner)
 other={**owner,'privateKeyPem':private.private_bytes(serialization.Encoding.PEM,serialization.PrivateFormat.TraditionalOpenSSL,serialization.NoEncryption()).decode()};pkcs1path=write('owner-pkcs1.json',other)
 m_entry=auth.create('M fixture','M');sender=auth.create('sender','B');sender2=auth.create('second sender','B')
 for i in range(85):auth.create('fixture '+str(i),'A')
 actor=auth.authenticate('Bearer '+sender['token']);rasterjob=''
 for i in range(20):
  body={'text':('Long fixture caption '+str(i)+'\n')*9,'request_id':str(uuid.uuid4())}
  if i==19:
   raw=bytes([0xaa])*72*24;body['content']={'schemaVersion':1,'type':'raster1','width':576,'height':24,'bitOrder':'msb-first','blackIsOne':True,'encoding':'base64','data':base64.b64encode(raw).decode(),'sha256':hashlib.sha256(raw).hexdigest()}
  code,job=store.route('POST','/v2/jobs',body,actor,'127.0.0.1',{})
  if i==19:rasterjob=job['id']
 fingerprint=entry['id']+':'+hashlib.sha256(private.public_key().public_bytes(serialization.Encoding.DER,serialization.PublicFormat.SubjectPublicKeyInfo)).hexdigest()
 config=write('fixture.json',dict(server=base,ownerPath=ownerpath,pkcs1Path=pkcs1path,fingerprint=fingerprint,rasterJob=rasterjob,mToken=m_entry['token'],vaultService='local.paperang.mac-upgrade-test.'+str(uuid.uuid4()),user1=sender['user_id'],user2=sender2['user_id']))
 statuses=[0];legacy=[False]
 original=store.route
 def observed(method,path,body,key,ip,headers):
  if path=='/v2/admin/validation-mode':
   assert key and key['role']=='C';legacy[0]=body['legacy'];return 200,{'ok':True}
  if legacy[0] and path in ['/v2/admin/keys/search','/v2/admin/identities/search','/v2/admin/jobs/search']:
   body={k:v for k,v in body.items() if k not in ['limit','summary','hide_revoked']}
  result=original(method,path,body,key,ip,headers)
  if path=='/v2/device/status':
   statuses[0]+=1
   if statuses[0]==2:
    with store.connect() as db:db.execute('UPDATE sessions SET expires=0')
  return result
 store.route=observed
 server=Server(store);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
 try:
  result=subprocess.run([str(ROOT/'work/native-check/check'),config],timeout=60,capture_output=True,text=True)
  print(result.stdout,end='')
  if result.returncode:print(result.stderr[-7000:],file=sys.stderr);raise SystemExit(result.returncode)
  with store.connect() as db:
   states=[r[0] for r in db.execute('SELECT state FROM jobs ORDER BY created')]
   assert states[:3]==['sent','unknown','sent'],states[:3]
  print('PASS: server confirms report-only recovery states; no printer bytes or replay')
 finally:
  server.shutdown();server.server_close();thread.join()
  service=json.loads(Path(config).read_text())['vaultService']
  for account in ['connection','pending','connectionDraft']:
   subprocess.run(['/usr/bin/security','delete-generic-password','-s',service,'-a',account],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
