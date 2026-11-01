#!/usr/bin/env bash
# Copy tracking-spike CSVs off the headset and analyse them.
#   tools/pull_spike_logs.sh [tester-name]
# Files land in spike_logs/<date>_<tester>/ (git-ignored). Commit the report, not the raw logs.
set -euo pipefail
source "$(dirname "$0")/_common.sh"
need adb "Install Android platform-tools."
need python3 "Install Python 3.9+."
one_device

APP_ID="$(app_id)"
TESTER="${1:-tester}"
DEST="$ROOT/spike_logs/$(date +%Y%m%d)_${TESTER}"
mkdir -p "$DEST"

REMOTE="/sdcard/Android/data/$APP_ID/files/spike"
echo "Pulling $REMOTE -> $DEST"
adb pull "$REMOTE/." "$DEST/" >/dev/null
ls -1 "$DEST"

python3 "$ROOT/tools/analyze_tracking_spike.py" "$DEST" --markdown "$DEST/report.md" --json "$DEST/report.json"
echo
echo "Report: $DEST/report.md"
echo "Clear the headset copy when done: adb shell rm -r $REMOTE"
