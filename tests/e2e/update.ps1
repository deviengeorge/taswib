# Auto-update check: installs the OLD version, points it at a local feed holding a NEW version,
# starts it like Windows would at login, and waits for the installed exe to become the new version.
param([Parameter(Mandatory)][string]$Setup, [Parameter(Mandatory)][string]$Feed, [Parameter(Mandatory)][string]$Expect)
$ErrorActionPreference = 'Stop'
$app = "$env:LOCALAPPDATA\Taswib"
$exe = "$app\current\Taswib.exe"
function Ver { if (Test-Path $exe) { (Get-Item $exe).VersionInfo.FileVersion } else { '' } }

# settings file present = not first run, so no welcome window gets in the way
New-Item -ItemType Directory -Force "$env:APPDATA\Taswib" | Out-Null
'{ "LastNewsVersion": "99.0.0" }' | Set-Content "$env:APPDATA\Taswib\settings.json" -Encoding UTF8

Start-Process $Setup -ArgumentList '--silent' -Wait
Get-Process Taswib -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2
$old = Ver
Write-Host "installed: $old"
if (-not $old -or $old -like "$Expect*") { throw "install failed or already new: '$old'" }

$env:TASWIB_UPDATE_FEED = $Feed
Start-Process $exe   # startup check: download, apply, restart
$end = (Get-Date).AddSeconds(120)
while ((Get-Date) -lt $end -and -not ((Ver) -like "$Expect*")) { Start-Sleep 2 }
$new = Ver
Write-Host "after update: $new"
Get-Content "$app\taswib.log" -ErrorAction SilentlyContinue | Select-Object -Last 20
if (-not ($new -like "$Expect*")) { throw "not updated: still '$new'" }
$end = (Get-Date).AddSeconds(30)  # restart lags the file swap by a moment
while ((Get-Date) -lt $end -and -not (Get-Process Taswib -ErrorAction SilentlyContinue)) { Start-Sleep 1 }
if (-not (Get-Process Taswib -ErrorAction SilentlyContinue)) { throw "updated but app did not restart" }
Write-Host 'ok'
Get-Process Taswib -ErrorAction SilentlyContinue | Stop-Process -Force
