@echo off
rem Install aipets (double-click): builds aipets.exe with the C# compiler that ships with Windows, turns on
rem "Start with Windows", adds the status hooks for Claude Code, Codex, Hermes and Cursor
rem (only for installed agents), approves them and starts aipets.
setlocal
cd /d "%~dp0"
if exist "%~dp0src\Program.cs" (
  echo Building aipets ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo.
    echo The build failed, details are above.
    pause
    exit /b 1
  )
)
if not exist "%~dp0aipets.exe" (
  echo aipets.exe is missing. Please extract the complete release ZIP.
  pause
  exit /b 1
)
echo Setting up aipets ...
start "" /wait "%~dp0aipets.exe" --install
exit /b %errorlevel%
