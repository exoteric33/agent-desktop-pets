@echo off
rem aipets entfernen (Doppelklick): beendet aipets, schaltet "Mit Windows starten" aus und entfernt
rem die Status-Hooks aus Claude Code, Codex und Hermes. Den Ordner danach einfach loeschen.
setlocal
cd /d "%~dp0"
if not exist "%~dp0aipets.exe" (
  echo aipets.exe fehlt, es gibt nichts zu entfernen.
  pause
  exit /b 1
)
"%~dp0aipets.exe" --uninstall
