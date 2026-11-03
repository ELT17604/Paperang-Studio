"""Paperang Studio v1 local API client. Python standard library only."""
import json
import time
import urllib.request
import urllib.error

class PaperangClient:
    def __init__(self, token, base_url='http://127.0.0.1:8176'):
        self.token = token
        self.base_url = base_url.rstrip('/')
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def request(self, method, path, body=None):
        data = None if body is None else json.dumps(body).encode('utf-8')
        req = urllib.request.Request(self.base_url + path, data=data, method=method,
            headers={'Authorization': 'Bearer ' + self.token, 'Content-Type': 'application/json'})
        try:
            with self.opener.open(req, timeout=12) as response:
                return json.load(response)
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode('utf-8', errors='replace')
            raise RuntimeError(f'Paperang API HTTP {exc.code}: {detail}') from exc

    def status(self):
        return self.request('GET', '/v1/status')

    def devices(self, all_devices=False):
        return self.request('GET', '/v1/devices' + ('?all=true' if all_devices else ''))

    def render(self, text, settings=None, include_raw=False):
        return self.request('POST', '/v1/render', {'text': text, 'settings': settings or {}, 'includeRaw': include_raw})

    def print_text(self, text, settings=None, allow_paper=False):
        return self.request('POST', '/v1/jobs', {'text': text, 'settings': settings or {}, 'allowPaper': allow_paper})

    def print_image(self, base64_image, settings=None, allow_paper=False):
        return self.request('POST', '/v1/jobs', {'imageBase64': base64_image, 'settings': settings or {}, 'allowPaper': allow_paper})

    def job(self, job_id):
        return self.request('GET', '/v1/jobs/' + job_id)

    def wait(self, job_id, timeout=300):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            job = self.job(job_id)
            if job['state'] != '发送中':
                return job
            time.sleep(0.5)
        raise TimeoutError('Timed out waiting for Paperang job')
