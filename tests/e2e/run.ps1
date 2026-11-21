# End-to-end check on a real Windows desktop: types wrong-layout text into a test window, presses the real
# shortcut, reads the result back. Then takes screenshots of every Settings page (EN + AR) and the welcome window.
param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string]$Out)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@

function Key([byte[]]$mods, [byte]$vk, [bool]$ext = $false) {
  foreach ($m in $mods) { [W]::keybd_event($m, 0, 0, [UIntPtr]::Zero) }
  $f = if ($ext) { 1 } else { 0 }
  [W]::keybd_event($vk, 0, $f, [UIntPtr]::Zero); [W]::keybd_event($vk, 0, ($f -bor 2), [UIntPtr]::Zero)
  foreach ($m in $mods) { [W]::keybd_event($m, 0, 2, [UIntPtr]::Zero) }
}
function Pump([int]$ms) { $end = (Get-Date).AddMilliseconds($ms); while ((Get-Date) -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 } }
function Write-Settings([string]$lang) {
  $d = "$env:APPDATA\Taswib"; New-Item -ItemType Directory -Force $d | Out-Null
  "{ `"Enabled`": true, `"Hotkey`": `"Insert`", `"CleanHotkey`": `"Shift+Pause`", `"Language`": `"$lang`", `"SoundOnFix`": false, `"LastNewsVersion`": `"99.0.0`" }" | Set-Content "$d\settings.json" -Encoding UTF8
}
function Stop-Taswib { Get-Process Taswib -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 800 }
function Shot([string]$title, [string]$file) {
  $h = [W]::FindWindow($null, $title)
  if ($h -eq [IntPtr]::Zero) { Write-Host "window '$title' not found"; $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds; $r = New-Object W+RECT; $r.L = $b.Left; $r.T = $b.Top; $r.R = $b.Right; $r.B = $b.Bottom }
  else { $r = New-Object W+RECT; [void][W]::GetWindowRect($h, [ref]$r) }
  $bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
  $g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $g.Dispose()
  $bmp.Save("$Out\$file", [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
}

$script:fails = 0
function Test-Fix([string]$name, [string]$typed, [string]$expect, [byte[]]$mods, [byte]$vk, [bool]$ext) {
  $form = New-Object System.Windows.Forms.Form; $form.Width = 520; $form.Height = 220; $form.TopMost = $true; $form.Text = 'e2e'
  $tb = New-Object System.Windows.Forms.TextBox; $tb.Multiline = $true; $tb.Dock = 'Fill'; $tb.Font = New-Object System.Drawing.Font('Segoe UI', 14)
  $form.Controls.Add($tb); $form.Show(); $form.Activate(); $tb.Focus(); $tb.Text = $typed; $tb.SelectAll()
  Pump 800
  Key $mods $vk $ext
  Pump 3000
  $got = $tb.Text; Shot 'e2e' "result-$name.png"; $form.Close()
  if ($got -eq $expect) { Write-Host "PASS $name" } else { Write-Host "FAIL $name : got '$got' expected '$expect'"; $script:fails++ }
}

# ---- 1. real shortcut tests
Stop-Taswib
Write-Settings 'en'
Start-Process $Exe; Start-Sleep -Seconds 5
Test-Fix 'fix-en-to-ar' 'high fd; ;dt phg;' 'اهلا بيك كيف حالك' @() 0x2D $true
Test-Fix 'fix-ar-to-en' 'اهلا' 'high' @() 0x2D $true
Test-Fix 'clean' 'ماذا? نعم, أحمد' 'ماذا؟ نعم، احمد' @(0x10) 0x13 $false
Stop-Taswib
$log = Get-Content "$env:LOCALAPPDATA\Taswib\taswib.log" -Raw -ErrorAction SilentlyContinue
if ($log -match '-> Fixed') { Write-Host 'PASS log records fixes' } else { Write-Host 'FAIL log has no "-> Fixed" line'; $script:fails++ }
Copy-Item "$env:LOCALAPPDATA\Taswib\taswib.log" "$Out\taswib.log" -ErrorAction SilentlyContinue

# ---- 2. screenshots
foreach ($lang in 'en', 'ar') {
  Write-Settings $lang
  $title = if ($lang -eq 'ar') { 'إعدادات تصويب' } else { 'Taswib settings' }
  foreach ($page in 0..5) {
    Stop-Taswib
    Start-Process $Exe -ArgumentList '--settings', '--page', $page
    Start-Sleep -Seconds 4
    Shot $title "settings-$lang-$page.png"
  }
}
Stop-Taswib
Remove-Item "$env:APPDATA\Taswib\settings.json" -ErrorAction SilentlyContinue   # first run => welcome window
Start-Process $Exe; Start-Sleep -Seconds 5
Shot 'Welcome to Taswib' 'welcome-en.png'
Stop-Taswib

if ($script:fails -gt 0) { Write-Host "$script:fails check(s) failed"; exit 1 }
Write-Host 'all checks passed'
