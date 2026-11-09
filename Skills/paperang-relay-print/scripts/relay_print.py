#!/usr/bin/env python3
"""Paperang relay sender. No POST retries; no client-side thermal overrides."""
import uuid
import argparse
import base64
import ctypes
import hashlib
import ipaddress
import json
import math
import os
from pathlib import Path
import re
import sys
import tempfile
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request

__version__ = '0.3.1-relayskill-opt1'


class PrintError(Exception):
    def __init__(self, message, status=None):
        super().__init__(message); self.status = status


def emit(value):
    print(json.dumps(value, ensure_ascii=True))


def default_config():
    if os.name == 'nt':
        return Path(os.environ.get('LOCALAPPDATA', str(Path.home()))) / 'ELT/PaperangRelaySkill/config.json'
    return Path.home() / '.config/paperang-relay-print/config.json'


def read_json(path, limit=1048576):
    path = Path(path).expanduser()
    if path.stat().st_size > limit:
        raise PrintError('File exceeds allowed size')
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save_json(path, value, exclusive=False):
    path = Path(path).expanduser().resolve()
    path.parent.mkdir(parents=True, exist_ok=True)
    data = json.dumps(value, ensure_ascii=False, indent=2).encode('utf-8')
    if exclusive:
        fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        with os.fdopen(fd, 'wb') as out:
            out.write(data); out.flush(); os.fsync(out.fileno())
    else:
        fd, tmp = tempfile.mkstemp(prefix=path.name + '.', dir=path.parent)
        try:
            with os.fdopen(fd, 'wb') as out:
                out.write(data); out.flush(); os.fsync(out.fileno())
            os.replace(tmp, path)
        finally:
            if os.path.exists(tmp):
                os.unlink(tmp)
    return str(path)


def winlite_server():
    """Decrypt locally, return endpoint only; the device key never leaves this function."""
    if os.name != 'nt':
        return None
    path = Path(os.environ.get('LOCALAPPDATA', str(Path.home()))) / 'ELT/PaperangStudioWinLite/settings.dat'
    if not path.exists():
        return None
    if path.stat().st_size > 4 * 1024 * 1024:
        raise PrintError('WinLite settings exceed allowed size')
    class Blob(ctypes.Structure):
        _fields_ = [('size', ctypes.c_uint32), ('data', ctypes.c_void_p)]
    raw = path.read_bytes(); buf = ctypes.create_string_buffer(raw)
    source = Blob(len(raw), ctypes.cast(buf, ctypes.c_void_p)); output = Blob()
    unprotect = ctypes.windll.crypt32.CryptUnprotectData
    unprotect.argtypes = [ctypes.POINTER(Blob), ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint32, ctypes.POINTER(Blob)]
    unprotect.restype = ctypes.c_int
    if not unprotect(ctypes.byref(source), None, None, None, None, 1, ctypes.byref(output)):
        raise PrintError('Cannot decrypt current-user WinLite settings')
    try:
        settings = json.loads(ctypes.string_at(output.data, output.size))
        return settings.get('server')
    finally:
        free = ctypes.windll.kernel32.LocalFree
        free.argtypes = [ctypes.c_void_p]; free.restype = ctypes.c_void_p
        free(output.data)


def validate_server(value, allow_http=False):
    u = urllib.parse.urlsplit(value or '')
    try:
        _ = u.port
    except ValueError:
        raise PrintError('Invalid relay port') from None
    try:
        loopback = u.hostname == 'localhost' or ipaddress.ip_address(u.hostname or '').is_loopback
    except ValueError:
        loopback = u.hostname == 'localhost'
    if not u.hostname or u.username is not None or u.password is not None or u.query or u.fragment or u.path not in ('', '/'):
        raise PrintError('Use a relay root URL without credentials, query or path')
    if u.scheme != 'https' and not (u.scheme == 'http' and (loopback or allow_http)):
        raise PrintError('HTTPS is required; use --allow-http only for an intended HTTP endpoint')
    return value.rstrip('/') + '/'


def connection(args, server=None):
    config = read_json(args.config, 65536) if Path(args.config).exists() else {}
    endpoint = server or args.server or os.environ.get('PAPERANG_RELAY_URL') or config.get('server') or winlite_server()
    endpoint = validate_server(endpoint, args.allow_http or config.get('allowHttp', False))
    key = os.environ.get('PAPERANG_RELAY_SUBMIT_KEY', '').strip()
    keyfile = args.key_file or config.get('submitKeyFile')
    if not key and keyfile:
        p = Path(keyfile).expanduser()
        if p.stat().st_size > 65536:
            raise PrintError('Key file too large')
        key = p.read_text(encoding='utf-8-sig').strip()
        if key.startswith('{'):
            obj = json.loads(key)
            if obj.get('role') not in (None, 'submit', 'A', 'B'):
                raise PrintError('A submit key is required, not a device/admin key')
            key = obj.get('token', '')
    if key and not re.fullmatch(r'psr_[A-Za-z0-9_-]{40,100}', key):
        raise PrintError('Invalid submit-key format; do not use the saved device key')
    return Client(endpoint, key)


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        raise PrintError('Relay redirected the request; use the intended final root URL')


class Client:
    def __init__(self, server, key=''):
        self.server, self.key = server, key
        self.opener = urllib.request.build_opener(NoRedirect())

    def request(self, method, path, body=None, receipt=None):
        wire = None if body is None else json.dumps(body, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
        headers = {'Accept': 'application/json', 'Cache-Control': 'no-store', 'User-Agent': 'PaperangStudio-RelayPrint/0.3.1'}
        if wire is not None:
            headers.update({'Content-Type': 'application/json', 'Content-Length': str(len(wire))})
        if self.key:
            headers['Authorization'] = 'Bearer ' + self.key
        if receipt:
            headers['X-Job-Receipt'] = receipt
        request = urllib.request.Request(self.server + path.lstrip('/'), data=wire, headers=headers, method=method)
        try:
            with self.opener.open(request, timeout=8) as response:
                raw = response.read(1048577)
            if len(raw) > 1048576:
                raise PrintError('Relay response too large')
            result = json.loads(raw)
            if not isinstance(result, dict):
                raise PrintError('Relay response must be an object')
            return result
        except urllib.error.HTTPError as exc:
            suffix = ('; check submit-key permissions' if method == 'POST' else '; check public API access and proxy policy') if exc.code in (401, 403) else ('; do not retry submission automatically' if method == 'POST' else '; status read failed')
            code = exc.code; exc.close()
            raise PrintError(f'Relay HTTP {code}' + suffix, status=code) from None
        except (urllib.error.URLError, TimeoutError, OSError):
            note = 'a POST may have been accepted, do not resend' if method == 'POST' else 'this read did not submit a print job'
            raise PrintError('Relay request unavailable or timed out; ' + note) from None


def validate_job(job):
    if not isinstance(job, dict) or set(job) - {'text', 'content', 'browser'}:
        raise PrintError('Job contains unsupported fields (thermal settings are client-only)')
    text = job.get('text', '')
    if not isinstance(text, str) or len(text) > 10000 or any(ord(c) < 32 and c not in '\n\r\t' for c in text):
        raise PrintError('Text must be at most 10000 characters, without unsupported controls')
    content = job.get('content'); rows = 0
    if content is not None:
        if not isinstance(content, dict) or content.get('type') != 'raster1' or type(content.get('schemaVersion')) is not int or content['schemaVersion'] != 1:
            raise PrintError('Expected raster1 schema 1')
        rows = content.get('height')
        if type(rows) is not int or not 1 <= rows <= 1400 or type(content.get('width')) is not int or content['width'] != 576:
            raise PrintError('Raster must be exactly 576 dots wide and 1–1400 rows')
        if content.get('bitOrder') != 'msb-first' or content.get('blackIsOne') is not True or content.get('encoding') != 'base64':
            raise PrintError('Unsupported raster encoding')
        encoded = content.get('data')
        if not isinstance(encoded, str) or len(encoded) > 134400:
            raise PrintError('Raster payload too large')
        try:
            raw = base64.b64decode(encoded, validate=True)
        except ValueError:
            raise PrintError('Invalid base64') from None
        if len(raw) != 72 * rows or hashlib.sha256(raw).hexdigest() != content.get('sha256'):
            raise PrintError('Raster byte count or SHA-256 mismatch')
    if not text.strip() and content is None:
        raise PrintError('No printable content')
    if len(json.dumps(job, ensure_ascii=False, separators=(',', ':')).encode('utf-8')) > 163840:
        raise PrintError('Submission exceeds 160 KiB')
    return len(text) + rows


def clusters(text):
    result = []
    for c in text:
        if result and (unicodedata.combining(c) or c in '\ufe0f\u200d' or result[-1].endswith('\u200d')):
            result[-1] += c
        else:
            result.append(c)
    return result


def find_font(explicit, text):
    candidates = [explicit] if explicit else [
        str(Path(os.environ.get('WINDIR', 'C:/Windows')) / 'Fonts/msyh.ttc'),
        '/System/Library/Fonts/PingFang.ttc',
        '/System/Library/Fonts/STHeiti Medium.ttc',
        '/System/Library/Fonts/Supplemental/Arial Unicode.ttf',
        '/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc',
        '/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf']
    for path in candidates:
        if path and Path(path).is_file():
            if not explicit and 'DejaVu' in path and any(ord(c) > 0x2fff for c in text):
                continue
            return path
    raise PrintError('No suitable font found; pass --font with a Chinese-capable TTF/OTF/TTC')


def render(args, text):
    try:
        from PIL import Image, ImageDraw, ImageFont, ImageOps
    except ImportError:
        raise PrintError('Pillow is required for rendering; use the skill Python venv') from None
    width = 576 - 2 * args.margin
    available = (args.height or args.max_height) - 2 * args.margin_y
    if available < 1:
        raise PrintError('Vertical margins exceed canvas height')
    if args.image:
        path = Path(args.image)
        if path.stat().st_size > 64 * 1024 * 1024:
            raise PrintError('Image exceeds 64 MiB')
        with Image.open(path) as source:
            if source.width * source.height > 40000000:
                raise PrintError('Image exceeds 40 million pixels')
            source.seek(0)
            rgba = ImageOps.exif_transpose(source).convert('RGBA')
        if args.rotate:
            rgba = rgba.rotate(-args.rotate, expand=True)
        white = Image.new('RGBA', rgba.size, 'white'); white.alpha_composite(rgba)
        source = white.convert('RGB')
        if args.fit == 'cover':
            if not args.height:
                raise PrintError('Cover crop requires --height')
            fitted = ImageOps.fit(source, (width, available), method=Image.Resampling.LANCZOS)
        else:
            scale = width / source.width
            if args.fit == 'contain':
                scale = min(scale, available / source.height)
            target = (max(1, round(source.width * scale)), max(1, round(source.height * scale)))
            if target[1] > available:
                raise PrintError('Image is too tall; use contain, reduce dimensions or explicitly accept cropping')
            fitted = source.resize(target, Image.Resampling.LANCZOS)
        height = args.height or fitted.height + 2 * args.margin_y
        canvas = Image.new('RGB', (576, height), 'white')
        x = args.margin if args.align == 'left' else 576 - args.margin - fitted.width if args.align == 'right' else (576 - fitted.width) // 2
        canvas.paste(fitted, (x, args.margin_y))
    else:
        font = ImageFont.truetype(find_font(args.font, text), args.font_size)
        asc, desc = font.getmetrics(); line_height = asc + desc + args.line_spacing
        lines = []
        for paragraph in text.replace('\r\n', '\n').replace('\r', '\n').expandtabs(4).split('\n'):
            line = ''
            for element in clusters(paragraph):
                if line and font.getlength(line + element) > width:
                    lines.append(line); line = ''
                line += element
            lines.append(line)
        height = len(lines) * line_height + 2 * args.margin_y
        if height > args.max_height:
            raise PrintError('Text exceeds maximum height; reduce font/spacing or request multiple sheets explicitly')
        canvas = Image.new('RGB', (576, height), 'white'); draw = ImageDraw.Draw(canvas)
        for i, line in enumerate(lines):
            line_width = font.getlength(line)
            x = args.margin if args.align == 'left' else 576 - args.margin - line_width if args.align == 'right' else (576 - line_width) / 2
            draw.text((x, args.margin_y + i * line_height), line, fill='black', font=font, anchor='lt')
    if not 1 <= canvas.height <= args.max_height <= 1400:
        raise PrintError('Rendered canvas exceeds requested height limit')
    lut = []
    for v in range(256):
        a = max(0, min(255, (v - 127.5) * args.contrast + 127.5 + args.brightness * 2.55))
        v = round((a / 255) ** (1 / args.gamma) * 255)
        lut.append(255 - v if args.invert else v)
    gray = canvas.convert('L').point(lut)
    if args.dither == 'floyd':
        gray = gray.point([max(0, min(255, v + 128 - args.threshold)) for v in range(256)])
        mono = gray.convert('1', dither=Image.Dither.FLOYDSTEINBERG)
    elif args.dither == 'bayer':
        matrix = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))
        pixels = gray.load(); mono = Image.new('1', gray.size, 1); dest = mono.load()
        for y in range(gray.height):
            for x in range(576):
                dest[x, y] = 255 if pixels[x, y] >= (matrix[y % 4][x % 4] + .5) * 16 + args.threshold - 128 else 0
    else:
        mono = gray.point([255 if v >= args.threshold else 0 for v in range(256)], mode='1')
    if args.preview:
        Path(args.preview).parent.mkdir(parents=True, exist_ok=True)
        mono.save(args.preview, format='PNG')
    raw = bytes(b ^ 255 for b in mono.tobytes())  # Pillow bit=1 white; relay bit=1 black.
    return {'type': 'raster1', 'schemaVersion': 1, 'width': 576, 'height': mono.height,
            'encoding': 'base64', 'bitOrder': 'msb-first', 'blackIsOne': True,
            'data': base64.b64encode(raw).decode('ascii'), 'sha256': hashlib.sha256(raw).hexdigest()}


def submit(client, job, receipt_path, queue=False):
    units = validate_job(job)
    public = client.request('GET', 'v2/public/status')
    policy = public['policy']; status = public['status']
    if not client.key:
        raise PrintError('API v2 requires an A or B key; browser anonymous credentials are not borrowed')
    prefix = 'authenticated' if client.key else 'anonymous'
    if units > policy[prefix + '_max_length'] or units > policy[prefix + '_hourly_chars']:
        raise PrintError(f'Job uses {units} units, exceeding policy; reduce size or use an authorized submit key')
    if job.get('content') and ('raster1' not in public.get('capabilities', {}).get('contentTypes', []) or public.get('capabilities', {}).get('contentSchemaVersion', 1) != 1):
        raise PrintError('Relay does not advertise raster1 support')
    if not status.get('accepting'):
        raise PrintError('Receiver is offline/disconnected/paused; queue only when the user permits waiting')
    record = {'server': client.server, 'state': 'submitting', 'created': time.time(), 'request_id': str(uuid.uuid4()), 'jobSha256': hashlib.sha256(json.dumps(job, sort_keys=True, ensure_ascii=False).encode()).hexdigest()}
    try:
        save_json(receipt_path, record, exclusive=True)
    except FileExistsError:
        raise PrintError('Receipt/attempt path already exists; inspect it, never overwrite to resend') from None
    try:
        result = client.request('POST', 'v2/jobs', {**job, 'request_id': record['request_id']})
        if not re.fullmatch(r'(?:[a-f0-9]{40}|[a-f0-9-]{36})', result.get('id', '')) or result.get('accepted') is not True:
            raise PrintError('Invalid submission receipt; outcome is uncertain, do not resend')
        record['state'] = 'accepted'
        record.update({k: result[k] for k in ('id', 'shortID', 'accepted', 'statusReadable') if k in result})
        save_json(receipt_path, record)
    except Exception:
        # The pre-POST record remains even if the relay accepted a response we lost.
        raise
    return {k: record[k] for k in ('id', 'shortID', 'accepted', 'statusReadable') if k in record} | {'receiptFile': str(Path(receipt_path).resolve())}


def ranged(cast, low, high):
    def parse(value):
        n = cast(value)
        if not math.isfinite(n) or not low <= n <= high:
            raise argparse.ArgumentTypeError(f'Value must be {low}..{high}')
        return n
    return parse


def parser():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--version', action='version', version=__version__)
    p.add_argument('--server'); p.add_argument('--key-file'); p.add_argument('--allow-http', action='store_true')
    p.add_argument('--config', default=str(default_config()))
    sub = p.add_subparsers(dest='command', required=True)
    config = sub.add_parser('configure'); config.add_argument('--server-url', required=True); config.add_argument('--submit-key-file')
    sub.add_parser('status')
    prepare = sub.add_parser('prepare'); source = prepare.add_mutually_exclusive_group(required=True)
    source.add_argument('--text-file'); source.add_argument('--image')
    prepare.add_argument('--caption-file', help='Optional UTF-8 image caption, rendered by the receiver')
    prepare.add_argument('--mode', choices=['text', 'raster'], default='raster')
    prepare.add_argument('--output', required=True); prepare.add_argument('--preview'); prepare.add_argument('--font')
    prepare.add_argument('--font-size', type=ranged(int, 8, 96), default=24)
    prepare.add_argument('--align', choices=['left', 'center', 'right'], default='left')
    prepare.add_argument('--line-spacing', type=ranged(int, 0, 40), default=4)
    prepare.add_argument('--margin', type=ranged(int, 0, 120), default=12); prepare.add_argument('--margin-y', type=ranged(int, 0, 200), default=12)
    prepare.add_argument('--max-height', type=ranged(int, 1, 1400), default=1000); prepare.add_argument('--height', type=ranged(int, 1, 1400))
    prepare.add_argument('--fit', choices=['contain', 'width', 'cover'], default='contain')
    prepare.add_argument('--rotate', type=int, choices=[0, 90, 180, 270], default=0)
    prepare.add_argument('--threshold', type=ranged(int, 0, 255), default=128); prepare.add_argument('--dither', choices=['none', 'floyd', 'bayer'], default='none')
    prepare.add_argument('--brightness', type=ranged(float, -100, 100), default=0); prepare.add_argument('--contrast', type=ranged(float, .2, 3), default=1); prepare.add_argument('--gamma', type=ranged(float, .2, 3), default=1)
    prepare.add_argument('--invert', action='store_true')
    send = sub.add_parser('submit'); send.add_argument('--job', required=True); send.add_argument('--receipt', required=True)
    send.add_argument('--confirm-print', action='store_true'); send.add_argument('--queue-if-offline', action='store_true'); send.add_argument('--copies', type=ranged(int, 1, 20), default=1)
    wait = sub.add_parser('wait'); wait.add_argument('--receipt', required=True); wait.add_argument('--timeout', type=ranged(int, 1, 600), default=120)
    return p


def main(argv=None):
    args = parser().parse_args(argv)
    if args.command == 'configure':
        config = {'server': validate_server(args.server_url, args.allow_http), 'allowHttp': args.allow_http}
        if args.submit_key_file:
            if Path(args.submit_key_file).expanduser().resolve() == Path(args.config).expanduser().resolve():
                raise PrintError('Configuration must not overwrite the submit-key file')
            config['submitKeyFile'] = str(Path(args.submit_key_file).expanduser().resolve(strict=True))
        emit({'configured': save_json(args.config, config), 'keyStored': False}); return
    if args.command == 'prepare':
        original = Path(args.text_file or args.image).resolve()
        outputs = [Path(args.output).resolve()] + ([Path(args.preview).resolve()] if args.preview else [])
        if original in outputs or len(set(outputs)) != len(outputs):
            raise PrintError('Use separate input, job output and preview paths')
        if args.caption_file:
            if not args.image:
                raise PrintError('Caption file requires an image input')
            if Path(args.caption_file).stat().st_size > 1000000 or Path(args.caption_file).resolve() in outputs:
                raise PrintError('Caption is too large or conflicts with an output path')
        text = ''
        if args.text_file:
            path = Path(args.text_file)
            if path.stat().st_size > 1000000:
                raise PrintError('Text file too large')
            text = path.read_text(encoding='utf-8-sig')
            validate_job({'text': text})
        if args.image or args.mode == 'raster':
            job = {'text': '', 'content': render(args, text)}
        else:
            if args.preview:
                raise PrintError('Native text jobs use receiver layout; select raster mode for a local preview')
            job = {'text': text}
        if args.caption_file:
            job['text'] = Path(args.caption_file).read_text(encoding='utf-8-sig')
        units = validate_job(job); output = save_json(args.output, job)
        emit({'prepared': output, 'type': 'raster1' if 'content' in job else 'text', 'units': units, 'preview': str(Path(args.preview).resolve()) if args.preview else None}); return
    if args.command == 'status':
        result = connection(args).request('GET', 'v2/public/status')
        emit({k: result[k] for k in ('version', 'status', 'policy', 'capabilities') if k in result}); return
    if args.command == 'submit':
        if not args.confirm_print:
            raise PrintError('Submission requires an explicit print request and --confirm-print')
        job = read_json(args.job, 163840); client = connection(args)
        receipts = []
        for i in range(args.copies):
            p = Path(args.receipt)
            receipts.append(p if args.copies == 1 else p.with_name(f'{p.stem}.{i + 1:03d}{p.suffix or ".json"}'))
        if any(p.exists() for p in receipts):
            raise PrintError('An intended copy already has an attempt record; do not replay the batch')
        for p in receipts:
            emit(submit(client, job, p, args.queue_if_offline))
        return
    record = read_json(args.receipt, 65536)
    if not re.fullmatch(r'(?:[a-f0-9]{40}|[a-f0-9-]{36})', record.get('id', '')):
        raise PrintError('No usable receipt: submission may be uncertain; do not resend')
    client = connection(args)
    if client.server != validate_server(record.get('server'), args.allow_http):
        raise PrintError('Receipt server differs from configured server')
    if not record.get('statusReadable'):
        raise PrintError('A permission: accepted by inbox, delivery status is forbidden')
    end = time.monotonic() + args.timeout; state = None
    while True:
        remaining = end - time.monotonic()
        try:
            row = client.request('GET', 'v2/jobs/' + record['id'])
            state = row.get('state')
        except PrintError as exc:
            if exc.status in (401,403,404) or remaining <= 0:
                raise
        if state in ('sent', 'confirmed', 'unknown', 'rejected') or time.monotonic() >= end:
            emit({'id': record['id'], 'state': state or 'unavailable', 'physicalPaperConfirmed': state == 'confirmed', 'timedOut': state not in ('sent', 'confirmed', 'unknown', 'rejected')}); return
        time.sleep(min(2, max(0, end - time.monotonic())))


if __name__ == '__main__':
    try:
        main()
    except (PrintError, ValueError, KeyError, TypeError, OSError) as exc:
        # Never echo arbitrary exception bodies/URLs that may include secrets.
        emit({'error': str(exc) if isinstance(exc, PrintError) else type(exc).__name__, 'automaticResubmission': False})
        sys.exit(2)
