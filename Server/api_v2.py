"""Least-privilege API; v1 content capabilities are deliberately retired."""
import hashlib
import json
import re
import time
import uuid
from security import Security


class APIv2:
    def __init__(self, store, problem):
        self.store, self.Problem = store, problem
        self.auth = Security(store, problem)

    def fail(self, status, text):
        raise self.Problem(status,text)

    @staticmethod
    def state(row):
        return {k:row[k] for k in ('id','state','created','updated')} | {'shortID':row['short_id']}

    def route(self, method, path, body, key, ip, headers):
        s, a = self.store, self.auth
        if path.startswith('/v1/'):
            self.fail(410,'api_v1_retired_upgrade_to_v0.3.0')
        if method=='GET' and path=='/v2/public/status':
            return 200,dict(version='0.3.0',status=dict(accepting=True),policy=s.policies(),capabilities=dict(contentTypes=['text','raster1'],contentSchemaVersion=1,maxRequestBytes=163840,identityRequired=True,authVersion=2))
        if method=='POST' and path=='/v2/auth/anonymous':
            if not s.c['allow_anonymous']: self.fail(403,'anonymous_disabled')
            a.limit('register:'+ip,20)
            browser = body.get('browser_id','')
            if not isinstance(browser,str) or not re.fullmatch(r'psb_[a-f0-9]{32}',browser): self.fail(400,'browser_identity_not_ready')
            entry = a.create('Anonymous','A')
            with s.connect() as db: db.execute('UPDATE identities SET anonymous=1 WHERE id=?',(entry['user_id'],))
            return 201,a.session(a.get(entry['id']),browser)
        if method=='POST' and path=='/v2/auth/login': return 200,a.login(body,ip)
        if method=='POST' and path=='/v2/auth/challenge': return 200,a.challenge(body,ip)
        if method=='POST' and path=='/v2/auth/prove': return 200,a.prove(body,ip)
        if path=='/v2/auth/me' and method=='GET':
            if not key: self.fail(401,'identity_required')
            return 200,{k:key.get(k) for k in ('id','user_id','browser_id','role','name','csrf','anonymous')}
        if path=='/v2/auth/logout' and method=='POST':
            if key:
                with s.connect() as db: db.execute('DELETE FROM sessions WHERE credential_id=?',(key['id'],))
            return 200,dict(loggedOut=True)
        if method=='POST' and path=='/v2/jobs':
            a.require(key,['A','B','C'])
            request_id=body.get('request_id','')
            if not isinstance(request_id,str) or not re.fullmatch(r'[a-f0-9-]{36}',request_id): self.fail(400,'request_id_required')
            # Serialized with submission itself (single Store lock, not the network).
            with s.submit_lock:
                with s.connect() as db: previous=db.execute('SELECT job_id FROM submission_ids WHERE user_id=? AND request_id=?',(key['user_id'],request_id)).fetchone()
                if previous:
                    with s.connect() as db: row=db.execute('SELECT * FROM jobs WHERE id=?',(previous['job_id'],)).fetchone()
                    if not row: self.fail(409,'idempotent_job_expired')
                    return 202,dict(id=row['id'],shortID=row['short_id'],accepted=True,statusReadable=key['role'] in ('B','C'))
                legacy={**key,'role':'submit','request_id':request_id}
                # Quotas: IP + stable identity + credential + aggregate, including anonymous.
                prefix='anonymous' if key['anonymous'] else 'authenticated'
                units=len(body.get('text','')) if isinstance(body.get('text',''),str) else 0
                units+=body.get('content',{}).get('height',0) if isinstance(body.get('content'),dict) and type(body['content'].get('height')) is int else 0
                window=int(time.time()//3600)
                buckets=['user:'+key['user_id'],'credential:'+key['id'],'global']
                with s.connect() as db:
                    for bucket in buckets:
                        use=db.execute('SELECT * FROM usage WHERE ip=? AND window=?',(bucket,window)).fetchone()
                        max_jobs=s.c['global_hourly_limit'] if bucket=='global' else s.c[prefix+'_hourly_limit']
                        max_chars=s.c['global_hourly_chars'] if bucket=='global' else s.c[prefix+'_hourly_chars']
                        if use and (use['jobs']>=max_jobs or use['chars']+units>max_chars): self.fail(429,'identity_or_global_quota_reached')
                # Anonymous has an identity but must retain the anonymous limits.
                if key['anonymous'] and units>s.c['anonymous_max_length']: self.fail(400,'anonymous_size_limit')
                result=s.submit(body,legacy,ip,headers)
                return 202,dict(id=result['id'],shortID=result['shortID'],accepted=True,statusReadable=key['role'] in ('B','C'))
        lookup=re.fullmatch(r'/v2/jobs/(?:lookup/)?([A-Fa-f0-9-]{12,40})',path)
        if method=='GET' and lookup:
            a.require(key,['B','C'])
            value=lookup.group(1).replace('-','')
            with s.connect() as db:
                row=db.execute('SELECT * FROM jobs WHERE id=? OR short_id=?',(lookup.group(1),value.upper())).fetchone()
            if not row or (key['role']!='C' and row['user_id']!=key['user_id']): self.fail(404,'job_not_found')
            return 200,self.state(row)
        if path.startswith('/v2/admin/'):
            a.require(key,['C'])
            if method=='POST' and path in ('/v2/admin/keys/search','/v2/admin/identities/search','/v2/admin/sessions/search'):
                before=body.get('before',1e20)
                if type(before) not in (int,float): self.fail(400,'invalid_cursor')
                collection=path.split('/')[3]
                with s.connect() as db:
                    if collection=='keys':
                        rows=db.execute('SELECT c.rowid AS cursor,c.id,c.user_id,i.name,c.role,c.device,c.kind,c.revoked,c.created FROM credentials c JOIN identities i ON i.id=c.user_id WHERE c.rowid<? ORDER BY c.rowid DESC LIMIT 100',(before,)).fetchall()
                    elif collection=='identities':
                        rows=db.execute('SELECT rowid AS cursor,* FROM identities WHERE rowid<? ORDER BY rowid DESC LIMIT 100',(before,)).fetchall()
                    else:
                        rows=db.execute('SELECT rowid AS cursor,credential_id,browser_id,expires FROM sessions WHERE rowid<? AND expires>? ORDER BY rowid DESC LIMIT 100',(before,time.time())).fetchall()
                return 200,{collection:[{k:r[k] for k in r.keys() if k!='cursor'} for r in rows], 'next':rows[-1]['cursor'] if rows else None}
            if path=='/v2/admin/keys' and method=='GET':
                with s.connect() as db: rows=db.execute('SELECT c.id,c.user_id,i.name,c.role,c.device,c.kind,c.revoked,c.created FROM credentials c JOIN identities i ON i.id=c.user_id ORDER BY c.created DESC LIMIT 500').fetchall()
                return 200,dict(keys=[dict(r) for r in rows])
            if path=='/v2/admin/keys' and method=='POST':
                entry=a.create(body.get('name'),body.get('role','A'),body.get('device'),body.get('kind','token'),body.get('user_id'),body.get('password'))
                a.audit(key,'create',entry['id']); return 201,entry
            if path=='/v2/admin/public-keys' and method=='POST':
                if not key.get('session'): self.fail(403,'reauthenticate_before_trust_change')
                # New public keys are inert until possession is proven using their challenge.
                entry=a.public_key_add(body.get('name'),body.get('public_key',''))
                a.audit(key,'trust_public_key',entry['id']); return 201,entry
            match=re.fullmatch(r'/v2/admin/keys/([a-f0-9-]{36})/(rename|revoke|rotate|delete)',path)
            if method=='POST' and match:
                identifier,action=match.groups()
                if action=='revoke': return 200,a.revoke(identifier,key)
                old=a.get(identifier) if action!='delete' else None
                if action=='rotate':
                    if old['kind'] in ('publickey','pendingkey'): self.fail(400,'register_replacement_public_key')
                    if old['id']==key['id']: self.fail(409,'use_another_admin_to_rotate_current_key')
                    entry=a.create(old['name'],old['role'],old['device'],old['kind'],old['user_id'],body.get('password'))
                    a.revoke(identifier,key); return 201,entry
                with s.connect() as db:
                    if action=='rename':
                        name=body.get('name','')
                        if not isinstance(name,str) or not 1<=len(name.strip())<=80: self.fail(400,'invalid_name')
                        db.execute('UPDATE identities SET name=? WHERE id=?',(name.strip(),old['user_id']))
                    else:
                        row=db.execute('SELECT revoked FROM credentials WHERE id=?',(identifier,)).fetchone()
                        if not row or not row['revoked']: self.fail(409,'revoke_before_delete')
                        # Retain credential tombstone to preserve audit identity.
                a.audit(key,action,identifier); return 200,dict(updated=True)
            if path=='/v2/admin/identities' and method=='GET':
                with s.connect() as db: rows=db.execute('SELECT * FROM identities ORDER BY rowid DESC LIMIT 500').fetchall()
                return 200,dict(identities=[dict(r) for r in rows])
            rev=re.fullmatch(r'/v2/admin/identities/([a-f0-9-]{36})/revoke',path)
            if method=='POST' and rev:
                uid=rev.group(1)
                with s.connect() as db:
                    db.execute('BEGIN IMMEDIATE')
                    # Guard any administrator identity, not only the current credential.
                    if db.execute("SELECT id FROM credentials WHERE user_id=? AND role='C' AND revoked=0",(uid,)).fetchone(): db.execute('ROLLBACK'); self.fail(409,'revoke_admin_credentials_individually')
                    db.execute('UPDATE identities SET revoked=1 WHERE id=?',(uid,))
                    db.execute("UPDATE jobs SET state='rejected',detail='Identity revoked',updated=? WHERE user_id=? AND state='queued'",(time.time(),uid))
                    db.execute('DELETE FROM sessions WHERE credential_id IN (SELECT id FROM credentials WHERE user_id=?)',(uid,))
                    db.execute('COMMIT')
                a.audit(key,'revoke_identity',uid); return 200,dict(revoked=True)
            if path=='/v2/admin/sessions' and method=='GET':
                with s.connect() as db: rows=db.execute('SELECT credential_id,browser_id,expires FROM sessions WHERE expires>? ORDER BY expires DESC LIMIT 500',(time.time(),)).fetchall()
                return 200,dict(sessions=[dict(r) for r in rows])
            if path=='/v2/admin/sessions/revoke' and method=='POST':
                with s.connect() as db: db.execute('DELETE FROM sessions WHERE credential_id=?',(body.get('credential_id',''),))
                a.audit(key,'revoke_sessions',body.get('credential_id','')); return 200,dict(revoked=True)
            if path=='/v2/admin/audit' and method=='POST':
                before=body.get('before',2**63-1)
                if type(before) is not int: self.fail(400,'invalid_cursor')
                with s.connect() as db: rows=db.execute('SELECT * FROM audit_events WHERE id<? ORDER BY id DESC LIMIT 100',(before,)).fetchall()
                return 200,dict(events=[dict(r) for r in rows],next=rows[-1]['id'] if rows else None)
            if path in ('/v2/admin/jobs/search','/v2/admin/jobs/export') and method=='POST':
                before=body.get('before',1e20); uid=body.get('user_id',''); job_id=body.get('job_id','')
                if type(before) not in (int,float) or not isinstance(uid,str) or not isinstance(job_id,str): self.fail(400,'invalid_filter')
                # Bounded pages; export streams pages in the native client.
                with s.connect() as db: rows=db.execute('SELECT rowid AS cursor,* FROM jobs WHERE rowid<? AND (?=\'\' OR user_id=?) AND (?=\'\' OR id=?) ORDER BY rowid DESC LIMIT 5',(before,uid,uid,job_id,job_id)).fetchall()
                jobs=[]
                for row in rows:
                    detail=s.job(row); detail.update(user_id=row['user_id'],browser_id=row['browser_id'],credential_id=row['credential_id'])
                    with s.connect() as db: detail['events']=[dict(r) for r in db.execute('SELECT at,state FROM job_events WHERE job_id=? ORDER BY id',(row['id'],))]
                    jobs.append(detail)
                a.audit(key,'export' if path.endswith('export') else 'history',uid)
                return 200,dict(jobs=jobs,next=rows[-1]['cursor'] if rows else None)
        if path.startswith('/v2/device/'):
            a.require(key,['M'])
            legacy={**key,'role':'device'}
            if method=='GET' and path=='/v2/device/jobs':
                with s.connect() as db:
                    s.prune(db,time.time())
                    rows=db.execute("SELECT * FROM jobs WHERE device=? AND state='queued' ORDER BY created LIMIT 25",(key['device'],)).fetchall()
                # No body or delivery metadata before claim.
                jobs=[dict(id=r['id'],shortID=r['short_id'],state=r['state'],created=r['created'],text='',metadata={},truncated=True) for r in rows]
                return 200,dict(jobs=jobs)
            detail=re.fullmatch(r'/v2/device/jobs/([a-f0-9-]{36,40})',path)
            if method=='GET' and detail:
                with s.connect() as db: row=db.execute('SELECT * FROM jobs WHERE id=? AND device=?',(detail.group(1),key['device'])).fetchone()
                claim=headers.get('X-Job-Claim','')
                if not row or row['state'] not in ('claimed','printing') or row['updated']<time.time()-300 or row['claim_key']!=key['id'] or not claim or not __import__('hmac').compare_digest(row['claim_hash'] or '',hashlib.sha256(claim.encode()).hexdigest()): self.fail(403,'active_claim_required')
                value=s.job(row)
                # Render fields only. No arbitrary browser metadata or full delivery record.
                meta=value['metadata']; value['metadata']={k:meta[k] for k in ('identity','authenticated') if k in meta}
                return 200,value
            if path.endswith('/confirm'): self.fail(403,'manual_confirmation_requires_C')
            status,result=s.route_legacy(method,path.replace('/v2/','/v1/',1),body,legacy,ip,headers)
            return status,result
        confirm=re.fullmatch(r'/v2/admin/jobs/([a-f0-9-]{36,40})/confirm',path)
        if method=='POST' and confirm:
            a.require(key,['C'])
            with s.connect() as db:
                row=db.execute("SELECT device FROM jobs WHERE id=? AND state IN ('sent','confirmed')",(confirm.group(1),)).fetchone()
            if not row: self.fail(409,'only_sent_can_be_confirmed')
            result=s.route_legacy('POST','/v1/device/jobs/'+confirm.group(1)+'/confirm',body,{**key,'role':'device','device':row['device']},ip,headers)
            a.audit(key,'paper_confirm',confirm.group(1)); return result
        self.fail(404,'unknown_route')
