param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet run --project (Join-Path $root "src\MlsmoonSkillManager.App\MlsmoonSkillManager.App.csproj") `
    -c $Configuration `
    -- --dev
