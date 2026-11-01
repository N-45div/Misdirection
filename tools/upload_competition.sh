#!/usr/bin/env bash
# Upload the latest signed release APK to the Meta release channel used for judging.
#   tools/upload_competition.sh "notes for this build"
# Needs ovr-platform-util (Meta Platform Command Line Utility) on PATH and, in env or secrets/meta.env:
#   META_APP_ID       the app ID from the Developer Dashboard
#   META_APP_SECRET   Dashboard > your app > Development > API
# Optional: META_CHANNEL (default "Competition"), META_AGE_GROUP (default TEENS_AND_ADULTS).
# Flags follow Meta's documented syntax; check with: ovr-platform-util help upload-quest-build
set -euo pipefail
source "$(dirname "$0")/_common.sh"
need ovr-platform-util "Download the Meta Platform Command Line Utility from the Meta developer site."

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
echo "Uploading $APK to channel '$CHANNEL'"
ovr-platform-util upload-quest-build \
  --app_id "$META_APP_ID" \
  --app_secret "$META_APP_SECRET" \
  --apk "$APK" \
  --channel "$CHANNEL" \
  --age_group "$AGE_GROUP" \
  --notes "$NOTES"
echo "Done. In the Dashboard, confirm the build is live on '$CHANNEL' and copy the Invite URL into docs/OPEN_ITEMS.md (#11)."
