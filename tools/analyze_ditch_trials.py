#!/usr/bin/env python3
"""Analyse Day 2 ditch-practice logs and check the exit criterion.

Day 2 exit: "Ditch succeeds about 19 times in 20 for two testers". Each tester's last 20
attempts (abandoned ones don't count) must include at least 19 ditches.

Usage:
    python3 tools/analyze_ditch_trials.py spike_logs/            # every ditch CSV under a folder
    python3 tools/analyze_ditch_trials.py a.csv --markdown docs/results/day-02-ditch.md

The tester is the name of the folder the CSV sits in (tools/pull_logs.sh ditch <tester> sets it).
Mirrors DitchTrialRecorder in Assets/Misdirection/Scripts/Core/Tricks/Coin.cs. Standard library only.
"""

from __future__ import annotations

import argparse
import csv
import glob
import json
import os
import statistics
import sys
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Sequence

FORMAT_TAG = "misdirection_ditch_trials v1"
COLUMNS = ["trial", "hand", "grab_t", "end_t", "duration_s", "outcome", "release_dist_m", "lost_s", "uncertain_s",
           "likely_accidental"]
WINDOW = 20
NEEDED = 19
TESTERS_NEEDED = 2


class DitchFormatError(ValueError):
    pass


@dataclass
class Trial:
    number: int
    hand: str
    duration: float
    outcome: str
    release_distance: Optional[float]
    lost: float
    uncertain: float
    accidental: bool


@dataclass
class TesterReport:
    tester: str
    headset: str
    files: List[str] = field(default_factory=list)
    trials: List[Trial] = field(default_factory=list)

    @property
    def counted(self) -> List[Trial]:
        return [t for t in self.trials if t.outcome != "abandoned"]

    def recent(self) -> (int, int):
        window = self.counted[-WINDOW:]
        return sum(1 for t in window if t.outcome == "ditched"), len(window)

    @property
    def meets_exit(self) -> bool:
        successes, attempts = self.recent()
        return attempts >= WINDOW and successes >= NEEDED


def read_ditch_csv(path: str) -> (Dict[str, str], List[Trial]):
    meta: Dict[str, str] = {}
    lines: List[str] = []
    with open(path, newline="", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("#"):
                body = line[1:].strip()
                if "=" in body:
                    key, value = body.split("=", 1)
                    meta[key.strip()] = value.strip()
                elif body:
                    meta.setdefault("format", body)
            elif line.strip():
                lines.append(line)
    if meta.get("format") != FORMAT_TAG:
        raise DitchFormatError(f"{path}: not a ditch-trials log")
    reader = csv.DictReader(lines)
    missing = [c for c in COLUMNS if c not in (reader.fieldnames or [])]
    if missing:
        raise DitchFormatError(f"{path}: missing columns {missing}")
    trials = []
    for row in reader:
        dist = row["release_dist_m"].strip()
        trials.append(Trial(
            number=int(row["trial"]),
            hand=row["hand"],
            duration=float(row["duration_s"]),
            outcome=row["outcome"].strip().lower(),
            release_distance=float(dist) if dist else None,
            lost=float(row["lost_s"]),
            uncertain=float(row["uncertain_s"]),
            accidental=row["likely_accidental"].strip() == "1",
        ))
    return meta, trials


def expand(inputs: Sequence[str]) -> List[str]:
    paths: List[str] = []
    for item in inputs:
        if os.path.isdir(item):
            paths.extend(sorted(glob.glob(os.path.join(item, "**", "*.csv"), recursive=True)))
        else:
            paths.append(item)
    return paths


def analyse(paths: Sequence[str]) -> List[TesterReport]:
    reports: Dict[str, TesterReport] = {}
    for path in paths:
        try:
            meta, trials = read_ditch_csv(path)
        except DitchFormatError:
            continue
        except OSError as err:
            print(f"Skipping {path}: {err.strerror}", file=sys.stderr)
            continue
        tester = os.path.basename(os.path.dirname(os.path.abspath(path))) or "tester"
        headset = meta.get("headset", "unknown")
        key = f"{tester}|{headset}"
        report = reports.setdefault(key, TesterReport(tester=tester, headset=headset))
        report.files.append(path)
        report.trials.extend(trials)
    return list(reports.values())


def render_markdown(reports: Sequence[TesterReport]) -> str:
    out = ["# Ditch practice results", "",
           f"Exit criterion: at least {NEEDED} of the last {WINDOW} attempts ditched, for {TESTERS_NEEDED} testers.", ""]
    if not reports:
        out.append("No ditch-trial logs found.")
        return "\n".join(out)
    out.append("| Tester | Headset | Last 20 | Overall | Exposed | Likely accidental | Median time | Lost while holding |")
    out.append("|---|---|---|---|---|---|---|---|")
    for r in reports:
        s, a = r.recent()
        counted = r.counted
        ditched = sum(1 for t in counted if t.outcome == "ditched")
        exposed = sum(1 for t in counted if t.outcome == "exposed")
        accidental = sum(1 for t in counted if t.accidental)
        median = statistics.median([t.duration for t in counted]) if counted else 0.0
        lost_trials = sum(1 for t in counted if t.lost > 0)
        mark = "✅" if r.meets_exit else ("⏳" if a < WINDOW else "❌")
        out.append(f"| {r.tester} | {r.headset} | {mark} {s}/{a} | {ditched}/{len(counted)} | {exposed} | {accidental} | "
                   f"{median:.1f} s | {lost_trials} trial(s) |")
    met = sum(1 for r in reports if r.meets_exit)
    out.append("")
    verdict = "MET" if met >= TESTERS_NEEDED else "NOT MET YET"
    out.append(f"**Day 2 exit: {verdict}** ({met} of {TESTERS_NEEDED} testers at {NEEDED}/{WINDOW}).")
    hints = []
    for r in reports:
        accidental = [t for t in r.counted if t.accidental]
        if accidental:
            hints.append(f"{r.tester}: {len(accidental)} drop(s) far from the servante, so the grip let go by mistake. "
                         "Check the release margin and openness threshold.")
        near_misses = [t for t in r.counted if t.outcome == "exposed" and not t.accidental]
        if near_misses:
            hints.append(f"{r.tester}: {len(near_misses)} release(s) just outside the servante. Consider widening the zone "
                         "or adding a visual cue for the edge.")
    for hint in hints:
        out.append(f"- {hint}")
    return "\n".join(out)


def to_json(reports: Sequence[TesterReport]) -> dict:
    result = {"window": WINDOW, "needed": NEEDED, "testers": []}
    for r in reports:
        s, a = r.recent()
        result["testers"].append({"tester": r.tester, "headset": r.headset, "files": r.files,
                                  "recent_successes": s, "recent_attempts": a, "meets_exit": r.meets_exit,
                                  "attempts": len(r.counted)})
    result["exit_met"] = sum(1 for r in reports if r.meets_exit) >= TESTERS_NEEDED
    return result


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", nargs="+")
    parser.add_argument("--markdown")
    parser.add_argument("--json")
    args = parser.parse_args(argv)
    reports = analyse(expand(args.inputs))
    text = render_markdown(reports)
    print(text)
    if args.markdown:
        with open(args.markdown, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
    if args.json:
        with open(args.json, "w", encoding="utf-8") as handle:
            json.dump(to_json(reports), handle, indent=2)
    return 0 if reports else 2


if __name__ == "__main__":
    sys.exit(main())
