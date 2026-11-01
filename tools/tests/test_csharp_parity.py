"""Runs the game's real C# scoring (SpikeModel.cs) under Mono and checks it matches the Python analyzer.

Skipped when mcs/mono are not installed (CI installs them).
"""

import os
import shutil
import subprocess
import tempfile
import unittest

from tools import analyze_tracking_spike as spike
from tools.tests import synthetic_spike as synth

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
MODEL = os.path.join(REPO, "Assets", "Misdirection", "Scripts", "Runtime", "Spike", "SpikeModel.cs")
HARNESS = os.path.join(HERE, "csharp", "SpikeModelHarness.cs")


def mixed_profile(secret, turn, angle, repeat):
    drops = []
    if turn == "away" and angle >= 35:
        drops.append((0.4 + 0.1 * repeat, 0.2 if secret == "L" else 0.12))
    if turn == "away" and angle == 50:
        drops += [(2.0, 0.5), (4.4, 0.3)]
    if turn == "toward" and angle == 20 and repeat == 1:
        drops.append((1.45, 0.16))  # straddles a window boundary
    if turn == "center" and secret == "R":
        drops.append((3.0, 0.149))  # just under the dropout threshold
    return drops


@unittest.skipUnless(shutil.which("mcs") and shutil.which("mono"), "mcs/mono not installed")
class CSharpParity(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.exe = os.path.join(cls.tmp.name, "harness.exe")
        subprocess.run(["mcs", "-nologo", "-out:" + cls.exe, HARNESS, MODEL], check=True)

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def run_harness(self, csv_path, max_fail="0.1"):
        out = subprocess.run(["mono", self.exe, csv_path, "1.5", "0.15", max_fail], check=True,
                             capture_output=True, text=True).stdout
        cells, arcs = {}, {}
        for line in out.splitlines():
            parts = line.split()
            if parts[0] == "CELL":
                hand, turn, angle = parts[1].split("|")
                cells[(hand, turn.lower(), int(angle))] = tuple(int(x) for x in parts[2:6])
            elif parts[0] == "ARC":
                arcs[parts[1]] = int(float(parts[2]))
        return cells, arcs

    def test_cells_and_arcs_match_python(self):
        for name, profile in (("clean", synth.no_dropouts), ("mixed", mixed_profile)):
            with self.subTest(profile=name):
                path = synth.write(os.path.join(self.tmp.name, name + ".csv"), profile)
                cs_cells, cs_arcs = self.run_harness(path)
                report = spike.analyse([spike.read_spike_csv(path)])["Meta_Quest_3"]
                py_cells = {k: (c.windows, c.failed_windows, c.frames, c.high_frames) for k, c in report.cells.items()}
                self.assertEqual(cs_cells, py_cells)
                self.assertEqual(cs_arcs["away"], spike.usable_angle(report.cells, report.angles, "away", 0.1))
                self.assertEqual(cs_arcs["toward"], spike.usable_angle(report.cells, report.angles, "toward", 0.1))

    def test_mixed_profile_is_not_trivial(self):
        path = synth.write(os.path.join(self.tmp.name, "check.csv"), mixed_profile)
        cells, arcs = self.run_harness(path)
        self.assertEqual(arcs["away"], 20)
        self.assertGreater(sum(v[1] for v in cells.values()), 0)


if __name__ == "__main__":
    unittest.main()
