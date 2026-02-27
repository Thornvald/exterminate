@echo off
setlocal
"%~dp0exterminate.cmd" %*
exit /b %errorlevel%
