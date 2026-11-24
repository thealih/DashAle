@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0dotnet-panel.ps1" -Action Backup
if errorlevel 1 echo Backup failed. Read the error above.
pause
