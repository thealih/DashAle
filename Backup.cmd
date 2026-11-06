@echo off
pushd "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Run Install.cmd first.
  popd
  pause
  exit /b 1
)
".venv\Scripts\python.exe" -X utf8 scripts/backup_sqlite.py
popd
pause
