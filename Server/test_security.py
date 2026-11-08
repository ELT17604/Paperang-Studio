"""Permission and real HTTP boundary regression tests; isolated temporary databases."""
import base64
import json
import sys
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
import uuid
import sqlite3
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import rsa, padding
from relay import Store, Server, Problem


class SecurityTests(unittest.TestCase):
    def owner(self):
        private=rsa.generate_private_key(public_exponent=65537,key_size=3072)
        pem=private.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode()
        identifier=str(uuid.uuid4())
        entry=self.auth.public_key_add('owner',pem,identifier)
        self.assertEqual(entry['id'],identifier)
        self.assertEqual(self.auth.public_key_add('owner',pem,identifier)['id'],identifier)
        challenge=self.auth.challenge({'id':identifier},'127.0.0.1')
        signature=private.sign(challenge['message'].encode(),padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        result=self.auth.prove({'challenge':challenge['challenge'],'signature':base64.b64encode(signature).decode()},'127.0.0.1')
        return self.auth.authenticate('Bearer '+result['token']),result['token']

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.s = Store(dict(database=str(Path(self.temp.name)/'relay.db'),port=8177))
        self.auth = self.s.api.auth
        self.credentials = {r:self.auth.create(r,r) for r in ('A','B','C','M')}
        self.keys = {r:self.auth.authenticate('Bearer '+v['token']) for r,v in self.credentials.items()}

    def tearDown(self): self.temp.cleanup()
    def route(self,method,path,role=None,body=None,headers=None):
        return self.s.route(method,path,body or {},self.keys.get(role),'127.0.0.1',headers or {})[1]
    def blocked(self,status,method,path,role=None,body=None,headers=None):
        with self.assertRaises(Problem) as exc: self.route(method,path,role,body,headers)
        self.assertEqual(exc.exception.status,status)
    def submit(self,role='A',request_id=None):
        return self.route('POST','/v2/jobs',role,dict(text='private Chinese 中文',request_id=request_id or str(uuid.uuid4())))

    def test_A_mailbox_and_legacy_bypasses(self):
        job=self.submit()
        self.assertTrue(job['accepted']);self.assertNotIn('state',job);self.assertNotIn('receipt',job)
        for path in ['/v2/jobs/'+job['id'],'/v2/jobs/lookup/'+job['shortID']]: self.blocked(403,'GET',path,'A')
        for path in ['/v1/jobs/'+job['id'],'/v1/jobs/lookup/'+job['shortID'],'/v1/device/jobs']:
            self.blocked(410,'GET',path,'C',headers={'X-Job-Receipt':'old-secret'})
        self.blocked(403,'POST','/v2/jobs',None,dict(text='x',request_id=str(uuid.uuid4())))

    def test_anonymous_own_progress_only_no_content(self):
        session=self.route('POST','/v2/auth/anonymous',body={'browser_id':'psb_'+uuid.uuid4().hex})
        self.keys['anon']=self.auth.authenticate('Bearer '+session['token'])
        own=self.submit('anon');other=self.submit('B')
        self.assertTrue(own['statusReadable'])
        state=self.route('GET','/v2/jobs/lookup/'+own['shortID'],'anon')
        self.assertEqual(state['state'],'queued');self.assertIn('progress',state)
        self.assertNotIn('text',state);self.assertNotIn('metadata',state);self.assertNotIn('content',state)
        self.blocked(404,'GET','/v2/jobs/'+other['id'],'anon')
        self.blocked(403,'POST','/v2/admin/jobs/search','anon')
        self.blocked(403,'GET','/v2/jobs/'+own['id'],'A')

    def test_owner_execution_and_safe_retirement(self):
        owner,token=self.owner();self.keys['owner']=owner
        self.route('GET','/v2/device/jobs','owner')
        self.blocked(403,'GET','/v2/device/jobs','C')
        job=self.submit('B');path='/v2/device/jobs/'+job['id']
        claim=self.route('POST',path+'/claim','owner')['claim']
        full=self.route('GET',path,'owner',headers={'X-Job-Claim':claim})
        self.assertIn('ip',full['metadata']);self.assertIn('userAgent',full['metadata'])
        self.blocked(409,'POST','/v2/admin/security/retire-others','owner',{'keep':owner['id'],'confirm':True})
        self.route('POST',path+'/result','owner',{'claim':claim,'state':'unknown'})
        result=self.route('POST','/v2/admin/security/retire-others','owner',{'keep':owner['id'],'confirm':True})
        self.assertEqual(result['kept'],owner['id'])
        for value in self.credentials.values():
            with self.assertRaises(Problem):self.auth.authenticate('Bearer '+value['token'])
        self.assertEqual(self.auth.authenticate('Bearer '+token)['role'],'C')

    def test_cutover_pauses_claims_without_exposing_payload(self):
        job=self.submit('B');self.s.c['execution_paused']=True
        self.blocked(503,'POST','/v2/device/jobs/'+job['id']+'/claim','M')
        self.assertEqual(self.route('GET','/v2/jobs/'+job['id'],'B')['state'],'queued')

    def test_B_own_state_only(self):
        job=self.submit('B');state=self.route('GET','/v2/jobs/'+job['id'],'B')
        self.assertEqual(set(state),{'id','shortID','state','created','updated','lastStage','progress'})
        other=self.submit('A');self.blocked(404,'GET','/v2/jobs/'+other['id'],'B')
        self.blocked(403,'POST','/v2/admin/jobs/search','B')

    def test_M_claim_scoped_payload_and_final_denial(self):
        job=self.submit('B');queue=self.route('GET','/v2/device/jobs','M')['jobs']
        self.assertEqual(queue[0]['text'],''); self.assertEqual(queue[0]['metadata'],{})
        self.blocked(403,'GET','/v2/device/jobs/'+job['id'],'M')
        claim=self.route('POST','/v2/device/jobs/'+job['id']+'/claim','M')['claim']
        full=self.route('GET','/v2/device/jobs/'+job['id'],'M',headers={'X-Job-Claim':claim})
        self.assertIn('中文',full['text']);self.assertEqual(set(full['metadata']),{'identity','authenticated','ip','userAgent','language','browser'})
        self.blocked(403,'POST','/v2/jobs','M',dict(text='x',request_id=str(uuid.uuid4())))
        self.route('POST','/v2/device/jobs/'+job['id']+'/result','M',dict(claim=claim,state='sent'))
        self.blocked(403,'GET','/v2/device/jobs/'+job['id'],'M',headers={'X-Job-Claim':claim})
        self.assertEqual(self.route('GET','/v2/device/jobs','M')['jobs'],[])
        self.blocked(403,'POST','/v2/device/jobs/'+job['id']+'/confirm','M')
        self.route('POST','/v2/admin/jobs/'+job['id']+'/confirm','C')

    def test_C_history_filters_and_no_secrets(self):
        self.submit('B');self.submit('A')
        result=self.route('POST','/v2/admin/jobs/export','C',dict(user_id=self.keys['B']['user_id']))
        self.assertEqual(len(result['jobs']),1)
        self.assertIn('中文',result['jobs'][0]['text']);self.assertIn('ip',result['jobs'][0]['metadata'])
        raw=json.dumps(result)
        for secret in ('claim_hash','claim_key','receipt','secret_hash'): self.assertNotIn('"'+secret+'"',raw)

    def test_passphrase_rotation_preserves_identity(self):
        entry=self.auth.create('User','B',kind='passphrase');session=self.auth.login({'credential':entry['token']},'1.2.3.4')
        self.assertEqual(self.auth.authenticate('Bearer '+session['token'])['user_id'],entry['user_id'])
        updated=self.route('POST','/v2/admin/keys/'+entry['id']+'/rotate','C')
        self.assertEqual(updated['user_id'],entry['user_id'])
        with self.assertRaises(Problem): self.auth.authenticate('Bearer '+session['token'])
        with self.assertRaises(Problem): self.auth.login({'credential':entry['token']},'1.2.3.4')
        self.assertTrue(self.auth.login({'credential':updated['token']},'1.2.3.4'))

    def test_anonymous_registration_and_revoke(self):
        self.blocked(400,'POST','/v2/auth/anonymous',body={'browser_id':'fake'})
        result=self.route('POST','/v2/auth/anonymous',body={'browser_id':'psb_'+uuid.uuid4().hex})
        key=self.auth.authenticate('Bearer '+result['token']);self.assertTrue(key['anonymous']);self.assertEqual(key['role'],'A')
        self.keys['anon']=key;job=self.submit('anon')
        self.route('POST','/v2/admin/identities/'+key['user_id']+'/revoke','C')
        self.assertEqual(self.route('GET','/v2/jobs/'+job['id'],'C')['state'],'rejected')
        with self.assertRaises(Problem):self.auth.authenticate('Bearer '+result['token'])

    def test_owner_challenge_replay_and_revoke(self):
        private=rsa.generate_private_key(public_exponent=65537,key_size=3072)
        pem=private.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode()
        entry=self.auth.public_key_add('owner',pem)
        self.assertEqual(self.auth.get(entry['id'])['kind'],'pendingkey')
        challenge=self.auth.challenge({'id':entry['id']},'1.2.3.4')
        signature=private.sign(challenge['message'].encode(),padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        body=dict(challenge=challenge['challenge'],signature=base64.b64encode(signature).decode())
        session=self.auth.prove(body,'1.2.3.4');self.assertEqual(session['role'],'C');self.assertEqual(session['maxAge'],600)
        with self.assertRaises(Problem): self.auth.prove(body,'1.2.3.4')
        self.auth.revoke(entry['id'],self.keys['C'])
        with self.assertRaises(Problem): self.auth.authenticate('Bearer '+session['token'])

    def test_idempotency(self):
        identifier=str(uuid.uuid4());one=self.submit('A',identifier);two=self.submit('A',identifier)
        self.assertEqual(one['id'],two['id'])
        with self.s.connect() as db:self.assertEqual(db.execute('SELECT count(*) FROM jobs').fetchone()[0],1)

    def test_concurrent_duplicate_has_one_physical_candidate(self):
        identifier=str(uuid.uuid4())
        with ThreadPoolExecutor(max_workers=8) as pool:
            jobs=list(pool.map(lambda _:self.submit('B',identifier),range(16)))
        self.assertEqual(len({j['id'] for j in jobs}),1)
        with self.s.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM jobs').fetchone()[0],1)
            self.assertEqual(db.execute("SELECT jobs FROM usage WHERE ip='user:'||?",(self.keys['B']['user_id'],)).fetchone()[0],1)

    def test_paging_does_not_skip_equal_timestamps(self):
        for _ in range(7): self.submit('B')
        with self.s.connect() as db:db.execute('UPDATE jobs SET created=42')
        first=self.route('POST','/v2/admin/jobs/export','C')
        second=self.route('POST','/v2/admin/jobs/export','C',{'before':first['next']})
        self.assertEqual(len(first['jobs']),5);self.assertEqual(len(second['jobs']),2)
        self.assertEqual(len({j['id'] for j in first['jobs']+second['jobs']}),7)

    def test_late_durable_report_recovers_without_reprint(self):
        job=self.submit('B');path='/v2/device/jobs/'+job['id']
        claim=self.route('POST',path+'/claim','M')['claim']
        with self.s.connect() as db:db.execute('UPDATE jobs SET updated=0 WHERE id=?',(job['id'],))
        self.blocked(403,'GET',path,'M',headers={'X-Job-Claim':claim})
        self.route('GET','/v2/device/jobs','M')
        self.assertEqual(self.route('GET','/v2/jobs/'+job['id'],'B')['state'],'unknown')
        self.route('POST',path+'/result','M',dict(claim=claim,state='sent'))
        self.assertEqual(self.route('GET','/v2/jobs/'+job['id'],'B')['state'],'sent')
        self.blocked(409,'POST',path+'/claim','M')

    def test_rename_keeps_snapshot_and_metadata_pages_hide_hashes(self):
        first=self.submit('B')
        self.route('POST','/v2/admin/keys/'+self.keys['B']['id']+'/rename','C',dict(name='renamed'))
        self.keys['B']=self.auth.get(self.keys['B']['id'])
        second=self.submit('B')
        page=self.route('POST','/v2/admin/jobs/search','C')
        names={j['id']:j['metadata']['identity'] for j in page['jobs']}
        self.assertEqual(names[first['id']],'B');self.assertEqual(names[second['id']],'renamed')
        for collection in ('keys','identities','sessions'):
            raw=json.dumps(self.route('POST','/v2/admin/'+collection+'/search','C'))
            self.assertNotIn('secret_hash',raw);self.assertNotIn('public_key',raw);self.assertNotIn('csrf',raw)

    def test_last_C_and_inactive_owner_protection(self):
        private=rsa.generate_private_key(public_exponent=65537,key_size=3072)
        pem=private.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode()
        self.auth.public_key_add('pending',pem)
        with self.assertRaises(Problem) as exc:self.auth.revoke(self.keys['C']['id'],{'id':'local-cli'})
        self.assertEqual(exc.exception.status,409)
        self.blocked(403,'POST','/v2/admin/public-keys','C',dict(name='x',public_key=pem))

    def test_invalid_and_expired_proof(self):
        private=rsa.generate_private_key(public_exponent=65537,key_size=3072)
        pem=private.public_key().public_bytes(serialization.Encoding.PEM,serialization.PublicFormat.SubjectPublicKeyInfo).decode()
        entry=self.auth.public_key_add('owner',pem)
        challenge=self.auth.challenge({'id':entry['id']},'1.2.3.4')
        with self.assertRaises(Problem):self.auth.prove(dict(challenge=challenge['challenge'],signature='AAAA'),'1.2.3.4')
        challenge=self.auth.challenge({'id':entry['id']},'1.2.3.4')
        with self.s.connect() as db:db.execute('UPDATE challenges SET expires=0')
        with self.assertRaises(Problem):self.auth.prove(dict(challenge=challenge['challenge'],signature='AAAA'),'1.2.3.4')

    def test_anonymous_limits_and_minimal_metadata(self):
        result=self.route('POST','/v2/auth/anonymous',body={'browser_id':'psb_'+uuid.uuid4().hex})
        self.keys['anon']=self.auth.authenticate('Bearer '+result['token'])
        first=self.route('POST','/v2/jobs','anon',dict(text='browser fields',request_id=str(uuid.uuid4()),browser=dict(language='zh-CN',platform='Windows',timezone='discard',screen='discard',secret='discard')),headers={'User-Agent':'Firefox/132.0'})
        claim=self.route('POST','/v2/device/jobs/'+first['id']+'/claim','M')['claim']
        payload=self.route('GET','/v2/device/jobs/'+first['id'],'M',headers={'X-Job-Claim':claim})
        self.assertEqual(payload['metadata']['browser'],dict(language='zh-CN',platform='Windows'))
        self.assertEqual(payload['metadata']['userAgent'],'Firefox/132.0')
        self.assertNotIn('metadata',self.route('GET','/v2/jobs/'+first['id'],'anon'))
        for _ in range(4):self.submit('anon')
        self.blocked(429,'POST','/v2/jobs','anon',dict(text='sixth',request_id=str(uuid.uuid4())))
        job=self.route('POST','/v2/admin/jobs/search','C')['jobs'][0]
        self.assertEqual(set(job['metadata']),{'ip','userAgent','language','browser','identity','authenticated'})
        self.assertFalse(job['metadata']['authenticated'])

    def test_legacy_migration_backup_mapping_and_repeat(self):
        path=str(Path(self.temp.name)/'legacy.db')
        key_id=str(uuid.uuid4());token='psr_legacy_fixture'
        import hashlib
        with sqlite3.connect(path) as db:
            db.executescript('''CREATE TABLE keys(id TEXT PRIMARY KEY,name TEXT,role TEXT,device TEXT,hash TEXT UNIQUE,revoked INTEGER DEFAULT 0,created REAL);
            CREATE TABLE jobs(id TEXT PRIMARY KEY,device TEXT,text TEXT,meta TEXT,state TEXT,receipt TEXT,created REAL,updated REAL,claim_key TEXT,claim_hash TEXT,detail TEXT DEFAULT '',submit_key TEXT);''')
            db.execute('INSERT INTO keys VALUES(?,?,?,?,?,0,1)',(key_id,'legacy','submit','p2',hashlib.sha256(token.encode()).hexdigest()))
            db.execute("INSERT INTO jobs(id,device,text,meta,state,receipt,created,updated,submit_key) VALUES(?,'p2','old','{}','queued','hash',1,1,?)",('b'*40,key_id))
        db.close()
        migrated=Store(dict(database=path))
        self.assertEqual(migrated.api.auth.authenticate('Bearer '+token)['role'],'B')
        with migrated.connect() as db:self.assertEqual(db.execute('SELECT user_id FROM jobs').fetchone()[0],key_id)
        backups=list(Path(self.temp.name).glob('legacy.db.pre-v0.3.0-*.bak'));self.assertEqual(len(backups),1)
        Store(dict(database=path))
        self.assertEqual(len(list(Path(self.temp.name).glob('legacy.db.pre-v0.3.0-*.bak'))),1)

    def test_raw_token_cannot_be_used_as_cookie(self):
        with self.assertRaises(Problem) as exc:
            self.auth.authenticate(cookie='psr_session='+self.credentials['C']['token'])
        self.assertEqual(exc.exception.status,401)

    def test_repeated_heartbeat_does_not_inflate_events(self):
        job=self.submit('B');path='/v2/device/jobs/'+job['id']
        claim=self.route('POST',path+'/claim','M')['claim']
        for _ in range(20):
            self.route('POST',path+'/result','M',dict(claim=claim,state='printing'))
        with self.s.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM job_events WHERE job_id=?',(job['id'],)).fetchone()[0],3)

    def test_prune_removes_related_rows_in_one_pass(self):
        import time
        job=self.submit('B')
        with self.s.connect() as db:
            db.execute('UPDATE jobs SET updated=0 WHERE id=?',(job['id'],))
            self.s.prune(db,time.time())
            for table in ('job_events','submission_ids'):
                self.assertEqual(db.execute('SELECT count(*) FROM '+table+' WHERE job_id=?',(job['id'],)).fetchone()[0],0)

    def test_admin_creation_generates_ids_and_optional_passphrase(self):
        token=self.route('POST','/v2/admin/keys','C',dict(name='Automatic identity',role='A',kind='token'))
        uuid.UUID(token['id']);uuid.UUID(token['user_id'])
        self.assertNotEqual(token['id'],token['user_id']);self.assertTrue(token['token'].startswith('psr_'))
        phrase=self.route('POST','/v2/admin/keys','C',dict(name='Automatic phrase',role='B',kind='passphrase'))
        self.assertTrue(phrase['token'].startswith('psp_'+phrase['id']+'.'))
        linked=self.route('POST','/v2/admin/keys','C',dict(name='Existing identity',role='B',kind='token',user_id=token['user_id']))
        self.assertEqual(linked['user_id'],token['user_id']);self.assertNotEqual(linked['id'],token['id'])

    def test_admin_table_limits_filter_before_paging_and_summary(self):
        for index in range(7):
            entry=self.auth.create('table '+str(index),'A')
            if index%2==0:self.auth.revoke(entry['id'],self.keys['C'])
        first=self.route('POST','/v2/admin/keys/search','C',dict(limit=2,hide_revoked=True))
        second=self.route('POST','/v2/admin/keys/search','C',dict(limit=2,hide_revoked=True,before=first['next']))
        self.assertEqual(len(first['keys']),2);self.assertEqual(len(second['keys']),2)
        self.assertFalse({r['id'] for r in first['keys']}&{r['id'] for r in second['keys']})
        self.assertTrue(all(not r['revoked'] for r in first['keys']+second['keys']))
        all_rows=self.route('POST','/v2/admin/keys/search','C',dict(limit=100,hide_revoked=False))
        self.assertTrue(any(r['revoked'] for r in all_rows['keys']))
        for limit in (0,101,True,'25'):
            self.blocked(400,'POST','/v2/admin/keys/search','C',dict(limit=limit))
        self.blocked(400,'POST','/v2/admin/identities/search','C',dict(hide_revoked='true'))
        jobs=[self.submit('B') for _ in range(6)]
        summary=self.route('POST','/v2/admin/jobs/search','C',dict(summary=True,limit=4))
        self.assertEqual(len(summary['jobs']),4)
        for row in summary['jobs']:self.assertFalse(set(row)&{'text','content','metadata','events'})
        remaining=self.route('POST','/v2/admin/jobs/search','C',dict(summary=True,limit=4,before=summary['next']))
        self.assertEqual(len(remaining['jobs']),2)
        full=self.route('POST','/v2/admin/jobs/search','C',dict(job_id=jobs[0]['id'],limit=1))
        self.assertEqual(len(full['jobs']),1);self.assertIn('text',full['jobs'][0])
        for path in ('/v2/admin/identities/search','/v2/admin/sessions/search','/v2/admin/audit'):
            result=self.route('POST',path,'C',dict(limit=1))
            self.assertLessEqual(len(result[next(k for k in result if k!='next')]),1)

    def test_HTTP_cookie_csrf_and_native_login(self):
        self.s.c['port']=0;server=Server(self.s);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
        base='http://127.0.0.1:'+str(server.server_port)
        def call(path,body=None,headers=None):
            wire=json.dumps(body).encode() if body is not None else None
            req=urllib.request.Request(base+path,data=wire,headers={'Content-Type':'application/json',**(headers or {})})
            return urllib.request.urlopen(req)
        try:
            for path in ('/v1/public/status','/v1/device/jobs'):
                with self.assertRaises(urllib.error.HTTPError) as exc:
                    call(path,headers={'Authorization':'Bearer revoked'})
                self.assertEqual(exc.exception.code,410);exc.exception.close()
            with call('/v2/auth/login',{'credential':self.credentials['B']['token']}) as res:
                cookie=res.headers['Set-Cookie'];data=json.load(res)
            self.assertIn('HttpOnly; Secure; SameSite=Strict',cookie);self.assertNotIn('token',data)
            header={'Cookie':cookie.split(';')[0]}
            with call('/v2/auth/me',headers=header) as res: me=json.load(res)
            with self.assertRaises(urllib.error.HTTPError) as exc:call('/v2/jobs',dict(text='x',request_id=str(uuid.uuid4())),header)
            self.assertEqual(exc.exception.code,403)
            exc.exception.close()
            header.update({'Origin':base,'X-CSRF-Token':me['csrf']})
            with call('/v2/jobs',dict(text='x',request_id=str(uuid.uuid4())),header) as res:self.assertTrue(json.load(res)['accepted'])
            with call('/v2/auth/login',{'credential':self.credentials['C']['token']},{'X-Paperang-Native':'1'}) as res:self.assertIn('token',json.load(res))
        finally:server.shutdown();server.server_close();thread.join()


if __name__=='__main__': unittest.main()
