# AGENTS.md – Einstieg für Agents (Claude Code, Codex, Hermes, …)

Diese Datei ist der Übergabepunkt: Wer hier weiterarbeitet, liest sie zuerst. Sie sagt, was es gibt, wie hier gearbeitet wird und was auf dem Rechner des Users eingerichtet ist.

| Datei | Für wen | Inhalt |
|---|---|---|
| [README.md](README.md) | User | Installieren (`install.cmd`), Bedienung, Hooks von Hand |
| [PET-BAUANLEITUNG.md](PET-BAUANLEITUNG.md) | Agents und Entwickler | Aufbau, Art-Pipeline, Status-Protokoll, Checkliste neues Pet, Stolperfallen |
| AGENTS.md | Agents | Stand, Arbeitsweise, Befehle, lokale Einrichtung, offene Punkte |
| [CLAUDE.md](CLAUDE.md) | Claude Code | lädt diese Datei |

## 1. Was aipets ist

Pixel-Art-Desktop-Pets für Windows, eins pro KI-Agent. Ein Tray-Programm (`aipets.exe`) startet jedes Pet als eigenen Prozess.

- **Klick:** öffnet je nach Modus das Programm im Windows Terminal oder die Website im Standardbrowser. Umschaltbar pro Pet in den Einstellungen („Klick öffnet“) und im Rechtsklick-Menü.
- **Status:** Hooks des Agents melden, ob er arbeitet (Spinner), auf den User wartet („?“) oder fertig ist (✓).
- **Technik:** C# 5 / WinForms (.NET Framework 4, `csc` aus Windows), Sprites per Python (`numpy`, `Pillow`).
- **Installation:** `install.cmd` baut, schaltet den Autostart ein, trägt die Hooks aller installierten Agents ein, gibt sie frei und startet aipets. `uninstall.cmd` macht das rückgängig. Beide rufen `aipets.exe --install` bzw. `--uninstall` auf, der Code steht in `src/Setup.cs`.

## 2. Stand (2026-09-15)

| id | Figur | Vorlage | Klick (Standard) | Alternative | Status | Besonderheiten |
|---|---|---|---|---|---|---|
| `claude` | Claude | `pets/claude/art/source.png` | Programm `claude --dangerously-skip-permissions` | claude.ai/new | Claude-Code-Hooks | Körperanimation, Sternchen-Logo |
| `hermes` | Hermes-chan | `Desktop\hermes-chan.jpg` | Programm `hermes --yolo` in PowerShell | hermes-agent.nousresearch.com | Hermes-Shell-Hooks | sitzt, nur Gesicht animiert, Musiknoten |
| `astra` | Astra (Codex) | `Desktop\astra.png` | Programm `codex --dangerously-bypass-approvals-and-sandbox` | chatgpt.com/codex | Codex-Hooks | OpenAI-Logo statt „ASTRA 6“, funkelnde Haarsterne, Katzenohren |
| `gemini` | Gemini | ins Chat eingefügtes Bild (nur im Repo) | Website gemini.google.com/app | `gemini` (CLI nicht installiert) | – | Google-„G“ statt „3.8 Flash“, gezeichnete Augen, winkt |
| `grok` | Grok | `Desktop\grok-chan-pixel-ohne-tablet.png` | Website grok.com | `grok` (CLI nicht installiert) | – | xAI-Logo statt „grok“ (User-Wahl), zwinkert beim Hover wie im Bild, Peace-Zeichen wippt |

Commits auf `main`: ClaudePet → aipets (Tray, Hermes) → Astra/Codex-Hooks → Gemini/Link-Pets → Grok und Programm/Website-Auswahl für alle → neues Grok-Aussehen → Ein-Klick-Installation.

Grok hatte zuerst eine andere Vorlage (`Desktop\grok-chan.png`: Brille, Tablet, Uhr). Der User hat danach nur das Aussehen durch `grok-chan-pixel-ohne-tablet.png` ersetzen lassen; alles andere (Link, Logo-Wahl, Effekte) blieb.

**Installation leichter gemacht** (Wunsch des Users):
- **Neu:** `install.cmd`, `uninstall.cmd`, `aipets.exe --install/--uninstall [--quiet]`, `src/Setup.cs` und `src/Json.cs`. In den Einstellungen steht unter „Statusanzeige“ der Link „Hooks einrichten“, sobald die Hooks fehlen oder eine andere exe aufrufen.
- **Getestet:**
  - Testklasse gegen Kopien der echten Configs des Users (41 Prüfungen: No-op, Umzug der exe, Entfernen, frische Dateien, fremde YAML-Einrückung, PyYAML-Stil, fremde Hooks vor unseren, CRLF ohne Zeilenende am Schluss, gemischte Zeilenenden).
  - Die YAML-Ergebnisse mit PyYAML aus Hermes' venv geparst.
  - Codex-Freigabe gegen ein leeres `CODEX_HOME`.
  - `install.cmd` als `--quiet`-Kopie.
  - Auf dem Rechner des Users war `--install` ein No-op (Hashes gleich), weil schon alles eingerichtet war.
- **Nicht getestet:** Doppelklick mit Fenstern und `--uninstall` auf dem echten Rechner (würde seine Hooks entfernen).

## 3. Befehle

```powershell
# bauen (beendet ein laufendes aipets aus dem Ordner und startet es danach neu)
.\build.ps1
.\build.ps1 -Art -Pet grok          # vorher nur Groks Sprites neu erzeugen
python pets\grok\art\make_sprites.py  # nur Sprites, ohne Neustart

# Test-exe neben der echten bauen: das laufende aipets bleibt unberührt (danach löschen)
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 /utf8output `
  /out:aipets-test.exe /win32icon:art\aipets.ico /resource:art\aipets.ico,aipets.ico `
  /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll src\*.cs

# prüfen ohne den Desktop anzufassen (GUI-exe: aus PowerShell über System.Diagnostics.Process mit WaitForExit starten)
aipets-test.exe --snapshot <ordner> --pet grok   # alle Zustände + Einstellungsseite als PNG
aipets-test.exe --command grok                   # was ein Klick öffnen würde
aipets-test.exe --status codex                   # zusammengefasster Agent-Status

# einrichten / entfernen (= install.cmd / uninstall.cmd ohne Bauen; --quiet = ohne Fenster, Zusammenfassung auf stdout und im Log)
aipets.exe --install --quiet
aipets.exe --uninstall --quiet                   # NIE auf dem Rechner des Users: entfernt seine Hooks
```

- **Logik testen:** alle `src\*.cs` plus eine Testklasse mit `/target:exe /main:AiPets.<Klasse>` kompilieren und laufen lassen.
- **Hooks testen:** Payload per `System.Diagnostics.Process` auf stdin von `aipets.exe --hook <quelle> <event>` geben. Details in PET-BAUANLEITUNG, Abschnitt 7 und 9.
- **Setup testen:** nie gegen die echten Dateien.
  - `Setup.Claude(pfad, exe, install)`, `Setup.Codex(home, exe, install, trust)`, `Setup.EditHermesConfig(pfad, exe, install)` und `Setup.EditHermesAllowlist(pfad, exe, install)` nehmen Pfade. Kopien der echten Configs in einen Scratch-Ordner legen und eine Testklasse dagegen laufen lassen.
  - Die Codex-Freigabe (`trust = true`) mit `CODEX_HOME` auf einen leeren Scratch-Ordner testen: Setup startet `codex app-server` mit dieser Umgebung.
  - Vorher und nachher Hashes der echten Dateien vergleichen.
- **Claude Code auf diesem Rechner:** Das PowerShell-Tool blockt `Remove-Item` in einem Aufruf, der auch csc-Argumente wie `/nologo` enthält (hält sie für Systempfade). Dateien dann per Bash löschen und getrennt kompilieren.

## 4. Arbeitsweise (so will es der User)

- **Sprache:** mit dem User Deutsch. UI-Texte und Doku Deutsch. Code-Kommentare und Commit-Messages Englisch.
- **Commits:** Englisch, eine Zusammenfassungszeile plus Stichpunkte. **Keine KI-Attribution** (kein `Co-Authored-By`, kein „Generated with …“). Direkt auf `main` committen und pushen, wenn der User „commit/push“ sagt. Das Repo ist privat.
- **Den User nicht stören:** Er arbeitet parallel am PC.
  - Nie seine Maus bewegen, keine Test-Terminals oder Browserfenster öffnen.
  - Testen per `--snapshot`, `--command`, `--status`, `--dry-run` und Test-exe. Screenshots nur lesend (BitBlt).
  - `build.ps1` startet aipets neu; das ist üblich und in Ordnung.
- **Positionen gehören dem User:** `x`, `y`, `scale` in `%APPDATA%\aipets\settings.ini` nicht zurücksetzen. Er zieht die Pets oft selbst herum.
  - Für ein neues Pet einen freien Startplatz ausrechnen (sichtbare Pixel aus dem Atlas × Größe) und nur dann einen Abschnitt schreiben, wenn der Home-Platz belegt ist. Immer unter dem Mutex `Local\aipets.settings`.
- **Optik:** Die Pets sollen so gut animiert sein wie die bestehenden. Gesichter, Logos und Details immer mit Vorschau-PNGs prüfen, nicht blind eintragen.
  - Schriftzüge auf Shirts wurden jeweils durch das Logo ersetzt. Logos aus echten Vektorpfaden rastern.
  - Bei echten Designfragen (welches Logo, was entfernen) kurz fragen. Der User hat erlaubt, Fragen zu stellen.
- **Bilder:** Vorlagen liegen meist auf dem Desktop. Ein ins Chat eingefügtes Bild kann das falsche sein: Hash vergleichen, auf dem Desktop nach der passenden Datei suchen.
- **Arbeitsdateien** (Zooms, Dumps, Testskripte, Test-exe) in einen Scratch-Ordner, nicht ins Repo.
- **Nach jeder Änderung** README, PET-BAUANLEITUNG und diese Datei nachziehen, damit der nächste Agent den Stand kennt.

## 5. Lokale Einrichtung auf dem Rechner des Users (nicht im Repo)

- **Programm:** `C:\Users\vitus\Desktop\aipets\aipets.exe`, Autostart per `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` → `aipets`.
- **Laufzeitdaten:** `%APPDATA%\aipets\` (`settings.ini`, `aipets.log`, `status\`).
- **Git:** Remote `https://github.com/homer311/aipets` (privat), Branch `main`. Lokale Identität `homer311` / `288114086+homer311@users.noreply.github.com`. `gh` ist angemeldet.
- **Claude Code:** Hooks in `%USERPROFILE%\.claude\settings.json` (Form mit `args`, siehe README).
- **Hermes Agent:** nativ in `%LOCALAPPDATA%\hermes` (= `HERMES_HOME`), nur Windows PowerShell 5.1.
  - Hooks in `config.yaml` (Pfade in einfachen Anführungszeichen).
  - Freigaben stehen in `shell-hooks-allowlist.json`. `aipets.exe --install` trägt sie selbst ein.
  - Beim ersten Mal wurden sie mit Hermes' eigener Funktion gesetzt, im Ordner `hermes-agent`: `venv\Scripts\python.exe -c "from hermes_cli.config import load_config; from agent.shell_hooks import register_from_config; register_from_config(load_config(), accept_hooks=True)"`.
  - Die Allowlist gilt pro Event und Befehlstext. Ein laufender Hermes-Gateway lädt geänderte Hooks erst nach Neustart.
  - Die `config.yaml` hat gemischte Zeilenenden (CRLF und LF) und keinen Zeilenumbruch am Schluss. Setup lässt das so.
- **Codex:** CLI 0.154 per npm (`%APPDATA%\npm\codex.cmd`), Home `%USERPROFILE%\.codex`. Die Codex-Desktop-App teilt dieses Home und hat ein eigenes Astra-Maskottchen (`.codex\pets\astra-6`, nicht von aipets).
  - Hooks in `.codex\hooks.json`. `aipets.exe --install` gibt sie über `codex app-server` frei (`hooks/list` → `config/batchWrite`). Deshalb stehen in `.codex\config.toml` Einträge unter `[hooks.state]`.
- **Gemini CLI / Grok CLI:** nicht installiert. Gemini und Grok laufen im Website-Modus.
- **Verschiebt jemand die exe:** Claude-Hooks, Hermes-Hooks plus Allowlist, Codex-Hooks plus Freigaben und der Autostart müssen neu, weil alle den Pfad enthalten.
  - `install.cmd` im neuen Ordner erledigt alles. „Hooks einrichten“ in den Einstellungen erledigt es pro Agent.
  - Setup erkennt die alten Einträge an `aipets.exe` + `--hook <quelle>` und ersetzt sie an derselben Stelle.
  - Die Einstellungsseite zeigt unter „Statusanzeige“, ob die Hooks die aktuelle exe aufrufen („Hooks rufen eine andere aipets.exe auf“).

## 6. Offene Punkte und Ideen

- Gemini und Grok haben keine Statusanzeige; Websites liefern keine Hooks. Mit einer installierten CLI im Programm-Modus könnte man eine Quelle ergänzen (neue `case` in `HookCommand.Run`, `status=` in der pet.ini).
- Codex meldet `Stop` nicht bei abgebrochenen Turns: Der Spinner hält dann bis zu 15 min (siehe PET-BAUANLEITUNG, Abschnitt 7).
- `src/Settings.cs` ist ungenutzte Altlast aus ClaudePet (Namespace `ClaudePet`) und könnte gelöscht werden.
- Home-Positionen sind in Sprite-Pixeln angegeben. Bei gemischten Größen (der User nutzt 2× und 3×) überlappen „Zurück in die Ecke“-Plätze.
- Die Grok-CLI-Vorgabe `program=grok` / `%APPDATA%\npm\grok.cmd` ist eine Annahme. Bei einer echten Installation Pfad und Argumente prüfen.
- **Setup:**
  - Wer nur die Codex-Desktop-App hat (ohne CLI), bekommt die Hooks, aber keine automatische Freigabe („codex nicht gefunden“).
    - Die App bringt eine eigene `codex.exe` mit (`%LOCALAPPDATA%\OpenAI\Codex\bin\<hash>\`). Die könnte als Fallback für `app-server` dienen; ungetestet.
  - `--uninstall` lässt die Codex-Freigaben (`[hooks.state.…]` in `config.toml`) stehen. Das schadet nicht: Bei einer Neuinstallation am selben Ort passen Schlüssel und Hash wieder.
  - Die Gemini CLI und die Grok CLI kennt Setup nicht (keine Statusquelle).
