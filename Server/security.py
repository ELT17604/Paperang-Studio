"""API v2 identities and authentication. Secrets never enter administrative listings."""
import base64
import hashlib
import hmac
import json
import re
import secrets
import time
import os
import threading
import uuid
from http.cookies import SimpleCookie

from argon2 import PasswordHasher
from argon2.exceptions import VerificationError, InvalidHashError
from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding, rsa

PASSWORD_SLOTS = threading.BoundedSemaphore(2)
PASSWORDS = PasswordHasher(time_cost=2, memory_cost=19456, parallelism=1)


class Security:
    def __init__(self, store, problem):
        self.store, self.Problem = store, problem
        with store.connect() as db:
            upgraded=db.execute("SELECT 1 FROM sqlite_master WHERE type='table' AND name='credentials'").fetchone()
            if not upgraded:
                if db.execute("SELECT 1 FROM jobs WHERE state IN ('claimed','printing') LIMIT 1").fetchone():
                    raise RuntimeError('Drain active claims before v0.3.0 migration')
                import sqlite3
                backup_path=store.db+'.pre-v0.3.0-'+str(time.time_ns())+'.bak'
                fd=os.open(backup_path,os.O_CREAT|os.O_EXCL|os.O_WRONLY,0o600);os.close(fd)
                backup=sqlite3.connect(backup_path)
                try: db.backup(backup)
                finally: backup.close()
            db.executescript('''
            CREATE TABLE IF NOT EXISTS identities(id TEXT PRIMARY KEY,name TEXT NOT NULL,anonymous INTEGER NOT NULL DEFAULT 0,revoked INTEGER NOT NULL DEFAULT 0,created REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS credentials(id TEXT PRIMARY KEY,user_id TEXT NOT NULL,role TEXT NOT NULL,device TEXT NOT NULL,kind TEXT NOT NULL,secret_hash TEXT,public_key TEXT,revoked INTEGER NOT NULL DEFAULT 0,created REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS sessions(hash TEXT PRIMARY KEY,credential_id TEXT NOT NULL,browser_id TEXT,csrf TEXT NOT NULL,expires REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS challenges(id TEXT PRIMARY KEY,credential_id TEXT NOT NULL,message TEXT NOT NULL,expires REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS auth_limits(bucket TEXT PRIMARY KEY,window INTEGER NOT NULL,count INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS audit_events(id INTEGER PRIMARY KEY,at REAL NOT NULL,actor TEXT NOT NULL,action TEXT NOT NULL,target TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS job_events(id INTEGER PRIMARY KEY,job_id TEXT NOT NULL,at REAL NOT NULL,state TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS submission_ids(user_id TEXT NOT NULL,request_id TEXT NOT NULL,job_id TEXT NOT NULL,PRIMARY KEY(user_id,request_id));
            ''')
            columns = {r[1] for r in db.execute('PRAGMA table_info(jobs)')}
            for name in ('user_id', 'browser_id', 'credential_id'):
                if name not in columns:
                    db.execute('ALTER TABLE jobs ADD COLUMN ' + name + ' TEXT')
            # Stable mapping, safe to run repeatedly. Legacy tokens stay hash-only.
            for row in db.execute('SELECT * FROM keys').fetchall():
                db.execute('INSERT OR IGNORE INTO identities VALUES(?,?,0,0,?)', (row['id'], row['name'], row['created']))
                db.execute('INSERT OR IGNORE INTO credentials VALUES(?,?,?,?,?,?,NULL,?,?)',
                           (row['id'], row['id'], {'submit':'B','admin':'C','device':'M'}[row['role']], row['device'], 'token', row['hash'], row['revoked'], row['created']))
                db.execute('UPDATE jobs SET user_id=?,credential_id=? WHERE submit_key=? AND user_id IS NULL', (row['id'], row['id'], row['id']))

    def fail(self, code, message):
        raise self.Problem(code, message)

    def limit(self, bucket, maximum=20):
        now = int(time.time() // 3600)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            db.execute('DELETE FROM auth_limits WHERE window<?', (now-1,))
            db.execute('INSERT INTO auth_limits VALUES(?,?,1) ON CONFLICT(bucket) DO UPDATE SET count=CASE WHEN window=excluded.window THEN count+1 ELSE 1 END,window=excluded.window', (bucket, now))
            n = db.execute('SELECT count FROM auth_limits WHERE bucket=?', (bucket,)).fetchone()[0]
            db.execute('COMMIT')
        if n > maximum:
            self.fail(429, 'authentication_rate_limited')

    def get(self, identifier):
        with self.store.connect() as db:
            row = db.execute('SELECT c.*,i.name,i.anonymous,i.revoked AS identity_revoked FROM credentials c JOIN identities i ON i.id=c.user_id WHERE c.id=?', (identifier,)).fetchone()
        if not row or row['revoked'] or row['identity_revoked']:
            self.fail(401, 'invalid_or_revoked_credential')
        return dict(row)

    def authenticate(self, authorization='', cookie=''):
        session = None
        token = ''
        if authorization:
            if not authorization.startswith('Bearer ') or len(authorization) > 512:
                self.fail(401, 'invalid_credential')
            token = authorization[7:]
        elif cookie:
            try:
                cookies = SimpleCookie(); cookies.load(cookie)
                token = cookies['psr_session'].value if 'psr_session' in cookies else ''
            except Exception:
                self.fail(401, 'invalid_session')
        if not token:
            return None
        with self.store.connect() as db:
            session = db.execute('SELECT * FROM sessions WHERE hash=? AND expires>?', (hashlib.sha256(token.encode()).hexdigest(), time.time())).fetchone()
            if session:
                key = self.get(session['credential_id'])
                key.update(session=True, browser_id=session['browser_id'], csrf=session['csrf'], cookie=not bool(authorization))
                return key
            row = db.execute("SELECT id FROM credentials WHERE kind='token' AND secret_hash=?", (hashlib.sha256(token.encode()).hexdigest(),)).fetchone()
        if row:
            return self.get(row['id'])
        self.fail(401, 'invalid_or_expired_credential')

    def require(self, key, roles):
        if not key or key.get('kind')=='pendingkey' or key['role'] not in roles:
            self.fail(403, 'status_read_forbidden' if roles == ['B','C'] else 'permission_denied')

    def create(self, name, role='A', device=None, kind='token', user_id=None, password=None):
        if role not in ('A','B','C','M') or kind not in ('token','passphrase'):
            self.fail(400, 'invalid_role_or_kind')
        if not isinstance(name, str) or not 1 <= len(name.strip()) <= 80:
            self.fail(400, 'invalid_display_name')
        if kind == 'passphrase' and role == 'M':
            self.fail(400, 'device_requires_random_token')
        device = device or self.store.c['device_id']
        if not isinstance(device,str) or not re.fullmatch(r'[A-Za-z0-9_-]{1,64}', device):
            self.fail(400, 'invalid_device')
        identifier, uid = str(uuid.uuid4()), user_id or str(uuid.uuid4())
        secret = 'psr_' + secrets.token_urlsafe(32)
        hashed = hashlib.sha256(secret.encode()).hexdigest()
        if kind == 'passphrase':
            password = password or secrets.token_urlsafe(18)
            if not isinstance(password, str) or not 12 <= len(password) <= 128:
                self.fail(400, 'passphrase_requires_12_to_128_characters')
            if len(set(password))<4: self.fail(400,'passphrase_too_repetitive')
            if not PASSWORD_SLOTS.acquire(False): self.fail(503,'password_service_busy')
            try: hashed = PASSWORDS.hash(password)
            finally: PASSWORD_SLOTS.release()
            secret = 'psp_' + identifier + '.' + password
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            if user_id and not db.execute('SELECT id FROM identities WHERE id=? AND revoked=0', (uid,)).fetchone():
                db.execute('ROLLBACK'); self.fail(404, 'identity_not_found')
            db.execute('INSERT OR IGNORE INTO identities VALUES(?,?,0,0,?)', (uid,name.strip(),time.time()))
            db.execute('INSERT INTO credentials VALUES(?,?,?,?,?,?,NULL,0,?)', (identifier,uid,role,device,kind,hashed,time.time()))
            db.execute('COMMIT')
        return dict(id=identifier,user_id=uid,name=name.strip(),role=role,device=device,kind=kind,token=secret)

    def session(self, key, browser=None, owner=False):
        token = ('psa_' if key['anonymous'] else 'pss_') + secrets.token_urlsafe(32)
        csrf = secrets.token_urlsafe(32)
        seconds = 600 if owner or key['role']=='C' else min(180, max(1, int(self.store.c.get('session_days',180)))) * 86400
        with self.store.connect() as db:
            db.execute('DELETE FROM sessions WHERE expires<?', (time.time(),))
            db.execute('DELETE FROM challenges WHERE expires<?', (time.time(),))
            db.execute('INSERT INTO sessions VALUES(?,?,?,?,?)', (hashlib.sha256(token.encode()).hexdigest(),key['id'],browser,csrf,time.time()+seconds))
        return dict(token=token,csrf=csrf,role=key['role'],user_id=key['user_id'],browser_id=browser,name=key['name'],maxAge=seconds)

    def login(self, body, ip):
        self.limit('login-ip:'+ip)
        value = body.get('credential','')
        if not isinstance(value,str) or len(value)>512:
            self.fail(401,'invalid_credential')
        if value.startswith('psp_') and '.' in value:
            identifier, password = value[4:].split('.',1)
            self.limit('login-key:'+identifier)
            key = self.get(identifier)
            if not PASSWORD_SLOTS.acquire(False): self.fail(503,'password_service_busy')
            try:
                valid = key['kind']=='passphrase' and PASSWORDS.verify(key['secret_hash'],password)
            except (VerificationError, InvalidHashError):
                valid = False
            finally: PASSWORD_SLOTS.release()
            if not valid: self.fail(401,'invalid_credential')
        else:
            key = self.authenticate('Bearer '+value)
        if not key or key['role']=='M': self.fail(403,'device_cannot_login_to_web')
        return self.session(key, str(uuid.uuid4()))

    def public_key_add(self, name, pem):
        try:
            public = serialization.load_pem_public_key(pem.encode())
            if not isinstance(public,rsa.RSAPublicKey) or public.key_size<3072 or public.key_size>4096: raise ValueError()
        except Exception:
            self.fail(400,'expected_RSA_3072_or_4096_public_PEM')
        entry = self.create(name,'C')
        with self.store.connect() as db:
            db.execute("UPDATE credentials SET kind='pendingkey',secret_hash=NULL,public_key=? WHERE id=?",(pem,entry['id']))
        entry.pop('token')
        return entry

    def challenge(self, body, ip):
        self.limit('challenge:'+ip,60)
        key = self.get(body.get('id',''))
        if key['kind'] not in ('publickey','pendingkey'): self.fail(401,'public_key_required')
        identifier = str(uuid.uuid4())
        message = 'PaperangStudio-owner-v2\n'+key['id']+'\n'+identifier+'\n'+secrets.token_urlsafe(32)
        with self.store.connect() as db:
            db.execute('DELETE FROM challenges WHERE expires<?',(time.time(),))
            db.execute('INSERT INTO challenges VALUES(?,?,?,?)',(identifier,key['id'],message,time.time()+60))
        return dict(challenge=identifier,message=message,algorithm='RSA-PSS-SHA256',expiresIn=60)

    def prove(self, body, ip):
        self.limit('proof:'+ip,60)
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM challenges WHERE id=? AND expires>?',(body.get('challenge',''),time.time())).fetchone()
            if row: db.execute('DELETE FROM challenges WHERE id=?',(row['id'],))
            db.execute('COMMIT')
        if not row: self.fail(401,'expired_or_replayed_challenge')
        key = self.get(row['credential_id'])
        try:
            public = serialization.load_pem_public_key(key['public_key'].encode())
            public.verify(base64.b64decode(body.get('signature',''),validate=True),row['message'].encode(),padding.PSS(mgf=padding.MGF1(hashes.SHA256()),salt_length=32),hashes.SHA256())
        except Exception:
            self.fail(401,'invalid_signature')
        with self.store.connect() as db: db.execute("UPDATE credentials SET kind='publickey' WHERE id=?",(key['id'],))
        return self.session(key,owner=True)

    def audit(self, actor, action, target):
        with self.store.connect() as db:
            db.execute('INSERT INTO audit_events(at,actor,action,target) VALUES(?,?,?,?)',(time.time(),actor['id'],action,str(target)))

    def revoke(self, identifier, actor):
        with self.store.connect() as db:
            db.execute('BEGIN IMMEDIATE')
            row = db.execute('SELECT * FROM credentials WHERE id=?',(identifier,)).fetchone()
            if not row: db.execute('ROLLBACK'); self.fail(404,'credential_not_found')
            if identifier==actor['id']: db.execute('ROLLBACK'); self.fail(409,'cannot_revoke_current_credential')
            if row['role']=='C' and not row['revoked'] and not db.execute("SELECT c.id FROM credentials c JOIN identities i ON i.id=c.user_id WHERE c.role='C' AND c.revoked=0 AND i.revoked=0 AND c.kind<>'pendingkey' AND c.id<>?",(identifier,)).fetchone():
                db.execute('ROLLBACK'); self.fail(409,'last_administrator')
            db.execute('UPDATE credentials SET revoked=1 WHERE id=?',(identifier,))
            db.execute('DELETE FROM sessions WHERE credential_id=?',(identifier,))
            db.execute('COMMIT')
        self.audit(actor,'revoke',identifier)
        return dict(revoked=True)
