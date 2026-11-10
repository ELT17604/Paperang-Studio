"""Offline tests; never open a device or consume paper."""
import importlib.util
from pathlib import Path
import threading
import unittest

spec = importlib.util.spec_from_file_location('usb_probe', Path(__file__).resolve().parents[2] / 'Windows/scripts/usb_probe.py')
usb = importlib.util.module_from_spec(spec)
spec.loader.exec_module(usb)


class UsbProtocolTests(unittest.TestCase):
    def test_captured_device_vectors(self):
        self.assertEqual(usb.frame(6, b'\x01').hex(), '020600010001d0b959e903')
        self.assertEqual(usb.frame(26, b'\x96\x00').hex(), '021a00020096003d78386a03')
        raw = bytes.fromhex('0207000b005061706572616e675f503242e4904603')
        self.assertEqual(usb.Decoder().push(raw), [(7, 0, b'Paperang_P2')])

    def test_fragmentation_and_coalescing(self):
        raw = usb.frame(7, b'Paperang_P2') + usb.frame(6, b'\x00')
        for split in range(len(raw) + 1):
            decoder = usb.Decoder()
            self.assertEqual(decoder.push(raw[:split]) + decoder.push(raw[split:]),
                             [(7, 0, b'Paperang_P2'), (6, 0, b'\x00')])

    def test_corrupt_frame_not_accepted(self):
        raw = bytearray(usb.frame(7, b'P2'))
        raw[-3] ^= 1
        self.assertEqual(usb.Decoder().push(raw + usb.frame(17, b'\x64')), [(17, 0, b'\x64')])

    def test_bounds(self):
        with self.assertRaises(ValueError):
            usb.frame(0, bytes(1024))
        with self.assertRaises(ValueError):
            usb.frame(0, remaining=256)

    def test_real_corrupt_partial_header_recovers(self):
        # Captured during the physical cover transition, then a valid status.
        raw = bytes.fromhex('020d0002020d0002001100b176fa1e03')
        decoder = usb.Decoder()
        self.assertEqual(decoder.push(raw), [(13, 0, b'\x11\x00')])

    def test_media_differential(self):
        for raw, opened, paper in [(0, False, True), (1, False, False),
                                   (0x10, True, None), (0x11, True, None), (0x19, True, None)]:
            value = usb.interpret('status', raw.to_bytes(2, 'little'))
            self.assertEqual(value['cover_open'], opened)
            self.assertEqual(value['paper_present'], paper)
            self.assertIsNone(value['printing'])
            self.assertIsNone(value['mechanically_complete'])
            self.assertIsNone(value['charging'])
        self.assertEqual(usb.interpret('status', b'\x08\x00')['unknown_bits'], 8)
        self.assertNotIn('cover_open', usb.interpret('status', b'\x00'))

    def test_identifiers_and_unknown_values(self):
        self.assertEqual(usb.interpret('mac', b'123456abcdef'), {'redacted': True, 'length': 12})
        self.assertIsNone(usb.interpret('battery', b'\xff')['percent'])
        self.assertEqual(usb.interpret('voltage', b'\x00\x00'), {'meaning': 'unconfirmed'})

    def test_no_firmware_or_unknown_writes(self):
        link = usb.UsbPrint.__new__(usb.UsbPrint)
        for cmd in (1, 2, 3, 0x14, 0x18, 0x2c, 0x40, 0xff):
            with self.assertRaises(ValueError):
                link.command(cmd)
        for cmd, payload in [(0, bytes(73)), (0, bytes(1023)), (25, b'\x65'),
                             (25, b'\x64\x00'), (26, b'\xff\xff')]:
            with self.assertRaises(ValueError):
                link.command(cmd, payload)


class FakeUsb(usb.UsbPrint):
    def __init__(self, chunks):
        self.chunks = list(chunks)
        self.sent = []
        self.events = []
        self.decoder = usb.Decoder()
        self.trace = False
        self.lock = threading.RLock()
        self.drained = 0

    def drain(self):
        self.drained += 1

    def io(self, data=None, timeout=2000):
        if data is not None:
            self.sent.append(data)
            return data
        if not self.chunks:
            raise TimeoutError()
        return self.chunks.pop(0)


class UsbQueryTests(unittest.TestCase):
    def test_ack_then_fragmented_data(self):
        data = usb.frame(0x11, b'\x64')
        link = FakeUsb([usb.frame(0x10, b'\x00'), data[:4], data[4:]])
        result = link.query('battery', timeout=.01)
        self.assertEqual((result['state'], result['ack'], result['value']), ('data', 0, {'percent': 100}))
        self.assertEqual(link.sent, [usb.frame(0x10, b'\x01')])
        self.assertEqual(link.drained, 1)

    def test_data_then_ack_same_read(self):
        link = FakeUsb([usb.frame(0x11, b'\x64') + usb.frame(0x10, b'\x00')])
        self.assertEqual(link.query('battery')['ack'], 0)

    def test_negative_ack_wins_over_coalesced_data(self):
        link = FakeUsb([usb.frame(0x11, b'\x64') + usb.frame(0x10, b'\x01')])
        result = link.query('battery')
        self.assertEqual(result['state'], 'negative_ack')
        self.assertIsNone(result['value'])

    def test_ack_only_not_a_zero_measurement(self):
        link = FakeUsb([usb.frame(0x0e, b'\x00')])
        result = link.query('voltage', timeout=.005)
        self.assertEqual(result['state'], 'ack_only')
        self.assertIsNone(result['value'])
        self.assertIsNone(result['raw'])

    def test_empty_data_not_ack_only(self):
        link = FakeUsb([usb.frame(0x2e) + usb.frame(0x2d, b'\x00')])
        result = link.query('country')
        self.assertEqual(result['state'], 'empty_data')
        self.assertEqual(result['raw'], '')

    def test_no_response_and_bad_crc(self):
        data = bytearray(usb.frame(0x11, b'\x64'))
        data[-2] ^= 1
        result = FakeUsb([data]).query('battery', timeout=.005)
        self.assertEqual(result['state'], 'no_response')

    def test_mac_exception_and_redaction(self):
        link = FakeUsb([usb.frame(0x0b, b'123456abcdef')])
        result = link.query('mac', timeout=.005)
        self.assertEqual(result['response_command'], '0b')
        self.assertEqual(result['raw'], '<redacted>')
        self.assertEqual(link.events[0]['payload'], '<redacted>')

    def test_unrelated_frame_is_not_a_measurement(self):
        link = FakeUsb([usb.frame(0x0d, b'\x00\x00')])
        self.assertEqual(link.query('battery', timeout=.005)['state'], 'no_response')

    def test_query_allowlist(self):
        self.assertEqual(len(usb.QUERIES), 17)
        with self.assertRaises(ValueError):
            FakeUsb([]).query('firmware_update')

    def test_persisted_zero_seed_round_trip(self):
        link = FakeUsb([usb.frame(0x11, b'\x64', seed=0)])
        link.seed = 0
        link.decoder = usb.Decoder(0)
        self.assertEqual(link.query('battery', timeout=.005)['value'], {'percent': 100})
        self.assertEqual(link.sent, [usb.frame(0x10, b'\x01', seed=0)])

    def test_identity_seed_fallback_is_read_only(self):
        link = FakeUsb([])
        link.seed = usb.SEED
        seen = []
        def query(name):
            seen.append((name, link.seed))
            return {'state': 'no_response', 'value': None} if link.seed else {'state': 'data', 'value': {'text': 'Paperang_P2'}}
        link.query = query
        self.assertEqual(link.identify()['value']['text'], 'Paperang_P2')
        self.assertEqual(seen, [('model', usb.SEED), ('model', 0)])
        self.assertEqual(link.sent, [])

    def test_foreign_model_never_triggers_seed_retry(self):
        link = FakeUsb([])
        link.seed = usb.SEED
        link.query = lambda name: {'state': 'data', 'value': {'text': 'Paperang_P1'}}
        with self.assertRaises(RuntimeError):
            link.identify()
        self.assertEqual(link.seed, usb.SEED)


if __name__ == '__main__':
    unittest.main()
