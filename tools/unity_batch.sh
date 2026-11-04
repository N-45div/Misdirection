#!/usr/bin/env bash
# Run Unity headless for this project: licence -> bootstrap -> build. Used by CI and cloud sessions.
#   tools/unity_batch.sh bootstrap   configure the project and create the scenes (no clicks)
#   tools/unity_batch.sh build       build the APK (signed if a keystore is configured)
#   tools/unity_batch.sh all         both
#
# Licence, one of:
#   UNITY_LICENSE        contents of Unity_lic.ulf (Personal: sign in to Unity Hub once on your own
#                        machine, then copy the file; see docs/BUILD_WITHOUT_A_HEADSET.md)
#   UNITY_LICENSE_B64    the same file, base64-encoded (easier to paste into env settings)
#   UNITY_EMAIL + UNITY_PASSWORD (+ UNITY_SERIAL for Plus/Pro)
#   or a licence already activated on this machine (sign in to Unity Hub)
# Unity binary: $UNITY, else $UNITY_HOME/Editor/Unity[.exe], else /opt/unity (unityci images),
# else the usual Windows/macOS folders for the project's editor version. Works in Git Bash on Windows.
# Without a keystore (MISDIRECTION_KEYSTORE / secrets/android-signing.properties) it makes a
# debug-signed development APK, which installs on a dev-mode Quest but can't go to the Store.
set -euo pipefail
source "$(dirname "$0")/_common.sh"

MODE="${1:-all}"
LOG_DIR="$ROOT/Builds/Android"
mkdir -p "$LOG_DIR"

EDITOR_VERSION=$(grep -E '^m_EditorVersion:' "$ROOT/ProjectSettings/ProjectVersion.txt" | awk '{print $2}' | tr -d '\r')
UNITY="${UNITY:-}"
if [[ -z "$UNITY" ]]; then
  for candidate in \
    "${UNITY_HOME:+$UNITY_HOME/Editor/Unity}" \
    "${UNITY_HOME:+$UNITY_HOME/Editor/Unity.exe}" \
    /opt/unity/Editor/Unity \
    "/c/Unity/$EDITOR_VERSION/Editor/Unity.exe" \
    "/c/Program Files/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity.exe" \
    "/Applications/Unity/Hub/Editor/$EDITOR_VERSION/Unity.app/Contents/MacOS/Unity" \
    "$HOME/Unity/Hub/Editor/$EDITOR_VERSION/Editor/Unity"; do
    if [[ -n "$candidate" && -x "$candidate" ]]; then UNITY="$candidate"; break; fi
  done
fi
if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "error: Unity $EDITOR_VERSION not found. Set UNITY, or install it with tools/install_unity.py --dest <folder> and set UNITY_HOME to that folder." >&2
  exit 1
fi
# Native Windows Unity wants Windows paths.
to_native() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }
PROJECT_PATH="$(to_native "$ROOT")"

# ---- licence
CRED_ARGS=()
ULF_DIR="$HOME/.local/share/unity3d/Unity"
if [[ -n "${UNITY_LICENSE_B64:-}" ]]; then
  mkdir -p "$ULF_DIR" && printf '%s' "$UNITY_LICENSE_B64" | base64 -d > "$ULF_DIR/Unity_lic.ulf"
elif [[ -n "${UNITY_LICENSE:-}" ]]; then
  mkdir -p "$ULF_DIR" && printf '%s' "$UNITY_LICENSE" > "$ULF_DIR/Unity_lic.ulf"
fi
if [[ -n "${UNITY_EMAIL:-}" && -n "${UNITY_PASSWORD:-}" ]]; then
  CRED_ARGS+=(-username "$UNITY_EMAIL" -password "$UNITY_PASSWORD")
  if [[ -n "${UNITY_SERIAL:-}" ]]; then CRED_ARGS+=(-serial "$UNITY_SERIAL"); fi
fi
LOCAL_LICENCE=""
for f in "$ULF_DIR/Unity_lic.ulf" "/c/ProgramData/Unity/Unity_lic.ulf" "/Library/Application Support/Unity/Unity_lic.ulf"; do
  if [[ -f "$f" ]]; then LOCAL_LICENCE="$f"; break; fi
done
if [[ -z "$LOCAL_LICENCE" && ${#CRED_ARGS[@]} -eq 0 ]]; then
  echo "error: no Unity licence. Sign in to Unity Hub on this machine, or set UNITY_LICENSE / UNITY_LICENSE_B64, or UNITY_EMAIL + UNITY_PASSWORD." >&2
  exit 3
fi

# ---- signing: decode a CI keystore if one was provided
if [[ -n "${ANDROID_KEYSTORE_BASE64:-}" && -z "${MISDIRECTION_KEYSTORE:-}" ]]; then
  mkdir -p "$ROOT/secrets"
  printf '%s' "$ANDROID_KEYSTORE_BASE64" | base64 -d > "$ROOT/secrets/ci.keystore"
  export MISDIRECTION_KEYSTORE="$ROOT/secrets/ci.keystore"
  export MISDIRECTION_KEYSTORE_PASS="${ANDROID_KEYSTORE_PASS:-}"
  export MISDIRECTION_KEY_ALIAS="${ANDROID_KEY_ALIAS:-}"
  export MISDIRECTION_KEY_PASS="${ANDROID_KEY_PASS:-}"
fi
if [[ -z "${MISDIRECTION_KEYSTORE:-}" && ! -f "$ROOT/$SIGNING_FILE_REL" ]]; then
  echo "note: no keystore configured, building a debug-signed development APK"
  export MISDIRECTION_DEV_BUILD=1 MISDIRECTION_ALLOW_DEBUG_SIGNING=1
fi

run_unity() {
  local name="$1"; shift
  local log="$LOG_DIR/unity-$name.log"
  echo "== Unity $name (log: $log)"
  set +e
  "$UNITY" -batchmode -nographics -projectPath "$PROJECT_PATH" -buildTarget Android -logFile "$(to_native "$log")" "${CRED_ARGS[@]}" "$@"
  local status=$?
  set -e
  if grep -q "No valid Unity Editor license" "$log" 2>/dev/null; then
    echo "error: Unity rejected the licence (see $log)." >&2
    exit 3
  fi
  if [[ $status -ne 0 ]]; then
    echo "Unity $name failed (exit $status). Last relevant lines:" >&2
    grep -E "\[Misdirection\]|error CS|Exception|Error building|BuildFailed" "$log" | tail -n 40 >&2 || tail -n 40 "$log" >&2
    exit $status
  fi
  grep -E "\[Misdirection\]" "$log" | tail -n 20 || true
}

if [[ "$MODE" == "bootstrap" || "$MODE" == "all" ]]; then
  # No -quit: the bootstrap exits by itself once Meta's tools finish.
  run_unity bootstrap -executeMethod Misdirection.EditorTools.MisdirectionBootstrap.RunBatch
fi
if [[ "$MODE" == "build" || "$MODE" == "all" ]]; then
  run_unity build -quit -executeMethod Misdirection.EditorTools.MisdirectionBuild.BuildFromCommandLine
  cat "$LATEST"
fi
