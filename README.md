# aipets

Pixel-Art-Desktop-Pets für Windows, eins pro KI-Agent. Die Pets sitzen auf der Taskleiste. Ein Klick öffnet ihren Agenten im Terminal, und sie zeigen an, ob er gerade arbeitet, auf dich wartet oder fertig ist.

| Pet | Klick öffnet | Status kommt von |
|---|---|---|
| **Claude** | Claude Code im Windows Terminal | Claude-Code-Hooks |
| **Hermes** | Hermes Agent in PowerShell (im Windows Terminal) | Hermes-Shell-Hooks |
| **Astra** | Codex CLI im Windows Terminal | Codex-Hooks |

## Schnellstart

1. **Bauen:** `.\build.ps1` erzeugt `aipets.exe` mit dem C#-Compiler, der in Windows eingebaut ist. Installieren musst du nichts.
   `.\build.ps1 -Art` erzeugt vorher App-Icon und Sprites neu. Dafür brauchst du Python mit `numpy` und `Pillow`.
2. **Starten:** `aipets.exe` doppelklicken. Die exe muss neben dem Ordner `pets\` liegen.
   Im Infobereich der Taskleiste (bei den ausgeblendeten Symbolen hinter `^`) erscheint das aipets-Icon:
   - **Linksklick:** Einstellungen. Dort kannst du Pets ein- und ausblenden und Größe, Programm, Argumente, Terminal und Arbeitsordner einstellen. Unten schaltest du „Mit Windows starten“ ein.
   - **Rechtsklick:** Menü mit allen Pets, Autostart und Beenden.
3. **Status-Anzeige:** Hooks eintragen, siehe unten.

**Hintergrund:** Das Tray-Programm startet jedes Pet als eigenen Prozess (`aipets.exe --pet <id>`). Stürzt ein Pet ab oder wird es beendet, startet das Tray-Programm es neu. Beendest du das Tray-Programm, verschwinden auch die Pets.

**Pet-Rechtsklick:** öffnen, Arbeitsordner, Größe, zurück in die Ecke, ausblenden, Einstellungen, aipets beenden.

Ein Klick startet Claude Code mit `--dangerously-skip-permissions`, Hermes mit `--yolo` und Codex mit `--dangerously-bypass-approvals-and-sandbox`. Das kannst du in den Einstellungen oder in `pets\<id>\pet.ini` ändern.

## Hooks für Claude Code

In `%USERPROFILE%\.claude\settings.json` eintragen und den Pfad anpassen. Danach einmal `/hooks` öffnen oder Claude Code neu starten.

```json
"hooks": {
  "UserPromptSubmit":   [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "working"], "timeout": 10 }] }],
  "PostToolUse":        [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "resume"], "async": true }] }],
  "PostToolUseFailure": [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "resume"], "async": true }] }],
  "Notification":       [{ "matcher": "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input", "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "waiting"], "timeout": 10 }] }],
  "Stop":               [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "done"], "timeout": 10 }] }],
  "SessionEnd":         [{ "hooks": [{ "type": "command", "command": "C:\\Pfad\\zu\\aipets.exe", "args": ["--hook", "claude", "end"], "timeout": 10 }] }]
}
```

## Hooks für Hermes Agent

In die `config.yaml` von Hermes eintragen. Unter Windows liegt sie in `%HERMES_HOME%`, sonst in `~/.hermes/`. Pfade in einfachen Anführungszeichen, sonst liest YAML die Backslashes als Escapes.

```yaml
hooks:
  pre_llm_call:
    - command: 'C:\Pfad\zu\aipets.exe --hook hermes working'
      timeout: 10
  pre_approval_request:
    - command: 'C:\Pfad\zu\aipets.exe --hook hermes waiting'
      timeout: 10
  post_approval_response:
    - command: 'C:\Pfad\zu\aipets.exe --hook hermes resume'
      timeout: 10
  on_session_end:
    - command: 'C:\Pfad\zu\aipets.exe --hook hermes done'
      timeout: 10
  on_session_finalize:
    - command: 'C:\Pfad\zu\aipets.exe --hook hermes end'
      timeout: 10
```

Hermes fragt beim nächsten Start einmal pro Hook, ob er laufen darf. Alternativ startest du Hermes einmal mit `hermes --accept-hooks`. Prüfen kannst du das mit `hermes hooks list`.

## Hooks für Codex

In `%USERPROFILE%\.codex\hooks.json` eintragen (bzw. `%CODEX_HOME%\hooks.json`) und den Pfad anpassen. Codex startet Hook-Befehle unter Windows mit PowerShell, deshalb `& '…'` und `| Out-Null`: Ohne `Out-Null` wartet PowerShell nicht auf `aipets.exe`.

```json
{
  "hooks": {
    "UserPromptSubmit":  [{ "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex working | Out-Null", "timeout": 10, "async": true }] }],
    "PermissionRequest": [{ "matcher": "*", "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex waiting | Out-Null", "timeout": 10, "async": true }] }],
    "PostToolUse":       [{ "matcher": "*", "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex resume | Out-Null", "timeout": 10, "async": true }] }],
    "Stop":              [{ "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex done | Out-Null", "timeout": 10, "async": true }] }],
    "Interrupt":         [{ "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex idle | Out-Null", "timeout": 3, "async": true }] }],
    "SessionEnd":        [{ "hooks": [{ "type": "command", "command": "& 'C:\\Pfad\\zu\\aipets.exe' --hook codex end | Out-Null", "timeout": 3 }] }]
  }
}
```

Codex führt neue Hooks erst aus, wenn du sie freigegeben hast: In Codex `/hooks` öffnen und die sechs aipets-Hooks als vertrauenswürdig markieren. Änderst du einen Befehl (z. B. weil die exe woanders liegt), fragt Codex erneut.

Ob die Hooks eingerichtet sind, zeigen auch die Einstellungen unter „Statusanzeige“.

## Mehr

In [PET-BAUANLEITUNG.md](PET-BAUANLEITUNG.md) findest du Aufbau, Art-Pipeline, Status-Protokoll, eine Checkliste für neue Pets und bekannte Stolperfallen.

> Die Bildvorlagen in `pets/*/art/` sind Fan-Art fremder Artists bzw. nicht selbst gezeichnet. Halte das Repo privat oder ersetze die Bilder (samt `sprites/`), bevor du es veröffentlichst.
