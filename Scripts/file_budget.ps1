$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$budgetPath = Join-Path $root ".agent\skills\mlsmoon-self-iterate\file-budget.json"
$budget = Get-Content -Raw -Encoding UTF8 $budgetPath | ConvertFrom-Json
$limit = [int]$budget.limit
$warn = [int]$budget.warn

function Normalize-Rel([string]$path) {
    return ($path -replace '\\', '/').TrimStart('/')
}

$exceptions = @{}
foreach ($item in @($budget.exceptions)) {
    $exceptions[(Normalize-Rel $item.path)] = $item
}

$debt = @{}
foreach ($item in @($budget.debt)) {
    $debt[(Normalize-Rel $item.path)] = $item
}

$includes = @($budget.include | ForEach-Object { $_.ToLowerInvariant() })
$files = foreach ($dirName in @($budget.roots)) {
    $dir = Join-Path $root $dirName
    if (-not (Test-Path $dir)) {
        continue
    }
    Get-ChildItem -Path $dir -Recurse -File | Where-Object {
        $includes -contains $_.Extension.ToLowerInvariant()
    }
}

$failed = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$okOver = 0

foreach ($file in $files) {
    $rel = Normalize-Rel ($file.FullName.Substring($root.Length + 1))
    $lines = @(Get-Content -LiteralPath $file.FullName).Count
    $ex = $exceptions[$rel]
    $owed = $debt[$rel]

    if ($null -ne $ex) {
        $ceiling = if ($ex.ceiling) { [int]$ex.ceiling } else { [int]::MaxValue }
        if ($lines -gt $ceiling) {
            $failed.Add("EXCEPTION OVER $rel : $lines > ceiling $($ex.ceiling) ($($ex.reason))")
        }
        else {
            $okOver++
            Write-Host "exception $rel : $lines ($($ex.reason))"
        }
        continue
    }

    if ($null -ne $owed) {
        $ceiling = [int]$owed.ceiling
        if ($lines -gt $ceiling) {
            $failed.Add("DEBT GREW $rel : $lines > ceiling $ceiling; split: $($owed.split)")
        }
        elseif ($lines -le $limit) {
            $warnings.Add("DEBT CLEARED $rel : $lines <= $limit, remove from file-budget.json debt")
        }
        else {
            $warnings.Add("debt $rel : $lines / ceiling $ceiling; next touch must split: $($owed.split)")
        }
        continue
    }

    if ($lines -gt $limit) {
        $failed.Add("OVER $rel : $lines > $limit (refactor, do not add to exceptions)")
    }
    elseif ($lines -ge $warn) {
        $warnings.Add("warn $rel : $lines >= $warn")
    }
}

foreach ($item in $warnings) {
    Write-Host "WARN $item"
}

if ($failed.Count -gt 0) {
    foreach ($item in $failed) {
        Write-Host "FAIL $item"
    }
    throw "file budget failed: $($failed.Count) file(s) over limit"
}

Write-Host "file budget ok: limit $limit, $($files.Count) files, $okOver exception(s), $($warnings.Count) warn(s)"
exit 0
