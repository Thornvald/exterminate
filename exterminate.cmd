@echo off
setlocal
if exist "%LOCALAPPDATA%\Exterminate\.uninstalling" (
  echo [INFO] exterminate uninstall is in progress.
  echo Try again in a moment or run install.cmd.
  exit /b 1
)

if exist "%LOCALAPPDATA%\Exterminate\exterminate.exe" (
  "%LOCALAPPDATA%\Exterminate\exterminate.exe" %*
  exit /b %errorlevel%
)

if exist "%~dp0exterminate.exe" (
  "%~dp0exterminate.exe" %*
  exit /b %errorlevel%
)

echo [ERROR] exterminate is not installed on this system.
echo Run "install.cmd" from the project folder, then open a new terminal.
exit /b 1
