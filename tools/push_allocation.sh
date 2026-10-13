#!/usr/bin/env bash
# Copies allocation.csv to the Quest, where the launch screen reads it. Usage:
#   tools/push_allocation.sh path/to/allocation.csv
# Columns: participant_code,condition,stratum (condition 1/2/3; stratum math_literate or novice).
# Desktop: copy the file into the app's data folder instead (shown at the bottom of the launch screen).
set -euo pipefail
# adb: from PATH, else the copy that ships with Unity's Android support.
ADB="$(command -v adb || true)"
if [ -z "$ADB" ]; then
  for c in "$HOME"/Unity/Hub/Editor/*/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb; do [ -x "$c" ] && ADB="$c"; done
fi
[ -n "$ADB" ] || { echo "adb not found: install Android platform tools"; exit 1; }
adb() { "$ADB" "$@"; }
PKG="${STUDY_PACKAGE:-edu.colostate.qupacabras.qubitlesson}"
REMOTE="/sdcard/Android/data/$PKG/files"
file="${1:?usage: push_allocation.sh allocation.csv}"
head -1 "$file" | tr -d '\r\357\273\277 ' | grep -qx 'participant_code,condition,stratum' \
  || { echo "$file: first line must be participant_code,condition,stratum"; exit 1; }
[ "$(adb get-state 2>/dev/null)" = "device" ] || { echo "no headset: connect it and allow USB debugging"; exit 1; }
adb shell "mkdir -p $REMOTE"
adb push "$file" "$REMOTE/allocation.csv" >/dev/null
echo "pushed $(($(grep -cv '^\s*$' "$file") - 1)) rows to $REMOTE/allocation.csv (start the app once first if this fails)"
