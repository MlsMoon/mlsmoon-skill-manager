@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0file_budget.ps1" %*
exit /b %ERRORLEVEL%
