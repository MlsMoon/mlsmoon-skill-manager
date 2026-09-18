@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0link-skill-roots.ps1" %*
exit /b %ERRORLEVEL%
