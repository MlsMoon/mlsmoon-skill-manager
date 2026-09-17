param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -Raw (Join-Path $root "VERSION")).Trim()
$dist = Join-Path $root "dist"
$publish = Join-Path $dist "app"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Push-Location $root
try {
    dotnet test (Join-Path $root "MlsmoonSkillManager.sln") -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw "tests failed" }

    dotnet publish (Join-Path $root "src\MlsmoonSkillManager.App\MlsmoonSkillManager.App.csproj") `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=embedded `
        -p:Version=$version `
        -p:InformationalVersion=$version `
        -o $publish
    if ($LASTEXITCODE -ne 0) { throw "publish failed" }

    Copy-Item (Join-Path $publish "MlsmoonSkillManager.exe") (Join-Path $dist "MlsmoonSkillManager.exe") -Force
    Copy-Item (Join-Path $root "catalog") (Join-Path $dist "catalog") -Recurse -Force
    if (Test-Path (Join-Path $dist "catalog\skills.override.json")) {
        Write-Host "portable: included local catalog/skills.override.json"
    }
    Write-Host "portable: dist\MlsmoonSkillManager.exe ($version)"
}
finally {
    Pop-Location
}
