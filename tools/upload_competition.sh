#!/usr/bin/env bash
# Upload the latest signed release APK to the Meta release channel used for judging.
#   tools/upload_competition.sh "notes for this build"
#   tools/upload_competition.sh --validate      run Meta's checks only, upload nothing
# Needs ovr-platform-util (Meta Platform Command Line Utility; winget id Meta.OculusPlatformUtility) on PATH
# or at D:\MetaXR\tools\ovr-platform-util.exe, and, in env or secrets/meta.env:
#   META_APP_ID       the app ID from the Developer Dashboard
#   META_APP_SECRET   Dashboard > your app > API
# Optional: META_CHANNEL (default "Competition"), META_AGE_GROUP (default TEENS_AND_ADULTS).
# After uploading it waits for Meta's postprocessing (malware/security) checks and fails if they fail.
# Flags: ovr-platform-util --help upload-quest-build
set -euo pipefail
source "$(dirname "$0")/_common.sh"
OVR="$(command -v ovr-platform-util || true)"
if [[ -z "$OVR" && -x /d/MetaXR/tools/ovr-platform-util.exe ]]; then OVR=/d/MetaXR/tools/ovr-platform-util.exe; fi
if [[ -z "$OVR" ]]; then
  echo "error: ovr-platform-util not found. winget install --id Meta.OculusPlatformUtility, or put it in D:\\MetaXR\\tools." >&2
  exit 1
fi
VALIDATE=0
if [[ "${1:-}" == "--validate" ]]; then VALIDATE=1; shift; fi

if [[ -f "$ROOT/secrets/meta.env" ]]; then
  # shellcheck disable=SC1091
  source "$ROOT/secrets/meta.env"
fi
: "${META_APP_ID:?set META_APP_ID (env or secrets/meta.env)}"
: "${META_APP_SECRET:?set META_APP_SECRET (env or secrets/meta.env)}"
CHANNEL="${META_CHANNEL:-Competition}"
AGE_GROUP="${META_AGE_GROUP:-TEENS_AND_ADULTS}"

APK="$(latest_value apk || true)"
if [[ -z "$APK" || ! -f "$APK" ]]; then
  echo "error: no APK in Builds/Android/latest.txt. Run tools/build_apk.sh first." >&2
  exit 1
fi
if [[ "$(latest_value signed_with)" != "team" || "$(latest_value development)" != "0" ]]; then
  echo "error: latest build is not a release build signed with the team keystore. Rebuild without --dev." >&2
  exit 1
fi

NOTES="${1:-Build $(latest_value version_name) from $(git -C "$ROOT" rev-parse --short HEAD 2>/dev/null || echo unknown)}"
ARGS=(upload-quest-build --app-id "$META_APP_ID" --app-secret "$META_APP_SECRET" --apk "$APK"
      --channel "$CHANNEL" --age-group "$AGE_GROUP" --notes "$NOTES" --disable-progress-bar)
if [[ $VALIDATE -eq 1 ]]; then
  echo "Validating $APK (nothing is uploaded)"
  "$OVR" "${ARGS[@]}" --validate
  exit $?
fi
echo "Uploading $APK to channel '$CHANNEL' and waiting for Meta's postprocessing checks"
"$OVR" "${ARGS[@]}" --wait-for-postprocessing
echo "Done. In the Dashboard, confirm the build is live on '$CHANNEL' and copy the Invite URL into docs/OPEN_ITEMS.md (#11)."
