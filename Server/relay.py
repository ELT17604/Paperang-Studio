#!/usr/bin/env python3
"""Paperang Studio relay: HTTP + SQLite with Argon2id/RSA authentication, Python 3.10+."""
import base64, binascii
import argparse, contextlib, hashlib, hmac, ipaddress, json, os, re, secrets, signal, sqlite3, sys, threading, time, uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

APP_VERSION = '0.3.1-linuxrelayserver-fix1'

JOB_PATTERN = r'(?:[a-f0-9]{40}|[a-f0-9-]{36})'
SHORT_JOB_PATTERN = r'(?:[A-Fa-f0-9]{12}|[A-Fa-f0-9]{4}(?:-[A-Fa-f0-9]{4}){2})'

def transport_progress(detail):
    """Read existing Mac reports; these are raster sends, not paper confirmation."""
    if not isinstance(detail,str):return None
    match=re.fullmatch(r'第([0-9]{1,2})/([0-9]{1,2})份 · 分包([0-9]{1,3})/([0-9]{1,3})',detail)
    if not match:return None
    copy,copies,part,total=map(int,match.groups())
    if not 1<=copy<=copies<=20 or not 1<=part<=total<=256:return None
    return dict(completed=(copy-1)*total+part,total=copies*total,unit='partitions',basis='transport')

DEFAULTS = dict(bind='127.0.0.1', port=8177, database='relay.sqlite3', allow_anonymous=True,
    anonymous_max_length=500, anonymous_hourly_limit=5, anonymous_hourly_chars=2000,
    authenticated_max_length=10000, authenticated_hourly_limit=60, authenticated_hourly_chars=60000,
    queue_limit=200, retention_days=7, global_hourly_limit=6000, global_hourly_chars=6000000, trusted_proxies=[], device_id='p2')

def digest(token): return hashlib.sha256(token.encode()).hexdigest()
def compact(value): return json.dumps(value, ensure_ascii=False, separators=(',', ':'))
def valid_device(value): return isinstance(value, str) and re.fullmatch(r'[A-Za-z0-9_-]{1,64}', value) is not None

class Problem(Exception):
    def __init__(self, status, message, **extra): self.status, self.message, self.extra = status, message, extra

class Store:
    version = APP_VERSION
    def __init__(self, config):
        self.c = {**DEFAULTS, **config}
        if not valid_device(self.c['device_id']): raise ValueError('Invalid device_id')
        for field in ['port','anonymous_max_length','anonymous_hourly_limit','anonymous_hourly_chars',
                      'authenticated_max_length','authenticated_hourly_limit','authenticated_hourly_chars','queue_limit','retention_days','global_hourly_limit','global_hourly_chars']:
            n=self.c[field]
            if type(n) is not int or n<1: raise ValueError('Invalid '+field)
        if self.c['port']>65535 or max(self.c['anonymous_max_length'],self.c['authenticated_max_length'])>10000:
            raise ValueError('Port must be <=65535; text limits <=10000')
        if type(self.c['allow_anonymous']) is not bool: raise ValueError('allow_anonymous must be boolean')
        self.proxies=[ipaddress.ip_network(v) for v in self.c['trusted_proxies']]
        db=Path(self.c['database']).expanduser().resolve(); db.parent.mkdir(parents=True,exist_ok=True)
        self.db=str(db)
        with self.connect() as conn:
            conn.executescript('''PRAGMA journal_mode=WAL;
              CREATE TABLE IF NOT EXISTS keys(id TEXT PRIMARY KEY,name TEXT NOT NULL,role TEXT NOT NULL,device TEXT NOT NULL,hash TEXT UNIQUE NOT NULL,revoked INTEGER DEFAULT 0,created REAL NOT NULL);
              CREATE TABLE IF NOT EXISTS usage(ip TEXT NOT NULL,window INTEGER NOT NULL,jobs INTEGER NOT NULL,chars INTEGER NOT NULL,PRIMARY KEY(ip,window));
              CREATE TABLE IF NOT EXISTS jobs(id TEXT PRIMARY KEY,device TEXT NOT NULL,text TEXT NOT NULL,meta TEXT NOT NULL,state TEXT NOT NULL,receipt TEXT NOT NULL,created REAL NOT NULL,updated REAL NOT NULL,claim_key TEXT,claim_hash TEXT,detail TEXT NOT NULL DEFAULT '');
              CREATE TABLE IF NOT EXISTS devices(id TEXT PRIMARY KEY,status TEXT NOT NULL,seen REAL NOT NULL);
              CREATE INDEX IF NOT EXISTS jobs_device_state ON jobs(device,state,created);
            ''')
        with self.connect() as conn:
            conn.execute('BEGIN IMMEDIATE')
            try:
                if 'short_id' not in [row[1] for row in conn.execute('PRAGMA table_info(jobs)')]:conn.execute('ALTER TABLE jobs ADD COLUMN short_id TEXT')
                columns={row[1] for row in conn.execute('PRAGMA table_info(jobs)')}
                if 'progress' not in columns:conn.execute('ALTER TABLE jobs ADD COLUMN progress TEXT')
                if 'submit_key' not in columns:conn.execute('ALTER TABLE jobs ADD COLUMN submit_key TEXT')
                for column,kind in [('content_meta','TEXT'),('payload','BLOB'),('last_stage','TEXT')]:
                    if column not in columns:conn.execute('ALTER TABLE jobs ADD COLUMN '+column+' '+kind)
                conn.execute("UPDATE jobs SET last_stage=state WHERE last_stage IS NULL AND state IN ('claimed','printing')")
                # Recover counts already reported by the unmodified Mac app.
                for row in conn.execute("SELECT id,detail FROM jobs WHERE progress IS NULL AND detail LIKE '第%'").fetchall():
                    progress=transport_progress(row['detail'])
                    if progress:conn.execute('UPDATE jobs SET progress=? WHERE id=?',(compact(progress),row['id']))
                conn.execute('CREATE UNIQUE INDEX IF NOT EXISTS jobs_short_id ON jobs(short_id)')
                for row in conn.execute('SELECT id FROM jobs WHERE short_id IS NULL').fetchall():conn.execute('UPDATE jobs SET short_id=? WHERE id=?',(self.short_id(conn),row['id']))
                conn.execute('COMMIT')
            except Exception:conn.execute('ROLLBACK');raise
        os.chmod(db,0o600)
        self.submit_lock=threading.Lock()
        from api_v2 import APIv2
        self.api=APIv2(self,Problem)
    def short_id(self,db):
        while True:
            value=secrets.token_hex(6).upper()
            if not db.execute('SELECT 1 FROM jobs WHERE short_id=?',(value,)).fetchone():return value
    def connect(self):
        conn=sqlite3.connect(self.db,timeout=10,isolation_level=None);conn.row_factory=sqlite3.Row
        return contextlib.closing(conn)
    def key_add(self,name,role,device,token=None):
        if role not in ['admin','device','submit'] or not valid_device(device): raise ValueError('Invalid role/device')
        if not isinstance(name,str) or not 1<=len(name)<=80: raise ValueError('Name must contain 1–80 characters')
        token=token or ('psr_'+secrets.token_urlsafe(32))
        if not isinstance(token,str) or not re.fullmatch(r'psr_[A-Za-z0-9_-]{40,100}',token):raise ValueError('Invalid key material')
        identifier=str(uuid.uuid4())
        with self.connect() as db: db.execute('INSERT INTO keys(id,name,role,device,hash,created) VALUES(?,?,?,?,?,?)',(identifier,name,role,device,digest(token),time.time()))
        if hasattr(self,'api'):
            with self.connect() as db:
                db.execute('INSERT INTO identities VALUES(?,?,0,0,?)',(identifier,name,time.time()))
                db.execute('INSERT INTO credentials VALUES(?,?,?,?,?,?,NULL,0,?)',(identifier,identifier,{'submit':'B','admin':'C','device':'M'}[role],device,'token',digest(token),time.time()))
        return dict(id=identifier,name=name,role=role,device=device,token=token)
    def key_revoke(self,identifier,actor=None,allow_lockout=False):
        if actor and identifier==actor['id']:
            raise Problem(409,'Cannot revoke the administrator key currently in use')
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            try:
                row=db.execute('SELECT * FROM keys WHERE id=?',(identifier,)).fetchone()
                if not row:raise Problem(404,'Key not found')
                if not row['revoked'] and not allow_lockout:
                    if row['role'] in ['admin','device']:
                        remaining=db.execute('SELECT count(*) FROM keys WHERE role=? AND device=? AND revoked=0 AND id<>?',(row['role'],row['device'],identifier)).fetchone()[0]
                        if not remaining:raise Problem(409,'Create and configure a replacement '+row['role']+' key before revoking the last active key')
                    if row['role']=='device':
                        device=db.execute('SELECT * FROM devices WHERE id=?',(row['device'],)).fetchone()
                        if device and time.time()-device['seen']<15 and json.loads(device['status']).get('_relay_key_id')==identifier:
                            raise Problem(409,'This device key is in use. Switch the Mac to its replacement key and reconnect before revoking it')
                db.execute('UPDATE keys SET revoked=1 WHERE id=?',(identifier,))
                db.execute('COMMIT')
            except Exception:db.execute('ROLLBACK');raise
        return dict(revoked=True)
    def authenticate(self,authorization):
        if not authorization: return None
        if not authorization.startswith('Bearer ') or len(authorization)>256: raise Problem(401,'Invalid access key')
        with self.connect() as db: row=db.execute('SELECT * FROM keys WHERE hash=? AND revoked=0',(digest(authorization[7:]),)).fetchone()
        if row is None: raise Problem(401,'Invalid or revoked access key')
        return dict(row)
    def require(self,key,roles):
        if key is None or key['role'] not in roles: raise Problem(403,'This operation requires a '+('/'.join(roles))+' key')
    def client_ip(self,peer,forwarded):
        def parse(value):
            p=ipaddress.ip_address(value.strip());return p.ipv4_mapped if isinstance(p,ipaddress.IPv6Address) and p.ipv4_mapped else p
        current=parse(peer)
        if any(current in net for net in self.proxies) and forwarded:
            try:
                chain=[parse(v) for v in forwarded.split(',')]
                if len(chain)>16: raise ValueError()
                for candidate in reversed(chain):
                    if not any(current in net for net in self.proxies): break
                    current=candidate
            except ValueError: raise Problem(400,'Invalid forwarded address')
        return str(current)
    def policies(self):
        return {k:self.c[k] for k in ['allow_anonymous','anonymous_max_length','anonymous_hourly_limit','anonymous_hourly_chars','authenticated_max_length','authenticated_hourly_limit','authenticated_hourly_chars','device_id','retention_days']}
    def content(self,body,limit):
        content=body.get('content')
        if content is None:return None,None,0
        if not isinstance(content,dict) or type(content.get('schemaVersion')) is not int or content.get('schemaVersion')!=1:raise Problem(400,'Unsupported content schema')
        if content.get('type')!='raster1':raise Problem(400,'Unsupported content type')
        width,height=content.get('width'),content.get('height')
        if type(width) is not int or width!=576 or type(height) is not int or not 1<=height<=min(1400,limit):raise Problem(400,'Raster must be 576 pixels wide and within the row allowance')
        if content.get('encoding')!='base64' or content.get('bitOrder')!='msb-first' or content.get('blackIsOne') is not True:raise Problem(400,'Unsupported raster encoding')
        encoded=content.get('data')
        if not isinstance(encoded,str) or len(encoded)!=4*((72*height+2)//3):raise Problem(400,'Invalid raster size')
        try:raw=base64.b64decode(encoded,validate=True)
        except (ValueError,binascii.Error):raise Problem(400,'Invalid raster base64')
        if len(raw)!=72*height:raise Problem(400,'Raster byte count mismatch')
        checksum=hashlib.sha256(raw).hexdigest()
        if content.get('sha256',checksum)!=checksum:raise Problem(400,'Raster checksum mismatch')
        meta=dict(schemaVersion=1,type='raster1',width=width,height=height,encoding='base64',bitOrder='msb-first',blackIsOne=True,sha256=checksum,byteLength=len(raw))
        return meta,raw,height
    def public_status(self):
        with self.connect() as db: row=db.execute('SELECT * FROM devices WHERE id=?',(self.c['device_id'],)).fetchone()
        data=json.loads(row['status']) if row else {}
        online=bool(row and time.time()-row['seen']<15)
        return dict(online=online,printerConnected=online and data.get('connected',False),busy=online and data.get('busy',False),automaticPrinting=online and data.get('relayAutomaticPrinting',data.get('automaticNetworkConsent',False)),lastSeen=row['seen'] if row else None,serverTime=time.time(),staleAfter=15)
    def prune(self,db,now):
        db.execute('DELETE FROM usage WHERE window<?',(int(now//3600)-48,))
        db.execute("DELETE FROM jobs WHERE updated<? AND state NOT IN ('claimed','printing')",(now-self.c['retention_days']*86400,))
        if hasattr(self,'api'):
            db.execute('DELETE FROM job_events WHERE job_id NOT IN (SELECT id FROM jobs)')
            db.execute('DELETE FROM submission_ids WHERE job_id NOT IN (SELECT id FROM jobs)')
            db.execute('DELETE FROM audit_events WHERE at<?',(now-90*86400,))
            db.execute('DELETE FROM sessions WHERE expires<?',(now,))
            db.execute('DELETE FROM challenges WHERE expires<?',(now,))
        if hasattr(self,'api'):
            db.execute("INSERT INTO job_events(job_id,at,state) SELECT id,?,'unknown' FROM jobs WHERE state IN ('claimed','printing') AND updated<?",(now,now-300))
        # A disconnected sender is never automatically retried: the paper outcome is uncertain.
        db.execute("UPDATE jobs SET state='unknown',detail='Device did not report completion; inspect paper before retrying',updated=? WHERE state IN ('claimed','printing') AND updated<?",(now,now-300))
    def submit(self,body,key,ip,headers):
        if key: self.require(key,['submit','admin'])
        if not key and not self.c['allow_anonymous']: raise Problem(403,'Anonymous submissions are disabled')
        text=body.get('text','');limit=10000
        content,payload,rows=self.content(body,limit)
        if not isinstance(text,str) or (not text.strip() and content is None) or len(text)+rows>limit: raise Problem(400,f'Provide text or a raster; caption characters plus raster rows must not exceed {limit}')
        if any(ord(v)<32 and v not in '\n\r\t' for v in text): raise Problem(400,'Control characters are not accepted')
        target=key['device'] if key else self.c['device_id']
        browser=body.get('browser',{})
        if not isinstance(browser,dict): raise Problem(400,'browser must be an object')
        # Browser descriptions are user-supplied, not verified device identities.
        safe={k:str(browser[k])[:160] for k in ['timezone','screen','platform','language'] if k in browser and isinstance(browser[k],(str,int,float))}
        meta=dict(ip=ip,userAgent=headers.get('User-Agent','')[:512],language=headers.get('Accept-Language','')[:160],
                  clientHints=headers.get('Sec-CH-UA','')[:300],browser=safe,identity=key['name'] if key else 'Anonymous',authenticated=bool(key and not key.get('anonymous')))
        if key and key.get('anonymous'):
            meta={k:meta[k] for k in ('ip','userAgent','language','identity','authenticated')}
            meta['browser']={k:safe[k] for k in ('language','platform') if k in safe}
        now=time.time();window=int(now//3600);reset=(window+1)*3600
        prefix='anonymous' if not key or key.get('anonymous') else 'authenticated';maxjobs=self.c[prefix+'_hourly_limit'];maxchars=self.c[prefix+'_hourly_chars']
        identifier=hashlib.sha256(time.time_ns().to_bytes(8,'big')+secrets.token_bytes(32)).hexdigest()[:40];receipt=secrets.token_urlsafe(32)
        with self.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            try:
                self.prune(db,now)
                quota=self.api.auth.effective_quota(key,db)
                units=len(text)+rows
                if units>quota['max_length']:raise Problem(400,'identity_size_limit')
                if key and key.get('user_id'):
                    for bucket in ['user:'+key['user_id'],'credential:'+key['id'],'global']:
                        use=db.execute('SELECT jobs,chars FROM usage WHERE ip=? AND window=?',(bucket,window)).fetchone()
                        jobs=use['jobs'] if use else 0;chars=use['chars'] if use else 0
                        job_limit=self.c['global_hourly_limit'] if bucket=='global' else quota['hourly_limit']
                        char_limit=self.c['global_hourly_chars'] if bucket=='global' else quota['hourly_chars']
                        if jobs+1>job_limit or chars+units>char_limit:raise Problem(429,'identity_or_global_quota_reached',resetAt=reset,retryAfter=max(1,int(reset-now)+1))
                usage=db.execute('SELECT * FROM usage WHERE ip=? AND window=?',(ip,window)).fetchone()
                if usage and (usage['jobs']>=maxjobs or usage['chars']+len(text)+rows>maxchars): raise Problem(429,'Hourly IP quota reached',resetAt=reset,retryAfter=max(1,int(reset-now)+1))
                if len(text)+rows>maxchars: raise Problem(400,'Message exceeds the hourly character allowance')
                count=db.execute("SELECT count(*) FROM jobs WHERE state IN ('queued','claimed','printing')").fetchone()[0]
                if count>=self.c['queue_limit']: raise Problem(503,'Printer inbox is full')
                db.execute('INSERT INTO usage VALUES(?,?,1,?) ON CONFLICT(ip,window) DO UPDATE SET jobs=jobs+1,chars=chars+excluded.chars',(ip,window,len(text)+rows))
                short=self.short_id(db)
                db.execute('INSERT INTO jobs(id,device,text,meta,state,receipt,created,updated,short_id,submit_key,content_meta,payload) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)',
                           (identifier,target,text,compact(meta),'queued',digest(receipt),now,now,short,key['id'] if key else None,compact(content) if content else None,payload))
                if key and key.get('user_id'):
                    db.execute('UPDATE jobs SET user_id=?,browser_id=?,credential_id=? WHERE id=?',(key['user_id'],key.get('browser_id'),key['id'],identifier))
                    db.execute('INSERT INTO submission_ids VALUES(?,?,?)',(key['user_id'],key['request_id'],identifier))
                    db.execute('INSERT INTO job_events(job_id,at,state) VALUES(?,?,?)',(identifier,now,'queued'))
                    for bucket in ['user:'+key['user_id'],'credential:'+key['id'],'global']:
                        db.execute('INSERT INTO usage VALUES(?,?,1,?) ON CONFLICT(ip,window) DO UPDATE SET jobs=jobs+1,chars=chars+excluded.chars',(bucket,window,len(text)+rows))
                db.execute('COMMIT')
            except Exception:db.execute('ROLLBACK');raise
        return dict(id=identifier,shortID=short,receipt=receipt,state='queued',resetAt=reset)
    def job(self,row,private=True,summary=False,include_payload=True):
        result={k:row[k] for k in ['id','state','created','updated','detail']};result['shortID']=row['short_id'];result['lastStage']=row['last_stage']
        progress=json.loads(row['progress']) if row['progress'] else None
        result['progress']={**progress,'percent':round(100*progress['completed']/progress['total'],1)} if progress else None
        if row['content_meta']:
            result['content']={'type':'raster1'}
            if private:
                result['content']=json.loads(row['content_meta'])
                if not summary and include_payload:result['content']['data']=base64.b64encode(row['payload']).decode('ascii')
        if private:result.update(text=row['text'][:120] if summary else row['text'],metadata=json.loads(row['meta']),device=row['device'],truncated=summary and (len(row['text'])>120 or bool(row['content_meta'])))
        return result
    def lookup_job(self,short_id,key,headers):
        identifier=short_id.replace('-','').upper()
        with self.connect() as db:
            self.prune(db,time.time())
            row=db.execute('SELECT * FROM jobs WHERE short_id=?',(identifier,)).fetchone()
        if row is None:raise Problem(404,'Job not found or expired')
        receipt=headers.get('X-Job-Receipt','')
        if receipt and not hmac.compare_digest(row['receipt'],digest(receipt)):raise Problem(404,'Job not found or expired')
        authorized=bool(receipt) or bool(key and (key['role']=='admin' or (key['role']=='device' and key['device']==row['device']) or (key['role']=='submit' and key['id']==row['submit_key'])))
        result=self.job(row,private=authorized,include_payload=headers.get('X-Paperang-Content-Summary')!='1')
        result['detailsAvailable']=authorized
        if not authorized:
            # The printed ID is a locator, not a secret. Device-supplied detail
            # may contain private text, so only expose fixed fields publicly.
            result.pop('id');result.pop('detail')
        return result
    def route(self,method,path,body,key,ip,headers):
        return self.api.route(method,path,body,key,ip,headers)
    def route_legacy(self,method,path,body,key,ip,headers):
        now=time.time()
        if method=='GET' and path=='/v1/public/status':return 200,dict(version=APP_VERSION,status=self.public_status(),policy=self.policies(),capabilities=dict(partitionProgress='transport',shortIDLookup=True,contentTypes=['text','raster1'],contentSchemaVersion=1,maxRequestBytes=163840))
        if method=='POST' and path=='/v1/jobs':return 202,self.submit(body,key,ip,headers)
        lookup=re.fullmatch(r'/v1/jobs/lookup/('+SHORT_JOB_PATTERN+')',path)
        if method=='GET' and lookup:return 200,self.lookup_job(lookup.group(1),key,headers)
        if method=='GET' and re.fullmatch(r'/v1/jobs/'+JOB_PATTERN,path):
            with self.connect() as db:
                self.prune(db,now)
                row=db.execute('SELECT * FROM jobs WHERE id=?',(path.rsplit('/',1)[1],)).fetchone()
            receipt=headers.get('X-Job-Receipt','')
            if not row or not receipt or not hmac.compare_digest(row['receipt'],digest(receipt)):raise Problem(404,'Job not found')
            return 200,self.job(row,False)
        if path.startswith('/v1/admin/'):
            self.require(key,['admin'])
            if method=='GET' and path=='/v1/admin/keys':
                with self.connect() as db:rows=db.execute('SELECT id,name,role,device,revoked,created FROM keys ORDER BY created DESC').fetchall()
                return 200,dict(keys=[dict(v) for v in rows])
            if method=='POST' and path=='/v1/admin/keys':
                try:return 201,self.key_add(body.get('name'),body.get('role'),body.get('device',self.c['device_id']))
                except ValueError as e:raise Problem(400,str(e))
            if method=='POST' and re.fullmatch(r'/v1/admin/keys/[a-f0-9-]{36}/revoke',path):
                identifier=path.split('/')[4]
                return 200,self.key_revoke(identifier,key)
            change=re.fullmatch(r'/v1/admin/keys/([a-f0-9-]{36})/(rename|delete)',path)
            if method=='POST' and change:
                identifier,action=change.groups()
                if identifier==key['id']:raise Problem(409,'Cannot modify the current administrator key')
                with self.connect() as db:
                    row=db.execute('SELECT * FROM keys WHERE id=?',(identifier,)).fetchone()
                    if not row:raise Problem(404,'Key not found')
                    if action=='rename':
                        name=body.get('name')
                        if not isinstance(name,str) or not 1<=len(name.strip())<=80:raise Problem(400,'Name must contain 1–80 characters')
                        db.execute('UPDATE keys SET name=? WHERE id=?',(name.strip(),identifier))
                    else:
                        if not row['revoked']:raise Problem(409,'Revoke the key before deleting it')
                        db.execute('DELETE FROM keys WHERE id=?',(identifier,))
                return 200,dict(updated=True)
        if path.startswith('/v1/device/'):
            self.require(key,['device'])
            if method=='POST' and path=='/v1/device/status':
                status=body.get('status',{})
                if not isinstance(status,dict) or len(compact(status).encode())>16384:raise Problem(400,'Invalid status')
                status={**status,'_relay_key_id':key['id']}
                with self.connect() as db:
                    db.execute('INSERT INTO devices VALUES(?,?,?) ON CONFLICT(id) DO UPDATE SET status=excluded.status,seen=excluded.seen',(key['device'],compact(status),now))
                    self.prune(db,now)
                return 200,dict(accepted=True)
            if method=='GET' and path=='/v1/device/jobs':
                supported='raster1' in headers.get('X-Paperang-Content-Types','').split(',')
                with self.connect() as db:
                    self.prune(db,now)
                    rows=db.execute("SELECT id,state,created,updated,detail,short_id,last_stage,progress,content_meta,text,meta,device FROM jobs WHERE device=? AND (content_meta IS NULL OR ?=1) ORDER BY CASE WHEN state IN ('queued','claimed','printing') THEN 0 ELSE 1 END,CASE WHEN state IN ('queued','claimed','printing') THEN created ELSE -created END LIMIT 100",(key['device'],int(supported))).fetchall()
                return 200,dict(jobs=[self.job(r,summary=True) for r in rows])
            detail=re.fullmatch(r'/v1/device/jobs/('+JOB_PATTERN+')',path)
            if method=='GET' and detail:
                with self.connect() as db:row=db.execute('SELECT * FROM jobs WHERE id=? AND device=?',(detail.group(1),key['device'])).fetchone()
                if not row:raise Problem(404,'Job not found')
                return 200,self.job(row)
            match=re.fullmatch(r'/v1/device/jobs/('+JOB_PATTERN+r')/(claim|result|reject|confirm)',path)
            if method=='POST' and match:
                identifier,action=match.groups()
                with self.connect() as db:
                    db.execute('BEGIN IMMEDIATE')
                    try:
                        row=db.execute('SELECT * FROM jobs WHERE id=? AND device=?',(identifier,key['device'])).fetchone()
                        if not row:raise Problem(404,'Job not found')
                        if action=='claim':
                            if row['content_meta'] and 'raster1' not in headers.get('X-Paperang-Content-Types','').split(','):raise Problem(409,'Client does not support this content type')
                            if row['state']!='queued':raise Problem(409,'Job already claimed or handled')
                            claim=secrets.token_urlsafe(32)
                            db.execute("UPDATE jobs SET state='claimed',last_stage='claimed',claim_key=?,claim_hash=?,updated=? WHERE id=?",(key['id'],digest(claim),now,identifier))
                            result=dict(id=identifier,claim=claim,state='claimed')
                        elif action=='confirm':
                            if row['state'] not in ['sent','confirmed']:raise Problem(409,'Only sent jobs can be physically confirmed')
                            db.execute("UPDATE jobs SET state='confirmed',detail='Physical paper output manually confirmed by owner',updated=? WHERE id=?",(now,identifier));result=dict(state='confirmed')
                        elif action=='reject':
                            if row['state']!='queued':raise Problem(409,'Only queued jobs can be rejected')
                            db.execute("UPDATE jobs SET state='rejected',detail='Rejected locally',updated=? WHERE id=?",(now,identifier));result=dict(state='rejected')
                        else:
                            claim=body.get('claim','');state=body.get('state')
                            if not isinstance(claim,str) or row['claim_key']!=key['id'] or not hmac.compare_digest(row['claim_hash'] or '',digest(claim)):raise Problem(403,'Invalid claim')
                            if state not in ['printing','sent','unknown','rejected']:raise Problem(400,'Invalid result state')
                            if row['state']=='unknown' and state=='sent':
                                # A late durable transport report is not a physical retry.
                                db.execute("UPDATE jobs SET state='sent',detail='Late transport completion; paper not independently confirmed',updated=? WHERE id=?",(now,identifier))
                            elif row['state'] in ['sent','confirmed','unknown','rejected']:
                                if row['state']!=state and not (row['state']=='confirmed' and state=='sent'):raise Problem(409,'Final state cannot change')
                            else:
                                detail=str(body.get('detail',''))[:500]
                                progress=transport_progress(detail) if state=='printing' else None
                                old_progress=json.loads(row['progress']) if row['progress'] else None
                                if state=='sent' and old_progress:
                                    progress={**old_progress,'completed':old_progress['total']}
                                # Ignore stale counts; never infer a missing total or
                                # reset progress when the Mac sends its final report.
                                if progress and old_progress and (progress['total']!=old_progress['total'] or progress['completed']<old_progress['completed']):progress=None
                                db.execute("UPDATE jobs SET state=?,detail=?,updated=?,progress=COALESCE(?,progress),last_stage=CASE WHEN ?='printing' THEN 'printing' ELSE last_stage END WHERE id=?",(state,detail,now,compact(progress) if progress else None,state,identifier))
                            result=dict(id=identifier,state='confirmed' if row['state']=='confirmed' else state)
                        if hasattr(self,'api'):
                            state=db.execute('SELECT state FROM jobs WHERE id=?',(identifier,)).fetchone()[0]
                            if state!=row['state']:
                                db.execute('INSERT INTO job_events(job_id,at,state) VALUES(?,?,?)',(identifier,now,state))
                        db.execute('COMMIT')
                    except Exception:db.execute('ROLLBACK');raise
                return 200,result
        raise Problem(404,'Unknown route')

class Handler(BaseHTTPRequestHandler):
    server_version='PaperangRelay/'+APP_VERSION
    def setup(self):
        self.request.settimeout(12)
        super().setup()
    def log_message(self,*args):pass # Never log credentials or submitted content.
    def send(self,status,data,content_type='application/json; charset=utf-8',extra=None):
        raw=data if isinstance(data,bytes) else compact(data).encode()
        self.send_response(status);self.send_header('Content-Type',content_type);self.send_header('Content-Length',str(len(raw)))
        for k,v in {'Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Referrer-Policy':'no-referrer','X-Frame-Options':'DENY',
                    'Content-Security-Policy':"default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'",**(extra or {})}.items():self.send_header(k,str(v))
        self.end_headers()
        try:self.wfile.write(raw)
        except (BrokenPipeError,ConnectionResetError):pass
    def do_GET(self):self.handle_request('GET')
    def do_POST(self):self.handle_request('POST')
    def handle_request(self,method):
        try:
            self.connection.settimeout(12)
            if '?' in self.path:raise Problem(400,'Query strings are not accepted; send credentials in headers')
            if method=='GET' and self.path in ['/','/app.js','/style.css']:
                name={'/':'index.html','/app.js':'app.js','/style.css':'style.css'}[self.path]
                mime={'index.html':'text/html; charset=utf-8','app.js':'text/javascript; charset=utf-8','style.css':'text/css; charset=utf-8'}[name]
                return self.send(200,(self.server.web/name).read_bytes(),mime)
            if self.headers.get('Origin'):
                from urllib.parse import urlsplit
                origin=urlsplit(self.headers['Origin'])
                if origin.scheme not in ['http','https'] or origin.netloc!=self.headers.get('Host'):raise Problem(403,'Cross-origin requests are not allowed')
            if len(self.headers.get_all('Authorization',[]))>1 or len(self.headers.get_all('Host',[]))!=1:raise Problem(400,'Duplicate/missing authentication or Host header')
            if self.path.startswith('/v1/'):
                raise Problem(410,'relay_v1_retired_use_v2')
            key=None if self.path in ['/v2/auth/login','/v2/auth/anonymous','/v2/auth/challenge','/v2/auth/prove'] else self.server.store.api.auth.authenticate(self.headers.get('Authorization',''),self.headers.get('Cookie',''))
            if key and key.get('cookie') and method=='POST':
                if not self.headers.get('Origin') or not hmac.compare_digest(self.headers.get('X-CSRF-Token',''),key['csrf']):raise Problem(403,'csrf_required')
            body={}
            if method=='POST':
                lengths=self.headers.get_all('Content-Length',[])
                if len(lengths)!=1 or self.headers.get('Transfer-Encoding'):raise Problem(400,'One Content-Length is required; chunked is not supported')
                try:length=int(lengths[0])
                except ValueError:raise Problem(400,'Invalid Content-Length')
                limit=163840 if self.path=='/v2/jobs' else 65536
                if length<0 or length>limit:raise Problem(413,'Request body exceeds '+str(limit)+' bytes')
                if self.headers.get('Content-Type','').split(';')[0]!='application/json':raise Problem(415,'Expected application/json')
                raw=self.rfile.read(length)
                if len(raw)!=length:raise Problem(400,'Incomplete request')
                try:body=json.loads(raw)
                except (ValueError,UnicodeDecodeError):raise Problem(400,'Invalid JSON')
                if not isinstance(body,dict):raise Problem(400,'Expected JSON object')
            ip=self.server.store.client_ip(self.client_address[0],self.headers.get('X-Forwarded-For',''))
            status,data=self.server.store.route(method,self.path,body,key,ip,self.headers)
            extra={}
            if self.path in ['/v2/auth/login','/v2/auth/anonymous','/v2/auth/prove'] and status<300 and not (self.headers.get('X-Paperang-Native')=='1' and not self.headers.get('Origin')):
                token=data.pop('token'); age=data.pop('maxAge')
                extra['Set-Cookie']='psr_session='+token+'; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age='+str(age)
            elif self.path=='/v2/auth/logout':
                extra['Set-Cookie']='psr_session=; Path=/; HttpOnly; Secure; SameSite=Strict; Max-Age=0'
            self.send(status,data,extra=extra)
        except Problem as e:self.send(e.status,dict(error=e.message,**e.extra),extra={'Retry-After':e.extra['retryAfter']} if 'retryAfter' in e.extra else {})
        except (TimeoutError,ConnectionError):self.close_connection=True
        except Exception as exc:
            print('relay internal error: '+type(exc).__name__,file=sys.stderr,flush=True)
            self.send(500,dict(error='Internal relay error'))

class Server(ThreadingHTTPServer):
    daemon_threads=True
    def __init__(self,store):
        self.store=store;self.web=Path(__file__).resolve().parent/'web';self.slots=threading.BoundedSemaphore(32)
        super().__init__((store.c['bind'],store.c['port']),Handler)
    def process_request(self,request,address):
        if not self.slots.acquire(False):self.shutdown_request(request);return
        try:super().process_request(request,address)
        except Exception:self.slots.release();raise
    def process_request_thread(self,request,address):
        try:super().process_request_thread(request,address)
        finally:self.slots.release()

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--config',required=True)
    sub=parser.add_subparsers(dest='action',required=True);sub.add_parser('serve')
    add=sub.add_parser('key-add');add.add_argument('--name',required=True);add.add_argument('--role',choices=['admin','device','submit','A','B','C','M'],required=True);add.add_argument('--device')
    imp=sub.add_parser('key-import');imp.add_argument('--file',required=True)
    pub=sub.add_parser('trust-public-key');pub.add_argument('--name',required=True);pub.add_argument('--file',required=True)
    sub.add_parser('key-list');rev=sub.add_parser('key-revoke');rev.add_argument('id');rev.add_argument('--allow-lockout',action='store_true',help='Explicit local emergency override for revoking the last or connected key')
    args=parser.parse_args();config=json.loads(Path(args.config).read_text());store=Store(config)
    if args.action=='key-add':print(compact(store.api.auth.create(args.name,{'admin':'C','device':'M','submit':'B'}.get(args.role,args.role),args.device or store.c['device_id'])))
    elif args.action=='trust-public-key':print(compact(store.api.auth.public_key_add(args.name,Path(args.file).read_text())))
    elif args.action=='key-import':
        source=Path(args.file)
        if source.stat().st_size>131072:raise ValueError('Key file too large')
        items=json.loads(source.read_text());items=items if isinstance(items,list) else [items]
        if not 1<=len(items)<=100:raise ValueError('Expected 1–100 keys')
        for item in items:
            if not isinstance(item,dict) or not item.get('token'):raise ValueError('Missing imported token')
            added=store.key_add(item.get('name'),item.get('role'),item.get('device',store.c['device_id']),item.get('token'))
            print(compact({k:v for k,v in added.items() if k!='token'}))
    elif args.action=='key-list':
        with store.connect() as db:print(json.dumps([dict(r) for r in db.execute('SELECT c.id,i.name,c.role,c.device,c.kind,c.revoked,c.created FROM credentials c JOIN identities i ON i.id=c.user_id ORDER BY c.created')],indent=2))
    elif args.action=='key-revoke':
        try:store.api.auth.revoke(args.id,{'id':'local-cli'})
        except Problem as e:raise SystemExit(e.message)
        print('Key revoked')
    else:
        server=Server(store);signal.signal(signal.SIGTERM,lambda *_:threading.Thread(target=server.shutdown,daemon=True).start())
        print(f'Paperang relay listening on {store.c["bind"]}:{store.c["port"]}',flush=True)
        try:server.serve_forever(poll_interval=.5)
        except KeyboardInterrupt:pass
        finally:server.server_close()
if __name__=='__main__':main()
