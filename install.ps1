# Install Afterimage to %LOCALAPPDATA%\Programs\Afterimage and pin a Start Menu shortcut.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA "Programs\Afterimage"

Push-Location $root
try {
    if (Test-Path (Join-Path $root "scripts\bundle-ffmpeg.ps1")) {
        try { & (Join-Path $root "scripts\bundle-ffmpeg.ps1") } catch { Write-Host "ffmpeg bundle skipped" }
    }
    dotnet publish Afterimage.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $dest
} finally {
    Pop-Location
}

$exe = Join-Path $dest "Afterimage.exe"
$programs = [Environment]::GetFolderPath("Programs")
$lnk = Join-Path $programs "Afterimage.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($lnk)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $dest
$shortcut.IconLocation = $exe
$shortcut.Save()

Start-Process $exe
Write-Host "Installed. Afterimage is in the Start Menu."
