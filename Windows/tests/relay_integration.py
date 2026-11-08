"""Use the v0.3.0 API v2 relay; secrets stay in a temporary file."""
import argparse
import base64
import hashlib
import json
import socket
import subprocess
import sys
import tempfile
import threading
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT.parent / 'Server'))
from relay import Store, Server


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--dotnet', default='dotnet')
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix='paperang-win-test-') as tmp:
        with socket.socket() as s:
            s.bind(('127.0.0.1', 0))
            port = s.getsockname()[1]
        store = Store({'database': str(Path(tmp) / 'relay.db'), 'port': port})
        admin = store.api.auth.create('Windows admin','C')
        device = store.key_add('Windows test', 'device', 'p2')
        submit = store.key_add('Test sender', 'submit', 'p2')
        actor = store.api.auth.authenticate('Bearer ' + submit['token'])
        # Long body forces the Windows client to retrieve the untruncated detail.
        first = store.route('POST','/v2/jobs',{'text': '这是一条用于 Windows 测试的完整长纸条。' * 9,'request_id':str(uuid.uuid4())}, actor, '127.0.0.1', {})[1]
        raw = bytes([0xAA] * 72 * 20)
        second = store.route('POST','/v2/jobs',{'text': '绘图说明', 'request_id':str(uuid.uuid4()),'content': {
            'type': 'raster1', 'schemaVersion': 1, 'width': 576, 'height': 20,
            'bitOrder': 'msb-first', 'blackIsOne': True, 'encoding': 'base64',
            'data': base64.b64encode(raw).decode(), 'sha256': hashlib.sha256(raw).hexdigest()
        }}, actor, '127.0.0.1', {})[1]
        config = Path(tmp) / 'client.json'
        config.write_text(json.dumps({'server': f'http://127.0.0.1:{port}', 'key': device['token']}))
        server = Server(store)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            subprocess.run([args.dotnet, 'run', '--project', str(ROOT / 'tests/Paperang.Tests'), '-c', 'Release', '--', '--live', str(config)], check=True)
            admin_config = Path(tmp) / 'admin.json'
            admin_config.write_text(json.dumps({'server':f'http://127.0.0.1:{port}','key':admin['token']}))
            subprocess.run([args.dotnet,'run','--project',str(ROOT / 'tests/Paperang.Tests'),'-c','Release','--','--admin-test',str(admin_config)],check=True)
            with store.connect() as db:
                states = [dict(row) for row in db.execute('SELECT id,state FROM jobs')]
            assert len(states) == 2 and all(row['state'] == 'sent' for row in states), states
            assert store.public_status()['online']
            print('PASS original relay database confirms both jobs sent; no physical completion fabricated')
        finally:
            server.shutdown()
            server.server_close()


if __name__ == '__main__':
    main()
