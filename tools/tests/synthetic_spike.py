"""Write synthetic tracking-spike CSVs in the exact format the headset produces.

Used by the tests, and handy for dry-running the analysis before real device data exists:
    python3 -m tools.tests.synthetic_spike /tmp/fake_spike.csv
"""

from __future__ import annotations

import sys
from typing import Callable, List, Optional, Sequence, Tuple

from tools.analyze_tracking_spike import COLUMNS, FORMAT_TAG

Dropouts = List[Tuple[float, float]]  # (start seconds into record phase, duration seconds)
Profile = Callable[[str, str, int, int], Dropouts]

RATE_HZ = 72.0
SETTLE_S = 2.0
RECORD_S = 6.0


def no_dropouts(secret: str, turn: str, angle: int, repeat: int) -> Dropouts:
    return []


def conditions(angles: Sequence[int] = (0, 20, 35, 50), repeats: int = 2) -> List[Tuple[int, int, str, str, int]]:
    """(index, repeat, secret, turn, angle) in the same order as TrackingSpikeController.BuildConditions."""
    out = []
    index = 0
    for secret in ("L", "R"):
        for repeat in range(repeats):
            for angle in angles:
                out.append((index, repeat, secret, "center" if angle == 0 else "away", angle))
                index += 1
            for angle in angles:
                if angle > 0:
                    out.append((index, repeat, secret, "toward", angle))
                    index += 1
    return out


def signed_target(secret: str, turn: str, angle: int) -> float:
    if turn == "center":
        return 0.0
    turn_right = (secret == "L") == (turn == "away")
    return float(angle) if turn_right else -float(angle)


def write(path: str, profile: Profile = no_dropouts, headset: str = "Meta_Quest_3", repeats: int = 2,
          angles: Sequence[int] = (0, 20, 35, 50), static_position: bool = False,
          yaw_offset: Optional[float] = None) -> str:
    dt = 1.0 / RATE_HZ
    lines = [f"# {FORMAT_TAG}", f"# headset={headset}", "# window_s=1.5", "# dropout_s=0.15", ",".join(COLUMNS)]
    t = 0.0
    frame = 0
    for index, repeat, secret, turn, angle in conditions(angles, repeats):
        target = signed_target(secret, turn, angle)
        drops = profile(secret, turn, angle, repeat)
        for phase, duration in (("settle", SETTLE_S), ("record", RECORD_S)):
            phase_start = t
            n = int(round(duration * RATE_HZ))
            for i in range(n):
                local = i * dt
                low = phase == "record" and any(s <= local < s + d for s, d in drops)
                yaw = target if yaw_offset is None else target - (yaw_offset if target > 0 else -yaw_offset if target < 0 else 0)
                for hand in ("L", "R"):
                    is_secret = hand == secret
                    high = not (is_secret and low)
                    x = 0.0 if static_position else (-0.15 if hand == "L" else 0.15) + 0.001 * (frame % 7)
                    row = [f"{t:.4f}", str(frame), f"{dt:.5f}", phase, str(index), str(repeat), secret, turn,
                           f"{target:.1f}", "head", f"{yaw:.2f}", "-5.00", hand, "1", "1", "1" if high else "0",
                           "0.950" if is_secret else "0.100", "0.00", "-40.00", f"{x:.4f}", "-0.4500", "0.3500"]
                    lines.append(",".join(row))
                t = phase_start + (i + 1) * dt
                frame += 1
        t += 0.5  # gap between conditions
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    return path


if __name__ == "__main__":
    print(write(sys.argv[1] if len(sys.argv) > 1 else "synthetic_spike.csv"))
