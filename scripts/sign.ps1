# Authenticode-sign Afterimage.exe when WINDOWS_CERT_PFX is set (base64 PFX).
# Does not print cert or password.
param(
    [Parameter(Mandatory = $true)]
    [string] $Path
)

$ErrorActionPreference = "Stop"
if (-not $env:WINDOWS_CERT_PFX) {
    Write-Host "No WINDOWS_CERT_PFX; skip sign."
    exit 0
}
if (-not (Test-Path $Path)) { throw "missing $Path" }

$pfx = Join-Path $env:TEMP "afterimage-sign.pfx"
try {
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:WINDOWS_CERT_PFX))
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $signtool) { throw "signtool.exe not found" }
    $args = @(
        "sign", "/fd", "SHA256", "/td", "SHA256",
        "/tr", "http://timestamp.digicert.com",
        "/f", $pfx
    )
    if ($env:WINDOWS_CERT_PASSWORD) { $args += @("/p", $env:WINDOWS_CERT_PASSWORD) }
    $args += $Path
    & $signtool @args
    if ($LASTEXITCODE -ne 0) { throw "signtool failed" }
    Write-Host "signed $Path"
}
finally {
    if (Test-Path $pfx) { Remove-Item $pfx -Force }
}
