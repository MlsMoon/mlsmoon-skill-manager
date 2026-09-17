param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$path = Join-Path $root "CHANGELOG.md"
if (-not (Test-Path $path)) {
    throw "CHANGELOG.md is missing"
}

$raw = [System.IO.File]::ReadAllText($path)
$raw = $raw -replace "`r`n", "`n"
$escaped = [regex]::Escape($Version)
$match = [regex]::Match(
    $raw,
    "(?m)^## \[$escaped\][^\n]*\n(.*?)(?=^## \[|\z)",
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

if (-not $match.Success) {
    throw "CHANGELOG.md has no ## [$Version] section"
}

$body = $match.Groups[1].Value.Trim()
if ([string]::IsNullOrWhiteSpace($body)) {
    throw "CHANGELOG.md section $Version is empty"
}

if ($body -match '(?im)^\s*(\*\*)?Full Changelog(\*\*)?\s*:') {
    throw "CHANGELOG.md section $Version must not be only a Full Changelog compare link"
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    Write-Output $body
    return
}

$parent = Split-Path -Parent $Output
if ($parent -and -not (Test-Path $parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$full = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path (Get-Location) $Output }
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($full, $body + "`n", $utf8)
exit 0
