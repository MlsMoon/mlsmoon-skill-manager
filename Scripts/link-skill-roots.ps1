$script = Join-Path $PSScriptRoot '..\.agent\skills\mlsmoon-init\scripts\init.ps1'
& $script @args
exit $LASTEXITCODE
