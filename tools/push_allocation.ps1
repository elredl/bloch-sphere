# Copies allocation.csv to the Quest, where the launch screen reads it. Usage:
#   powershell -ExecutionPolicy Bypass -File tools\push_allocation.ps1 path\to\allocation.csv
param([Parameter(Mandatory = $true)][string]$File)
$ErrorActionPreference = "Stop"
$pkg = if ($env:STUDY_PACKAGE) { $env:STUDY_PACKAGE } else { "com.DefaultCompany.VRTemplate" }
$remote = "/sdcard/Android/data/$pkg/files"
$header = (Get-Content $File -TotalCount 1).Trim([char]0xFEFF).Replace(" ", "")
if ($header -ne "participant_code,condition,stratum") { Write-Host "$File: first line must be participant_code,condition,stratum"; exit 1 }
if ((adb get-state 2>$null) -ne "device") { Write-Host "no headset: connect it and allow USB debugging"; exit 1 }
adb shell "mkdir -p $remote" | Out-Null
adb push "$File" "$remote/allocation.csv" | Out-Null
Write-Host "pushed to $remote/allocation.csv"
