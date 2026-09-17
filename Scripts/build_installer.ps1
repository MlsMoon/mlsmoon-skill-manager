param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Version) {
    $Version = (Get-Content -Raw (Join-Path $root "VERSION")).Trim()
}

$exe = Join-Path $root "dist\MlsmoonSkillManager.exe"
if (-not (Test-Path $exe)) {
    throw "Missing dist\MlsmoonSkillManager.exe. Run Scripts\build.ps1 first."
}

$iscc = @(
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install from https://jrsoftware.org/isinfo.php"
}

& $iscc "/DMyAppVersion=$Version" (Join-Path $root "Scripts\installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Write-Host "installer: dist\MlsmoonSkillManager-Setup-$Version.exe"
