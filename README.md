# aipets

*Agent desktop pets for Windows — pixel-art desktop pets, one per AI agent: Claude Code, Codex, Cursor, Copilot, Gemini, Grok, Hermes. Documentation in German.*

Pixel-Art-Desktop-Pets für Windows, eins pro KI-Agent. Die Pets sitzen auf der Taskleiste. Ein Klick öffnet ihren Agenten im Terminal, als Desktop-App oder seine Website im Browser, und sie zeigen an, ob er gerade arbeitet, auf dich wartet oder fertig ist.

| Pet | Klick öffnet (Standard) | Umschaltbar auf | Status kommt von |
|---|---|---|---|
| **Claude** | Claude Code im Windows Terminal | Claude-Desktop-App, claude.ai | Claude-Code-Hooks |
| **Hermes** | Hermes Agent in PowerShell (im Windows Terminal) | Hermes Desktop, hermes-agent.nousresearch.com | Hermes-Shell-Hooks |
| **Astra** | Codex CLI im Windows Terminal | Codex-Desktop-App (unter Windows „ChatGPT“), chatgpt.com/codex | Codex-Hooks |
| **Gemini** | gemini.google.com im Standardbrowser | Gemini CLI (`gemini`) | – |
| **Grok** | grok.com im Standardbrowser | Grok CLI (`grok`) | – |
| **Cursor** | Cursor-Desktop-App | Cursor im Terminal (`cursor`), cursor.com | Cursor-Hooks |
| **Copilot** | Microsoft-Copilot-Desktop-App | copilot.microsoft.com | – |

**Cursor in zwei Optiken:** Unter **Einstellungen → Cursor → Aussehen** oder **Rechtsklick auf Cursor → Aussehen** wählst du **Pixel** (Standard, wie Grok) oder **Original** (fein aufgelöstes Entwurfsbild). Beide sind animiert: Atmen, wehende Haare, Blinzeln, Zwinkern beim Hover, eine kleine Fingerbewegung, Klickfreude und Schlafen. Der Wechsel wirkt sofort und behält Position, Höhe und Sichtbarkeit. Es bleibt ein einziges Pet. Den Status bekommt Cursor über Cursors Hooks (siehe unten); ein „?“ für „wartet auf dich“ gibt es bei Cursor nicht, weil Cursor dafür kein Ereignis meldet.

**Copilot in zwei Optiken:** Entwurf 1 mit türkisen Haaren, weißem Shirt und Winken. Unter **Einstellungen → Copilot → Aussehen** oder **Rechtsklick → Aussehen** wählst du **Pixel** (Standard, passend zu Grok und Cursor) oder **Original**. Beide Optiken animieren Atmen, Haare, Blinzeln, ein Hover-Lächeln mit beiden geschlossenen Augen und Winken, Klickfreude mit Hüpfer sowie Schlafen. Das Shirtlogo folgt als Stoffdruck der Schattierung und Bewegung des Shirts. Position, Höhe, Sichtbarkeit und Ebenen bleiben beim Wechsel erhalten. Ein Klick öffnet die Microsoft-Copilot-App; alternativ wählst du die Website. Es gibt keine eingerichtete Statusquelle und keinen voreingestellten Terminalbefehl; das allgemeine Feld „Programm“ bleibt für einen selbst gewählten Befehl verfügbar. Dieses Pet gehört zu Microsoft Copilot, nicht zu GitHub Copilot.

## Schnellstart

Voraussetzung: Windows 10/11 mit .NET Framework 4.8. Windows Terminal ist optional; ohne es wird die Windows-Konsole verwendet. Für das fertige Paket sind weder Python noch ein separat installiertes .NET-SDK nötig.

1. **Installieren:** Den Ordner dorthin legen, wo er bleiben soll (klonen oder ZIP entpacken), und **`install.cmd` doppelklicken.** Autostart und Hooks merken sich den Pfad.
   - Im Quellcode-Ordner wird `aipets.exe` mit dem C#-Compiler aus Windows gebaut. Das Release-ZIP enthält die fertige exe und überspringt den Build.
   - Schaltet „Mit Windows starten“ ein.
   - Trägt die Status-Hooks für Claude Code, Codex, Hermes Agent und Cursor ein, soweit sie installiert sind, und richtet die Freigaben ein. Für die automatische Codex-Freigabe muss die Codex CLI vorhanden sein; andernfalls zeigt die Zusammenfassung die nötigen Schritte unter `/hooks`.
   - Startet aipets und zeigt am Ende, was es gemacht hat.

   Mehrmals ausführen schadet nicht: Was schon stimmt, bleibt unverändert. Von jeder geänderten Datei liegt die vorige Fassung als `<datei>.bak-aipets` daneben. Agents, die gerade laufen, einmal neu starten, damit sie die Hooks laden.
2. **Bedienen:** Im Infobereich der Taskleiste (bei den ausgeblendeten Symbolen hinter `^`) erscheint das aipets-Icon.
   - **Linksklick:** Einstellungen. Dort kannst du Pets ein- und ausblenden, mit zwei Reglern die Größe wählen und unter **„Klick öffnet“** zwischen **Programm**, **Desktop-App** und **Website** umschalten. Die Desktop-App gibt es bei Claude, Hermes, Astra, Cursor und Copilot.
     - **„Größe aller Pets“:** die Höhe aller Pets in Pixeln, von 162 px bis zur Bildschirmhöhe. Die Striche stehen bei 162, 324, 486 … px. Verschiebst du ihn, springen **alle** Pets auf diese Höhe, auch die mit eigener Größe, und sind dann gleich hoch.
     - **„Größe von <Pet>“:** eine eigene Höhe nur für dieses Pet, ebenfalls bis zur vollen Bildschirmhöhe. Sie bleibt, bis du den oberen Regler wieder bewegst. **„wie alle“** daneben setzt das Pet schon vorher auf die Höhe aller zurück.
     - Es wird nichts mehr malgenommen. Kleiner als 162 px geht nicht, größer als der Bildschirm auch nicht. Steht ein Pet auf einem niedrigeren Bildschirm, wird es dort passend kleiner.
     - Die Pets folgen beim Ziehen sofort. Die Pixel-Art wird nur gestreckt, nicht verändert.
     - Programm: Programm, Argumente, Terminal und Arbeitsordner.
     - Desktop-App: welche App gefunden wurde, mit Version und Ort. Mit „…“ wählst du stattdessen eine andere exe.
       - Claude öffnet die Claude-App, Astra die Codex-App (Windows nennt sie „ChatGPT“), Hermes die Hermes-Desktop-App.
       - Astra nimmt dafür `codex app`, ohne Terminalfenster. Das öffnet den Arbeitsordner als Workspace in der App (die Seite zeigt ihn deshalb an), und fehlt die App, öffnet es ihren Installer.
       - Hermes Desktop muss einmal gebaut werden. Solange sie fehlt, startet ein Klick `hermes desktop` im Terminal. Das baut die App (beim ersten Mal einige Minuten) und öffnet sie, danach startet sie direkt.
     - Website: der Link (nur http/https; `grok.com` wird automatisch zu `https://grok.com/`).
     - Unten: „Mit Windows starten“. Das ist von Anfang an an: aipets schaltet es beim Start selbst ein, auch nach dem Verschieben des Ordners. Schaltest du es aus, bleibt es aus.
     - Unten: **„Alle Pets ausblenden“** blendet alle Pets auf einmal aus, zum Beispiel beim Bildschirmteilen. Schaltest du es wieder aus, kommen genau die Pets zurück, die vorher zu sehen waren; einzeln ausgeblendete bleiben aus. Solange es an ist, ist „Anzeigen“ der einzelnen Pets ausgegraut. Es bleibt auch nach einem Neustart an.
   - **Rechtsklick:** Menü mit allen Pets, „Alle Pets ausblenden“, Autostart und Beenden.
3. **Entfernen:** **`uninstall.cmd` doppelklicken.** Es beendet aipets, schaltet „Mit Windows starten“ aus und nimmt die Hooks wieder heraus. Danach kannst du den Ordner löschen, und `%APPDATA%\aipets` (Einstellungen, Log) auch.

**Ordner verschoben oder einen Agent erst später installiert?** Die Einstellungen zeigen unter „Statusanzeige“, ob die Hooks zu dieser `aipets.exe` passen. Wenn nicht, trägt der Link **„Hooks einrichten“** darunter sie mit einem Klick neu ein. Nach dem Verschieben erledigt `install.cmd` im neuen Ordner alles auf einmal, auch den Autostart.

**Ohne Doppelklick:** `.\build.ps1` baut nur. `aipets.exe --install` und `aipets.exe --uninstall` machen dasselbe wie die beiden cmd-Dateien, mit `--quiet` ohne Fenster. Die exe muss neben dem Ordner `pets\` liegen.

**Hintergrund:** Das Tray-Programm startet jedes Pet als eigenen Prozess (`aipets.exe --pet <id>`). Stürzt ein Pet ab oder wird es beendet, startet das Tray-Programm es neu. Beendest du das Tray-Programm, verschwinden auch die Pets.

**Pet-Rechtsklick:** öffnen, Arbeitsordner (nur im Programm-Modus), „Klick öffnet“ → Programm / Desktop-App / Website, Größe (alle Pets auf 162 bis 648 px; nur dieses Pet: wie alle, kleiner, größer, so hoch wie der Bildschirm), zurück in die Ecke, ausblenden, Einstellungen, aipets beenden. Ein ausgeblendetes Pet bleibt aus, auch nach einem Neustart, bis du es in den Einstellungen oder im Tray-Menü wieder einschaltest.

**Überlappende Pets** liegen in fester Reihenfolge übereinander. **Ziehst du ein Pet mit der Maus, kommt es nach vorn und bleibt dort**, wie ein Fenster unter Windows. So stellst du die Reihenfolge selbst ein; aipets merkt sie sich auch über einen Neustart (`layers=` unter `[app]` in `%APPDATA%\aipets\settings.ini`, Zeile löschen = zurücksetzen). Solange du nichts gezogen hast, gilt die Reihenfolge der Liste (Einstellungen, Tray-Menü, `order=` in `pets\<id>\pet.ini`): Claude vorn, dann Hermes, Astra, Gemini, Grok, Cursor und Copilot. Menüs und Dialoge von aipets liegen immer über allen Pets. Schiebt sich ein anderes Programm mit „immer im Vordergrund“ davor, stehen die Pets nach spätestens gut drei Sekunden wieder darüber.

Ein Klick startet Claude Code mit `--dangerously-skip-permissions`, Hermes mit `--yolo` und Codex mit `--dangerously-bypass-approvals-and-sandbox`. Gemini öffnet `https://gemini.google.com/app`, Grok `https://grok.com/`, beide im Standardbrowser. Das alles kannst du in den Einstellungen oder in `pets\<id>\pet.ini` ändern. Gemini CLI und Grok CLI sind nicht dabei; wer den Programm-Modus will, muss sie selbst installieren.

Im Desktop-App-Modus startet das Pet die Claude-App und Hermes Desktop so wie das Startmenü. Die installierten Apps findet es selbst: Claude als App-Paket, Hermes Desktop im Ordner, in den `hermes desktop` sie baut. Die Codex-App öffnet `codex app`; ohne Codex CLI startet das Pet das App-Paket direkt. Die Statusanzeige der Codex-App kommt über dieselben Codex-Hooks. Ob die Claude-App und Hermes Desktop die Hooks auslösen, ist noch nicht ausprobiert.

## Hooks von Hand eintragen

Brauchst du normalerweise nicht: `install.cmd` und „Hooks einrichten“ tragen genau das hier ein. Die Beispiele zeigen, was in den Dateien steht, falls du etwas prüfen oder anpassen willst.

### Claude Code

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

### Hermes Agent

In die `config.yaml` von Hermes eintragen. Sie liegt in `%HERMES_HOME%`, bei der Windows-Installation also meist in `%LOCALAPPDATA%\hermes`, sonst in `~/.hermes/`. Pfade in einfachen Anführungszeichen, sonst liest YAML die Backslashes als Escapes.

```yaml
hooks:
  pre_llm_call:
    - command: '"C:\Pfad\zu\aipets.exe" --hook hermes working'
      timeout: 10
  pre_approval_request:
    - command: '"C:\Pfad\zu\aipets.exe" --hook hermes waiting'
      timeout: 10
  post_approval_response:
    - command: '"C:\Pfad\zu\aipets.exe" --hook hermes resume'
      timeout: 10
  on_session_end:
    - command: '"C:\Pfad\zu\aipets.exe" --hook hermes done'
      timeout: 10
  on_session_finalize:
    - command: '"C:\Pfad\zu\aipets.exe" --hook hermes end'
      timeout: 10
```

Hermes fragt beim nächsten Start einmal pro Hook, ob er laufen darf. Alternativ startest du Hermes einmal mit `hermes --accept-hooks`. Prüfen kannst du das mit `hermes hooks list`.

### Codex

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

### Cursor

In `%USERPROFILE%\.cursor\hooks.json` eintragen und den Pfad anpassen. Cursor startet Hook-Befehle unter Windows über PowerShell und gibt das Ereignis in den Daten mit, die der Hook bekommt. Deshalb steht hier nur der Pfad, ohne Argumente. Enthält er Leerzeichen, kommt er in einfache Anführungszeichen, etwa `"command": "'C:\\Program Files\\aipets\\aipets.exe'"`.

```json
{
  "version": 1,
  "hooks": {
    "beforeSubmitPrompt": [{ "command": "C:\\Pfad\\zu\\aipets.exe", "timeout": 10 }],
    "stop":               [{ "command": "C:\\Pfad\\zu\\aipets.exe", "timeout": 10 }],
    "sessionEnd":         [{ "command": "C:\\Pfad\\zu\\aipets.exe", "timeout": 10 }]
  }
}
```

Cursor übernimmt außerdem von sich aus die Hooks aus Claude Code (Cursor-Einstellung „Include Third-Party Plugins, Skills, and Other Configs“), allerdings ohne deren Argumente. aipets erkennt diese Aufrufe und zeigt sie bei Cursor an, statt sich selbst zu öffnen. Steht in beiden Dateien derselbe Befehl, führt Cursor ihn nur einmal aus. Jeder Hook startet unter Windows eine PowerShell und kostet etwa eine halbe Sekunde, auf die Cursor wartet. aipets trägt für Cursor deshalb keinen Hook nach jedem Tool-Aufruf ein; der übernommene `PostToolUse`-Hook aus Claude Code läuft aber mit, solange die Übernahme in Cursor an ist. Welche Hooks Cursor ausführt, steht in Cursor im Ausgabekanal „Hooks“.

## Selbst bauen

```powershell
.\build.ps1                         # bauen, danach laufende Pets neu starten
.\build.ps1 -OutputDirectory C:\Temp\aipets-build  # bauen ohne laufende Pets anzufassen
```

`-OutputDirectory` enthält nur die exe; zum Ausführen muss daneben `pets\` liegen. Die Versionsnummer steht in `src/AssemblyInfo.cs`. Die exe ist nicht signiert.

## Bekannte Grenzen

- Gemini und Grok zeigen im Website-Modus keinen Arbeitsstatus.
- Cursor meldet nicht, wann es auf deine Bestätigung wartet: Das „?“ fehlt bei Cursor. Die Cursor-Hooks folgen der öffentlichen Doku und dem Verhalten von Cursor 3.5 und sind noch nicht in einer echten Cursor-Sitzung erprobt.
- Bei abgebrochenen Codex-Turns ohne Abschluss-Hook kann der Spinner bis zum Timeout von 15 Minuten bleiben.
- Die Status-Hooks der Claude-Desktop-App und Hermes Desktop sowie das Verhalten auf mehreren Monitoren müssen noch manuell geprüft werden.
- Der YAML-Editor unterstützt eingerückte Hook-Listen, leere Blöcke und Kommentare. Kompakte nichtleere Inline-Hook-Listen oder doppelte Hook-Schlüssel werden mit einer Fehlermeldung abgelehnt; die Datei bleibt erhalten.

## Lizenz

Der Quellcode steht unter der [MIT-Lizenz](LICENSE). Die Pet-Grafiken in `pets/*/sprites/` sowie Namen und Logos von Anthropic, OpenAI, Google, xAI, Nous Research und Anysphere (Cursor) fallen nicht darunter. aipets ist ein inoffizielles Fan-Projekt und steht in keiner Verbindung zu diesen Anbietern.
