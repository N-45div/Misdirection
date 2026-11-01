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
# Unity binary: $UNITY, else $UNITY_HOME/Editor/Unity, else /opt/unity/Editor/Unity (unityci images).
# Without a keystore (MISDIRECTION_KEYSTORE / secrets/android-signing.properties) it makes a
# debug-signed development APK, which installs on a dev-mode Quest but can't go to the Store.
set -euo pipefail
source "$(dirname "$0")/_common.sh"

MODE="${1:-all}"
LOG_DIR="$ROOT/Builds/Android"
mkdir -p "$LOG_DIR"

UNITY="${UNITY:-}"
if [[ -z "$UNITY" && -n "${UNITY_HOME:-}" ]]; then UNITY="$UNITY_HOME/Editor/Unity"; fi
if [[ -z "$UNITY" && -x /opt/unity/Editor/Unity ]]; then UNITY=/opt/unity/Editor/Unity; fi
if [[ -z "$UNITY" || ! -x "$UNITY" ]]; then
  echo "error: Unity not found. Set UNITY, or install it with tools/install_unity_linux.py --dest ~/unity and set UNITY_HOME=~/unity" >&2
  exit 1
fi

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
if [[ ! -f "$ULF_DIR/Unity_lic.ulf" && ${#CRED_ARGS[@]} -eq 0 ]]; then
  echo "error: no Unity licence. Set UNITY_LICENSE / UNITY_LICENSE_B64, or UNITY_EMAIL + UNITY_PASSWORD." >&2
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
  "$UNITY" -batchmode -nographics -projectPath "$ROOT" -buildTarget Android -logFile "$log" "${CRED_ARGS[@]}" "$@"
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
