# Afterimage

Windows tray app. Press **F8** to keep the last 15, 20, or 30 seconds. No overlay. A beep. Clips go to `Videos\Afterimage` as names like `Sep 15 2.41 PM.mp4`.

**Site:** https://afterimage-site.cohencool.workers.dev
**Source:** https://github.com/cococool13/afterimage

## On the Windows PC

Needs an **NVIDIA** GPU.

1. Download Afterimage.exe
2. Double-click it
3. Click **Got it**

It installs to `%LOCALAPPDATA%\Programs\Afterimage`, adds a Start Menu shortcut, and shows up under Installed apps. You can delete the download. First run may download a helper once if the release did not already include it.

From this repo on a Windows PC:

```powershell
.\install.ps1
```

Click the tray icon (by the clock) for settings, or run Afterimage again to bring that window forward. Change the hotkey, length, quality, and microphone there. **View clips** / **Open last** / recent files are in that window. Old clips are deleted past 50 files or 5 GB. If a game is running as Administrator, use **Run as administrator**. Uninstall from settings, or from Installed apps.

Signed releases need GitHub secrets `WINDOWS_CERT_PFX` (base64 PFX) and `WINDOWS_CERT_PASSWORD`. Without them SmartScreen may warn once: More info → Run anyway.

Captures the game window, including exclusive fullscreen. NVENC runs only while a game is open. Status **Waiting for a game** means the desktop is idle. If a title still comes out black, run Afterimage as Administrator (required when the game is elevated). Some anti-cheat will block any capture.

## Mac

```bash
dotnet run --project Afterimage.Tests
```
