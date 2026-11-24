@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0dotnet-panel.ps1" -Action Start
if errorlevel 1 (
  echo.
  echo Could not start. Run Install.cmd first or read README.md.
  pause
  exit /b 1
)
start "" "http://127.0.0.1:8000/"
