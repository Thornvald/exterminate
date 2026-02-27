@echo off
setlocal
if exist "%LOCALAPPDATA%\Exterminate\exterminate.exe" (
  "%LOCALAPPDATA%\Exterminate\exterminate.exe" %*
  exit /b %errorlevel%
)

if exist "%~dp0exterminate.exe" (
  "%~dp0exterminate.exe" %*
  exit /b %errorlevel%
)

if exist "%~dp0dist\win-x64\exterminate.exe" (
  "%~dp0dist\win-x64\exterminate.exe" %*
  exit /b %errorlevel%
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0exterminate.ps1" %*
exit /b %errorlevel%
