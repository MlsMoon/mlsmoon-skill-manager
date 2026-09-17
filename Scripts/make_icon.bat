@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make_icon.ps1" %*
exit /b %ERRORLEVEL%
