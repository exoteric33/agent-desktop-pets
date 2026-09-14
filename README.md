# ClaudePet

Pixel-Art-Desktop-Pet für Windows. Sie steht auf der Taskleiste, öffnet per Klick Claude Code im Windows Terminal und zeigt an, ob Claude Code gerade arbeitet, auf dich wartet oder fertig ist.

## Schnellstart

1. **Bauen:** `.\build.ps1` erzeugt `ClaudePet.exe` mit dem C#-Compiler, der in Windows eingebaut ist; installieren musst du nichts.
   `.\build.ps1 -Art` erzeugt vorher die Sprites aus `art\source.png` neu. Dafür brauchst du Python mit `numpy` und `Pillow`.
2. **Starten:** `ClaudePet.exe` doppelklicken, dann Rechtsklick auf das Pet → „Mit Windows starten“.
3. **Status-Anzeige:** folgende Hooks in `%USERPROFILE%\.claude\settings.json` eintragen und den Pfad anpassen. Danach einmal `/hooks` öffnen oder Claude Code neu starten.

```json
"hooks": {
  "UserPromptSubmit":   [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "working"], "timeout": 10 }] }],
  "PostToolUse":        [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "resume"], "async": true }] }],
  "PostToolUseFailure": [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "resume"], "async": true }] }],
  "Notification":       [{ "matcher": "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "waiting"], "timeout": 10 }] }],
  "Stop":               [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "done"], "timeout": 10 }] }],
  "SessionEnd":         [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\ClaudePet.exe", "args": ["--hook", "end"], "timeout": 10 }] }]
}
```

Ein Klick startet Claude Code mit `--dangerously-skip-permissions`. Das kannst du über `ClaudeArgs` in `src\Launcher.cs` ändern.

## Mehr

In [PET-BAUANLEITUNG.md](PET-BAUANLEITUNG.md) findest du Aufbau, Art-Pipeline, Status-Protokoll, eine Checkliste für neue Pets und bekannte Stolperfallen.

> `art/source.png` basiert auf Fan-Art eines fremden Artists. Halte das Repo privat oder ersetze das Bild (samt `art/build`), bevor du es veröffentlichst.
