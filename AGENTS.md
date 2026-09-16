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

- **Klick:** öffnet je nach Modus das Programm im Windows Terminal, die Desktop-App (Claude, Hermes, Astra) oder die Website im Standardbrowser. Umschaltbar pro Pet in den Einstellungen („Klick öffnet“) und im Rechtsklick-Menü.
- **Status:** Hooks des Agents melden, ob er arbeitet (Spinner), auf den User wartet („?“) oder fertig ist (✓).
- **Technik:** C# 5 / WinForms (.NET Framework 4, `csc` aus Windows), Sprites per Python (`numpy`, `Pillow`).
- **Installation:** `install.cmd` baut, schaltet den Autostart ein, trägt die Hooks aller installierten Agents ein, gibt sie frei und startet aipets. `uninstall.cmd` macht das rückgängig. Beide rufen `aipets.exe --install` bzw. `--uninstall` auf, der Code steht in `src/Setup.cs`.

## 2. Stand (2026-09-16)

| id | Figur | Vorlage | Klick (Standard) | Alternativen | Status | Besonderheiten |
|---|---|---|---|---|---|---|
| `claude` | Claude | `pets/claude/art/source.png` | Programm `claude --dangerously-skip-permissions` | Claude-Desktop-App, claude.ai/new | Claude-Code-Hooks | Körperanimation, Sternchen-Logo |
| `hermes` | Hermes-chan | `Desktop\hermes-chan.jpg` | Programm `hermes --yolo` in PowerShell | Hermes Desktop (ohne Build: `hermes desktop`), hermes-agent.nousresearch.com | Hermes-Shell-Hooks | sitzt, nur Gesicht animiert, Musiknoten |
| `astra` | Astra (Codex) | `Desktop\astra.png` | Programm `codex --dangerously-bypass-approvals-and-sandbox` | Codex-Desktop-App („ChatGPT“) über `codex app`, chatgpt.com/codex | Codex-Hooks | OpenAI-Logo statt „ASTRA 6“, funkelnde Haarsterne, Katzenohren |
| `gemini` | Gemini | ins Chat eingefügtes Bild (nur im Repo) | Website gemini.google.com/app | `gemini` (CLI nicht installiert) | – | Google-„G“ statt „3.8 Flash“, gezeichnete Augen, winkt |
| `grok` | Grok | `Desktop\grok-chan-pixel-ohne-tablet.png` | Website grok.com | `grok` (CLI nicht installiert) | – | xAI-Logo statt „grok“ (User-Wahl), zwinkert beim Hover wie im Bild, Peace-Zeichen wippt |

Commits auf `main`: ClaudePet → aipets (Tray, Hermes) → Astra/Codex-Hooks → Gemini/Link-Pets → Grok und Programm/Website-Auswahl für alle → neues Grok-Aussehen → Ein-Klick-Installation → Desktop-App-Modus und gleich große Pets → Größen-Regler (alle und einzeln) und Autostart als Standard.

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

**Größen-Regler und Autostart als Standard** (Wunsch des Users):
- **Größe:** Einstellungen → zwei Regler, beide gelten sofort.
  - „Größe aller Pets“: 1× bis 4× in Viertelschritten, gespeichert in `[app] size`.
  - „Größe von <Pet>“: 50–200 % davon für dieses Pet, gespeichert als `[id] percent`. Der User wollte die Pets auch einzeln größer oder kleiner machen.
  - Die Pet-Größe ist `size × percent`, begrenzt auf 0,5–6. Das Pet-Menü bietet beides als Stufen an.
  - Beim ersten Start hat das Tray die alten Werte `scale` übernommen, beim User 2× für alle (also kein `percent`).
- **Autostart:** „Mit Windows starten“ ist standardmäßig an. Das Tray schaltet es beim Start ein, außer der User hat es ausgeschaltet (`[app] autostart=0`). Test-exes und `--dry-run` fassen es nie an.
- **Getestet:**
  - 39 Prüfungen: Größe und Anteil lesen, Grenzen, Pet-Größe, alte Werte übernehmen (auch als Anteil), Anzeige.
    - Dazu, dass der Autostart-Standard die Registry für eine Test-exe nicht anfasst (Run-Wert vorher und nachher gleich).
  - Die 70 Desktop-App-Prüfungen erneut. Einstellungsseite als PNG.
- **Nicht getestet:** die Regler von Hand ziehen, das Pet-Menü.

**Alle Pets gleich groß** (Wunsch des Users: nach Pixelhöhe gleich skalieren, an den Bildern sonst nichts ändern):
- Größe n heißt jetzt: Die Figur ist n × 162 px hoch (`SizeUnit` in `PetForm`, die höchste Figur Grok). Jedes Pet bekommt dafür einen eigenen Zoom aus seiner Figurenhöhe (`Atlas.FigureHeight`, gemessen in `normal_0`). Details in PET-BAUANLEITUNG, Abschnitt 6, „Größe“.
- Sprites und Atlas sind unverändert. Die Art-Snapshots (ganze 3×-Pixel) sind byte-gleich zu vorher, bis auf die Bilder mit zufälligen Funken.
- `home` gilt jetzt in Pixeln bei 1× und wurde neu berechnet (Claude 12, Hermes 115, Astra 211, Gemini 334, Grok 482), damit sich die Pets bei „Zurück in die Ecke“ nicht überlappen.
- Der User hatte während der Arbeit selbst alle Pets auf 4× gestellt und nach links geschoben. Das bleibt so: Bei 4× sind jetzt alle 648 px hoch (an den laufenden Fenstern nachgemessen, ±1 px).
  - Ein Agent hatte zwischendurch alle auf 2× gesetzt, ohne vorher neu zu lesen. Das ist zurückgenommen.
- Neu: `lineup.png` im Snapshot, alle Pets nebeneinander auf einer Grundlinie.

**Desktop-App als dritter Klick-Modus** (Wunsch des Users: Codex/Astra, Claude und Hermes sollen auf Wunsch die GUI-Version öffnen):
- **Neu:**
  - `mode=app`, dazu `app=` (App-IDs oder exe-Pfade), `appcommand=` und `appfallback=` in der pet.ini, `app` in settings.ini.
  - `src/DesktopApp.cs`, „Klick öffnet: Programm / Desktop-App / Website“ in Einstellungen und Rechtsklick-Menü.
  - `--command <id> --mode <modus>`, Snapshots `settings-<modus>.png`.
- **Was ein Klick öffnet:**
  - Claude: App-Paket `Claude_pzs8sxrjxfjjc!Claude` (beim User 2.110.0.0).
  - Astra: `codex app` ohne Fenster im Arbeitsordner. So öffnet der User die App auch selbst (sein Hinweis); der Befehl öffnet den Ordner als Workspace oder ohne App den Installer.
    - Ohne CLI oder mit einer per „…“ gewählten exe startet die App direkt: App-Paket `OpenAI.Codex_2p2nqsd0c76g0!App` (26.908.9136.0, Windows nennt sie „ChatGPT“).
  - Hermes: die von `hermes desktop` gebaute `Hermes.exe`. Beim User ist sie nicht gebaut, deshalb startet ein Klick `hermes desktop` in PowerShell (baut beim ersten Mal einige Minuten, danach startet die exe direkt).
- **Getestet:**
  - `--command` für alle Pets in allen Modi; Programm- und Website-Befehle sind unverändert.
  - Testklasse mit 70 Prüfungen: Kandidatenliste, fehlende und kaputte App-IDs, exe mit `;` im Pfad, Logon-Umgebung ohne `CLAUDECODE`, kein `EnvironmentVariables` beim Shell-Execute, `codex app` samt Arbeitsordner und Rückfällen, `RunHidden` (Ausgabe, Exit-Code, Kind hält die Pipe offen), Fallback, Fehlermeldungen, Anzeigename auch im MTA-Thread.
  - Einstellungsseiten aller Modi als PNG (Claude, Astra, Hermes, Gemini).
- **Nicht getestet:**
  - Der echte Klick auf die Apps, auch nicht `codex app` aus dem Pet (hätte dem User Fenster nach vorn geholt). Also auch nicht, ob die App dabei nach vorn kommt.
  - Rechtsklick-Menü, „…“-Dialog, `hermes desktop` selbst.

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
aipets-test.exe --snapshot <ordner> --pet grok   # alle Zustände, lineup.png (alle Pets gleich hoch?), Einstellungsseiten (gespeicherter Modus und jeder Modus)
aipets-test.exe --command grok                   # was ein Klick öffnen würde
aipets-test.exe --command astra --mode app       # dasselbe in einem anderen Modus (program | app | website), settings.ini bleibt
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
- **Commits:** Englisch, eine Zusammenfassungszeile plus Stichpunkte. **Keine KI-Attribution** (kein `Co-Authored-By`, kein „Generated with …“). Das Repo ist privat.
  - Automatisch (Wunsch des Users seit 2026-09-16): Ist eine Änderung fertig, getestet, gebaut und die Doku nachgezogen, direkt auf `main` committen und pushen, ohne nachzufragen. Danach kurz sagen, was committet wurde.
  - Halbfertiges oder Ungetestetes nicht committen.
- **Den User nicht stören:** Er arbeitet parallel am PC.
  - Nie seine Maus bewegen, keine Test-Terminals oder Browserfenster öffnen.
  - Testen per `--snapshot`, `--command`, `--status`, `--dry-run` und Test-exe. Screenshots nur lesend (BitBlt).
  - `build.ps1` startet aipets neu; das ist üblich und in Ordnung.
- **Positionen und Größe gehören dem User:** `x`, `y`, `percent` und `[app] size` in `%APPDATA%\aipets\settings.ini` nicht zurücksetzen. Er zieht die Pets oft selbst herum und stellt die Größe um (am 2026-09-16 erst 4×, dann 2×).
  - Für ein neues Pet einen freien Startplatz ausrechnen (sichtbare Pixel aus dem Atlas × Zoom, Zoom = Größe × 162 / Figurenhöhe) und nur dann einen Abschnitt schreiben, wenn der Home-Platz belegt ist. Immer unter dem Mutex `Local\aipets.settings`.
  - Soll die Größe doch einmal geändert werden (nur auf ausdrücklichen Wunsch), vorher die aktuelle `settings.ini` lesen. Der User stellt Größe und Position oft selbst um, während ein Agent arbeitet.
  - Danach jedem Pet `Ipc.PostToPet(id, Ipc.CmdReload)` schicken, sonst kann ein Ziehen die Änderung überschreiben (PET-BAUANLEITUNG, Abschnitt 9).
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
  - Hermes Desktop ist nicht gebaut (Hermes Agent 0.21.3): kein `hermes-agent\apps\desktop\release`, `hermes update` meldete „desktop skipped“, keine Startmenü-Verknüpfung.
    - Das Protokoll `hermes://` zeigt trotzdem schon auf `…\release\win-unpacked\Hermes.exe`.
    - Bauen ohne Start ginge mit `hermes desktop --build-only`. Das ist ein großer Eingriff (npm, Electron), nur auf Wunsch des Users.
- **Claude-Desktop-App:** App-Paket `Claude` 2.110.0.0 (`Claude_pzs8sxrjxfjjc!Claude`, Protokoll `claude://`). Sie läuft meist im Hintergrund, ein Test-Klick würde sie nach vorn holen.
- **Codex:** CLI 0.154 per npm (`%APPDATA%\npm\codex.cmd`), Home `%USERPROFILE%\.codex`. Die Codex-Desktop-App teilt dieses Home und hat ein eigenes Astra-Maskottchen (`.codex\pets\astra-6`, nicht von aipets).
  - Die Desktop-App ist das App-Paket `OpenAI.Codex` 26.908.9136.0 (`OpenAI.Codex_2p2nqsd0c76g0!App`, Protokoll `codex://`). Windows zeigt sie als „ChatGPT“ (exe `app\ChatGPT.exe`).
  - Der User öffnet sie mit `codex app` in PowerShell. Den Befehl nie zum Testen aufrufen, er öffnet die App; nur `codex app --help`.
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
- Home-Positionen gelten in Pixeln bei 1×. Ab etwa 3,5× reicht ein 1920 px breiter Bildschirm nicht mehr für alle fünf, dann überlappen die „Zurück in die Ecke“-Plätze. Eine Idee dafür: „Alle aufreihen“ mit Umbruch oder kleinerem Abstand.
- Weitere Ideen, die der User gut fand, aber noch nicht bestellt hat:
  - Klick auf ein wartendes Pet holt die Sitzung nach vorn.
  - Sitzungsliste im Pet-Menü.
  - Hinweis im Tray, wenn die Pets im Vollbild versteckt sind.
  - Mittelklick/Shift-Klick für die anderen Modi.
  - Cursor-Pet.
- Die Grok-CLI-Vorgabe `program=grok` / `%APPDATA%\npm\grok.cmd` ist eine Annahme. Bei einer echten Installation Pfad und Argumente prüfen.
- **Desktop-App-Modus:**
  - Den ersten echten Klick auf Claude-App, Codex-App und `hermes desktop` macht der User. Kommt eine App dabei nicht nach vorn, liegt es am Vordergrundrecht (siehe PET-BAUANLEITUNG, Abschnitt 4, `app`).
    - Bei `codex app` am ehesten: Bis die App aufgeht, vergehen ein paar Sekunden, und Eingaben dazwischen können das weitergegebene Recht kosten. Dann für Astra den direkten Start erwägen (`appcommand` weglassen).
  - Statusanzeige: Die Codex-App nutzt dieselben Hooks (Abschnitt 5). Ob die Claude-Desktop-App (Code-Tab) die Hooks aus `~\.claude\settings.json` ausführt und ob Hermes Desktop die Shell-Hooks auslöst, ist ungetestet.
  - Annahmen ohne Test: der zweite Claude-Kandidat `%LOCALAPPDATA%\AnthropicClaude\claude.exe` (älterer Installer) und `%LOCALAPPDATA%\Programs\Hermes\Hermes.exe` (Standard von electron-builder für ein Hermes-Setup).
  - Ändert ein Hersteller seine Paket-Signatur, ändert sich die App-ID. Dann `app=` in der pet.ini nach `Get-StartApps` anpassen.
  - „…“ setzt nur exe-Dateien. Eine andere App-ID geht nur über `app=` von Hand (pet.ini oder settings.ini).
- **Setup:**
  - Wer nur die Codex-Desktop-App hat (ohne CLI), bekommt die Hooks, aber keine automatische Freigabe („codex nicht gefunden“).
    - Die App bringt eine eigene `codex.exe` mit (`%LOCALAPPDATA%\OpenAI\Codex\bin\<hash>\`). Die könnte als Fallback für `app-server` dienen; ungetestet.
  - `--uninstall` lässt die Codex-Freigaben (`[hooks.state.…]` in `config.toml`) stehen. Das schadet nicht: Bei einer Neuinstallation am selben Ort passen Schlüssel und Hash wieder.
  - Die Gemini CLI und die Grok CLI kennt Setup nicht (keine Statusquelle).
