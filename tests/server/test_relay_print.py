"""Offline protocol/rendering tests. Never connects to a real printer."""
import contextlib
import io
import os
import json
from pathlib import Path
import tempfile
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import unittest
from unittest.mock import patch

import relay_print as rp
from PIL import Image


class Handler(BaseHTTPRequestHandler):
    posts = 0
    body = None
    online = True
    response_mode = 'ok'

    def log_message(self, *_):
        pass

    def reply(self, obj, code=200):
        raw = json.dumps(obj).encode()
        self.send_response(code); self.send_header('Content-Length', str(len(raw))); self.end_headers()
        self.wfile.write(raw)

    def do_GET(self):
        if self.path == '/v2/public/status':
            self.reply({'policy': {'allow_anonymous': True, 'anonymous_max_length': 500, 'anonymous_hourly_chars': 2000, 'authenticated_max_length': 10000, 'authenticated_hourly_chars': 60000},
                        'status': {'accepting': self.online}, 'capabilities': {'contentTypes': ['text', 'raster1']}})
        elif self.path.startswith('/v2/jobs/'):
            assert self.headers.get('Authorization') == 'Bearer psr_'+'a'*43
            self.reply({'id': 'a' * 40, 'state': 'sent'})
        else:
            self.send_response(302); self.send_header('Location', '/v2/public/status'); self.end_headers()

    def do_POST(self):
        Handler.posts += 1
        assert self.headers.get('Transfer-Encoding') is None
        Handler.body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        if self.response_mode == 'lost':
            self.reply({'error': 'upstream unavailable'}, 503)
        else:
            self.reply({'id': 'a' * 40, 'shortID': 'A1B2C3D4E5F6', 'accepted': True, 'statusReadable': True}, 202)


class Tests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        threading.Thread(target=cls.server.serve_forever, daemon=True).start()
        cls.url = f'http://127.0.0.1:{cls.server.server_port}/'

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown(); cls.server.server_close()

    def setUp(self):
        self.env = patch.dict(os.environ, {"PAPERANG_RELAY_SUBMIT_KEY":"psr_"+"a"*43}); self.env.start()
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        Handler.posts = 0; Handler.online = True; Handler.response_mode = 'ok'

    def tearDown(self):
        self.env.stop(); self.temp.cleanup()

    def command(self, *args):
        with contextlib.redirect_stdout(io.StringIO()) as output:
            rp.main(list(args))
        return [json.loads(x) for x in output.getvalue().splitlines()]

    def image_args(self, **values):
        defaults = ['prepare', '--image', str(self.root / 'source.png'), '--output', str(self.root / 'job.json'), '--margin', '0', '--margin-y', '0']
        for key, value in values.items():
            defaults += ['--' + key.replace('_', '-'), str(value)]
        return rp.parser().parse_args(defaults)

    def test_exact_bit_and_row_order(self):
        image = Image.new('RGB', (576, 2), 'white'); image.putpixel((0, 0), (0, 0, 0)); image.putpixel((575, 1), (0, 0, 0))
        image.save(self.root / 'source.png')
        content = rp.render(self.image_args(), '')
        import base64
        raw = base64.b64decode(content['data'])
        self.assertEqual(raw[0], 0x80); self.assertEqual(raw[-1], 1); self.assertEqual(sum(raw), 129)
        self.assertEqual(rp.validate_job({'text': '', 'content': content}), 2)

    def test_transparency_white_and_rotation(self):
        Image.new('RGBA', (120, 576), (0, 0, 0, 0)).save(self.root / 'source.png')
        content = rp.render(self.image_args(rotate=90), '')
        import base64
        self.assertEqual(content['height'], 120)
        self.assertTrue(all(x == 0 for x in base64.b64decode(content['data'])))

    def test_cjk_and_preview(self):
        source = self.root / 'note.txt'; source.write_text('Hello\n中文排版测试', encoding='utf-8')
        result = self.command('prepare', '--text-file', str(source), '--font', (os.environ.get('PAPERANG_TEST_CJK_FONT') or rp.find_font(None, '中文')), '--font-size', '30', '--align', 'center', '--output', str(self.root / 'job.json'), '--preview', str(self.root / 'preview.png'))[0]
        self.assertEqual(result['type'], 'raster1'); self.assertTrue((self.root / 'preview.png').exists())
        job = rp.read_json(self.root / 'job.json'); self.assertGreater(rp.validate_job(job), 1)
        self.assertLessEqual(job['content']['height'], 1000)

    def test_render_modes_and_limits(self):
        Image.new('L', (700, 300), 128).save(self.root / 'source.png')
        for dither in ('none', 'floyd', 'bayer'):
            content = rp.render(self.image_args(dither=dither, fit='cover', height=200, gamma=1.2, contrast=1.1), '')
            self.assertEqual(rp.validate_job({'content': content}), 200)
        source = self.root / 'long.txt'; source.write_text('汉' * 10000, encoding='utf-8')
        with self.assertRaisesRegex(rp.PrintError, 'maximum height'):
            self.command('prepare', '--text-file', str(source), '--font',
                         (os.environ.get('PAPERANG_TEST_CJK_FONT') or rp.find_font(None, '中文')),
                         '--output', str(self.root / 'long.json'))

    def test_unsupported_and_corrupt_rejected(self):
        with self.assertRaises(rp.PrintError):
            rp.validate_job({'text': 'test', 'settings': {'density': 50}})
        Image.new('L', (576, 2), 0).save(self.root / 'source.png')
        content = rp.render(self.image_args(), ''); content['sha256'] = '0' * 64
        with self.assertRaises(rp.PrintError):
            rp.validate_job({'content': content})

    def test_exclusive_submit_and_receipt(self):
        receipt = self.root / 'receipt.json'; client = rp.Client(self.url, 'psr_'+'a'*43)
        result = rp.submit(client, {'text': 'test'}, receipt)
        self.assertTrue(result['accepted']); self.assertNotIn('receipt', result)
        self.assertEqual(Handler.posts, 1); self.assertEqual(Handler.body['text'], 'test'); self.assertIn('request_id',Handler.body)
        with self.assertRaises(rp.PrintError):
            rp.submit(client, {'text': 'test'}, receipt)
        self.assertEqual(Handler.posts, 1)
        out = self.command('--server', self.url, 'wait', '--receipt', str(receipt), '--timeout', '1')[0]
        self.assertEqual(out['state'], 'sent'); self.assertFalse(out['physicalPaperConfirmed'])

    def test_ambiguous_error_never_retried(self):
        Handler.response_mode = 'lost'; receipt = self.root / 'uncertain.json'
        with self.assertRaises(rp.PrintError):
            rp.submit(rp.Client(self.url, 'psr_'+'a'*43), {'text': 'test'}, receipt)
        self.assertEqual(Handler.posts, 1); self.assertEqual(rp.read_json(receipt)['state'], 'submitting')
        with self.assertRaises(rp.PrintError):
            rp.submit(rp.Client(self.url, 'psr_'+'a'*43), {'text': 'test'}, receipt)
        self.assertEqual(Handler.posts, 1)

    def test_offline_and_quota_preflight(self):
        receipt = self.root / 'receipt.json'; Handler.online = False
        with self.assertRaises(rp.PrintError):
            rp.submit(rp.Client(self.url, 'psr_'+'a'*43), {'text': 'test'}, receipt)
        self.assertEqual(Handler.posts, 0); self.assertFalse(receipt.exists())
        Handler.online = True
        with self.assertRaises(rp.PrintError):
            rp.submit(rp.Client(self.url, 'psr_'+'a'*43), {'text': 'a' * 10001}, receipt)
        self.assertEqual(Handler.posts, 0)

    def test_requested_copies_and_authorization(self):
        job = self.root / 'job.json'; rp.save_json(job, {'text': 'two intended copies'})
        receipt = self.root / 'copies.json'; config = self.root / 'config.json'
        with self.assertRaises(rp.PrintError):
            self.command('--config', str(config), '--server', self.url, 'submit', '--job', str(job), '--receipt', str(receipt))
        result = self.command('--config', str(config), '--server', self.url, 'submit', '--job', str(job), '--receipt', str(receipt), '--copies', '2', '--confirm-print')
        self.assertEqual(len(result), 2); self.assertEqual(Handler.posts, 2)
        self.assertTrue((self.root / 'copies.001.json').exists())
        with self.assertRaises(rp.PrintError):
            self.command('--config', str(config), '--server', self.url, 'submit', '--job', str(job), '--receipt', str(receipt), '--copies', '2', '--confirm-print')
        self.assertEqual(Handler.posts, 2)

    def test_url_and_redirect_boundary(self):
        for url in ('https://user:secret@example.com', 'http://example.com', 'https://example.com/path', 'https://example.com/?x=secret', 'https://example.com:invalid'):
            with self.assertRaises(rp.PrintError):
                rp.validate_server(url)
        with self.assertRaises(rp.PrintError):
            rp.Client(self.url, 'psr_fixture').request('GET', '/redirect')

    def test_inputs_are_never_overwritten(self):
        source = self.root / 'source.png'; Image.new('L', (10, 10), 0).save(source)
        original = source.read_bytes()
        with self.assertRaises(rp.PrintError):
            self.command('prepare', '--image', str(source), '--output', str(source))
        self.assertEqual(source.read_bytes(), original)
        caption = self.root / 'caption.txt'; caption.write_text('中文图片说明', encoding='utf-8')
        with self.assertRaises(rp.PrintError):
            self.command('prepare', '--image', str(source), '--caption-file', str(caption), '--output', str(self.root / 'job.json'), '--preview', str(caption))
        self.assertEqual(caption.read_text(encoding='utf-8'), '中文图片说明')

    def test_caption_and_no_credential_reuse(self):
        source = self.root / 'source.png'; Image.new('L', (576, 100), 0).save(source)
        caption = self.root / 'caption.txt'; caption.write_text('图片说明', encoding='utf-8')
        job_path = self.root / 'job.json'
        self.command('prepare', '--image', str(source), '--caption-file', str(caption), '--output', str(job_path))
        self.assertEqual(rp.read_json(job_path)['text'], '图片说明')
        device_key = self.root / 'device.json'; rp.save_json(device_key, {'role': 'device', 'token': 'psr_' + 'a' * 43})
        args = rp.parser().parse_args(['--server', self.url, '--config', str(self.root / 'config.json'), '--key-file', str(device_key), 'status'])
        with patch.dict('os.environ', {}, clear=True):
            with self.assertRaises(rp.PrintError):
                rp.connection(args)


if __name__ == '__main__':
    unittest.main()
