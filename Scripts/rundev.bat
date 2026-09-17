@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0rundev.ps1" %*
exit /b %ERRORLEVEL%
