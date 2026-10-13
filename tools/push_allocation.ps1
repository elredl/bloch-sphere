# Copies allocation.csv to the Quest, where the launch screen reads it. Usage:
#   powershell -ExecutionPolicy Bypass -File tools\push_allocation.ps1 path\to\allocation.csv
param([Parameter(Mandatory = $true)][string]$File)
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
$header = (Get-Content $File -TotalCount 1).Trim([char]0xFEFF).Replace(" ", "")
if ($header -ne "participant_code,condition,stratum") { Write-Host "$File: first line must be participant_code,condition,stratum"; exit 1 }
if ((adb get-state 2>$null) -ne "device") { Write-Host "no headset: connect it and allow USB debugging"; exit 1 }
adb shell "mkdir -p $remote" | Out-Null
adb push "$File" "$remote/allocation.csv" | Out-Null
Write-Host "pushed to $remote/allocation.csv"
