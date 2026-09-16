# Afterimage

Windows tray app. Press **F8** to keep the last 15, 20, or 30 seconds. No overlay. A beep. Clips go to `Videos\Afterimage` as names like `Sep 15 2.41 PM.mp4`.

**Site:** https://afterimage-site.cohencool.workers.dev
**Source:** https://github.com/cococool13/afterimage

## On the Windows PC

Needs an **NVIDIA** GPU. The download is self-contained.

1. Download Afterimage.zip
2. Run `Afterimage.exe`
3. Wait for the one-time encoder download, click **Got it**

It copies itself into `%LOCALAPPDATA%\Programs\Afterimage` and adds a Start Menu shortcut.

From this repo on a Windows PC:

```powershell
.\install.ps1
```

Click the tray icon for settings. Change the hotkey, length, quality, and microphone there. **View clips** / **Open last** / recent files are in that window. Old clips are deleted past 50 files or 5 GB. If a game is running as Administrator, use **Run as administrator**.

Signed releases need GitHub secrets `WINDOWS_CERT_PFX` (base64 PFX) and `WINDOWS_CERT_PASSWORD`. Without them the zip is unsigned and SmartScreen may warn once.

Captures the game window, including exclusive fullscreen. NVENC runs only while a game is open. Status **Waiting** means the desktop is idle. If a title still comes out black, run Afterimage as Administrator (required when the game is elevated). Some anti-cheat will block any capture.

## Mac

```bash
dotnet run --project Afterimage.Tests
```
