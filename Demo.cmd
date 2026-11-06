@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0launch.ps1" -Demo
if errorlevel 1 (
  echo Could not start the demo. Run Install.cmd first.
  pause
  exit /b 1
)
start "" "http://127.0.0.1:8001/"
