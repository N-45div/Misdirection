#!/usr/bin/env bash
# Build a signed release APK without opening the Editor UI.
#   tools/build_apk.sh            signed release APK (needs secrets/android-signing.properties or MISDIRECTION_KEYSTORE*)
#   tools/build_apk.sh --dev      development build, debug-signed is allowed
# Close the Unity Editor on this project first (Unity locks the project).
# Set UNITY=/path/to/Unity if the Editor is not in a standard Unity Hub location.
set -euo pipefail
source "$(dirname "$0")/_common.sh"

if [[ "${1:-}" == "--dev" ]]; then
  export MISDIRECTION_DEV_BUILD=1
  export MISDIRECTION_ALLOW_DEBUG_SIGNING="${MISDIRECTION_ALLOW_DEBUG_SIGNING:-1}"
fi

VERSION_FILE="$ROOT/ProjectSettings/ProjectVersion.txt"
if [[ ! -f "$VERSION_FILE" ]]; then
  echo "error: $VERSION_FILE missing. Create the Unity project first (docs/days/day-01.md, step A)." >&2
  exit 1
fi
EDITOR_VERSION=$(grep -E '^m_EditorVersion:' "$VERSION_FILE" | awk '{print $2}')

if [[ -z "${UNITY:-}" ]]; then
  for candidate in \
    "/Applications/Unity/Hub/Editor/$EDITOR_VERSION/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity" \
    "/c/Program Files/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity.exe" \
    "/mnt/c/Program Files/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity.exe"; do
    if [[ -x "$candidate" ]]; then UNITY="$candidate"; break; fi
  done
fi
if [[ -z "${UNITY:-}" ]]; then
  echo "error: Unity $EDITOR_VERSION not found. Set UNITY=/path/to/Unity." >&2
  exit 1
fi

mkdir -p "$ROOT/Builds/Android"
LOG="$ROOT/Builds/Android/build.log"
echo "Building with Unity $EDITOR_VERSION (log: $LOG)"
set +e
"$UNITY" -batchmode -quit -projectPath "$ROOT" -buildTarget Android \
  -executeMethod Misdirection.EditorTools.MisdirectionBuild.BuildFromCommandLine -logFile "$LOG"
STATUS=$?
set -e
if [[ $STATUS -ne 0 ]]; then
  echo "Build failed (exit $STATUS). Last errors:" >&2
  grep -E "error|Error|\[Misdirection\]" "$LOG" | tail -n 25 >&2 || true
  exit $STATUS
fi
cat "$LATEST"
