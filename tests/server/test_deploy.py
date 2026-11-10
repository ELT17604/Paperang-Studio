"""Real SQLite upgrade/rollback with simulated service control; no production data."""
import json
import os
from pathlib import Path
import shutil
import sqlite3
import sys
import tempfile
import unittest
from unittest.mock import patch
import deploy
from relay import Store


class DeploymentTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        for name in ['config', 'data', 'docs', 'releases', 'venvs']:
            (self.root/name).mkdir(parents=True)
        self.source = Path(__file__).resolve().parents[2]/'Server'
        self.old = self.root/'releases/old'
        shutil.copytree(self.source, self.old)
        (self.root/'current').symlink_to(self.old)
        (self.root/'venvs/current').symlink_to(sys.prefix)
        shutil.copy2(self.source/'manage.py', self.root/'manage.py')
        self.c = {'database':str(self.root/'data/relay.sqlite3')}
        (self.root/'config/config.json').write_text(json.dumps(self.c))
        self.store = Store(self.c)
        self.store.api.auth.create('preserved identity', 'C')
        self.package = self.root/'package'
        shutil.copytree(self.source, self.package/'Server')
        files = {str(p.relative_to(self.package/'Server')):deploy.hashlib.sha256(p.read_bytes()).hexdigest()
                 for p in (self.package/'Server').rglob('*') if p.is_file()}
        (self.package/'manifest.json').write_text(json.dumps({'files':files, 'source':'a'*40,
                                                             'version':'0.3.2-linuxrelayserver'}))
        root = self.root
        class Simulated(deploy.Deployment):
            fail = False
            actions = []
            def control(this, action):
                this.actions.append(action)
            def healthy(this, version):
                # A write accepted AFTER cutover must survive failure rollback.
                with sqlite3.connect(root/'data/relay.sqlite3') as db:
                    db.execute("INSERT OR IGNORE INTO usage VALUES('post-upgrade',0,1,1)")
                if this.fail:
                    this.fail = False
                    raise RuntimeError('Injected startup failure')
            def public_health(this, version):
                pass  # External DNS is deliberately absent from this simulation.
        self.d = Simulated(self.root, self.package)
        self.ownership = patch('deploy.os.chown')
        self.ownership.start()

    def tearDown(self):
        self.ownership.stop()
        self.tmp.cleanup()

    def test_upgrade_and_program_rollback_keep_new_data(self):
        self.d.apply()
        self.assertNotEqual((self.root/'current').resolve(), self.old)
        backup = next((self.root/'backups').iterdir())
        self.d.rollback(backup)
        self.assertEqual((self.root/'current').resolve(), self.old)
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM identities').fetchone()[0], 1)
            self.assertEqual(db.execute("SELECT jobs FROM usage WHERE ip='post-upgrade'").fetchone()[0], 1)
        self.assertEqual((backup/'database.sqlite3').stat().st_mode & 0o777, 0o600)
        self.assertEqual(backup.stat().st_mode & 0o777, 0o700)

    def test_startup_failure_preserves_post_upgrade_writes(self):
        self.d.fail = True
        with self.assertRaisesRegex(RuntimeError, 'Injected'):
            self.d.apply()
        self.assertEqual((self.root/'current').resolve(), self.old)
        with self.store.connect() as db:
            self.assertEqual(db.execute("SELECT jobs FROM usage WHERE ip='post-upgrade'").fetchone()[0], 1)

    def test_busy_preflight_leaves_service_running(self):
        with self.store.connect() as db:
            db.execute("INSERT INTO jobs(id,device,text,meta,state,receipt,created,updated) VALUES('test','p2','','{}','printing','',0,0)")
        with self.assertRaisesRegex(RuntimeError, 'Active print'):
            self.d.apply()
        self.assertEqual(self.d.actions, [])

    def test_checksum_failure_has_no_service_effect(self):
        (self.package/'Server/relay.py').write_text('corrupt')
        with self.assertRaisesRegex(ValueError, 'checksum'):
            self.d.apply()
        self.assertEqual(self.d.actions, [])
