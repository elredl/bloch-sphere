#!/usr/bin/env bash
# End of each session day: copy the session files off the Quest, check them, then remove
# them from the headset (spec, Session flow). Usage:
#   tools/pull_data.sh [destination folder]      (default: study_data/)
# Needs adb (Android platform tools) and the headset connected with USB debugging allowed.
# The files are deleted from the headset only if every one arrived with the same size.
# session_index.csv (the list of codes already used) is copied but stays on the headset.
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
DEST_ROOT="${1:-study_data}"

[ "$(adb get-state 2>/dev/null)" = "device" ] || { echo "no headset: connect it and allow USB debugging"; exit 1; }

mapfile -t files < <(adb shell "ls $REMOTE/ 2>/dev/null" | tr -d '\r' | grep -E '_C[123]_[0-9]{8}-[0-9]{4}\.csv$' || true)
if [ ${#files[@]} -eq 0 ]; then echo "no session files on the headset ($REMOTE)"; exit 0; fi

dest="$DEST_ROOT/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$dest"
echo "pulling ${#files[@]} session files to $dest"
ok=0
for f in "${files[@]}"; do
  adb pull "$REMOTE/$f" "$dest/$f" >/dev/null
  remote_size=$(adb shell "stat -c %s '$REMOTE/$f'" | tr -d '\r')
  local_size=$(wc -c < "$dest/$f" | tr -d ' ')
  if [ "$remote_size" = "$local_size" ]; then ok=$((ok+1)); echo "  ok  $f ($local_size bytes)"
  else echo "  MISMATCH $f: headset $remote_size bytes, copy $local_size bytes"; fi
done
adb pull "$REMOTE/session_index.csv" "$dest/session_index.csv" >/dev/null 2>&1 || echo "  (no session_index.csv on the headset)"

if [ "$ok" -ne "${#files[@]}" ]; then
  echo "only $ok of ${#files[@]} files verified: NOTHING deleted from the headset. Run again."
  exit 1
fi
for f in "${files[@]}"; do adb shell "rm '$REMOTE/$f'"; done
left=$(adb shell "ls $REMOTE/ 2>/dev/null" | tr -d '\r' | grep -cE '_C[123]_.*\.csv$' || true)
echo "verified $ok files; removed them from the headset ($left session files left there)"
