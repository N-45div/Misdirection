"""Runs the engine-free game logic tests (tools/tests/csharp/CoreTests.cs) under Mono.

Skipped when mcs/mono are not installed. CI installs mono-devel.
"""

import glob
import os
import shutil
import subprocess
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
CORE = os.path.join(REPO, "Assets", "Misdirection", "Scripts", "Core")


@unittest.skipUnless(shutil.which("mcs") and shutil.which("mono"), "mcs/mono not installed")
class CoreLogic(unittest.TestCase):
    def test_core_logic(self):
        sources = sorted(glob.glob(os.path.join(CORE, "**", "*.cs"), recursive=True))
        self.assertTrue(sources, "no core sources found")
        with tempfile.TemporaryDirectory() as tmp:
            exe = os.path.join(tmp, "coretests.exe")
            subprocess.run(["mcs", "-nologo", "-out:" + exe, "-r:System.Numerics.Vectors", "-r:System.Numerics",
                            *sources, os.path.join(HERE, "csharp", "MiniTest.cs"), os.path.join(HERE, "csharp", "CoreTests.cs")],
                           check=True)
            run = subprocess.run(["mono", exe], capture_output=True, text=True)
        failures = [line for line in run.stdout.splitlines() if line.startswith("FAIL")]
        self.assertEqual(run.returncode, 0, "\n".join(failures) or run.stdout + run.stderr)
        self.assertIn("RESULT", run.stdout)


if __name__ == "__main__":
    unittest.main()
