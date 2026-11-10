"""Release packaging checks; no GitHub calls, tags or publication."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('publisher', Path(__file__).resolve().parents[2]/'scripts/publish-release.py')
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)


class ReleaseAssets(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        self.input = self.root/'input'
        self.input.mkdir()
        self.names = ['PaperangStudio-0.3.2-winlite-win-x64-setup.exe',
                      'V0.3.2-winlite-win-x64-portable.zip', 'V0.3.2-winlite-win-x64-lite.zip',
                      'PaperangStudio-0.3.2-macclient-macos-arm64.zip']
        for name in self.names:
            (self.input/name).write_bytes(b'isolated package fixture')
        self.sums()

    def sums(self):
        (self.input/'SHA256SUMS').write_text(''.join(hashlib.sha256((self.input/n).read_bytes()).hexdigest()+'  '+n+'\n' for n in self.names))

    def tearDown(self):
        self.tmp.cleanup()

    def test_checked_complete_packages(self):
        publisher.prepare(self.input, self.root/'out')
        self.assertEqual({p.name for p in (self.root/'out').iterdir()}, set(self.names))

    def test_corrupt_or_incomplete_packages_block_publication(self):
        (self.input/self.names[0]).write_bytes(b'corrupt')
        with self.assertRaisesRegex(ValueError, 'mismatch'):
            publisher.prepare(self.input, self.root/'bad')
        self.names.pop()
        self.sums()
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            publisher.prepare(self.input, self.root/'missing')
