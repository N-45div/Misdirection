import contextlib
import io
import os
import re
import tempfile
import unittest

from tools import analyze_ditch_trials as ditch

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PRACTICE = os.path.join(REPO, "Assets", "Misdirection", "Scripts", "Runtime", "Tricks", "DitchPractice.cs")


def write_log(folder, outcomes, headset="Meta_Quest_3", name="ditch.csv"):
    os.makedirs(folder, exist_ok=True)
    path = os.path.join(folder, name)
    lines = [f"# {ditch.FORMAT_TAG}", f"# headset={headset}", ",".join(ditch.COLUMNS)]
    t = 0.0
    for i, outcome in enumerate(outcomes, 1):
        dist = {"ditched": "0.000", "exposed": "0.250", "near": "0.040", "abandoned": ""}[outcome]
        real = "exposed" if outcome == "near" else outcome
        accidental = "1" if outcome == "exposed" else "0"
        lines.append(f"{i},R,{t:.3f},{t + 2:.3f},2.000,{real},{dist},0.000,0.100,{accidental}")
        t += 5
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    return path


class DitchAnalysis(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()

    def tearDown(self):
        self.tmp.cleanup()

    def test_columns_match_the_game(self):
        with open(PRACTICE, encoding="utf-8") as handle:
            source = handle.read()
        block = re.search(r"CsvHeader\s*=\s*(.*?);", source, re.S).group(1)
        self.assertEqual("".join(re.findall(r'"([^"]*)"', block)).split(","), ditch.COLUMNS)
        self.assertIn('"' + ditch.FORMAT_TAG + '"', source)

    def test_last_twenty_ignores_abandoned_and_old_failures(self):
        path = write_log(os.path.join(self.tmp.name, "alice"), ["exposed"] * 5 + ["ditched"] * 19 + ["abandoned", "near"])
        (report,) = ditch.analyse([path])
        self.assertEqual(report.tester, "alice")
        self.assertEqual(report.recent(), (19, 20))
        self.assertTrue(report.meets_exit)

    def test_exit_needs_two_testers(self):
        write_log(os.path.join(self.tmp.name, "alice"), ["ditched"] * 20)
        write_log(os.path.join(self.tmp.name, "bob"), ["ditched"] * 17 + ["exposed"] * 3)
        reports = ditch.analyse(ditch.expand([self.tmp.name]))
        self.assertFalse(ditch.to_json(reports)["exit_met"])
        write_log(os.path.join(self.tmp.name, "bob"), ["ditched"] * 20, name="second.csv")
        reports = ditch.analyse(ditch.expand([self.tmp.name]))
        self.assertTrue(ditch.to_json(reports)["exit_met"])
        self.assertIn("MET", ditch.render_markdown(reports))

    def test_too_few_attempts_is_pending_and_hints_explain_failures(self):
        write_log(os.path.join(self.tmp.name, "cara"), ["ditched"] * 8 + ["exposed", "near"])
        reports = ditch.analyse(ditch.expand([self.tmp.name]))
        text = ditch.render_markdown(reports)
        self.assertIn("⏳ 8/10", text)
        self.assertIn("let go by mistake", text)
        self.assertIn("just outside the servante", text)

    def test_spike_logs_in_the_same_folder_are_ignored(self):
        folder = os.path.join(self.tmp.name, "dan")
        write_log(folder, ["ditched"] * 3)
        with open(os.path.join(folder, "spike.csv"), "w") as handle:
            handle.write("# misdirection_tracking_spike v1\nt,frame\n0,1\n")
        (report,) = ditch.analyse(ditch.expand([self.tmp.name]))
        self.assertEqual(len(report.trials), 3)

    def test_cli(self):
        write_log(os.path.join(self.tmp.name, "erin"), ["ditched"] * 20)
        md = os.path.join(self.tmp.name, "out.md")
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(ditch.main([self.tmp.name, "--markdown", md]), 0)
            self.assertEqual(ditch.main([os.path.join(self.tmp.name, "nothing-here")]), 2)
        with open(md, encoding="utf-8") as handle:
            self.assertIn("erin", handle.read())


if __name__ == "__main__":
    unittest.main()
