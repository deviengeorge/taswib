# Changelog

User-facing changes per release. Updates download as a small delta (~130 KB, ~60 KB on the `fd` channel).

## 0.10.0 — 2026-11-27
- Taswib no longer closes and reopens by itself after login when it finds an update; the update installs the next time Taswib starts.
- Your clipboard comes back exactly as it was after a fix, and Taswib's temporary text stays out of Win+V history.
- Notices (fixed, nothing selected, shortcut taken) show as a small popup in the screen corner instead of a Windows balloon.
- More keys work in the shortcut box, such as comma and period.
- Runs on .NET 10, and the download is smaller. The small installer needs only the .NET 10 Runtime, not the Desktop Runtime.

## 0.9.1 — 2026-11-26
- Settings, Welcome and What's new show only the close button again (no minimize/maximize).

## 0.9.0 — 2026-11-26
- New look for Settings, Welcome and What's new: sharp at any display scaling, including a second monitor with different scaling.
- Arabic interface uses the Cairo font and a fully mirrored layout.
- Faster fix: Taswib reacts as soon as the copy lands, and falls back to the current line sooner when nothing is selected.
- "+ Line" works with NumLock on, and keys you type during a fix can no longer slip into the middle of it.

## 0.8.10 — 2026-11-25
- If there is no internet when Taswib starts (e.g. Wi-Fi not connected yet at login), the update check retries every 30 minutes instead of waiting 4 hours.

## 0.8.9 — 2026-11-25
- Sharp, correctly sized UI at 125% / 150% display scaling (every size now follows the screen DPI).
- A background update no longer closes Taswib after 60 seconds; it installs the next time Taswib starts.

## 0.8.8 — 2026-11-25
- Settings no longer cuts off text at 125% / 150% scaling (Windows stretches the window; replaced by a sharp fix in 0.8.9).

## 0.8.7 — 2026-11-24
- "Check for updates" shows progress: checking, download %, restart and "ready" notices.
- Offline is reported separately from other update errors.
- Settings shows "Saved" after each change.
- A balloon confirms a successful fix when sound is off.
- The shortcut box explains why a bare key is rejected.
- Icons in the tray menu and Settings sidebar.

## 0.8.6 — 2026-11-24
- Shortcut box shows the captured key right away (no stuck "…").
- Reset button next to each shortcut.
- Default scope is now "Whole box".

## 0.8.5 — 2026-11-24
- First fix for text cut off at 125% / 150% scaling.
- Waits longer for slow apps to copy the text.
- Settings are saved safely (no corrupt file if the PC turns off mid-save).
- Telegram link on the Contact page.

## 0.8.4 — 2026-11-22
- Checks for updates every 4 hours, so PCs that never restart still update.

## 0.8.3 — 2026-11-22
- Fixes text typed with Caps Lock on.

## 0.8.2 — 2026-11-22
- Clipboard is restored even if a fix fails midway.
- Bare letter keys are rejected as shortcuts (they would block typing everywhere).
- Opening Taswib again opens Settings in the running copy.
- Portable exe: a new version shows a tray balloon instead of opening the browser at startup.

## 0.8.1 — 2026-11-21
- One click on the tray icon opens Settings (or brings it to the front).

## 0.8.0 — 2026-11-21
- Full clipboard restore (images, files, formatting — not just text).
- Clean uninstall (removes the "Start with Windows" entry).
- Tray icon shows when Taswib is turned off.
- "What's new" window after updates that add features.
- Arabic phone number and descriptions display correctly.

## 0.7.0 — 2026-11-21
- Feedback after a fix, diagnostics log, welcome window on first run.
- Fix: settings were not loaded after a restart.

## 0.6.0 — 2026-11-21
- Contact page.
- Small installer (~16 MB) for PCs that already have .NET 8.

## 0.5.0 — 2026-11-21
- Redesigned Settings: sidebar pages, cards, toggles, dark mode, autosave.

## 0.4.2 — 2026-11-21
- Taswib's own clipboard use stays out of Win+V history.
- Shortcuts are paused while recording a new one.

## 0.4.1 — 2026-11-21
- Default shortcut is Insert.

## 0.4.0 — 2026-11-21
- "Clean" shortcut: fixes Arabic punctuation and normalizes letters.
- Settings in Egyptian Arabic.

## 0.3.x — 2026-11-21
- Switches keyboard layout after a fix.
- Installer with automatic updates; app icon.
- One-line PowerShell installer.

## 0.2.0 — 2026-11-21
- Settings window: shortcut, scope, direction, on/off, start with Windows.

## 0.1.0 — 2026-11-21
- First release: fixes text typed in the wrong layout (English ⇄ Arabic) with a global shortcut.
