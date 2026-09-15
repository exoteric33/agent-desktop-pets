# Desktop-Pets – Bauanleitung (aipets)

Diese Datei beschreibt, wie aipets aufgebaut ist und wie man ein neues Pet dazubaut.
Sie ist auch als Kontext für Agents gedacht: „Lies `AGENTS.md` und `PET-BAUANLEITUNG.md` und bau mir ein Pet für X.“
Der Einstieg für Agents (Arbeitsweise, Stand, lokale Einrichtung) steht in [AGENTS.md](AGENTS.md).

---

## 1. Was aipets macht

- **Ein Programm, mehrere Pets.**
  - Jedes Pet ist ein Ordner unter `pets\` mit Sprites und `pet.ini`.
  - Für ein neues Pet brauchst du nur einen neuen Ordner, `aipets.exe` bleibt gleich.
- **Tray-Programm** (`aipets.exe` ohne Argumente): Icon im Infobereich, Linksklick öffnet die Einstellungen, Rechtsklick das Menü.
  - Es startet jedes eingeschaltete Pet als eigenen Prozess (`aipets.exe --pet <id> --host <pid>`).
  - Stürzt ein Pet ab oder wird es beendet, startet es das Pet neu. Wartezeiten steigen von 1 s auf 60 s, nach 4 Abstürzen zeigt es einen Hinweis.
  - Autostart: `HKCU\...\CurrentVersion\Run`, Wert `aipets`.
- **Pet-Fenster:**
  - Die Figur steht bzw. sitzt unten auf der Taskleiste, immer im Vordergrund. Transparente Stellen lassen Klicks durch.
  - **Maus drüber:** Hover-Gesicht plus Sprechblase mit Prompt.
  - **Klick:** Funken, Hüpfer (falls das Pet Bounce-Frames hat), dann öffnet sich je nach Modus das **Programm** (im Windows Terminal) oder die **Website** (im Standardbrowser).
  - **Programm oder Website:** Jedes Pet kann beides. Umschalten in den Einstellungen („Klick öffnet“) oder im Rechtsklick-Menü des Pets („Klick öffnet“ → Programm / Website).
  - **Ziehen:** frei verschiebbar. In Taskleistennähe rastet das Pet ein und schaut zur Bildschirmmitte (Sprites gespiegelt).
  - **Leerlauf:** Nach 1 Minute ohne Eingabe schläft das Pet ein (Zzz).
  - **Vollbild:** Bei Vollbild-Apps blendet es sich aus, bei „Desktop anzeigen“ nicht.
- **Agent-Status über Hooks:** Spinner-Blase = arbeitet, „?“ = braucht dich, grüner Haken = fertig.

| Pet | Animation | Klick (Standard) | Website / Programm als Alternative | Status |
|---|---|---|---|---|
| Claude | Atmen, wehende Haare, Antenne, Blinzeln, Lächeln, Hüpfer | Programm: `wt → claude.exe --dangerously-skip-permissions` | `https://claude.ai/new` | Claude-Code-Hooks |
| Hermes | nur Gesicht: Blinzeln, Aufschauen, Lächeln, glücklich, schlafen; Musiknoten aus dem Kopfhörer | Programm: `wt → powershell -NoExit → hermes --yolo` | `https://hermes-agent.nousresearch.com/` | Hermes-Shell-Hooks |
| Astra | Atmen, wehende Haare, funkelnde Sterne in den Galaxie-Strähnen, Ahoge, Katzenohren stellen sich auf, Blinzeln, Aufschauen, „:3“, glücklich, schlafen, Hüpfer; OpenAI-Logo auf dem Shirt | Programm: `wt → cmd /c codex.cmd --dangerously-bypass-approvals-and-sandbox` | `https://chatgpt.com/codex` | Codex-Hooks |
| Gemini | Atmen, wehende Haare, Ahoge, Blinzeln, Aufschauen, glücklich mit Zunge wie im Bild, schlafen, Hüpfer; winkt beim Hover und Klick; Google-„G“ auf dem Shirt | Website: `https://gemini.google.com/app` | Programm `gemini` (Gemini CLI, nicht installiert) | – |
| Grok | Atmen, wehende Haarspitzen, Ahoge, Blinzeln, Brillenglanz, zwinkert beim Hover, glücklich, schlafen, Hüpfer; xAI-Logo auf dem Shirt, Uhr entfernt | Website: `https://grok.com/` | Programm `grok` (nicht installiert) | – |

## 2. Ordner

```
aipets\
├─ aipets.exe             Programm (Tray, Pets und Hook-Befehl in einer Datei; nicht im Repo, build.ps1 baut sie)
├─ build.ps1              baut die exe  (-Art = Icon und Sprites vorher neu, -Pet <id> = nur dieses Pet)
├─ README.md              für den User: Schnellstart, Hooks eintragen
├─ PET-BAUANLEITUNG.md    diese Datei: Technik, Art-Pipeline, Status-Protokoll, Stolperfallen
├─ AGENTS.md              Einstieg für Agents: Arbeitsweise, Stand, lokale Einrichtung
├─ CLAUDE.md              lädt AGENTS.md für Claude Code
├─ art\
│  ├─ pixelkit.py         gemeinsame Pipeline: Flood-Fill, Pixelisieren, Patches, Blasen, Atlas, Icon
│  ├─ app_icon.py         → aipets.ico (Tray und exe)
│  └─ aipets.ico
├─ pets\
│  ├─ claude\
│  │  ├─ pet.ini          Name, Modus, Programm, Link, Terminal, Statusquelle, Home-Position
│  │  ├─ art\             source.png, make_sprites.py, preview\ (nicht im Repo)
│  │  └─ sprites\         atlas.png, atlas.txt, icon.ico  ← liest das Programm zur Laufzeit
│  ├─ hermes\             genauso (source.jpg)
│  ├─ astra\              genauso (source.png)
│  ├─ gemini\             genauso (source.png)
│  └─ grok\               genauso (source.png)
└─ src\
   ├─ Program.cs          Einstieg: Tray / --pet / --hook / --snapshot / --status / --command
   ├─ TrayHost.cs         Tray-Icon, Menü, Pet-Prozesse starten und neu starten, Befehle der Pets
   ├─ SettingsForm.cs     Einstellungsfenster (Seite pro Pet, „Klick öffnet: Programm / Website“)
   ├─ PetForm.cs          Pet-Fenster, Animation, Maus, Menü, Status-Anzeige
   ├─ Pets.cs             pet.ini lesen (PetInfo), wirksame Einstellungen (PetSettings, Mode/Website)
   ├─ Atlas.cs            atlas.png/atlas.txt laden, Frames und Sprites zeichnen
   ├─ Launcher.cs         Klick-Aktion: Programm finden und Terminal-Befehl bauen, oder Link prüfen und im Browser öffnen
   ├─ Status.cs           Hook-Befehl (claude, hermes, codex) + Auswertung der Statusdateien
   ├─ Native.cs           Win32: Layered Window, DPI, Idle, Vollbild, Logon-Umgebung, IPC
   ├─ Settings.cs         Altlast aus ClaudePet (nicht mehr benutzt, kompiliert aber mit)
   └─ App.cs              Pfade, settings.ini (Ini/Store), Autostart, Log
```

Laufzeitdaten liegen in `%APPDATA%\aipets\`:
- `settings.ini`: ein Abschnitt pro Pet (`x`, `y`, `scale`, `enabled`, `workdir`, `mode`, `program`, `args`, `shell`, `url`), geschrieben von Tray und Pets, immer unter dem Mutex `Local\aipets.settings`
- `aipets.log`: jede Zeile mit `[tray]`, `[claude]`, `[hook hermes]` …
- `status\<quelle>\*.txt`

## 3. Bauen und testen

- **Voraussetzung Code:** nichts extra. Der C#-Compiler von .NET Framework 4.x ist in Windows enthalten (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
  - **Achtung:** nur C#-5-Syntax, also kein `$"..."`, kein `?.`, keine `=>`-Properties, kein `nameof`, kein `out var`.
- **Voraussetzung Sprites:** Python mit `numpy` und `Pillow`.
- `.\build.ps1` beendet ein laufendes aipets aus diesem Ordner, baut und startet es danach wieder (über `explorer.exe`, damit es nicht die Umgebung der Shell erbt).
  - `.\build.ps1 -Art -Pet grok` erzeugt vorher nur Groks Sprites neu.
- **Testen, ohne den Desktop anzufassen:**
  - **Test-exe:** mit demselben `csc`-Aufruf wie in `build.ps1`, aber `/out:aipets-test.exe`, in den aipets-Ordner kompilieren (dann findet sie `pets\`). Das laufende aipets bleibt unberührt. Danach löschen.
  - `aipets.exe --snapshot <ordner> [--pet id]` rendert jedes Pet in allen Zuständen (idle, hover, look, sleep, click, working, waiting, done, beide Blickrichtungen) plus das Einstellungsfenster (mit `--pet` auf der Seite dieses Pets) als PNG.
  - `aipets.exe --command <id>` gibt aus, was ein Klick starten würde (Terminal-Befehl oder Link).
  - `aipets.exe --status <quelle>` gibt den zusammengefassten Agent-Status aus.
  - `--dry-run` (Tray oder Pet): Klicks protokollieren nur in `aipets.log`.
  - Das Programm ist eine GUI-exe. PowerShell wartet nicht darauf und liest ihre Ausgabe nur zuverlässig über `System.Diagnostics.Process` mit Umleitung (siehe Stolperfallen).
  - **Logik-Tests ohne Fenster:** alle `src\*.cs` plus eine eigene Testklasse mit `/target:exe /main:AiPets.<Klasse>` kompilieren (so wurden `Launcher.NormalizeUrl` und die Modus-Auswahl geprüft).

## 4. `pet.ini`

```ini
name=Hermes                                  ; Anzeigename
order=2                                      ; Reihenfolge in Menü und Einstellungen
open=Hermes öffnen                           ; Menütext
mode=program                                 ; program | website: was ein Klick öffnet
program=hermes                               ; Name im PATH oder voller Pfad
find=%LOCALAPPDATA%\hermes\bin\hermes.exe    ; bekannte Installationsorte, vor dem PATH probiert (;-getrennt)
args=--yolo
shell=powershell                             ; direct | powershell | cmd
url=https://hermes-agent.nousresearch.com/   ; Website für mode=website
status=hermes                                ; claude | hermes | codex | leer
home=88                                      ; Abstand zum rechten Bildschirmrand in Sprite-Pixeln
```

- **Einstellungen:** Die Werte hier sind Standardwerte. Was du in den Einstellungen änderst, landet in `settings.ini` und überschreibt sie. Die „zurücksetzen“-Links löschen diese Überschreibungen wieder.
- **`mode`:** `program` oder `website`. Fehlt der Schlüssel, gilt: nur `url` und kein `program` → `website`, sonst `program`. Unbekannte Werte werden `program`.
  - Umschalten speichert `mode` in `settings.ini`. Wird der pet.ini-Standard gewählt, wird der Schlüssel dort gelöscht.
  - Im Programm-Modus zeigen die Einstellungen Programm, Argumente, „Öffnen in“ und Arbeitsordner. Im Website-Modus zeigen sie „Link“, „Öffnen in: Standardbrowser“ und „Link zurücksetzen“.
- **`shell=direct`:** Das Terminal startet das Programm selbst, der Tab schließt mit dem Programm.
- **`shell=powershell` / `cmd`:** Das Programm läuft in dieser Shell, und die bleibt danach offen.
- **`url`:** Es gelten nur http(s)-Links. Eingaben wie `gemini.google.com` bekommen `https://` davor. Alles andere (Dateipfade, `file://`, `javascript:`) wird abgelehnt: im Einstellungsfeld springt der alte Wert zurück, beim Klick kommt eine Meldung.
- **Programm nicht installiert:** Ein Klick zeigt „… wurde nicht gefunden“ (z. B. Gemini oder Grok im Programm-Modus ohne CLI).
- **Mehrere Pets:** Die Home-Positionen müssen sich unterscheiden, sonst sitzen die Pets übereinander. Home = Home des rechten Nachbarn + dessen Zellbreite + 4.

## 5. Art-Pipeline (`art/pixelkit.py` + `pets/<id>/art/make_sprites.py`)

1. **Freistellen** (bildspezifisch):
   - Hintergrund per Flood-Fill vom Rand entfernen, dann den größten zusammenhängenden Blob behalten.
   - Claude: `pockets` für eingeschlossene Hintergrundlöcher.
   - Hermes: grauen Bodenschatten wegfluten und weiße Randlichter in Haaren und Stiefeln schwarz malen. Beim Verkleinern würden sie zu Pünktchen.
   - Astra: dieselbe Vorlage wie Claude (Shirt-Schriftzug „ASTRA 6“, Ärmeltext, Wasserzeichen). Schrift wird mit Weiß zugeflossen, das Wasserzeichen auf dem Rock bekommt das glatte Lila (Zufließen würde die Faltenlinien hineinziehen).
   - Gemini: Der Hintergrund ist leicht verrauschtes Off-White (Referenz 250, Toleranz 12), dazu zwei `pockets` zwischen Haarsträhnen. Den Schriftzug „3.8 Flash“ (blaue Ziffern, dunkle Buchstaben) findet die Maske über Helligkeit *oder* Sättigung.
   - Grok: Den weißen Schriftzug „grok“ auf dem schwarzen Shirt nicht zufließen lassen: Das zieht den dunklen Rand der Buchstaben mit und hinterlässt ein „grok“-Schattenbild. Stattdessen den Kasten flach mit dem Median-Schwarz der Zeilen darüber und darunter füllen.
2. **`kit.pixelise(rgb, fg, factor, palette, outline, outline_bottom)`:**
   - Blöcke verkleinern. Dünne dunkle Linien bleiben erhalten: Hat ein Block genug dunkle Pixel, bekommt er deren Farbe.
   - Palette per k-means (Lab), einzelne Pixel glätten, 1 px Außenlinie.
   - `outline_bottom=False`, wenn die Figur unten abgeschnitten ist (Claude, Astra, Gemini, Grok). Hermes sitzt komplett im Bild.
   - **Faktor:** Claude 3 bei 263×350 Vorlage. Hermes 6 bei 720×1280; kleiner geht nicht, sonst ist das Gesicht zu klein für Animationen.
   - **Maßstab zwischen den Pets:** Die Köpfe sollen etwa gleich groß sein (Astra: 46 px vom Haaransatz bis Kinn). Deshalb wird die Vorlage vor dem Faktor 3 skaliert (`ENLARGE`):
     - Astra: 252×304, `ENLARGE` 1,2, 28 Farben.
     - Gemini: 229×257, `ENLARGE` 1,5, 32 Farben für den Haarverlauf von Blau über Pink zu Lila.
     - Grok: 548×704, `ENLARGE` 0,7 (verkleinern), 28 Farben. Damit ist sie etwa so groß wie Hermes (115×163).
   - Vorher Varianten nebeneinander mit den fertigen Pets vergleichen (Atlas-Frame `normal_0` der anderen daneben legen).
3. **Handarbeit** mit `kit.patch(img, x, y, rows, colors)` (Zeichen → Farbe, `.` = unverändert). Was die Verkleinerung zerstört, wird neu gesetzt: Claude-Sternchen, Hermes-Kopfhörer und „N“-Halsband.
   - **Logos statt Schriftzug** (Wunsch des Users): immer aus dem echten Vektorlogo rastern, nie freihändig.
     - Astra, OpenAI-Logo: 12 px, punktsymmetrisch gemittelt, drei Tinten (voll, 62 %, 30 % auf Shirt-Weiß). Unter 12 px zerfällt der Knoten.
     - Gemini, Google-„G“: aus den vier farbigen Pfaden des Vektorlogos (`svg_polygon` kann M, L, H, V, C, S, Z). Jedes Pixel nimmt die Farbe mit der größten Abdeckung, schwach abgedeckte Randpixel werden mit Shirt-Weiß gemischt.
     - Grok, xAI-Zeichen (vom User statt „grok“ gewählt): vier Polygone, 12×13 px, hell auf Schwarz, Randpixel zu 55 % gemischt.
     - Vorgehen: Größen 11–13 px mit 1–3 Tinten direkt auf dem Shirt bei 2× und 4× vergleichen, dann wählen.
   - Gemini: goldene Brosche und die Haarspange als vierfarbiger Stern.
   - Grok: **Uhr entfernt** (Wunsch des Users). Haut über Gehäuse und Armband, die Handgelenk-Kontur läuft als `m`-Linie durch (`take_off_watch`).
4. **Gesichter:** Die Pipeline zerstört Augen und Mund praktisch immer, sie werden als Patches gezeichnet.
   - Koordinaten findest du über einen Symbol-Dump des Basis-Sprites plus Grid-Zoom mit Koordinatenlinien (großer Zoom, Beschriftung alle 2 px).
   - Umrechnung Vorlage → Basis-Sprite: `x = (ENLARGE·x_src − x0)/3 + 1`, `y` genauso. `x0`/`y0` ist der Rand der erodierten Maske im skalierten Bild.
   - Faces-Vorschau in `preview\faces.png`.
   - Gemini hat in der Vorlage die Augen zu: Augen und Mund werden erst mit Haut übermalt, dann offene Augen gezeichnet. Ihr Kopf ist geneigt, das rechte Auge sitzt 3 px höher. „happy“ ist das „^^“ mit Zunge aus dem Bild.
   - Grok trägt eine Brille: Die Brillenränder bleiben stehen, übermalt wird nur das Auge darunter (rechts x 53–61, y 29–32; links x 37–47, y 35–37). „look“ ist ein Glanz auf den Gläsern, „hover“ ein Zwinkern mit dem rechten Auge.
5. **Animation** (bildspezifisch):
   - **Claude:** 8 Phasen je Gesicht (`hair_wave`, `wiggle_ahoge`, `restretch` fürs Atmen) plus `bounce_-2..3`.
   - **Hermes:** 1 Phase je Gesicht, keine Bounce-Frames, die Bewegung steckt nur im Gesicht.
   - **Astra:** wie Claude 8 Phasen je Gesicht plus `bounce_-2..3`, dazu `twinkle_stars` (die Sterne in den Galaxie-Strähnen funkeln versetzt) und `perk_ears` für hover und happy.
     - Ihr `hair_wave` erkennt Haar an der Farbe (schwarz oder Galaxie-Blau), nicht an der Helligkeit: sonst würde der lila Rock mitwehen.
     - Gesichter: normal, blink, look, hover („:3“), happy, sleep.
   - **Gemini:** wie Claude 8 Phasen je Gesicht plus `bounce_-2..3`. `wave_hand` lässt die erhobene Hand bei hover und happy auf- und abwinken; im Leerlauf winkt sie nicht, das wäre auf dem Desktop zu unruhig.
     - Links wehen die Haare erst unterhalb des erhobenen Arms, sonst würde der Arm mitwackeln.
   - **Grok:** wie Claude 8 Phasen je Gesicht plus `bounce_-2..3`.
     - Shirt und Rock sind so schwarz wie ihr Haar, Farbe hilft also nicht. `hair_span` hat deshalb eine Reichweite (9 px vom Außenrand): Es wehen nur die Haarspitzen.
     - Links erst unterhalb des Tablets, sonst würde das Tablet mitwackeln.
     - Gesichter: normal, blink, look (Brillenglanz), hover (Zwinkern), happy, sleep.
6. **`mirror`:** alle Frames gespiegelt (`m_…`). Logos werden zurückgedreht (Claudes Haarspange, Hermes' „N“).
   - Astra, Gemini, Grok: Das Logo kommt erst auf die fertige (ggf. gespiegelte) Zelle, mit dem Versatz aus Atmen und Hüpfer (`with_logo`/`with_logos`). Ein fester Ausschnitt würde bei Frames mit verdoppelten Zeilen danebenliegen.
   - Versatz: Verdoppelte Zeilen *unterhalb* des Logos schieben es nach oben, gelöschte nach unten. Zeilen darüber verschieben nichts (die Zelle ist unten ausgerichtet).
7. **Effekte:** Funken, Zzz, Musiknoten und die Sprechblasen über `kit.bubbles(contents, outline, fill)`. `contents` sagt, was die Blase zeigen kann: `on`/`off` (Prompt mit blinkendem Cursor), `spin*`, `wait`, `done`.
   - Jedes Pet hat seinen eigenen Stil: Claude oranges „>“ und pulsierender Stern, Hermes gold, Astra lila „›“ und Stern, Gemini Funkelstern in Blau-Lila-Pink, Grok schwarzes „X“ und Terminal-Spinner `| / — \`.
   - Weiße Partikel brauchen `kit.shadowed`, sonst verschwinden sie auf hellem Desktop. Weiße Punkte in der Blase sind unsichtbar (die Blase ist fast weiß).
   - Auch Pets ohne Statusquelle brauchen `spin*`, `wait` und `done` im Atlas.
8. **Ausgabe:** `kit.write_atlas(...)` → `sprites/atlas.png` + `atlas.txt`, `kit.make_icon(head, …)` → `icon.ico` (BMP-Einträge!), Vorschaubilder.

Nach einem Umbau am Kit prüfen, dass Claudes `atlas.png`/`atlas.txt` byte-identisch bleiben. Die Pipeline ist deterministisch (k-means mit festem Seed): Zweimal laufen lassen, Hashes vergleichen.

### `atlas.txt`

```
cell 89 158                               # Größe jeder Figur-Zelle
frame <name> <x> <y>                      # normal_0 … , m_normal_0 (gespiegelt), bounce_1 …
sprite <name> <x> <y> <w> <h> <px> <py>   # Effekte; (px,py) = Pivot (Mitte bzw. Blasen-Spitze)
anchor <name> <x> <y>                     # Punkte in der ungespiegelten Zelle: bubble, zzz, logo, head
facing left                               # optional: ungespiegelt schaut die Figur nach links
anim <name> <sprite> <sprite> …           # optional: burst, twinkle*, z, spin
```

**Was das Programm aus dem Atlas liest:**
- **Pflicht:** `normal_0`, `blink_0`, `happy_0` (+ `m_`-Varianten), Sprites `bubble_r_*`/`bubble_l_*` für `on`, `off`, `wait`, `done` und den Spinner.
- **Optional:** `hover_0` (sonst happy), `look_0`, `sleep_0` (sonst blink), `drag_0`, `bounce_*` (sonst kein Hüpfer; „braucht dich“ wird dann Aufschauen plus Funkeln).
- **Phasen:** Anzahl der `normal_N`-Frames.
- **Partikel und Spinner:** kommen aus den `anim`-Zeilen, sonst gelten Claudes Standardnamen (`spark*`, `z*`, `spin0..4` pulsierend).
- **Blasenseite:** folgt der Blickrichtung (`facing` XOR gespiegelt). Anker `bubble` liegt deshalb auf der Seite, in die das Pet ungespiegelt schaut.

## 6. Laufzeit (C#)

- **Fenster:**
  - WinForms-Form mit `WS_EX_LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE`.
  - Gezeichnet wird in einen premultiplied 32-Bit-DIB, angezeigt per `UpdateLayeredWindow`.
  - `WM_MOUSEACTIVATE → MA_NOACTIVATE`: Anklicken klaut keinen Fokus.
- **Skalierung:** ganzzahlig, Nearest-Neighbor mit `WrapMode.TileFlipXY`. Pets sind Per-Monitor-DPI-aware, Tray und Einstellungen System-DPI-aware.
- **Timer:** 33 ms bei Bewegung, 55 ms beim Spinner, sonst 80 ms. Neu gezeichnet wird nur bei Änderungen.
- **Gesicht nach Priorität:** ziehen > schlafen > happy (Klick, Aufwachen) > hover/fertig/Lächeln (mit Blinzeln, falls es ein hover-Gesicht gibt) > blinzeln > aufschauen > normal.
- **Klick-Aktion:** `Launcher.BuildStartInfo(pet, settings)`. Im Website-Modus ein Shell-Execute-Start des geprüften Links, sonst `wt.exe` mit frischer Logon-Umgebung. `PetSettings.Website` ist der wirksame Modus.
- **Einstellungsfenster:** 700×504, eine Seite pro Pet.
  - Zeilen: Größe, Klick öffnet, dann Programm-Zeilen (`programRows`) oder Website-Zeilen (`linkRows`), Statusanzeige, Buttons.
  - Jede Änderung wird sofort gespeichert (`TrayHost.ChangeSetting` → `settings.ini` → Nachricht an das Pet).
- **Rechtsklick-Menü des Pets:** öffnen, Ordner (nur im Programm-Modus), Klick öffnet → Programm/Website, Größe, zurück in die Ecke, ausblenden, Einstellungen, beenden.
- **Tray ⇄ Pet:**
  - Pets heißen `aipets.pet.<id>` (Fenstertitel), das Tray-Fenster `aipets.host`.
  - Tray → Pet: registrierte Nachricht `aipets.command` (1 = Einstellungen neu laden, 2 = öffnen) oder `WM_CLOSE`.
  - Pet → Tray: `WM_COPYDATA` mit Text (`settings <id>`, `hide <id>`, `quit`).
  - Pets prüfen jede Sekunde, ob der Tray-Prozess noch lebt, und schließen sich sonst.
  - Settings-Änderungen erkennen alle Prozesse zusätzlich am Zeitstempel von `settings.ini`.
- **Einzelinstanz:**
  - Mutexe `Local\aipets.tray` und `Local\aipets.pet.<id>`.
  - Ein zweiter Start öffnet die Einstellungen des laufenden Trays.
  - Ein Pet, dessen Mutex noch belegt ist, beendet sich mit Code 3. Das Tray versucht es dann nach 2 s nochmal, ohne das als Absturz zu zählen.
- **Absturz im Pet:** `ThreadException` → Log → `Environment.Exit(1)`, das Tray startet es neu. Das Tray selbst loggt Fehler und läuft weiter.

## 7. Status-Protokoll (Hooks → Datei → Pet)

`aipets.exe --hook <quelle> <event>`: liest das Hook-JSON von stdin (immer komplett!), gibt **nichts** aus und schreibt `%APPDATA%\aipets\status\<quelle>\<datei>.txt` mit `State|Unix-ms|Agent-PID|Extra`.

**Claude Code** (`quelle=claude`, eine Datei pro `session_id`, Extra = Transcript-Pfad):

| Event | Aufruf | Wirkung |
|---|---|---|
| UserPromptSubmit | `--hook claude working` | arbeitet |
| PostToolUse / PostToolUseFailure (async) | `--hook claude resume` | nur Waiting → Working |
| Notification `permission_prompt\|elicitation_dialog\|…` | `--hook claude waiting` | braucht dich |
| Stop | `--hook claude done` | fertig |
| SessionEnd | `--hook claude end` | Datei löschen |

- **Zeitstempel:** die Prozess-Startzeit. Dadurch überschreiben verspätete async-Hooks keinen neueren Zustand. Ein Named Mutex `Local\aipets.status.<quelle>-<session_id>` hält Lesen, Vergleichen und Schreiben zusammen.
- **PID:** vom Vorfahren `claude.exe`.
- **Esc-Abbruch:** erkennt das Pet am Transcript-Marker `[Request interrupted by user`.
- **Stale-Timeout:** 15 min.

**Hermes Agent** (`quelle=hermes`, Shell-Hooks in `config.yaml`, eine Datei pro Hermes-Prozess `hermes-<pid>.txt`, Extra = offene `turn_id`s):

| Hermes-Event | Aufruf | Wirkung |
|---|---|---|
| pre_llm_call | `--hook hermes working` | Turn öffnen, arbeitet |
| pre_approval_request | `--hook hermes waiting` | braucht dich |
| post_approval_response | `--hook hermes resume` | Waiting → Working |
| on_session_end | `--hook hermes done` | Turn schließen; erst wenn keiner mehr offen ist: `completed` → fertig, sonst still |
| on_session_finalize | `--hook hermes end` | Datei löschen |

- **Aufruf:** Hermes ruft Shell-Hooks synchron aus dem Agent-Prozess auf. Die Eltern-PID des Hooks ist deshalb der Hermes-Prozess.
- **Subagents:** öffnen eigene Turns im selben Prozess. Deshalb zählt die Datei offene Turns, und ein Named Mutex `Local\aipets.status.hermes-<pid>` serialisiert parallele Hooks.
- **Freigaben:** Hermes verlangt pro (Event, Befehl) eine einmalige Freigabe (`shell-hooks-allowlist.json`, `hermes hooks list`).
- **Stale-Timeout:** 2 h, nur für verlorene Hooks. Das Ende eines Turns meldet Hermes immer, auch bei Abbruch.

**Codex** (`quelle=codex`, `hooks.json` im Codex-Home, eine Datei pro `session_id` wie bei Claude, Extra = `transcript_path`, bei Codex meist `null`):

| Codex-Event | Aufruf | Wirkung |
|---|---|---|
| UserPromptSubmit (async) | `--hook codex working` | arbeitet |
| PermissionRequest (async) | `--hook codex waiting` | braucht dich |
| PostToolUse (async) | `--hook codex resume` | Waiting → Working, hält einen laufenden Turn frisch |
| Stop (async) | `--hook codex done` | fertig |
| Interrupt (async) | `--hook codex idle` | Esc: still |
| SessionEnd | `--hook codex end` | Datei löschen |

- **Aufruf:** Codex startet Hook-Befehle unter Windows mit `powershell.exe -NoProfile -Command "<befehl>"`. Deshalb `& '…\aipets.exe' --hook codex … | Out-Null`. Ohne `Out-Null` wartet PowerShell nicht auf die GUI-exe und der Vorfahre `codex.exe` ist nicht mehr sicher zu finden.
- **Async:** PowerShell braucht rund eine halbe Sekunde zum Starten. Deshalb laufen alle Hooks im Hintergrund, außer SessionEnd (bei Codex immer synchron, höchstens 3 s). Die Reihenfolge sichern Prozess-Startzeit und Mutex.
- **PID:** vom Vorfahren `codex.exe`. Das gilt auch für den App-Server der Codex-Desktop-App, die dieselbe `hooks.json` liest.
- **Stop kommt nur, wenn ein Turn normal endet.** Bricht er mit einem Fehler ab, meldet Codex nichts.
  - Nach 15 min ohne Hook zeigt das Pet still, die Datei bleibt aber auf Working.
  - Läuft ein einzelner Befehl länger, holt das nächste `resume` den Spinner zurück.
- **Freigaben:** Codex führt Hooks erst nach einer Freigabe aus (`/hooks`).
  - Gespeichert wird sie als `[hooks.state.'<pfad>\hooks.json:<event>:0:0'] trusted_hash` in `config.toml`. Der Hash hängt am Befehl: Liegt die exe woanders, sind neue Freigaben nötig.
  - Ohne TUI geht es über `codex app-server` (JSON-RPC über stdio), genau wie `/hooks`: `initialize`, `initialized`, dann `hooks/list` (`cwds`) für `key` und `currentHash`, dann `config/batchWrite` mit `keyPath: "hooks.state"`, `value: {key: {trusted_hash}}`, `mergeStrategy: "upsert"`.
- **Stale-Timeout:** 15 min seit dem letzten Hook.

**Pet** (`StatusMonitor`, alle 500 ms):
- Stirbt der Agent-Prozess, wird die Datei gelöscht.
- Waiting schlägt Working schlägt Idle; der neueste Done-Zeitstempel zeigt den Haken.
- Der Haken bleibt, bis du ein paar Sekunden wieder am PC warst.

**Für eine andere Quelle:** eine neue `case` in `HookCommand.Run` und `status=<quelle>` in der pet.ini. Das Dateiformat ist bewusst simpel, notfalls schreibt ein Skript die Dateien direkt. Gemini und Grok (Websites) haben keine Statusquelle.

## 8. Neues Pet – Checkliste

1. Ordner `pets\<id>\` mit `art\` anlegen. Das `make_sprites.py` des ähnlichsten Pets als Vorlage kopieren:
   - Hermes = nur Gesicht (sitzend).
   - Claude = Körperanimation.
   - Astra = Funkeln, Ohren, Logo nach dem Spiegeln.
   - Gemini = gezeichnete Augen, Winken, farbiges SVG-Logo.
   - Grok = Brille, Uhr entfernt, einfarbiges Polygon-Logo, dunkle Kleidung.
2. Bild nach `art\source.*`. Am besten eine freigestellte Figur auf einfarbigem Hintergrund, groß genug fürs Gesicht (nach dem Verkleinern ≥ 20 px Gesichtsbreite).
3. **Bildspezifisches** anpassen: Freistellen, `ENLARGE`/`FACTOR`, Details, Gesichts-Patches, Animation, Logo, `anchors`, Icon-Ausschnitt.
   - **Vorgehen:** erst nur pixelisieren und neben die anderen Pets legen, dann Symbol-Dump und Grid-Zoom anschauen, dann die Koordinaten eintragen.
   - Arbeitsskripte (Zooms, Dumps, Vergleiche) in einen Scratch-Ordner, nicht ins Repo.
4. `pet.ini` schreiben: `mode`, `program`/`find`/`args`/`shell` und `url` (beides, damit man umschalten kann), `status`, `home` ≠ andere Pets.
5. `.\build.ps1 -Art -Pet <id>` (oder erst eine Test-exe), dann `aipets.exe --snapshot <ordner> --pet <id>` und die PNGs anschauen (alle Zustände plus Einstellungsseite).
6. `aipets.exe --command <id>` prüfen. Einmal echt klicken, wenn der Befehl stimmt.
7. Für den Status die Hooks des Agents eintragen und mit `--status <quelle>` prüfen.
8. README-Tabelle, diese Datei (Tabelle in 1, Art-Notizen in 5) und `AGENTS.md` (Stand) nachziehen.

## 9. Stolperfallen (alle schon einmal passiert)

- **„Desktop anzeigen“ (Win+D)** meldet über `SHQueryUserNotificationState` dasselbe wie Vollbild. Deshalb zusätzlich prüfen: Vordergrundfenster nicht Progman/WorkerW/Taskleiste, gleicher Monitor, deckt den ganzen Monitor ab.
- **Windows Terminal reicht die Umgebung des Aufrufers weiter.** Lösung: frische Logon-Umgebung per `CreateEnvironmentBlock`.
- **`wt.exe`:** Ein Pfad mit `\` am Ende maskiert das schließende Anführungszeichen. Argumente mit Leerzeichen setzt wt selbst wieder in Anführungszeichen, `;` trennt bei wt Befehle.
- **Esc löst in Claude Code kein `Stop` aus.** Ohne Transcript-Check würde der Spinner ewig laufen.
- **`PostToolUse` feuert auch nach `Stop`** (Hintergrund-Agents). Deshalb belebt `resume` bei Claude nur einen Waiting-Zustand.
- **Hook-Pfad schnell halten:** `--hook` darf keine WinForms-Typen anfassen (`Run` mit `NoInlining`, `App.ExePath` über Reflection statt `Application`).
- **Hermes schickt bei `pre_llm_call` die ganze Konversation auf stdin.**
  - Der Hook muss stdin komplett lesen, sonst sieht Hermes einen Broken Pipe.
  - Die IDs stehen vor dem Nutzertext; die Regex nimmt jeweils den ersten Treffer.
- **YAML:** Windows-Pfade in `config.yaml` nur in einfachen Anführungszeichen. In doppelten ist `\U…` ein Escape.
- **Codex-Hooks laufen in Windows PowerShell 5.1**, nicht direkt: GUI-exe nur mit `| Out-Null` aufrufen (siehe Abschnitt 7). Ein Exit-Code aus einem inneren Aufruf kommt nur mit `; exit $LASTEXITCODE` bei Codex an.
- **Codex zum Testen ohne Spuren:** `codex exec --ephemeral --ignore-user-config --dangerously-bypass-hook-trust -c "hooks.<Event>=[{hooks=[{type=…,command=…}]}]"`. Ein zusätzlicher UserPromptSubmit-Hook mit `Start-Sleep -Seconds 4; [Console]::Error.WriteLine(1); exit 2` blockt den Prompt, dann gibt es keinen Modellaufruf.
- **Link öffnen:** `ProcessStartInfo` mit `UseShellExecute = true` darf nie `EnvironmentVariables` anfassen, auch nicht lesend fürs Log. Schon das Anlegen des Dictionarys lässt `Process.Start` werfen. `Process.Start` gibt `null` zurück, wenn der Browser schon läuft.
- **RadioButtons in WinForms** bilden pro Container eine Gruppe. „Klick öffnet“ steht deshalb in einem eigenen `Panel`, sonst schaltet „Website“ die Größen-Buttons ab.
- **Icons:** `System.Drawing.Icon` liest keine PNG-komprimierten ICO-Einträge (Pillow-Standard). Daraus wird bunter Pixelmüll. Deshalb `bitmap_format="bmp"`.
- **`new Bitmap(pfad)` sperrt die Datei**, solange das Bitmap lebt. Der Atlas wird deshalb aus dem Speicher dekodiert.
- **Einstellungsfenster-Snapshot:**
  - Ein nie gezeigtes Form rendert per `DrawToBitmap` keine Kinder.
  - Deshalb wird es kurz unsichtbar gezeigt: `Opacity 0`, außerhalb des Bildschirms, ohne Aktivierung.
- **Aus PowerShell testen:**
  - `aipets.exe` ist eine GUI-exe. `$json | & aipets.exe --hook …` wartet nicht, die Ergebnisse kommen versetzt.
  - Deshalb `System.Diagnostics.Process` mit umgeleitetem stdin/stdout und `WaitForExit`.
  - `Add-Type`-Typen und Funktionen leben nur in einem Aufruf. `H` ist in PowerShell ein Alias (`Get-History`), eigene Funktionen nicht so nennen.
- **Premultiplied Alpha:** Für `UpdateLayeredWindow` in ein `Format32bppPArgb`-Bitmap über dem DIB zeichnen, nicht `GetHbitmap()` pro Frame.
- **`DrawImageUnscaled`** skaliert nach der DPI des PNG. Immer mit expliziten Pixel-Rechtecken zeichnen.
- **Testen, ohne den User zu stören:** nicht seine Maus bewegen. `--snapshot`, `--command`, `--status`, `--dry-run` genügen. Screenshots nur lesend per BitBlt.
- **Pet „fehlt“ auf dem Desktop:** Erst Fensterliste und `settings.ini` prüfen. Der User verschiebt die Pets gern selbst, manchmal gleich nach dem Start an den Rand.
- **Ins Chat eingefügte Bilder können das falsche sein** (einmal kam das Gemini-Bild statt Grok): Hash mit bisherigen Vorlagen vergleichen und auf dem Desktop nach der passenden Datei schauen.
- **Die Bildvorlagen sind nicht selbst gezeichnet:** privat ok, vor dem Veröffentlichen fragen.
