param(
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
Set-Location $repo

$fail = 0
function Write-Ok([string]$msg) { Write-Host "OK    $msg" }
function Write-Fail([string]$msg) { Write-Host "FAIL  $msg"; $script:fail++ }

$skillDir = Join-Path $repo '.agent\skills'
$skillTarget = '..\.agent\skills'
$mdTarget = 'AGENTS.md'
$skillRoots = @('.claude', '.grok')
$mdLinks = @('CLAUDE.md', 'GROK.md')

function Get-LinkItem([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        return $null
    }

    return Get-Item -LiteralPath $path -Force
}

function Get-ResolvedLinkTarget([string]$path) {
    $item = Get-LinkItem $path
    if (-not $item) {
        return $null
    }

    if ($item.LinkType -notin @('SymbolicLink', 'Junction')) {
        return $null
    }

    $raw = [string]$item.Target
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    if ([IO.Path]::IsPathRooted($raw)) {
        return [IO.Path]::GetFullPath($raw)
    }

    return [IO.Path]::GetFullPath((Join-Path (Split-Path $item.FullName) $raw))
}

function Test-DirLink([string]$path) {
    $actual = Get-ResolvedLinkTarget $path
    return $actual -and ($actual -eq $skillDir)
}

function Test-FileLink([string]$path) {
    $item = Get-LinkItem $path
    if (-not $item) {
        return $false
    }

    if ($item.LinkType -eq 'SymbolicLink') {
        $raw = [string]$item.Target
        return $raw -eq $mdTarget -or $raw -eq ".\$mdTarget"
    }

    $listed = & cmd.exe /c "fsutil hardlink list `"$path`"" 2>$null
    return $LASTEXITCODE -eq 0 -and ($listed | Where-Object { $_ -match '\\AGENTS\.md$' })
}

function Remove-Reparse([string]$path) {
    $item = Get-LinkItem $path
    if ($null -eq $item) {
        return
    }

    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) {
        throw "refusing to delete non-link $path"
    }

    if ($item.PSIsContainer) {
        & cmd.exe /c "rmdir `"$path`""
        if ($LASTEXITCODE -ne 0) {
            throw "could not remove directory link $path"
        }

        return
    }

    Remove-Item -LiteralPath $path -Force
}

function Invoke-Mklink([string]$workingDirectory, [string]$command) {
    Push-Location $workingDirectory
    try {
        & cmd.exe /c $command
        return $LASTEXITCODE -eq 0
    }
    finally {
        Pop-Location
    }
}

function New-DirLink([string]$path) {
    $parent = Split-Path $path
    New-Item -ItemType Directory -Force -Path $parent | Out-Null
    if (Test-DirLink $path) {
        return
    }

    $item = Get-LinkItem $path
    if ($item) {
        Remove-Reparse $path
    }

    $name = Split-Path $path -Leaf
    if (Invoke-Mklink $parent "mklink /D `"$name`" `"$skillTarget`"") {
        return
    }

    if (Invoke-Mklink $parent "mklink /J `"$name`" `"$skillTarget`"") {
        return
    }

    throw "need Developer Mode, admin, or junction permission"
}

function New-FileLink([string]$path) {
    if (Test-FileLink $path) {
        return
    }

    $item = Get-LinkItem $path
    if ($item) {
        Remove-Reparse $path
    }

    $name = Split-Path $path -Leaf
    if (Invoke-Mklink $repo "mklink `"$name`" `"$mdTarget`"") {
        return
    }

    if (Invoke-Mklink $repo "mklink /H `"$name`" `"$mdTarget`"") {
        return
    }

    throw "need Developer Mode, admin, or hardlink permission"
}

Write-Host "repo=$repo  mode=$(if ($CheckOnly) { 'check' } else { 'init' })"

foreach ($root in $skillRoots) {
    $link = Join-Path $repo "$root\skills"
    if (Test-DirLink $link) {
        Write-Ok "$root/skills -> $skillTarget"
        continue
    }

    if ($CheckOnly) {
        Write-Fail "$root/skills missing or not pointing at $skillTarget"
        continue
    }

    try {
        New-DirLink $link
        if (Test-DirLink $link) {
            Write-Ok "$root/skills -> $skillTarget"
        }
        else {
            Write-Fail "could not create $root/skills"
        }
    }
    catch {
        Write-Fail "$root/skills $($_.Exception.Message)"
    }
}

foreach ($name in $mdLinks) {
    $link = Join-Path $repo $name
    if (Test-FileLink $link) {
        Write-Ok "$name -> $mdTarget"
        continue
    }

    if ($CheckOnly) {
        Write-Fail "$name missing or not pointing at $mdTarget"
        continue
    }

    try {
        New-FileLink $link
        if (Test-FileLink $link) {
            Write-Ok "$name -> $mdTarget"
        }
        else {
            Write-Fail "could not create $name"
        }
    }
    catch {
        Write-Fail "$name $($_.Exception.Message)"
    }
}

if ($fail -gt 0) {
    Write-Host "DONE  $fail FAIL"
    exit 1
}

Write-Host "DONE  all required checks passed"
exit 0
