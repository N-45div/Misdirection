"""Runs the engine-free game logic tests (tools/tests/csharp/*Tests.cs).

Uses Mono when installed (CI installs mono-devel), or on Windows the compiler inside the Unity
editor (see csharp_toolchain.py). Skipped when neither is available.
"""

import glob
import os
import tempfile
import unittest

from tools.tests import csharp_toolchain as cs

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
CORE = os.path.join(REPO, "Assets", "Misdirection", "Scripts", "Core")


@unittest.skipUnless(cs.available(), "no C# toolchain (Mono, or a Unity editor on Windows)")
class CoreLogic(unittest.TestCase):
    def test_core_logic(self):
        sources = sorted(glob.glob(os.path.join(CORE, "**", "*.cs"), recursive=True))
        self.assertTrue(sources, "no core sources found")
        with tempfile.TemporaryDirectory() as tmp:
            exe = os.path.join(tmp, "coretests.exe")
            cs.compile_exe([*sources, os.path.join(HERE, "csharp", "MiniTest.cs"),
                            *sorted(glob.glob(os.path.join(HERE, "csharp", "*Tests.cs")))], exe)
            run = cs.run_exe(exe)
        failures = [line for line in run.stdout.splitlines() if line.startswith("FAIL")]
        self.assertEqual(run.returncode, 0, "\n".join(failures) or run.stdout + run.stderr)
        self.assertIn("RESULT", run.stdout)


if __name__ == "__main__":
    unittest.main()
