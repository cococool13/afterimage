# Download ffmpeg.exe next to the project so a Release publish can ship it inside Afterimage.exe.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root "ffmpeg.exe"
if (Test-Path $dest) {
    Write-Host "ffmpeg.exe already present"
    exit 0
}

$urls = @(
    "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
)
$zip = Join-Path $env:TEMP "afterimage-ffmpeg.zip"
$extract = Join-Path $env:TEMP "afterimage-ffmpeg"
$last = $null
foreach ($url in $urls) {
    try {
        Write-Host "downloading $url"
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
        if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
        Expand-Archive $zip $extract
        $found = Get-ChildItem $extract -Recurse -Filter ffmpeg.exe |
            Where-Object { $_.DirectoryName -match '[\\/]bin$' } |
            Select-Object -First 1
        if (-not $found) {
            $found = Get-ChildItem $extract -Recurse -Filter ffmpeg.exe | Select-Object -First 1
        }
        if (-not $found) { throw "ffmpeg.exe missing from zip" }
        Copy-Item $found.FullName $dest -Force
        Write-Host "wrote $dest"
        exit 0
    }
    catch {
        $last = $_
        Write-Host "bundle failed: $($_.Exception.Message)"
    }
}
if ($last) { throw $last }
throw "ffmpeg bundle failed"
