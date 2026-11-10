"""Isolated HTTP Relay v2 contract tests. Temporary identities/DB, no printer."""
import json, socket, sys, tempfile, threading, unittest, urllib.request, urllib.error, uuid
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Server'))
from relay import Store,Server
class RelayContract(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.tmp=tempfile.TemporaryDirectory(prefix='ps032-relay-')
  with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
  cls.store=Store({'database':cls.tmp.name+'/relay.sqlite3','port':port})
  cls.server=Server(cls.store);cls.thread=threading.Thread(target=cls.server.serve_forever,daemon=True);cls.thread.start()
  cls.base=f'http://127.0.0.1:{port}';cls.opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
  cls.keys={r:cls.store.api.auth.create('PS032 '+r,r) for r in ['A','B','C','M']}
  cls.other=cls.store.api.auth.create('PS032 other B','B')
 @classmethod
 def tearDownClass(cls):cls.server.shutdown();cls.server.server_close();cls.thread.join();cls.tmp.cleanup()
 def req(self,method,path,body=None,key=None,extra=None):
  h={'Content-Type':'application/json'}
  if key:h['Authorization']='Bearer '+key['token']
  if extra:h.update(extra)
  q=urllib.request.Request(self.base+path,method=method,data=None if body is None else json.dumps(body).encode(),headers=h)
  try:r=self.opener.open(q,timeout=10)
  except urllib.error.HTTPError as e:r=e
  with r:return r.code,json.load(r)
 def test_public(self):
  code,d=self.req('GET','/v2/public/status');self.assertEqual(code,200);self.assertEqual(d['capabilities']['authVersion'],2);self.assertEqual(d['capabilities']['contentSchemaVersion'],1)
 def test_v1_retired(self):
  for path in ['/v1','/v1/status','/v1/jobs','/v1/content']:
   for method in ['GET','POST']:
    code,d=self.req(method,path,{} if method=='POST' else None);self.assertEqual(code,410);self.assertIsInstance(d['error'],str)
 def test_roles_dedup_and_claim(self):
  b=self.keys['B'];body={'text':'PS032 isolated contract; NEVER physical print','request_id':str(uuid.uuid4())}
  code,job=self.req('POST','/v2/jobs',body,b);self.assertEqual(code,202)
  self.assertEqual(self.req('POST','/v2/jobs',body,b)[1]['id'],job['id'])
  code,state=self.req('GET','/v2/jobs/'+job['id'],key=b);self.assertEqual(code,200);self.assertNotIn('text',state);self.assertEqual(state['state'],'queued')
  self.assertEqual(self.req('GET','/v2/jobs/'+job['id'],key=self.keys['A'])[0],403)
  self.assertEqual(self.req('GET','/v2/jobs/'+job['id'],key=self.other)[0],404)
  self.assertEqual(self.req('POST','/v2/jobs',body,self.keys['M'])[0],403)
  self.assertEqual(self.req('POST','/v2/admin/keys/search',{},b)[0],403)
  self.assertEqual(self.req('POST','/v2/jobs',{'text':'missing UUID'},b)[0],400)
  self.assertEqual(self.req('GET','/v2/device/jobs',key=self.keys['C'])[0],403)
  m=self.keys['M'];code,claim=self.req('POST',f"/v2/device/jobs/{job['id']}/claim",{},m);self.assertEqual(code,200)
  self.assertEqual(self.req('GET',f"/v2/device/jobs/{job['id']}",key=m)[0],403)
  code,payload=self.req('GET',f"/v2/device/jobs/{job['id']}",key=m,extra={'X-Job-Claim':claim['claim']});self.assertEqual(code,200);self.assertEqual(payload['text'],body['text'])
  code,result=self.req('POST',f"/v2/device/jobs/{job['id']}/result",{'claim':claim['claim'],'state':'sent'},m);self.assertEqual(code,200)
  self.assertEqual(self.req('GET',f"/v2/device/jobs/{job['id']}",key=m,extra={'X-Job-Claim':claim['claim']})[0],403)
  self.assertEqual(self.req('GET','/v2/jobs/'+job['id'],key=b)[1]['state'],'sent')
 def test_confirmation_and_expired_claim(self):
  b=self.keys['B'];m=self.keys['M'];c=self.keys['C']
  code,job=self.req('POST','/v2/jobs',{'text':'isolated paper confirmation fixture','request_id':str(uuid.uuid4())},b);self.assertEqual(code,202)
  jid=job['id'];confirm='/v2/admin/jobs/'+jid+'/confirm'
  self.assertEqual(self.req('POST',confirm,{},c)[0],409)
  code,claim=self.req('POST',f'/v2/device/jobs/{jid}/claim',{},m);self.assertEqual(code,200)
  # Simulate loss of the receiver for longer than the five-minute active window.
  with self.store.connect() as db:db.execute('UPDATE jobs SET updated=updated-301 WHERE id=?',(jid,))
  self.req('GET','/v2/device/jobs',key=m)
  self.assertEqual(self.req('GET','/v2/jobs/'+jid,key=b)[1]['state'],'unknown')
  self.assertEqual(self.req('GET',f'/v2/device/jobs/{jid}',key=m,extra={'X-Job-Claim':claim['claim']})[0],403)
  self.assertEqual(self.req('POST',f'/v2/device/jobs/{jid}/claim',{},m)[0],409)
  # A late report of already-sent bytes updates status without obtaining payload again.
  self.assertEqual(self.req('POST',f'/v2/device/jobs/{jid}/result',{'claim':claim['claim'],'state':'sent'},m)[0],200)
  self.assertEqual(self.req('POST',confirm,{},m)[0],403)
  self.assertEqual(self.req('POST',confirm,{},b)[0],403)
  self.assertEqual(self.req('POST',confirm,{},c)[0],200)
  self.assertEqual(self.req('GET','/v2/jobs/'+jid,key=b)[1]['state'],'confirmed')
 def test_queued_rejection(self):
  b=self.keys['B'];m=self.keys['M']
  code,job=self.req('POST','/v2/jobs',{'text':'isolated cancellation fixture','request_id':str(uuid.uuid4())},b);self.assertEqual(code,202)
  jid=job['id']
  self.assertEqual(self.req('POST',f'/v2/device/jobs/{jid}/reject',{},b)[0],403)
  self.assertEqual(self.req('POST',f'/v2/device/jobs/{jid}/reject',{},m)[0],200)
  self.assertEqual(self.req('GET','/v2/jobs/'+jid,key=b)[1]['state'],'rejected')
if __name__=='__main__':unittest.main()
