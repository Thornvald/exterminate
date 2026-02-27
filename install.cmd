@echo off
setlocal
set "PAUSE_ON_EXIT=1"
set "FORWARD_ARGS="

:parseArgs
if "%~1"=="" goto runInstall
if /i "%~1"=="--no-pause" (
  set "PAUSE_ON_EXIT=0"
) else (
  set "FORWARD_ARGS=%FORWARD_ARGS% "%~1""
)
shift
goto parseArgs

:runInstall
echo ==========================================
echo exterminate installer
echo ==========================================
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %FORWARD_ARGS%
set "EXIT_CODE=%errorlevel%"
echo.
if "%EXIT_CODE%"=="0" (
  echo [OK] install finished.
  echo Open a NEW terminal and run:
  echo   exterminate "C:\path\to\target"
  echo   ex "C:\path\to\target"
) else (
  echo [ERROR] install failed. Exit code: %EXIT_CODE%
)

if "%PAUSE_ON_EXIT%"=="1" (
  echo.
  echo Press any key to close...
  pause >nul
)

exit /b %EXIT_CODE%
