# End of each session day: copy the session files off the Quest, check them, then remove
# them from the headset (spec, Session flow). Usage:
#   powershell -ExecutionPolicy Bypass -File tools\pull_data.ps1 [destination folder]
# Needs adb (Android platform tools) and the headset connected with USB debugging allowed.
# The files are deleted from the headset only if every one arrived with the same size.
# session_index.csv (the list of codes already used) is copied but stays on the headset.
param([string]$DestRoot = "study_data")
$ErrorActionPreference = "Stop"
# adb: from PATH, else the copy that ships with Unity's Android support.
$adbCmd = Get-Command adb -ErrorAction SilentlyContinue
$adbPath = if ($adbCmd) { $adbCmd.Source } else {
    Get-ChildItem "C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" -ErrorAction SilentlyContinue |
        Select-Object -Last 1 -ExpandProperty FullName }
if (-not $adbPath) { Write-Host "adb not found: install Android platform tools"; exit 1 }
function adb { & $adbPath @args }
$pkg = if ($env:STUDY_PACKAGE) { $env:STUDY_PACKAGE } else { "edu.colostate.qupacabras.qubitlesson" }
$remote = "/sdcard/Android/data/$pkg/files"

if ((adb get-state 2>$null) -ne "device") { Write-Host "no headset: connect it and allow USB debugging"; exit 1 }

$files = @(adb shell "ls $remote/ 2>/dev/null" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '_C[123]_\d{8}-\d{4}\.csv$' })
if ($files.Count -eq 0) { Write-Host "no session files on the headset ($remote)"; exit 0 }

$dest = Join-Path $DestRoot (Get-Date -Format "yyyyMMdd-HHmmss")
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Write-Host "pulling $($files.Count) session files to $dest"
$ok = 0
foreach ($f in $files) {
    $local = Join-Path $dest $f
    adb pull "$remote/$f" "$local" | Out-Null
    $remoteSize = (adb shell "stat -c %s '$remote/$f'").Trim()
    $localSize = (Get-Item $local).Length
    if ("$remoteSize" -eq "$localSize") { $ok++; Write-Host "  ok  $f ($localSize bytes)" }
    else { Write-Host "  MISMATCH $f: headset $remoteSize bytes, copy $localSize bytes" }
}
adb pull "$remote/session_index.csv" (Join-Path $dest "session_index.csv") 2>$null | Out-Null

if ($ok -ne $files.Count) { Write-Host "only $ok of $($files.Count) files verified: NOTHING deleted from the headset. Run again."; exit 1 }
foreach ($f in $files) { adb shell "rm '$remote/$f'" | Out-Null }
Write-Host "verified $ok files; removed them from the headset"
