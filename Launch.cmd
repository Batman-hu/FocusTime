@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\Build.ps1" -Run
if errorlevel 1 (
  echo.
  echo FocusTime could not start. Please send build-log.txt for diagnosis.
  pause
)
endlocal
