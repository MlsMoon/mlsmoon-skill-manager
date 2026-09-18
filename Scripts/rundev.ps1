param(
    [string]$Configuration = "Debug",
    [int]$WaitPid = 0
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$project = Join-Path $root "src\MlsmoonSkillManager.App\MlsmoonSkillManager.App.csproj"
$runArgs = @("--project", $project, "-c", $Configuration)

if ($WaitPid -gt 0) {
    $previous = Get-Process -Id $WaitPid -ErrorAction SilentlyContinue
    if ($null -ne $previous) {
        Write-Host "Waiting for previous DEV (PID $WaitPid) to exit..."
        $previous.WaitForExit()
    }

    $code = 1
    foreach ($try in 1..8) {
        & dotnet build $project -c $Configuration --nologo
        $code = $LASTEXITCODE
        if ($code -eq 0) {
            break
        }

        Write-Host "Build failed (exit $code), waiting to unlock output ($try/8)..."
        Start-Sleep -Seconds 1
    }

    if ($code -ne 0) {
        exit $code
    }

    $runArgs += "--no-build"
}

& dotnet run @runArgs -- --dev
