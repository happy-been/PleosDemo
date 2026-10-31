import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("probe_validation", Path(__file__).resolve().parents[1] / "validate-unity-input-probe.py")
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)


class ProbeLogTests(unittest.TestCase):
    def test_effective_readiness_is_separate_from_native_allowed(self):
        line = 'PLEOS_PROBE {"allowed":true,"throttle":0.37} fps=30 inputReady=False running=False'
        sample = probe.parse_probe_line(line)
        self.assertTrue(sample["snapshot"]["allowed"])
        self.assertFalse(sample["inputReady"])

    def test_valid_ready_metadata_is_read(self):
        sample = probe.parse_probe_line('PLEOS_PROBE {"allowed":true} fps=30 inputReady=True')
        self.assertTrue(sample["inputReady"])

    def test_json_text_cannot_spoof_readiness(self):
        payload = json.dumps({"blockReason": "inputReady=True", "allowed": False})
        sample = probe.parse_probe_line("PLEOS_PROBE " + payload + " inputReady=False")
        self.assertFalse(sample["inputReady"])

    def test_old_or_ambiguous_metadata_is_not_accepted(self):
        for tail in ("", " inputReady=unknown", " inputReady=True inputReady=False"):
            self.assertIsNone(probe.parse_probe_line('PLEOS_PROBE {"allowed":true}' + tail)["inputReady"])

    def test_unrelated_or_malformed_logs_are_ignored(self):
        for line in ("Other Unity output", "PLEOS_PROBE {not json}"):
            self.assertIsNone(probe.parse_probe_line(line))


class AdbResolutionTests(unittest.TestCase):
    def test_local_sdk_works_without_machine_environment(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sdk = root / "Android SDK"
            adb = sdk / "platform-tools/adb.exe"
            adb.parent.mkdir(parents=True)
            adb.touch()
            escaped = str(sdk).replace("\\", "\\\\").replace(":", "\\:")
            (root / "local.properties").write_text("sdk.dir=" + escaped, encoding="utf-8")
            with patch.object(probe, "REPOSITORY", root), patch.dict(os.environ, {}, clear=True), patch.object(probe.shutil, "which", return_value=None):
                self.assertEqual(Path(probe.resolve_adb_path()), adb)

    def test_bad_explicit_adb_does_not_silently_use_another_sdk(self):
        with patch.object(probe.shutil, "which", return_value=None):
            with self.assertRaises(FileNotFoundError):
                probe.resolve_adb_path("missing-parkside-adb.exe")


if __name__ == "__main__":
    unittest.main()
