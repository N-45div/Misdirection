#!/usr/bin/env bash
# Copy test logs off the headset and analyse them.
#   tools/pull_logs.sh spike [tester]    tracking spike (Day 1)
#   tools/pull_logs.sh ditch [tester]    ditch practice (Day 2)
# Files land in spike_logs/<date>_<tester>/ (git-ignored). Commit the report, not the raw logs.
set -euo pipefail
source "$(dirname "$0")/_common.sh"
need adb "Install Android platform-tools."
need python3 "Install Python 3.9+."

KIND="${1:-}"
case "$KIND" in
  spike) ANALYZER="analyze_tracking_spike.py" ;;
  ditch) ANALYZER="analyze_ditch_trials.py" ;;
  *) echo "usage: tools/pull_logs.sh spike|ditch [tester]" >&2; exit 2 ;;
esac
one_device

APP_ID="$(app_id)"
TESTER="${2:-tester}"
DEST="$ROOT/spike_logs/$(date +%Y%m%d)_${TESTER}"
mkdir -p "$DEST"

REMOTE="/sdcard/Android/data/$APP_ID/files/$KIND"
echo "Pulling $REMOTE -> $DEST"
adb pull "$REMOTE/." "$DEST/" >/dev/null
ls -1 "$DEST"

python3 "$ROOT/tools/$ANALYZER" "$DEST" --markdown "$DEST/${KIND}-report.md" --json "$DEST/${KIND}-report.json"
echo
echo "Report: $DEST/${KIND}-report.md"
echo "Clear the headset copy when done: adb shell rm -r $REMOTE"
