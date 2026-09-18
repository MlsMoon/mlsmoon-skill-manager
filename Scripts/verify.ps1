$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$Rest = @($args)

function Normalize-Tag([string]$value) {
    return $value.Trim().TrimStart('-').ToLowerInvariant()
}

$tags = [System.Collections.Generic.List[string]]::new()
foreach ($item in @($Rest)) {
    $name = Normalize-Tag $item
    if ([string]::IsNullOrWhiteSpace($name) -or $name -eq "t") {
        continue
    }
    $tags.Add($name)
}

if ($tags.Count -eq 0) {
    $tags.AddRange([string[]]@("build", "catalog"))
}

if ($tags -contains "all") {
    $keep = @($tags | Where-Object { $_ -in @("gh", "ui") })
    $tags = [System.Collections.Generic.List[string]]::new()
    $tags.AddRange([string[]]@("build", "catalog", "install", "workspace", "sync", "release", "size"))
    foreach ($item in $keep) {
        if (-not ($tags -contains $item)) {
            $tags.Add($item)
        }
    }
}

function Invoke-Step([string]$name, [scriptblock]$body) {
    Write-Host "==> -t -$name"
    & $body
}

function Assert-True([bool]$ok, [string]$message) {
    if (-not $ok) {
        throw $message
    }
}

if ($tags -contains "build") {
    Invoke-Step "build" {
        dotnet build (Join-Path $root "MlsmoonSkillManager.sln") --nologo
        if ($LASTEXITCODE -ne 0) { throw "build failed" }
    }
}

if ($tags -contains "catalog") {
    Invoke-Step "catalog" {
        $path = Join-Path $root "catalog\skills.json"
        $catalog = Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json
        $skills = @($catalog.skills)
        $plugins = @($catalog.plugins)
        $packages = @($catalog.packages)
        $ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($item in $skills) {
            Assert-True (-not [string]::IsNullOrWhiteSpace($item.id)) "skill missing id"
            Assert-True ($ids.Add([string]$item.id)) "duplicate id: $($item.id)"
        }

        $companionIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($item in @($plugins) + @($packages)) {
            Assert-True (-not [string]::IsNullOrWhiteSpace($item.id)) "plugin/package missing id"
            Assert-True ($ids.Add([string]$item.id)) "duplicate id: $($item.id)"
            if ($item.installPath) {
                Assert-True ($item.installPath -notmatch '\.\.') "installPath escapes workspace: $($item.installPath)"
            }
            foreach ($companion in @($item.companionSkills)) {
                Assert-True ($companionIds.Add([string]$companion.id)) "duplicate companion id: $($companion.id)"
                $inSkills = $skills | Where-Object { $_.id -eq $companion.id }
                Assert-True (-not $inSkills) "companion $($companion.id) must not be in skills[]"
            }
        }

        $psd = $skills | Where-Object { $_.id -eq "open-psd-kit" } | Select-Object -First 1
        $model = $skills | Where-Object { $_.id -eq "3d-model-data-reader" } | Select-Object -First 1
        $spine = $plugins | Where-Object { $_.id -eq "spine-gpu-skinning" } | Select-Object -First 1
        $lan = @($plugins) + @($packages) + @($skills) | Where-Object { $_.source -eq "lan" -or $_.host }
        Assert-True ($null -ne $psd) "open-psd-kit missing"
        Assert-True ($null -ne $model) "3d-model-data-reader missing"
        Assert-True ($null -ne $spine) "spine-gpu-skinning missing"
        Assert-True ($lan.Count -eq 0) "public catalog must not contain lan host entries"
        Assert-True (@($psd.engines) -contains "all") "open-psd-kit should be all engines"
        Assert-True (@($model.engines) -contains "all") "3d-model-data-reader should be all engines"
        Assert-True ((@($spine.engines) -join ",") -eq "unity") "spine-gpu-skinning should be unity-only"
        $lanHostNeedle = @('172', '16', '20', '223') -join '.'
        $leaks = git -C $root grep -n -I -e $lanHostNeedle --
        if ($LASTEXITCODE -eq 0 -and $leaks) {
            throw "public tree must not contain LAN host:`n$leaks"
        }
        Write-Host "catalog ok: $($skills.Count) skills, $($plugins.Count) plugins, $($packages.Count) packages, $($companionIds.Count) companions"
    }
}

if ($tags -contains "install") {
    Invoke-Step "install" {
        dotnet test (Join-Path $root "tests\MlsmoonSkillManager.Tests\MlsmoonSkillManager.Tests.csproj") `
            --nologo --filter "FullyQualifiedName~CatalogAndInstallTests"
        if ($LASTEXITCODE -ne 0) { throw "install tests failed" }
    }
}

if ($tags -contains "workspace") {
    Invoke-Step "workspace" {
        dotnet test (Join-Path $root "tests\MlsmoonSkillManager.Tests\MlsmoonSkillManager.Tests.csproj") `
            --nologo --filter "FullyQualifiedName~WorkspaceBookTests"
        if ($LASTEXITCODE -ne 0) { throw "workspace tests failed" }
    }
}

if ($tags -contains "sync") {
    Invoke-Step "sync" {
        function Get-SkillGitState([bool]$Installed, [bool]$Local, [string]$Relation, [bool]$BranchDiff) {
            if (-not $Installed) { return "notinstalled" }
            $remoteHasNew = $Relation -in @("behind", "diverged")
            if ($Local -and ($remoteHasNew -or $BranchDiff)) { return "conflict" }
            if ($Local) { return "local" }
            if ($BranchDiff) { return "switch" }
            switch ($Relation) {
                "behind" { return "behind" }
                "ahead" { return "ahead" }
                "diverged" { return "diverged" }
                "unknown" { return "unclear" }
                default { return "current" }
            }
        }

        Assert-True ((Get-SkillGitState $true $false "same" $false) -eq "current") "clean should be current"
        Assert-True ((Get-SkillGitState $true $false "behind" $false) -eq "behind") "local behind remote"
        Assert-True ((Get-SkillGitState $true $false "ahead" $false) -eq "ahead") "local ahead of remote"
        Assert-True ((Get-SkillGitState $true $false "diverged" $false) -eq "diverged") "diverged history"
        Assert-True ((Get-SkillGitState $true $false "unknown" $false) -eq "unclear") "unknown ancestry"
        Assert-True ((Get-SkillGitState $true $true "same" $false) -eq "local") "dirty aligned-remote should be local"
        Assert-True ((Get-SkillGitState $true $true "ahead" $false) -eq "local") "dirty + local ahead is local, not conflict"
        Assert-True ((Get-SkillGitState $true $true "behind" $false) -eq "conflict") "dirty + remote must conflict"
        Assert-True ((Get-SkillGitState $true $true "same" $true) -eq "conflict") "dirty + branch switch must conflict"
        Assert-True ((Get-SkillGitState $true $false "same" $true) -eq "switch") "clean branch switch"

        function Get-InspectCard([bool]$Installed, [bool]$TreeMatch, [bool]$TreeCompared, [bool]$UserGit, [string]$LocalSha, [string]$Relation, [bool]$Local) {
            if (-not $Installed) { return "notinstalled" }
            if ($TreeMatch) { return "current" }
            if ([string]::IsNullOrWhiteSpace($LocalSha)) {
                $Relation = $(if ($TreeCompared -and -not $UserGit) { "behind" } else { "unknown" })
            }
            $state = Get-SkillGitState $Installed $Local $Relation $false
            if ($UserGit -and $state -in @("behind", "switch")) { return "unclear" }
            return $state
        }

        Assert-True ((Get-InspectCard $true $true $true $true "" "behind" $false) -eq "current") "aligned tree is current even with empty sha"
        Assert-True ((Get-InspectCard $true $false $false $false "" "behind" $false) -eq "unclear") "empty sha without tree compare is not behind"
        Assert-True ((Get-InspectCard $true $false $true $false "" "unknown" $false) -eq "behind") "stale folder without git can pull"
        Assert-True ((Get-InspectCard $true $false $true $true "abc" "behind" $false) -eq "unclear") "user git copy is not fast-forwarded"

        $git = Get-Command git -ErrorAction SilentlyContinue
        if (-not $git) {
            Write-Host "SKIP: git not installed"
            return
        }

        $repo = Join-Path ([System.IO.Path]::GetTempPath()) ("msm-sync-" + [guid]::NewGuid().ToString("N"))
        $left = Join-Path $repo "left"
        $right = Join-Path $repo "right"
        New-Item -ItemType Directory -Path $left | Out-Null
        try {
            git -C $left init -q
            if ($LASTEXITCODE -ne 0) { throw "git init failed" }
            Set-Content -Path (Join-Path $left "SKILL.md") -Value "base" -Encoding utf8
            git -C $left add SKILL.md
            git -C $left -c user.email="dev@mlsmoon.local" -c user.name="verify" commit -qm "init"
            git -C $left branch -M master
            git -C $left branch other
            $heads = @(git -C $left ls-remote --heads $left) -join "`n"
            if ($LASTEXITCODE -ne 0) { throw "git ls-remote failed" }
            Assert-True ($heads.Contains("refs/heads/master")) "ls-remote missing master"
            Assert-True ($heads.Contains("refs/heads/other")) "ls-remote missing other"
            New-Item -ItemType Directory -Path $right | Out-Null
            Copy-Item (Join-Path $left "SKILL.md") (Join-Path $right "SKILL.md")
            Set-Content -Path (Join-Path $right "SKILL.md") -Value "edited" -Encoding utf8
            Set-Content -Path (Join-Path $right "extra.md") -Value "new" -Encoding utf8
            $prev = $ErrorActionPreference
            $ErrorActionPreference = "Continue"
            $diff = git diff --no-index --name-status -- $left $right 2>&1 | Out-String
            $ErrorActionPreference = $prev
            Assert-True ($diff -match "SKILL.md") "diff should see SKILL.md"
            Assert-True ($diff -match "extra.md") "diff should see extra.md"

            $align = Join-Path $repo "align"
            New-Item -ItemType Directory -Path $align | Out-Null
            git -C $align init -q
            Set-Content -Path (Join-Path $align "SKILL.md") -Value "v1" -Encoding utf8
            git -C $align add SKILL.md
            git -C $align -c user.email="dev@mlsmoon.local" -c user.name="verify" commit -qm "v1"
            $old = (git -C $align rev-parse HEAD).Trim()
            Set-Content -Path (Join-Path $align "SKILL.md") -Value "v2" -Encoding utf8
            Set-Content -Path (Join-Path $align "new.md") -Value "added" -Encoding utf8
            git -C $align add SKILL.md new.md
            git -C $align -c user.email="dev@mlsmoon.local" -c user.name="verify" commit -qm "v2"
            $tip = (git -C $align rev-parse HEAD).Trim()
            git -C $align reset --soft $old
            if ($LASTEXITCODE -ne 0) { throw "git reset --soft failed" }
            git -C $align diff --quiet $tip
            Assert-True ($LASTEXITCODE -eq 0) "working tree can match remote tip while HEAD is old"
            Assert-True (((git -C $align rev-parse HEAD).Trim()) -eq $old) "HEAD should stay on old commit"
            Write-Host "sync ok: conflict policy + git ls-remote/diff + tree align"
        }
        finally {
            if (Test-Path $repo) {
                Remove-Item -Recurse -Force $repo -ErrorAction SilentlyContinue
            }
        }
    }
}

if ($tags -contains "gh") {
    Invoke-Step "gh" {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if (-not $gh) {
            Write-Host "SKIP: gh not installed"
            return
        }
        $path = Join-Path $root "catalog\skills.json"
        $catalog = Get-Content -Raw -Encoding UTF8 $path | ConvertFrom-Json
        $github = @($catalog.skills) + @($catalog.plugins) | Where-Object { $_.source -ne "lan" -and -not $_.host }
        $repos = @($github | ForEach-Object { $_.repo }) | Where-Object { $_ } | Select-Object -Unique
        foreach ($repo in $repos) {
            if ($repo -notmatch 'github\.com[:/]([^/]+)/([^/.]+)') {
                throw "not a github repo: $repo"
            }
            $owner = $Matches[1]
            $name = $Matches[2]
            Write-Host "gh repo view $owner/$name"
            gh repo view "$owner/$name" --json name,visibility,isPrivate
            if ($LASTEXITCODE -ne 0) {
                throw "gh repo view failed: $owner/$name"
            }
        }
    }
}

if ($tags -contains "release") {
    Invoke-Step "release" {
        $version = (Get-Content -Raw (Join-Path $root "VERSION")).Trim()
        Assert-True ($version -match '^\d+\.\d+\.\d+$') "VERSION must be MAJOR.MINOR.PATCH, got '$version'"
        $workflow = Get-Content -Raw (Join-Path $root ".github\workflows\release.yml")
        Assert-True ($workflow -match 'origin/release') "release.yml must require tag on origin/release"
        Assert-True ($workflow -match 'v\*\.\*\.\*') "release.yml must trigger on v*.*.* tags"
        Assert-True ($workflow -match 'MlsmoonSkillManager-Setup-') "release.yml must upload Setup exe"
        Assert-True ($workflow -match 'release_notes\.ps1') "release.yml must extract notes from CHANGELOG.md"
        Assert-True ($workflow -match 'body_path:') "release.yml must publish CHANGELOG text as the Release body"
        Assert-True ($workflow -notmatch 'generate_release_notes:\s*true') "release.yml must not use GitHub auto compare notes"
        $iss = Get-Content -Raw (Join-Path $root "Scripts\installer.iss")
        Assert-True ($iss -match '(?m)^DisableDirPage=no\s*$') "installer.iss must keep DisableDirPage=no so Setup shows the folder page"
        Assert-True ($iss -notmatch 'Excludes: "skills\.override\.json"') "local pack must include catalog/skills.override.json when it exists"
        $build = Get-Content -Raw (Join-Path $root "Scripts\build.ps1")
        Assert-True ($build -notmatch 'Remove-Item \$privateOverride') "build.ps1 must keep a local skills.override.json"
        $notes = Join-Path $env:TEMP "mlsmoon-release-notes-$version.md"
        & (Join-Path $root "Scripts\release_notes.ps1") -Version $version -Output $notes
        if (-not (Test-Path $notes)) { throw "release_notes.ps1 did not write $notes" }
        $notesText = Get-Content -Raw -Encoding UTF8 $notes
        Assert-True ($notesText.Trim().Length -ge 40) "CHANGELOG section $version is too short"
        Assert-True ($notesText -notmatch '(?m)^\s*(\*\*)?Full Changelog(\*\*)?\s*:') "CHANGELOG section must not be a Full Changelog compare link"
        Write-Host "VERSION $version"
    }
}

if ($tags -contains "size") {
    Invoke-Step "size" {
        & (Join-Path $root "Scripts\file_budget.ps1")
        if ($LASTEXITCODE -ne 0) { throw "file budget failed" }
    }
}

if ($tags -contains "ui") {
    Invoke-Step "ui" {
        $python = Get-Command python -ErrorAction SilentlyContinue
        if (-not $python) { throw "python is required for -t -ui" }
        python (Join-Path $root "Scripts\ui_flow.py")
        if ($LASTEXITCODE -ne 0) { throw "ui_flow.py failed" }
    }
}

Write-Host "verify ok: $($tags -join ', ')"
