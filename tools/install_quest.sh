#!/usr/bin/env bash
# Install the latest built APK on the connected headset and launch it.
#   tools/install_quest.sh [path/to/app.apk]
set -euo pipefail
source "$(dirname "$0")/_common.sh"
need adb "Install Android platform-tools, or use Unity's bundled adb."
one_device

APK="${1:-$(latest_value apk || true)}"
if [[ -z "$APK" || ! -f "$APK" ]]; then
  echo "error: no APK. Build first (tools/build_apk.sh) or pass a path." >&2
  exit 1
fi
APP_ID="$(app_id)"

echo "Installing $APK"
adb install -r -g "$APK"
echo "Launching $APP_ID"
adb shell monkey -p "$APP_ID" -c android.intent.category.LAUNCHER 1 >/dev/null
echo "Running. Put the headset on, controllers off."
