# Shared helpers for the tools/*.sh scripts. Source, don't run.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LATEST="$ROOT/Builds/Android/latest.txt"
DEFAULT_APP_ID="com.n45div.misdirection"
SIGNING_FILE_REL="secrets/android-signing.properties"

latest_value() {
  # latest_value <key>: read a value written by MisdirectionBuild into Builds/Android/latest.txt
  [[ -f "$LATEST" ]] || return 1
  grep -E "^$1=" "$LATEST" | head -n1 | cut -d= -f2-
}

app_id() {
  if [[ -n "${MISDIRECTION_APP_ID:-}" ]]; then echo "$MISDIRECTION_APP_ID"; return; fi
  latest_value app_id 2>/dev/null || echo "$DEFAULT_APP_ID"
}

need() {
  command -v "$1" >/dev/null 2>&1 || { echo "error: '$1' not found on PATH. $2" >&2; exit 1; }
}

one_device() {
  local count
  count=$(adb devices | awk 'NR>1 && $2=="device"' | wc -l | tr -d ' ')
  if [[ "$count" == "0" ]]; then
    echo "error: no headset over adb. Plug in the Quest, enable developer mode and accept the USB debugging prompt." >&2
    exit 1
  fi
  if [[ "$count" != "1" && -z "${ANDROID_SERIAL:-}" ]]; then
    echo "error: $count devices connected. Set ANDROID_SERIAL to pick one (see 'adb devices')." >&2
    exit 1
  fi
}
