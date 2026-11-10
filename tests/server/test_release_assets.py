"""Release packaging checks; no GitHub calls, tags or publication."""
import hashlib
import io
import importlib.util
from pathlib import Path
import tempfile
import unittest
import urllib.error
from unittest.mock import patch

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

    def test_release_lookup_requires_explicit_404(self):
        error = urllib.error.HTTPError('https://api.github.com/test', 404, 'Not Found', {}, None)
        with patch.object(publisher.urllib.request, 'urlopen', side_effect=error):
            self.assertIsNone(publisher.existing_release('ELT17604/Paperang-Studio', 'fixture'))
        error = urllib.error.HTTPError('https://api.github.com/test', 403, 'Forbidden', {}, None)
        with patch.object(publisher.urllib.request, 'urlopen', side_effect=error):
            with self.assertRaises(urllib.error.HTTPError):
                publisher.existing_release('ELT17604/Paperang-Studio', 'fixture')

    def test_existing_draft_and_unexpected_response_are_not_absence(self):
        with patch.object(publisher.urllib.request, 'urlopen', return_value=io.BytesIO(b'{"tag_name":"v0.3.2","draft":true,"id":42}')):
            self.assertTrue(publisher.existing_release('ELT17604/Paperang-Studio', 'fixture')['draft'])
        with patch.object(publisher.urllib.request, 'urlopen', return_value=io.BytesIO(b'{}')):
            with self.assertRaises(ValueError):
                publisher.existing_release('ELT17604/Paperang-Studio', 'fixture')
