import contextlib
import io
import json
import os
import re
import tempfile
import unittest

from tools import analyze_tracking_spike as spike
from tools.tests import synthetic_spike as synth

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CSHARP_DIR = os.path.join(REPO, "Assets", "Misdirection", "Scripts", "Runtime", "Spike")


def read(path):
    with open(path, encoding="utf-8") as handle:
        return handle.read()


def feed(scorer, seconds=6.0, drops=(), rate=72.0):
    n = int(round(seconds * rate))
    for i in range(n):
        t = i / rate
        low = any(s <= t < s + d for s, d in drops)
        scorer.add(t, True, not low)
    return scorer.finish()


class ContractWithCSharp(unittest.TestCase):
    def test_csv_header_matches_csharp_writer(self):
        source = read(os.path.join(CSHARP_DIR, "SpikeCsvLog.cs"))
        block = re.search(r"Header\s*=\s*(.*?);", source, re.S).group(1)
        header = "".join(re.findall(r'"([^"]*)"', block))
        self.assertEqual(header.split(","), spike.COLUMNS)

    def test_scoring_defaults_match_csharp_controller(self):
        source = read(os.path.join(CSHARP_DIR, "TrackingSpikeController.cs"))
        def value(name):
            return float(re.search(name + r"\s*=\s*([0-9.]+)f", source).group(1))
        self.assertAlmostEqual(value("moveWindowSeconds"), spike.DEFAULT_WINDOW_S)
        self.assertAlmostEqual(value("dropoutSeconds"), spike.DEFAULT_DROPOUT_S)
        self.assertAlmostEqual(value("maxWindowFailRate"), spike.DEFAULT_MAX_FAIL_RATE)

    def test_format_tag_matches(self):
        source = read(os.path.join(CSHARP_DIR, "SpikeCsvLog.cs"))
        self.assertIn('"' + spike.FORMAT_TAG + '"', source)


class Scorer(unittest.TestCase):
    def test_clean_record_has_four_windows(self):
        s = feed(spike.MoveWindowScorer())
        self.assertEqual((s.windows, s.failed_windows), (4, 0))
        self.assertEqual(s.high_frames, s.frames)

    def test_short_blip_is_tolerated(self):
        s = feed(spike.MoveWindowScorer(), drops=[(2.0, 0.10)])
        self.assertEqual(s.failed_windows, 0)

    def test_long_dropout_fails_one_window(self):
        s = feed(spike.MoveWindowScorer(), drops=[(2.0, 0.25)])
        self.assertEqual((s.windows, s.failed_windows), (4, 1))
        self.assertGreater(s.longest_low_run, 0.2)

    def test_dropout_spanning_boundary_fails_both_windows(self):
        s = feed(spike.MoveWindowScorer(), drops=[(1.3, 0.6)])
        self.assertEqual(s.failed_windows, 2)

    def test_untracked_counts_as_low(self):
        s = spike.MoveWindowScorer()
        for i in range(int(6 * 72)):
            s.add(i / 72.0, tracked=False, high_confidence=True)
        s.finish()
        self.assertEqual(s.failed_windows, s.windows)
        self.assertEqual(s.tracked_frames, 0)


class EndToEnd(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.tmp.cleanup()

    def path(self, name):
        return os.path.join(self.tmp.name, name)

    def run_reports(self, *paths):
        files = [spike.read_spike_csv(p) for p in paths]
        return spike.analyse(files)

    def test_clean_data_gives_full_arc(self):
        report = self.run_reports(synth.write(self.path("a.csv")))["Meta_Quest_3"]
        self.assertEqual(report.angles, [0, 20, 35, 50])
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "away", 0.1), 50)
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "toward", 0.1), 50)
        self.assertEqual(report.warnings, [])

    def test_left_hand_lost_when_turning_far_away(self):
        def profile(secret, turn, angle, repeat):
            if secret == "L" and turn == "away" and angle >= 35:
                return [(0.2, 0.4), (1.7, 0.4), (3.2, 0.4), (4.7, 0.4)]
            return []
        report = self.run_reports(synth.write(self.path("b.csv"), profile))["Meta_Quest_3"]
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "away", 0.1), 20)
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "toward", 0.1), 50)
        cell = report.cells[("L", "away", 50)]
        self.assertEqual((cell.windows, cell.failed_windows), (8, 8))

    def test_one_lost_move_in_eight_fails_the_ten_percent_rule(self):
        def profile(secret, turn, angle, repeat):
            return [(0.5, 0.3)] if (secret == "R" and turn == "away" and angle == 35 and repeat == 0) else []
        report = self.run_reports(synth.write(self.path("c.csv"), profile))["Meta_Quest_3"]
        self.assertAlmostEqual(report.cells[("R", "away", 35)].fail_rate, 1 / 8)
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "away", 0.1), 20)
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "away", 0.15), 50)

    def test_centre_failure_reports_no_arc(self):
        def profile(secret, turn, angle, repeat):
            return [(0.0, 6.0)] if turn == "center" else []
        report = self.run_reports(synth.write(self.path("d.csv"), profile))["Meta_Quest_3"]
        self.assertEqual(spike.usable_angle(report.cells, report.angles, "away", 0.1), -1)
        self.assertIn("FAIL", spike.verdict(-1))

    def test_headsets_are_reported_separately(self):
        a = synth.write(self.path("q3.csv"), headset="Meta_Quest_3")
        b = synth.write(self.path("q3s.csv"), headset="Meta_Quest_3S",
                        profile=lambda s, t, a, r: [(0.0, 6.0)] if t == "away" and a == 50 else [])
        reports = self.run_reports(a, b)
        self.assertEqual(spike.usable_angle(reports["Meta_Quest_3"].cells, [0, 20, 35, 50], "away", 0.1), 50)
        self.assertEqual(spike.usable_angle(reports["Meta_Quest_3S"].cells, [0, 20, 35, 50], "away", 0.1), 35)

    def test_warnings_for_static_positions_and_lazy_head_turns(self):
        report = self.run_reports(synth.write(self.path("e.csv"), static_position=True, yaw_offset=15.0))["Meta_Quest_3"]
        text = " ".join(report.warnings)
        self.assertIn("never moves", text)
        self.assertIn("off the target", text)

    def test_metadata_and_bad_header(self):
        p = synth.write(self.path("f.csv"))
        parsed = spike.read_spike_csv(p)
        self.assertEqual(parsed.meta["headset"], "Meta_Quest_3")
        self.assertEqual(parsed.meta["format"], spike.FORMAT_TAG)
        bad = self.path("bad.csv")
        with open(bad, "w") as handle:
            handle.write("t,frame\n0,1\n")
        with self.assertRaises(spike.SpikeFormatError):
            spike.read_spike_csv(bad)

    def test_cli_writes_markdown_and_json(self):
        synth.write(self.path("g.csv"))
        md, js = self.path("report.md"), self.path("report.json")
        with contextlib.redirect_stdout(io.StringIO()):
            code = spike.main([self.tmp.name, "--markdown", md, "--json", js])
        self.assertEqual(code, 0)
        with open(md, encoding="utf-8") as handle:
            self.assertIn("Widest usable crowd arc", handle.read())
        with open(js, encoding="utf-8") as handle:
            data = json.load(handle)
        self.assertEqual(data["headsets"]["Meta_Quest_3"]["arc_away_deg"], 50)
        self.assertGreater(data["headsets"]["Meta_Quest_3"]["frame_stats"]["median_fps"], 70)


if __name__ == "__main__":
    unittest.main()
