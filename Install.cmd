@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1"
if errorlevel 1 (
  echo.
  echo Installation failed. Read the error above and the troubleshooting section in README.md.
  pause
  exit /b 1
)
pause
