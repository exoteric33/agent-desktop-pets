@echo off
rem aipets installieren (Doppelklick): baut aipets.exe mit dem C#-Compiler aus Windows, schaltet
rem "Mit Windows starten" ein, traegt die Status-Hooks fuer Claude Code, Codex und Hermes ein
rem (nur fuer installierte Agents), gibt sie frei und startet aipets.
setlocal
cd /d "%~dp0"
if exist "%~dp0src\Program.cs" (
  echo aipets wird gebaut ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo.
    echo Bauen ist fehlgeschlagen, Details stehen oben.
    pause
    exit /b 1
  )
)
if not exist "%~dp0aipets.exe" (
  echo aipets.exe fehlt. Bitte das vollstaendige Release-ZIP entpacken.
  pause
  exit /b 1
)
echo aipets wird eingerichtet ...
start "" /wait "%~dp0aipets.exe" --install
exit /b %errorlevel%
