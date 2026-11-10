#!/usr/bin/env bash
# Play Show.unity in the Meta XR Simulator and check it comes up cleanly (no headset needed).
#   tools/sim_smoke.sh
# Opens the Unity Editor window (Play mode needs the UI), switches the simulator on, plays for ~30 s,
# then writes Builds/Smoke/report.json + show.png and exits: 0 pass, 1 checks failed, 2 timed out.
# Needs Unity (tools/install_unity.py), a Unity licence, and Meta XR Simulator 207 installed.
# Close any open Unity Editor on this project first.
set -euo pipefail
source "$(dirname "$0")/_common.sh"

EDITOR_VERSION=$(grep -E '^m_EditorVersion:' "$ROOT/ProjectSettings/ProjectVersion.txt" | awk '{print $2}' | tr -d '\r')
UNITY="${UNITY:-}"
if [[ -z "$UNITY" ]]; then
  for candidate in "/d/Unity/$EDITOR_VERSION/Editor/Unity.exe" "/c/Unity/$EDITOR_VERSION/Editor/Unity.exe""/c/Program Files/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity.exe"; do
    if [[ -x "$candidate" ]]; then UNITY="$candidate"; break; fi
  done
fi
if [[ -z "$UNITY" ]]; then
  echo "error: Unity $EDITOR_VERSION not found (Windows only: the simulator is Windows/macOS). Set UNITY." >&2
  exit 1
fi
to_native() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }

mkdir -p "$ROOT/Builds/Smoke"
rm -f "$ROOT/Builds/Smoke/report.json" "$ROOT/Builds/Smoke/show.png"
LOG="$ROOT/Builds/Smoke/smoke.log"
echo "== Simulator smoke test (log: $LOG). A Unity window and the simulator will open; don't close them."
set +e
"$UNITY" -projectPath "$(to_native "$ROOT")" -buildTarget Android -logFile "$(to_native "$LOG")" \
  -executeMethod Misdirection.EditorTools.SimulatorSmokeTest.Run
STATUS=$?
set -e
grep -E "\[Misdirection\] Smoke:" "$LOG" | tail -n 12 || true
if [[ -f "$ROOT/Builds/Smoke/report.json" ]]; then
  echo "--- report"
  cat "$ROOT/Builds/Smoke/report.json"
fi
case $STATUS in
  0) echo "PASS" ;;
  1) echo "FAIL: see errors above and $LOG" ;;
  2) echo "TIMED OUT: see $LOG" ;;
  *) echo "Unity exited with $STATUS: see $LOG" ;;
esac
exit $STATUS
