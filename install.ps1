# Taswib one-line installer: irm https://raw.githubusercontent.com/deviengeorge/taswib/main/install.ps1 | iex
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$rel = Invoke-RestMethod 'https://api.github.com/repos/deviengeorge/taswib/releases/latest' -Headers @{ 'User-Agent' = 'taswib-installer' }
$asset = $rel.assets | Where-Object name -like '*Setup.exe' | Select-Object -First 1
if (-not $asset) { throw 'Setup.exe not found in the latest release.' }
$tmp = Join-Path $env:TEMP $asset.name
Write-Host "Downloading Taswib $($rel.tag_name)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $tmp -UseBasicParsing
Write-Host 'Installing...'
Start-Process $tmp -ArgumentList '--silent' -Wait
Remove-Item $tmp -ErrorAction SilentlyContinue
Write-Host 'Done. Taswib is in the tray. Press Insert to fix text.'
