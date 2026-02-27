@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0exterminate.ps1" %*
exit /b %errorlevel%
