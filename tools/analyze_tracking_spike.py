#!/usr/bin/env python3
"""Analyse Day 1 tracking-spike logs and report the widest usable crowd arc (Gate 1).

Usage:
    python3 tools/analyze_tracking_spike.py spike_logs/            # every CSV in a folder
    python3 tools/analyze_tracking_spike.py a.csv b.csv --markdown docs/results/day-01-tracking-spike-data.md

Scoring mirrors MoveWindowScorer in
Assets/Misdirection/Scripts/Runtime/Spike/SpikeModel.cs. Keep the two in sync.

A "move window" is a fixed slice of the record phase (default 1.5 s, roughly one secret
move). A window fails when the secret hand stays below high confidence for at least the
dropout threshold (default 150 ms) inside it. An angle is usable when both hands lose at
most --max-fail-rate of their windows (default 10%, the plan's "one move in ten" pivot rule).

Standard library only, so anyone on the team can run it.
"""

from __future__ import annotations

import argparse
import csv
import glob
import json
import math
import os
import statistics
import sys
from dataclasses import dataclass, field
from typing import Dict, Iterable, List, Optional, Sequence, Tuple

FORMAT_TAG = "misdirection_tracking_spike v1"

COLUMNS = [
    "t", "frame", "dt", "phase", "cond", "repeat", "secret_hand", "turn", "target_deg", "look",
    "head_yaw", "head_pitch", "hand", "valid", "tracked", "high_conf", "pinch", "hand_az",
    "hand_el", "hand_lx", "hand_ly", "hand_lz",
]

DEFAULT_WINDOW_S = 1.5
DEFAULT_DROPOUT_S = 0.15
DEFAULT_MAX_FAIL_RATE = 0.10
FRAME_BUDGET_72HZ = 1.0 / 72.0


class SpikeFormatError(ValueError):
    pass


# --------------------------------------------------------------------------- scoring


class MoveWindowScorer:
    """Port of the C# MoveWindowScorer. Feed samples in time order, then call finish()."""

    def __init__(self, window_s: float = DEFAULT_WINDOW_S, dropout_s: float = DEFAULT_DROPOUT_S):
        self.window_s = max(0.1, window_s)
        self.dropout_s = max(0.0, dropout_s)
        self._start: Optional[float] = None
        self._window_index = -1
        self._window_first = 0.0
        self._window_last = 0.0
        self._window_failed = False
        self._low_run_start = -1.0
        self.frames = 0
        self.tracked_frames = 0
        self.high_frames = 0
        self.windows = 0
        self.failed_windows = 0
        self.longest_low_run = 0.0

    def add(self, time: float, tracked: bool, high_confidence: bool) -> None:
        if self._start is None:
            self._start = time
        index = int(math.floor((time - self._start) / self.window_s + 1e-9))
        if index != self._window_index:
            self._close_window()
            self._window_index = index
            self._window_first = time
            self._window_failed = False
        self._window_last = time

        self.frames += 1
        if tracked:
            self.tracked_frames += 1

        if tracked and high_confidence:
            self.high_frames += 1
            self._low_run_start = -1.0
            return

        if self._low_run_start < 0.0:
            self._low_run_start = time
        run = time - self._low_run_start
        self.longest_low_run = max(self.longest_low_run, run)
        if run >= self.dropout_s - 1e-9:
            self._window_failed = True

    def finish(self) -> "MoveWindowScorer":
        self._close_window()
        self._window_index = -1
        return self

    def _close_window(self) -> None:
        if self._window_index < 0:
            return
        if self._window_last - self._window_first >= 0.5 * self.window_s - 0.05:
            self.windows += 1
            if self._window_failed:
                self.failed_windows += 1
        self._window_index = -1


@dataclass
class Cell:
    frames: int = 0
    tracked_frames: int = 0
    high_frames: int = 0
    windows: int = 0
    failed_windows: int = 0
    longest_low_run: float = 0.0
    yaw_errors: List[float] = field(default_factory=list)

    def add(self, scorer: MoveWindowScorer) -> None:
        self.frames += scorer.frames
        self.tracked_frames += scorer.tracked_frames
        self.high_frames += scorer.high_frames
        self.windows += scorer.windows
        self.failed_windows += scorer.failed_windows
        self.longest_low_run = max(self.longest_low_run, scorer.longest_low_run)

    @property
    def fail_rate(self) -> float:
        return 1.0 if self.windows == 0 else self.failed_windows / self.windows

    @property
    def high_rate(self) -> float:
        return 0.0 if self.frames == 0 else self.high_frames / self.frames

    @property
    def tracked_rate(self) -> float:
        return 0.0 if self.frames == 0 else self.tracked_frames / self.frames


CellKey = Tuple[str, str, int]  # (secret_hand "L"/"R", turn "center"/"away"/"toward", angle)


def usable_angle(cells: Dict[CellKey, Cell], angles: Sequence[int], turn: str, max_fail_rate: float) -> int:
    """Largest angle A where every measured angle up to A passes for both hands. -1 if centre fails."""
    usable = -1
    for angle in sorted(set(angles)):
        kind = "center" if angle == 0 else turn
        for hand in ("L", "R"):
            cell = cells.get((hand, kind, angle))
            if cell is None or cell.windows == 0 or cell.fail_rate > max_fail_rate + 1e-6:
                return usable
        usable = angle
    return usable


# --------------------------------------------------------------------------- input


@dataclass
class SpikeFile:
    path: str
    meta: Dict[str, str]
    rows: List[Dict[str, str]]

    @property
    def headset(self) -> str:
        return self.meta.get("headset", "unknown")


def read_spike_csv(path: str) -> SpikeFile:
    meta: Dict[str, str] = {}
    data_lines: List[str] = []
    with open(path, newline="", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("#"):
                body = line[1:].strip()
                if "=" in body:
                    key, value = body.split("=", 1)
                    meta[key.strip()] = value.strip()
                elif body:
                    meta.setdefault("format", body)
                continue
            if line.strip():
                data_lines.append(line)

    if not data_lines:
        raise SpikeFormatError(f"{path}: no header row")
    reader = csv.DictReader(data_lines)
    missing = [c for c in COLUMNS if c not in (reader.fieldnames or [])]
    if missing:
        raise SpikeFormatError(f"{path}: missing columns {missing}")
    return SpikeFile(path=path, meta=meta, rows=list(reader))


def expand_inputs(inputs: Iterable[str]) -> List[str]:
    paths: List[str] = []
    for item in inputs:
        if os.path.isdir(item):
            paths.extend(sorted(glob.glob(os.path.join(item, "**", "*.csv"), recursive=True)))
        else:
            paths.append(item)
    return paths


# --------------------------------------------------------------------------- analysis


@dataclass
class HeadsetReport:
    headset: str
    files: List[str] = field(default_factory=list)
    cells: Dict[CellKey, Cell] = field(default_factory=dict)
    angles: List[int] = field(default_factory=list)
    frame_times: List[float] = field(default_factory=list)
    look_sources: Dict[str, int] = field(default_factory=dict)
    warnings: List[str] = field(default_factory=list)


def _flag(value: str) -> bool:
    return value.strip() == "1"


def analyse(files: Sequence[SpikeFile], window_s: Optional[float] = None, dropout_s: Optional[float] = None) -> Dict[str, HeadsetReport]:
    reports: Dict[str, HeadsetReport] = {}

    for spike in files:
        report = reports.setdefault(spike.headset, HeadsetReport(headset=spike.headset))
        report.files.append(spike.path)
        w = window_s if window_s is not None else float(spike.meta.get("window_s", DEFAULT_WINDOW_S))
        d = dropout_s if dropout_s is not None else float(spike.meta.get("dropout_s", DEFAULT_DROPOUT_S))

        by_cond: Dict[int, List[Dict[str, str]]] = {}
        seen_frames = set()
        secret_positions: List[Tuple[float, float, float]] = []

        for row in spike.rows:
            frame = row["frame"]
            if frame not in seen_frames:
                seen_frames.add(frame)
                try:
                    report.frame_times.append(float(row["dt"]))
                except ValueError:
                    pass
                look = row["look"] or "unknown"
                report.look_sources[look] = report.look_sources.get(look, 0) + 1

            if row["phase"] != "record" or row["hand"] != row["secret_hand"]:
                continue
            by_cond.setdefault(int(row["cond"]), []).append(row)
            if _flag(row["tracked"]):
                secret_positions.append((float(row["hand_lx"]), float(row["hand_ly"]), float(row["hand_lz"])))

        for cond_rows in by_cond.values():
            cond_rows.sort(key=lambda r: float(r["t"]))
            first = cond_rows[0]
            angle = int(round(abs(float(first["target_deg"]))))
            turn = first["turn"]
            key: CellKey = (first["secret_hand"], turn, angle)
            scorer = MoveWindowScorer(w, d)
            for r in cond_rows:
                scorer.add(float(r["t"]), _flag(r["tracked"]), _flag(r["high_conf"]))
            cell = report.cells.setdefault(key, Cell())
            cell.add(scorer.finish())
            target = float(first["target_deg"])
            cell.yaw_errors.extend(abs(float(r["head_yaw"]) - target) for r in cond_rows)
            if angle not in report.angles:
                report.angles.append(angle)

        if len(secret_positions) > 10:
            spread = max(statistics.pstdev(axis) for axis in zip(*secret_positions))
            if spread < 1e-4:
                report.warnings.append(
                    f"{os.path.basename(spike.path)}: secret-hand position never moves. The rig's hand anchors may not "
                    "follow tracked hands; confidence data is still valid, position columns are not.")

    for report in reports.values():
        report.angles.sort()
        for (hand, turn, angle), cell in sorted(report.cells.items()):
            if cell.yaw_errors and angle > 0:
                err = statistics.mean(cell.yaw_errors)
                if err > 10.0:
                    report.warnings.append(
                        f"{hand} {turn} {angle}°: head was on average {err:.0f}° off the target. Testers may not have "
                        "turned fully, so this angle may look better than it is.")
    return reports


def frame_stats(frame_times: Sequence[float]) -> Optional[Dict[str, float]]:
    times = [t for t in frame_times if t > 0]
    if not times:
        return None
    ordered = sorted(times)
    p95 = ordered[min(len(ordered) - 1, int(0.95 * len(ordered)))]
    median = statistics.median(ordered)
    slow = sum(1 for t in ordered if t > FRAME_BUDGET_72HZ * 1.5) / len(ordered)
    return {"median_fps": 1.0 / median, "p95_ms": p95 * 1000.0, "slow_frame_pct": slow * 100.0, "frames": len(ordered)}


def verdict(arc_away: int) -> str:
    if arc_away < 0:
        return ("FAIL: the secret hand is unreliable even with the head centred. Check lighting, hand height and "
                "the hands-only setting, then re-run before drawing conclusions.")
    if arc_away == 0:
        return ("AT RISK: the secret hand only holds with the head centred. Under the pivot rule (losing more than "
                "about one move in ten) the team should prepare to switch to Kolam Dawn unless a narrower layout fixes it.")
    if arc_away < 35:
        return (f"PASS (narrow): keep spectators within ±{arc_away}°. Use the shallow-arc crowd layout and keep secret "
                "moves low and centred.")
    return f"PASS: spectators can stand anywhere within ±{arc_away}°."


# --------------------------------------------------------------------------- output


def _pct(value: float) -> str:
    return f"{value * 100:.0f}%"


def render_markdown(reports: Dict[str, HeadsetReport], max_fail_rate: float) -> str:
    out: List[str] = ["# Tracking spike results", ""]
    out.append(f"Usable means both hands lose at most {max_fail_rate * 100:.0f}% of move windows at that angle.")
    out.append("")
    for name, report in sorted(reports.items()):
        arc_away = usable_angle(report.cells, report.angles, "away", max_fail_rate)
        arc_toward = usable_angle(report.cells, report.angles, "toward", max_fail_rate)
        out.append(f"## {name}")
        out.append("")
        out.append(f"Files: {len(report.files)}  ·  look source frames: " +
                   ", ".join(f"{k} {v}" for k, v in sorted(report.look_sources.items())))
        out.append("")
        header = "| Secret hand | Head turn | " + " | ".join(f"{a}°" for a in report.angles) + " |"
        out.append(header)
        out.append("|" + "---|" * (2 + len(report.angles)))
        for hand in ("L", "R"):
            for turn in ("away", "toward"):
                cells = []
                any_data = False
                for angle in report.angles:
                    kind = "center" if angle == 0 else turn
                    cell = report.cells.get((hand, kind, angle))
                    if cell is None or cell.windows == 0:
                        cells.append("–")
                        continue
                    any_data = True
                    mark = "✅" if cell.fail_rate <= max_fail_rate + 1e-6 else "❌"
                    cells.append(f"{mark} lost {cell.failed_windows}/{cell.windows} · high {_pct(cell.high_rate)}")
                if any_data:
                    out.append(f"| {hand} | {turn} | " + " | ".join(cells) + " |")
        out.append("")
        out.append(f"- **Widest usable crowd arc (turning away from the secret hand): "
                   f"{'none' if arc_away < 0 else '±' + str(arc_away) + '°'}**")
        out.append(f"- Turning toward the secret hand: {'none' if arc_toward < 0 else '±' + str(arc_toward) + '°'}")
        out.append(f"- Verdict: {verdict(arc_away)}")
        stats = frame_stats(report.frame_times)
        if stats:
            out.append(f"- Frame rate: median {stats['median_fps']:.1f} fps, p95 frame {stats['p95_ms']:.1f} ms, "
                       f"{stats['slow_frame_pct']:.1f}% of frames over 1.5× the 72 Hz budget")
        for warning in report.warnings:
            out.append(f"- ⚠️ {warning}")
        out.append("")
    return "\n".join(out)


def to_json(reports: Dict[str, HeadsetReport], max_fail_rate: float) -> Dict[str, object]:
    result: Dict[str, object] = {"max_fail_rate": max_fail_rate, "headsets": {}}
    for name, report in reports.items():
        result["headsets"][name] = {  # type: ignore[index]
            "files": report.files,
            "arc_away_deg": usable_angle(report.cells, report.angles, "away", max_fail_rate),
            "arc_toward_deg": usable_angle(report.cells, report.angles, "toward", max_fail_rate),
            "frame_stats": frame_stats(report.frame_times),
            "warnings": report.warnings,
            "cells": [
                {"secret_hand": h, "turn": t, "angle": a, "windows": c.windows, "failed_windows": c.failed_windows,
                 "fail_rate": round(c.fail_rate, 4), "high_rate": round(c.high_rate, 4),
                 "tracked_rate": round(c.tracked_rate, 4), "longest_low_run_s": round(c.longest_low_run, 3)}
                for (h, t, a), c in sorted(report.cells.items())
            ],
        }
    return result


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("inputs", nargs="+", help="spike CSV files or folders")
    parser.add_argument("--max-fail-rate", type=float, default=DEFAULT_MAX_FAIL_RATE)
    parser.add_argument("--window", type=float, default=None, help="move window seconds (default: from file, else 1.5)")
    parser.add_argument("--dropout", type=float, default=None, help="dropout seconds (default: from file, else 0.15)")
    parser.add_argument("--markdown", help="also write the report to this Markdown file")
    parser.add_argument("--json", help="also write machine-readable results to this JSON file")
    args = parser.parse_args(argv)

    paths = expand_inputs(args.inputs)
    if not paths:
        print("No CSV files found.", file=sys.stderr)
        return 2

    files: List[SpikeFile] = []
    for path in paths:
        try:
            files.append(read_spike_csv(path))
        except (OSError, SpikeFormatError) as err:
            print(f"Skipping {path}: {err}", file=sys.stderr)
    if not files:
        return 2

    reports = analyse(files, args.window, args.dropout)
    text = render_markdown(reports, args.max_fail_rate)
    print(text)
    if args.markdown:
        with open(args.markdown, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
    if args.json:
        with open(args.json, "w", encoding="utf-8") as handle:
            json.dump(to_json(reports, args.max_fail_rate), handle, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main())
