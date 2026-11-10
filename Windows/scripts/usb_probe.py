"""Experimental P2 USBPRINT transport. Standard library only; no driver replacement.

Default: query model/version/status/battery/density. --all queries 17 known GETs.
--print-test prints one bounded pattern; --watch polls timestamped telemetry.
No retries of physical writes: a timed-out write may already have reached the printer.
"""
import argparse
import ctypes as C
from ctypes import wintypes as W
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import struct
import threading
import time
import uuid
import zlib

SEED = 0x35769521

# Explicitly established traditional GETs only. Response codes are not GETs.
QUERIES = {
    'version': (0x04, (0x05,)), 'model': (0x06, (0x07,)),
    'mac': (0x08, (0x09, 0x0b)), 'serial': (0x0a, (0x0b,)),
    'status': (0x0c, (0x0d,)), 'voltage': (0x0e, (0x0f,)),
    'battery': (0x10, (0x11,)), 'temperature': (0x12, (0x13,)),
    'factory': (0x15, (0x16,)), 'heat': (0x1c, (0x1d,)),
    'power_down': (0x1f, (0x20,)), 'board': (0x23, (0x24,)),
    'hardware': (0x25, (0x26,)), 'max_gap': (0x28, (0x29,)),
    'paper_type': (0x2a, (0x2b,)), 'country': (0x2d, (0x2e,)),
    'short_name': (0x30, (0x31,)),
}


def timestamp():
    return datetime.now(timezone.utc).isoformat(timespec='milliseconds')


def interpret(name, data):
    if name in ('mac', 'serial'):
        return {'redacted': True, 'length': len(data)}
    if name == 'status' and len(data) == 2:
        raw = int.from_bytes(data, 'little')
        opened = bool(raw & 0x10)
        return {'raw': raw, 'cover_open': opened, 'cover_closed': not opened,
                'paper_present': None if opened else not bool(raw & 1),
                'unknown_bits': raw & ~0x11,
                'printing': None, 'mechanically_complete': None, 'charging': None}
    if name == 'battery' and len(data) == 1:
        return {'percent': data[0] if data[0] <= 100 else None}
    if name == 'heat' and len(data) == 1:
        return {'density': data[0] if data[0] <= 100 else None}
    if name == 'power_down' and len(data) == 2:
        return {'seconds': int.from_bytes(data, 'little')}
    if name in ('model', 'board', 'short_name', 'country'):
        return {'text': data.decode('ascii', errors='backslashreplace').strip('\x00\r\n ')}
    # Voltage/temperature/other bit units have not been calibrated on USB.
    return {'meaning': 'unconfirmed'}


def frame(command, payload=b'', remaining=0, seed=SEED):
    if len(payload) > 1023 or not 0 <= remaining <= 255:
        raise ValueError('Invalid payload length or remaining count')
    return (struct.pack('<BBBH', 2, command, remaining, len(payload)) + payload
            + struct.pack('<IB', zlib.crc32(payload, seed), 3))


class Decoder:
    def __init__(self, seed=SEED):
        self.buffer = bytearray()
        self.seed = seed

    def push(self, data):
        self.buffer.extend(data)
        result = []
        while len(self.buffer) >= 10:
            n = int.from_bytes(self.buffer[3:5], 'little')
            if self.buffer[0] != 2 or n > 1023:
                del self.buffer[0]
                continue
            if len(self.buffer) < n + 10:
                # A truncated/corrupt header can announce a plausible but wrong
                # length. Resync only to a complete CRC-valid later frame.
                replacement = None
                for offset in range(1, len(self.buffer) - 9):
                    if self.buffer[offset] != 2:
                        continue
                    size = int.from_bytes(self.buffer[offset+3:offset+5], 'little')
                    end = offset + size + 10
                    if size > 1023 or end > len(self.buffer):
                        continue
                    candidate = self.buffer[offset+5:offset+5+size]
                    if self.buffer[end-1] == 3 and int.from_bytes(self.buffer[end-5:end-1], 'little') == zlib.crc32(candidate, self.seed):
                        replacement = offset
                        break
                if replacement is not None:
                    del self.buffer[:replacement]
                    continue
                break
            p = bytes(self.buffer[5:5+n])
            crc = int.from_bytes(self.buffer[5+n:9+n], 'little')
            if self.buffer[9+n] != 3 or crc != zlib.crc32(p, self.seed):
                del self.buffer[0]
                continue
            result.append((self.buffer[1], self.buffer[2], p))
            del self.buffer[:n+10]
        return result


def device_paths():
    """Enumerate present USBPRINT interfaces, excluding stale registry records."""
    class Guid(C.Structure):
        _fields_ = [('data1', W.DWORD), ('data2', W.WORD), ('data3', W.WORD), ('data4', W.BYTE * 8)]

    class Interface(C.Structure):
        _fields_ = [('cbSize', W.DWORD), ('guid', Guid), ('flags', W.DWORD), ('reserved', C.c_size_t)]

    guid = Guid.from_buffer_copy(uuid.UUID('28d78fad-5a12-11d1-ae5b-0000f803a8c2').bytes_le)
    api = C.WinDLL('setupapi', use_last_error=True)
    api.SetupDiGetClassDevsW.argtypes = [C.POINTER(Guid), W.LPCWSTR, W.HWND, W.DWORD]
    api.SetupDiGetClassDevsW.restype = W.HANDLE
    api.SetupDiEnumDeviceInterfaces.argtypes = [W.HANDLE, C.c_void_p, C.POINTER(Guid), W.DWORD, C.POINTER(Interface)]
    api.SetupDiGetDeviceInterfaceDetailW.argtypes = [W.HANDLE, C.POINTER(Interface), C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.c_void_p]
    api.SetupDiDestroyDeviceInfoList.argtypes = [W.HANDLE]
    devices = api.SetupDiGetClassDevsW(C.byref(guid), None, None, 0x12)
    if devices == C.c_void_p(-1).value:
        raise C.WinError(C.get_last_error())
    paths = []
    try:
        index = 0
        while True:
            item = Interface(cbSize=C.sizeof(Interface))
            if not api.SetupDiEnumDeviceInterfaces(devices, None, C.byref(guid), index, C.byref(item)):
                if C.get_last_error() == 259:
                    break
                raise C.WinError(C.get_last_error())
            needed = W.DWORD()
            api.SetupDiGetDeviceInterfaceDetailW(devices, C.byref(item), None, 0, C.byref(needed), None)
            if C.get_last_error() != 122:
                raise C.WinError(C.get_last_error())
            detail = C.create_string_buffer(needed.value)
            C.cast(detail, C.POINTER(W.DWORD))[0] = 8 if C.sizeof(C.c_void_p) == 8 else 6
            if not api.SetupDiGetDeviceInterfaceDetailW(devices, C.byref(item), detail, needed, None, None):
                raise C.WinError(C.get_last_error())
            path = C.wstring_at(C.addressof(detail) + 4)
            if path.lower().startswith('\\\\?\\usb#vid_4348&pid_5584#'):
                paths.append(path)
            index += 1
        return paths
    finally:
        api.SetupDiDestroyDeviceInfoList(devices)


class UsbPrint:
    def __init__(self, trace=False):
        paths = device_paths()
        if len(paths) != 1:
            raise RuntimeError(f'Expected one P2 USB interface, found {len(paths)}')
        self.k = C.WinDLL('kernel32', use_last_error=True)
        self.k.CreateFileW.argtypes = [W.LPCWSTR, W.DWORD, W.DWORD, C.c_void_p, W.DWORD, W.DWORD, W.HANDLE]
        self.k.CreateFileW.restype = W.HANDLE
        self.k.CreateEventW.argtypes = [C.c_void_p, W.BOOL, W.BOOL, W.LPCWSTR]
        self.k.CreateEventW.restype = W.HANDLE
        for name in ('ReadFile', 'WriteFile'):
            getattr(self.k, name).argtypes = [W.HANDLE, C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.c_void_p]
        self.k.GetOverlappedResult.argtypes = [W.HANDLE, C.c_void_p, C.POINTER(W.DWORD), W.BOOL]
        self.k.CancelIoEx.argtypes = [W.HANDLE, C.c_void_p]
        self.k.DeviceIoControl.argtypes = [W.HANDLE, W.DWORD, C.c_void_p, W.DWORD, C.c_void_p, W.DWORD, C.POINTER(W.DWORD), C.c_void_p]
        self.k.WaitForSingleObject.argtypes = [W.HANDLE, W.DWORD]
        self.k.CloseHandle.argtypes = [W.HANDLE]
        self.handle = self.k.CreateFileW(paths[0], 0xC0000000, 3, None, 3, 0x40000000, None)
        if self.handle == C.c_void_p(-1).value:
            raise C.WinError(C.get_last_error())
        self.seed = SEED
        self.decoder = Decoder(self.seed)
        self.trace = trace
        self.events = []
        self.writes = []
        self.lock = threading.RLock()

    def io(self, data=None, timeout=2000, ioctl=None, size=4096):
        class Overlapped(C.Structure):
            _fields_ = [('Internal', C.c_size_t), ('InternalHigh', C.c_size_t),
                        ('Offset', W.DWORD), ('OffsetHigh', W.DWORD), ('hEvent', W.HANDLE)]
        event = self.k.CreateEventW(None, True, False, None)
        if not event:
            raise C.WinError(C.get_last_error())
        ov = Overlapped(hEvent=event)
        buf = C.create_string_buffer(data, len(data)) if data is not None else C.create_string_buffer(size)
        count = W.DWORD()
        write_record = None
        if data is not None:
            write_record = {'at': timestamp(), 'command': f'{data[1]:02x}', 'index': data[2],
                            'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest(), 'state': 'unknown'}
            self.writes.append(write_record)
        try:
            if ioctl is not None:
                if ioctl not in (0x220030, 0x220034):
                    raise ValueError('Only standard read-only USBPRINT status/device-ID IOCTLs are enabled')
                ok = self.k.DeviceIoControl(self.handle, ioctl, None, 0, buf, len(buf), C.byref(count), C.byref(ov))
            else:
                api = self.k.WriteFile if data is not None else self.k.ReadFile
                ok = api(self.handle, buf, len(buf), C.byref(count), C.byref(ov))
            if not ok:
                err = C.get_last_error()
                if err != 997:
                    raise C.WinError(err)
                wait = self.k.WaitForSingleObject(event, timeout)
                if wait != 0:
                    self.k.CancelIoEx(self.handle, C.byref(ov))
                    # Keep buffer/OVERLAPPED alive until cancellation completes.
                    self.k.GetOverlappedResult(self.handle, C.byref(ov), C.byref(count), True)
                    raise TimeoutError('USB I/O timeout; operation cancelled, never automatically replayed')
                if not self.k.GetOverlappedResult(self.handle, C.byref(ov), C.byref(count), False):
                    raise C.WinError(C.get_last_error())
            if data is not None and count.value != len(data):
                raise IOError('Short USB write; outcome unknown, do not replay')
            if write_record is not None:
                write_record['state'] = 'sent'
            return bytes(buf.raw[:count.value])
        finally:
            self.k.CloseHandle(event)

    def class_info(self):
        results = {}
        with self.lock:
            for name, code, size in [('lpt_status', 0x220030, 4), ('ieee1284_id', 0x220034, 4094)]:
                try:
                    raw = self.io(ioctl=code, size=size)
                    if name == 'lpt_status':
                        results[name] = {'raw': raw.hex(), 'meaning': 'not calibrated on this device'}
                    else:
                        # Never print a potential SERN/SN field from the ID string.
                        text = raw[2:].rstrip(b'\x00').decode('ascii', errors='backslashreplace')
                        fields = {}
                        for field in text.split(';'):
                            key, separator, value = field.partition(':')
                            if separator:
                                fields[key] = value if key.upper() in ('MFG', 'MANUFACTURER', 'MDL', 'MODEL', 'CMD', 'CLS', 'DES') else '<redacted>'
                        results[name] = {'bytes': len(raw), 'length_prefix_raw': raw[:2].hex(), 'fields': fields}
                except (OSError, TimeoutError) as error:
                    results[name] = {'error': str(error)}
        return results

    def receive(self, timeout=1000):
        raw = self.io(timeout=timeout)
        if not raw:
            time.sleep(.005)
        frames = self.decoder.push(raw)
        for cmd, index, data in frames:
            record = {'at': timestamp(), 'command': f'{cmd:02x}', 'index': index,
                      'payload': '<redacted>' if cmd in (0x09, 0x0b) else data.hex()}
            self.events.append(record)
            if self.trace:
                print('RX', json.dumps(record), flush=True)
        return frames

    def drain(self):
        # Read old notifications/ACKs before opening a new request window.
        deadline = time.monotonic() + .5
        while time.monotonic() < deadline:
            try:
                if not self.receive(timeout=60):
                    time.sleep(.005)
            except TimeoutError:
                break
        else:
            raise RuntimeError('USB input did not become quiet; competing client or event flood')
        # Partial stale messages cannot be safely associated with the next GET.
        self.decoder = Decoder(getattr(self, 'seed', SEED))

    def identify(self):
        result = self.query('model')
        if result['state'] == 'no_response' and self.seed == SEED:
            self.seed = 0
            self.decoder = Decoder(self.seed)
            result = self.query('model')
        if result['state'] != 'data' or (result['value'] or {}).get('text') not in ('P2', 'Paperang_P2'):
            raise RuntimeError('No confirmed USB P2 identity at the two known CRC seeds')
        return result

    def query(self, name, timeout=1.5):
        if name not in QUERIES:
            raise ValueError('Query is not in the established GET allowlist')
        with self.lock:
            self.drain()
            cmd, responses = QUERIES[name]
            started = time.monotonic()
            at = timestamp()
            self.io(frame(cmd, b'\x01', seed=getattr(self, 'seed', SEED)))
            ack, value = None, None
            response_code = None
            deadline = started + timeout
            while time.monotonic() < deadline:
                try:
                    frames = self.receive(timeout=max(1, min(150, int((deadline-time.monotonic())*1000))))
                except TimeoutError:
                    if value is not None:
                        break
                    continue
                for code, _, payload in frames:
                    if code == cmd and len(payload) == 1:
                        ack = payload[0]
                    elif code in responses:
                        value, response_code = payload, code
                if ack is not None and ack != 0:
                    break
                if value is not None and ack == 0:
                    break
            state = ('negative_ack' if ack not in (None, 0) else
                     ('empty_data' if value == b'' else 'data') if value is not None else
                     'ack_only' if ack == 0 else 'no_response')
            return {'name': name, 'at': at, 'command': f'{cmd:02x}',
                    'response_command': None if response_code is None else f'{response_code:02x}',
                    'state': state, 'ack': ack, 'elapsed_ms': round((time.monotonic()-started)*1000),
                    'raw': None if value is None else '<redacted>' if name in ('mac', 'serial') else value.hex(),
                    'value': None if value is None or state == 'negative_ack' else interpret(name, value)}

    def command(self, cmd, payload=b'', response=None, remaining=0, needs_reply=True):
        # Physical writes are deliberately constrained; there is no raw-command CLI.
        if cmd not in (0, 25, 26):
            raise ValueError('Write command not enabled in the USB research tool')
        if cmd == 0 and (not payload or len(payload) > 1008 or len(payload) % 72):
            raise ValueError('Raster must contain complete 72-byte rows, at most 1008 bytes')
        if cmd == 25 and (len(payload) != 1 or payload[0] > 100):
            raise ValueError('Density must be one byte in 0..100')
        if cmd == 26 and (len(payload) != 2 or int.from_bytes(payload, 'little') > 300):
            raise ValueError('Research feed must be u16LE, at most 300 lines')
        with self.lock:
            return self._command(cmd, payload, response, remaining, needs_reply)

    def _command(self, cmd, payload, response, remaining, needs_reply):
        self.drain()
        packet = frame(cmd, payload, remaining, seed=getattr(self, 'seed', SEED))
        if self.trace:
            print('TX', packet.hex() if len(packet) <= 80 else f'cmd={cmd:02x} bytes={len(packet)} crc={packet[-5:-1].hex()}', flush=True)
        self.io(packet)
        if not needs_reply:
            return None
        deadline = time.monotonic() + 4
        while time.monotonic() < deadline:
            try:
                frames = self.receive(timeout=1000)
            except TimeoutError:
                continue
            matched = None
            for command, _, data in frames:
                if command == cmd and len(data) == 1 and data[0] != 0:
                    raise RuntimeError(f'Negative ACK {data.hex()} for {cmd:02x}')
                if command == (cmd if response is None else response) and (response is not None or data == b'\x00'):
                    matched = data
            if matched is not None:
                return matched
        raise TimeoutError(f'No validated response for {cmd:02x}')

    def close(self):
        self.k.CloseHandle(self.handle)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--print-test', action='store_true')
    parser.add_argument('--all', action='store_true', help='All 17 established GETs, including redacted identifiers')
    parser.add_argument('--class-info', action='store_true', help='Read USB printer-class port status and IEEE1284 identity')
    parser.add_argument('--query', choices=QUERIES)
    parser.add_argument('--watch', action='store_true', help='Poll status/battery for a bounded duration')
    parser.add_argument('--duration', type=float, default=10)
    parser.add_argument('--interval', type=float, default=.5)
    parser.add_argument('--rows', type=int, default=14, help='Test pattern rows: 14..140 in multiples of 14')
    parser.add_argument('--trace', action='store_true', help='Redacted parsed RX frames')
    parser.add_argument('--output', type=Path, help='New local JSON evidence file; never overwrite')
    parser.add_argument('--label', default='usb-sample')
    args = parser.parse_args()
    if not 0 < args.duration <= 120 or not .1 <= args.interval <= 10:
        parser.error('duration must be (0,120], interval [0.1,10] seconds')
    if args.rows not in range(14, 141, 14):
        parser.error('rows must be 14..140 in multiples of 14')
    report = {'schema': 1, 'at': timestamp(), 'label': args.label, 'transport': 'windows-usbprint',
              'samples': [], 'events': [], 'print_outcome': 'not_requested', 'paper_confirmed': None}
    if args.print_test and args.output is None:
        args.output = Path(__file__).resolve().parents[2] / 'work/usb' / f'attempt-{uuid.uuid4()}.json'
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        # Reserve before any physical write; never reuse an existing attempt.
        with args.output.open('x', encoding='utf-8') as handle:
            json.dump(report, handle, indent=2)
    link = None
    try:
        link = UsbPrint(trace=args.trace)
        report['identity'] = link.identify()
        report['crc_seed'] = f'{link.seed:08x}'
        if args.class_info:
            report['usb_class'] = link.class_info()
            print(json.dumps({'usb_class': report['usb_class']}), flush=True)
        values = {}
        names = list(QUERIES) if args.all else [args.query] if args.query else ['model', 'version', 'status', 'battery', 'heat']

        def sample(name):
            value = link.query(name)
            report['samples'].append(value)
            values[name] = value
            print(json.dumps(value, ensure_ascii=True), flush=True)
            return value

        for name in names:
            sample(name)
        if args.print_test:
            model = sample('model')
            if model['state'] != 'data' or model['value'].get('text') not in ('P2', 'Paperang_P2'):
                raise RuntimeError('P2 model not confirmed')

            def media_ready():
                status = sample('status')
                data = status['value'] or {}
                if status['state'] != 'data' or data.get('cover_open') is not False or data.get('paper_present') is not True:
                    raise RuntimeError('Fresh paper/cover status not ready')

            media_ready()
            # This USB firmware reports and accepts a one-byte density.
            link.command(25, b'\x64')
            if (sample('heat')['value'] or {}).get('density') != 100:
                raise RuntimeError('Density 100 not confirmed; no bitmap sent')
            # Distinct 14-row bands allow visual verification of countdown order.
            packets = []
            for band in range(args.rows // 14):
                packets.append(bytes(0xff if column in (0, 71) or column == 4 + band * 5 else 0
                                     for _ in range(14) for column in range(72)))
            report['raster_sha256'] = hashlib.sha256(b''.join(packets)).hexdigest()
            report['rows'] = args.rows
            report['print_outcome'] = 'unknown'
            # Persist intent and geometry before the first raster write.
            if args.output:
                args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
            for i, packet in enumerate(packets):
                media_ready()
                link.command(0, packet, remaining=len(packets)-i-1, needs_reply=False)
                sample('status')
            link.command(26, struct.pack('<H', 150))
            report['print_outcome'] = 'sent'
            print('Pattern sent once. Paper output requires human confirmation.', flush=True)
            sample('status')
        if args.watch:
            deadline = time.monotonic() + args.duration
            while time.monotonic() < deadline:
                sample('status')
                sample('battery')
                time.sleep(min(args.interval, max(0, deadline-time.monotonic())))
    except Exception as error:
        report['error'] = str(error)
        raise
    finally:
        if link is not None:
            report['events'] = link.events
            report['writes'] = link.writes
            link.close()
        report['finished_at'] = timestamp()
        if args.output:
            args.output.write_text(json.dumps(report, indent=2), encoding='utf-8')
            print('Evidence:', args.output.resolve(), flush=True)


if __name__ == '__main__':
    main()
