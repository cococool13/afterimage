# Afterimage

Windows tray app. Press **F8** to keep the last 15, 20, or 30 seconds. No overlay. A beep. Clips go to `Videos\Afterimage` as names like `Sep 15 2.41 PM.mp4`.

**Site:** https://afterimage-site.cohencool.workers.dev
**Source:** https://github.com/cococool13/afterimage

## On the Windows PC

Needs **.NET 8 Desktop** and an **NVIDIA** GPU.

```powershell
dotnet publish Afterimage.csproj -c Release -r win-x64 --self-contained false -o .\publish
.\publish\Afterimage.exe
```

First run downloads FFmpeg into `%LOCALAPPDATA%\Afterimage\ffmpeg`. Click the tray icon for settings. **View clips** opens the folder.

Captures the game window, including exclusive fullscreen. If a title still comes out black, run Afterimage as Administrator (required when the game is elevated). Some anti-cheat will block any capture.

## Mac

```bash
dotnet run --project Afterimage.Tests
```
