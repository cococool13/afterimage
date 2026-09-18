# CLAUDE.md — Afterimage

Personal Windows 11 NVIDIA replay clipper, now a public free app. Tray icon only. Left-click opens settings. F8 dumps the last 15/20/30 seconds to `Videos\Afterimage`. Beep on save, no overlay.

Build and run the app on the Windows PC. A Mac can compile and run `Afterimage.Tests` and the marketing site.

## Stack

- .NET 8, `net8.0-windows`, WinForms tray + settings
- FFmpeg `gfxcapture` of the game window (HWND) at 1920×1080 + `h264_nvenc` p1/ull
- WASAPI loopback via NAudio
- Resident processes: Afterimage.exe + one ffmpeg.exe
- Site: Cloudflare Worker static assets (`site/public/`), worker name `afterimage-site`

## Commands

Windows:

```powershell
dotnet publish Afterimage.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
dotnet run --project Afterimage.Tests
```

Mac:

```bash
dotnet run --project Afterimage.Tests
```

Site:

```bash
cd site && npx wrangler deploy
```

## Gotchas

1. Encode only while a game window is live. Desktop idle = no ffmpeg. 1080p titles skip GPU scale. Fast is NVENC p1 qp 26.
2. No overlay or toast. Confirm with `Tick` only.
3. F8 is a low-level hook. Elevated games need Afterimage elevated.
4. Filenames: `Sep 15 2.41 PM.mp4` via `ClipName`.
5. Site is a single no-scroll poster: Honk-locked sky `#254fb1`, sunshine wordmark `#ffe400`, white Download button. Do not add sections or a scrollbar.
6. First run relocates the exe to `%LOCALAPPDATA%\Programs\Afterimage`, writes a Start Menu shortcut and HKCU Uninstall key, then shows WelcomeForm. `Onboarded` is set only after setup succeeds. `--uninstall` removes the app and keeps clips.
7. Hotkey is click-to-bind. Mic and Fast/Quality restart the buffer. A second launch signals the running instance to show settings. GitHub Actions on `v*` tags publishes `Afterimage.exe` (and a zip of that exe). CI tries `scripts/bundle-ffmpeg.ps1` so first run can skip the download.
8. Clip cap: 50 files or 5 GB. Admin relaunch uses `runas` and a Global mutex. Signing is `scripts/sign.ps1` when `WINDOWS_CERT_PFX` is set. Use bundled/cached FFmpeg, not PATH. New installs start with Windows.
9. Out of scope: multi-monitor picker, 120 fps, HDR, AMD/Intel encode.
