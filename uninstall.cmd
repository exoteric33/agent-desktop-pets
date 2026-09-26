@echo off
rem Uninstall aipets (double-click): quits aipets, turns "Start with Windows" off and removes
rem the status hooks from Claude Code, Codex, Hermes and Cursor. Then just delete the folder.
setlocal
cd /d "%~dp0"
if not exist "%~dp0aipets.exe" (
  echo aipets.exe is missing, there is nothing to remove.
  pause
  exit /b 1
)
start "" /wait "%~dp0aipets.exe" --uninstall
exit /b %errorlevel%
