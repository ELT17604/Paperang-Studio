"""Actual RelaySkill/experimental ChatGPT clients against isolated HTTP Relay v2."""
import asyncio
import importlib.util
from pathlib import Path
import tempfile
import threading
import unittest
import uuid
from relay import Store, Server
import relay_print

spec = importlib.util.spec_from_file_location('chatgpt_core', Path(__file__).resolve().parents[2]/'Integrations/ChatGPT/core.py')
chat = importlib.util.module_from_spec(spec)
spec.loader.exec_module(chat)


class AdapterTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.store = Store({'database':str(Path(self.tmp.name)/'db')})
        self.store.c['port'] = 0
        self.server = Server(self.store)
        self.thread = threading.Thread(target=self.server.serve_forever, daemon=True)
        self.thread.start()
        self.url = f'http://127.0.0.1:{self.server.server_port}'
        self.b = self.store.api.auth.create('isolated sender', 'B')

    def tearDown(self):
        self.server.shutdown()
        self.server.server_close()
        self.thread.join()
        self.tmp.cleanup()

    def test_skill_submit_lookup_and_no_resubmit(self):
        c = relay_print.Client(self.url+'/', self.b['token'])
        receipt = Path(self.tmp.name)/'attempt.json'
        result = relay_print.submit(c, {'text':'Isolated adapter test; no receiver'}, receipt, queue=True)
        state = c.request('GET', 'v2/jobs/lookup/'+result['shortID'])
        self.assertEqual(state['state'], 'queued')
        self.assertNotIn('text', state)
        with self.assertRaises(relay_print.PrintError):
            relay_print.submit(c, {'text':'Must not repeat'}, receipt, queue=True)
        with self.store.connect() as db:
            self.assertEqual(db.execute('SELECT count(*) FROM jobs').fetchone()[0], 1)

    def test_chatgpt_B_submit_own_status_and_A_denial(self):
        async def run():
            c = chat.RelayClient(self.url, self.b['token'])
            job = await c.submit({'text':'Isolated ChatGPT adapter'}, str(uuid.uuid4()))
            self.assertEqual((await c.status(job['shortID']))['state'], 'queued')
            a = self.store.api.auth.create('mailbox', 'A')
            with self.assertRaises(chat.PaperangError):
                await chat.RelayClient(self.url, a['token']).status(job['id'])
        asyncio.run(run())
