@echo off
setlocal
cd /d "%~dp0.."
python "%~dp0ui_flow.py" %*
exit /b %ERRORLEVEL%
