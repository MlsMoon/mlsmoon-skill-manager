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

$hints = [System.Collections.Generic.List[string]]::new()
$okOver = 0

foreach ($file in $files) {
    $rel = Normalize-Rel ($file.FullName.Substring($root.Length + 1))
    $lines = @(Get-Content -LiteralPath $file.FullName).Count
    $ex = $exceptions[$rel]
    $owed = $debt[$rel]

    if ($null -ne $ex) {
        $ceiling = if ($ex.ceiling) { [int]$ex.ceiling } else { [int]::MaxValue }
        if ($lines -gt $ceiling) {
            $hints.Add("exception $rel : $lines > $($ex.ceiling) ($($ex.reason))")
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
            $hints.Add("grew $rel : $lines > last noted $ceiling; split when mixed: $($owed.split)")
        }
        elseif ($lines -le $limit) {
            $hints.Add("cleared $rel : $lines <= $limit, can drop from file-budget.json debt")
        }
        else {
            $hints.Add("long $rel : $lines; split when mixed: $($owed.split)")
        }
        continue
    }

    if ($lines -gt $limit) {
        $hints.Add("over $rel : $lines > $limit (hint only; split if mixed duties, keep readable)")
    }
    elseif ($lines -ge $warn) {
        $hints.Add("near $rel : $lines >= $warn")
    }
}

foreach ($item in $hints) {
    Write-Host "HINT $item"
}

Write-Host "file budget hint: guide $limit, $($files.Count) files, $okOver exception(s), $($hints.Count) hint(s)"
exit 0
