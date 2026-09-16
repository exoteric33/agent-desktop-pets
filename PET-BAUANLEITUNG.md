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
    - Er ist standardmäßig an: Das Tray schaltet ihn beim Start ein (`Autostart.ApplyDefault`), auch wenn ein alter Pfad drinsteht.
    - Ausnahmen: Der User hat ihn ausgeschaltet (`[app] autostart=0`, geschrieben von `Autostart.Choose`), die exe heißt nicht `aipets.exe` (Test-Kopien), oder `--dry-run` läuft.
- **Pet-Fenster:**
  - Die Figur steht bzw. sitzt unten auf der Taskleiste, immer im Vordergrund. Transparente Stellen lassen Klicks durch.
  - **Maus drüber:** Hover-Gesicht plus Sprechblase mit Prompt.
  - **Klick:** Funken, Hüpfer (falls das Pet Bounce-Frames hat), dann öffnet sich je nach Modus das **Programm** (im Windows Terminal), die **Desktop-App** oder die **Website** (im Standardbrowser).
  - **Programm, Desktop-App oder Website:** Programm und Website kann jedes Pet, die Desktop-App nur ein Pet mit `app=` in der pet.ini (Claude, Hermes, Astra). Umschalten in den Einstellungen („Klick öffnet“) oder im Rechtsklick-Menü des Pets („Klick öffnet“ → Programm / Desktop-App / Website).
  - **Ziehen:** frei verschiebbar. In Taskleistennähe rastet das Pet ein und schaut zur Bildschirmmitte (Sprites gespiegelt).
  - **Leerlauf:** Nach 1 Minute ohne Eingabe schläft das Pet ein (Zzz).
  - **Vollbild:** Bei Vollbild-Apps blendet es sich aus, bei „Desktop anzeigen“ nicht.
- **Agent-Status über Hooks:** Spinner-Blase = arbeitet, „?“ = braucht dich, grüner Haken = fertig.

| Pet | Animation | Klick (Standard) | Desktop-App / Website / Programm als Alternative | Status |
|---|---|---|---|---|
| Claude | Atmen, wehende Haare, Antenne, Blinzeln, Lächeln, Hüpfer | Programm: `wt → claude.exe --dangerously-skip-permissions` | App-Paket `Claude_pzs8sxrjxfjjc!Claude` (sonst `%LOCALAPPDATA%\AnthropicClaude\claude.exe`); `https://claude.ai/new` | Claude-Code-Hooks |
| Hermes | nur Gesicht: Blinzeln, Aufschauen, Lächeln, glücklich, schlafen; Musiknoten aus dem Kopfhörer | Programm: `wt → powershell -NoExit → hermes --yolo` | Hermes Desktop: `hermes-agent\apps\desktop\release\win-unpacked\Hermes.exe`, ohne sie `hermes desktop` im Terminal; `https://hermes-agent.nousresearch.com/` | Hermes-Shell-Hooks |
| Astra | Atmen, wehende Haare, funkelnde Sterne in den Galaxie-Strähnen, Ahoge, Katzenohren stellen sich auf, Blinzeln, Aufschauen, „:3“, glücklich, schlafen, Hüpfer; OpenAI-Logo auf dem Shirt | Programm: `wt → cmd /c codex.cmd --dangerously-bypass-approvals-and-sandbox` | Codex-App: `codex app` im Arbeitsordner, ohne Fenster (ohne CLI das App-Paket `OpenAI.Codex_2p2nqsd0c76g0!App`, Windows zeigt „ChatGPT“); `https://chatgpt.com/codex` | Codex-Hooks |
| Gemini | Atmen, wehende Haare, Ahoge, Blinzeln, Aufschauen, glücklich mit Zunge wie im Bild, schlafen, Hüpfer; winkt beim Hover und Klick; Google-„G“ auf dem Shirt | Website: `https://gemini.google.com/app` | Programm `gemini` (Gemini CLI, nicht installiert) | – |
| Grok | Atmen, wehende Haarspitzen, Ahoge, Blinzeln, lacht beim Aufschauen, zwinkert beim Hover wie im Bild, Peace-Zeichen wippt bei Hover und Klick, glücklich, schlafen, Hüpfer; xAI-Logo auf dem Shirt | Website: `https://grok.com/` | Programm `grok` (nicht installiert) | – |

## 2. Ordner

```
aipets\
├─ aipets.exe             Programm (Tray, Pets und Hook-Befehl in einer Datei; nicht im Repo, build.ps1 baut sie)
├─ build.ps1              baut die exe  (-Art = Icon und Sprites vorher neu, -Pet <id> = nur dieses Pet)
├─ install.cmd            Doppelklick: build.ps1, dann aipets.exe --install (Autostart, Hooks + Freigaben, starten)
├─ uninstall.cmd          Doppelklick: aipets.exe --uninstall (beenden, Autostart aus, Hooks raus)
├─ README.md              für den User: Installieren, Bedienung, Hooks von Hand
├─ PET-BAUANLEITUNG.md    diese Datei: Technik, Art-Pipeline, Status-Protokoll, Stolperfallen
├─ AGENTS.md              Einstieg für Agents: Arbeitsweise, Stand, lokale Einrichtung
├─ CLAUDE.md              lädt AGENTS.md für Claude Code
├─ art\
│  ├─ pixelkit.py         gemeinsame Pipeline: Flood-Fill, Pixelisieren, Patches, Blasen, Atlas, Icon
│  ├─ app_icon.py         → aipets.ico (Tray und exe)
│  └─ aipets.ico
├─ pets\
│  ├─ claude\
│  │  ├─ pet.ini          Name, Modus, Programm, Desktop-App, Link, Terminal, Statusquelle, Home-Position
│  │  ├─ art\             source.png, make_sprites.py, preview\ (nicht im Repo)
│  │  └─ sprites\         atlas.png, atlas.txt, icon.ico  ← liest das Programm zur Laufzeit
│  ├─ hermes\             genauso (source.jpg)
│  ├─ astra\              genauso (source.png)
│  ├─ gemini\             genauso (source.png)
│  └─ grok\               genauso (source.png)
└─ src\
   ├─ Program.cs          Einstieg: Tray / --pet / --hook / --snapshot / --status / --command / --install / --uninstall
   ├─ Setup.cs            Einrichten und Entfernen: Autostart, Hooks in Claude-/Codex-/Hermes-Configs, Freigaben
   ├─ Json.cs             kleiner JSON-Leser/-Schreiber, der Schlüsselreihenfolge und Werte unverändert lässt
   ├─ TrayHost.cs         Tray-Icon, Menü, Pet-Prozesse starten und neu starten, Befehle der Pets
   ├─ SettingsForm.cs     Einstellungsfenster (Seite pro Pet, „Klick öffnet: Programm / Desktop-App / Website“, „Hooks einrichten“)
   ├─ PetForm.cs          Pet-Fenster, Animation, Maus, Menü, Status-Anzeige
   ├─ Pets.cs             pet.ini lesen (PetInfo), wirksame Einstellungen (PetSettings, Mode/UseMode)
   ├─ Atlas.cs            atlas.png/atlas.txt laden, Figurenhöhe messen, Frames und Sprites zeichnen
   ├─ Launcher.cs         Klick-Aktion: Programm finden und Terminal-Befehl bauen, Desktop-App starten, oder Link prüfen und im Browser öffnen
   ├─ DesktopApp.cs       Desktop-App finden (App-Paket per App-ID oder exe-Pfad), Name, Version, Startinfo
   ├─ Status.cs           Hook-Befehl (claude, hermes, codex) + Auswertung der Statusdateien
   ├─ Native.cs           Win32: Layered Window, DPI, Idle, Vollbild, Logon-Umgebung, IPC
   ├─ Settings.cs         Altlast aus ClaudePet (nicht mehr benutzt, kompiliert aber mit)
   └─ App.cs              Pfade, settings.ini (Ini/Store), Autostart, Log
```

Laufzeitdaten liegen in `%APPDATA%\aipets\`:
- `settings.ini`: `[app]` (`size` für alle Pets, `autostart=0` nach einem „aus“) und ein Abschnitt pro Pet (`x`, `y`, `percent`, `enabled`, `workdir`, `mode`, `program`, `args`, `shell`, `app`, `url`), geschrieben von Tray und Pets, immer unter dem Mutex `Local\aipets.settings`
  - Ältere Dateien haben `scale` pro Pet. Das Tray übernimmt beim Start den häufigsten Wert (bei Gleichstand den größeren) als `[app] size` und löscht die alten Schlüssel (`TrayHost.MoveSizesToApp`).
  - Ein Pet mit anderer alter Größe bekommt sie als `percent` (5-%-Schritte, 50–200).
- `aipets.log`: jede Zeile mit `[tray]`, `[claude]`, `[hook hermes]` …
- `status\<quelle>\*.txt`

## 3. Bauen und testen

- **Voraussetzung Code:** nichts extra. Der C#-Compiler von .NET Framework 4.x ist in Windows enthalten (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
  - **Achtung:** nur C#-5-Syntax, also kein `$"..."`, kein `?.`, keine `=>`-Properties, kein `nameof`, kein `out var`.
- **Voraussetzung Sprites:** Python mit `numpy` und `Pillow`.
- `.\build.ps1` beendet ein laufendes aipets aus diesem Ordner, baut und startet es danach wieder (über `explorer.exe`, damit es nicht die Umgebung der Shell erbt).
  - `.\build.ps1 -Art -Pet grok` erzeugt vorher nur Groks Sprites neu.
  - Auf 32-Bit-Windows nimmt es `Framework\…\csc.exe` statt `Framework64`.
- **`install.cmd`:** `build.ps1` (mit `-ExecutionPolicy Bypass`), bei Fehler Meldung und `pause`, sonst `aipets.exe --install`.
  - Das zeigt am Ende eine Zusammenfassung (✓ = erledigt oder war schon so, – = übersprungen oder fehlgeschlagen) und startet das Tray, falls es nicht läuft.
  - Exit-Code 0, wenn alles geklappt hat oder ein Agent nur nicht installiert ist.
  - `--quiet` lässt alle Fenster weg; die Zusammenfassung steht dann auf stdout und im Log (`[install]`).
- **`uninstall.cmd`:** `aipets.exe --uninstall` fragt nach, beendet das Tray per IPC (`quit`), schaltet den Autostart aus und entfernt die Hooks. `%APPDATA%\aipets` bleibt.
  - Das merkt sich kein „aus“: Startet jemand aipets danach wieder, ist der Autostart wieder an (Standard). `--install` nimmt ein früheres „aus“ zurück.
- **Testen, ohne den Desktop anzufassen:**
  - **Test-exe:** mit demselben `csc`-Aufruf wie in `build.ps1`, aber `/out:aipets-test.exe`, in den aipets-Ordner kompilieren (dann findet sie `pets\`). Das laufende aipets bleibt unberührt. Danach löschen.
  - `aipets.exe --snapshot <ordner> [--pet id]` rendert jedes Pet in allen Zuständen (idle, hover, look, sleep, click, working, waiting, done, beide Blickrichtungen) als PNG, in ganzen 3×-Pixeln.
    - `lineup.png`: alle Pets bei Größe 2 (ohne ihren eigenen Anteil) nebeneinander auf einer Grundlinie. Jeder Kopf muss die obere Linie berühren (2 × 162 px über dem Boden).
    - Click und working enthalten zufällige Funken und sind bei jedem Lauf anders, alle anderen Bilder bleiben byte-gleich.
    - Dazu kommt das Einstellungsfenster, mit `--pet` auf der Seite dieses Pets: `settings.png` im gespeicherten Modus, `settings-program.png`, `settings-app.png` (nur mit Desktop-App) und `settings-website.png`.
  - `aipets.exe --command <id> [--mode program|app|website]` gibt aus, was ein Klick starten würde (Terminal-Befehl, Desktop-App oder Link). `--mode` ändert dabei nichts an `settings.ini`. Fehler (Programm nicht gefunden) stehen in der Ausgabe, Exit-Code 1.
  - `aipets.exe --status <quelle>` gibt den zusammengefassten Agent-Status aus.
  - `--dry-run` (Tray oder Pet): Klicks protokollieren nur in `aipets.log`.
  - Das Programm ist eine GUI-exe. PowerShell wartet nicht darauf und liest ihre Ausgabe nur zuverlässig über `System.Diagnostics.Process` mit Umleitung (siehe Stolperfallen).
  - **Logik-Tests ohne Fenster:** alle `src\*.cs` plus eine eigene Testklasse mit `/target:exe /main:AiPets.<Klasse>` kompilieren (so wurden `Launcher.NormalizeUrl`, die Modus-Auswahl und `DesktopApp` geprüft).
    - Die Test-exe gehört in den aipets-Ordner, sonst findet `PetInfo.ById` die Pets nicht. Einstellungen baut die Testklasse selbst: `PetSettings.From(pet, Ini.Parse(new[] { "[claude]", "mode=app" }, "app"))`.
  - **Setup-Tests:** nur gegen Kopien der Configs in einem Scratch-Ordner. Die Setup-Methoden nehmen Pfade (siehe Abschnitt 7, „Einrichten“). Die Codex-Freigabe mit `CODEX_HOME` auf einen leeren Ordner. Echte Dateien vorher und nachher per Hash vergleichen.

## 4. `pet.ini`

```ini
name=Hermes                                  ; Anzeigename
order=2                                      ; Reihenfolge in Menü und Einstellungen
open=Hermes öffnen                           ; Menütext
mode=program                                 ; program | app | website: was ein Klick öffnet
program=hermes                               ; Name im PATH oder voller Pfad
find=%LOCALAPPDATA%\hermes\bin\hermes.exe    ; bekannte Installationsorte, vor dem PATH probiert (;-getrennt)
args=--yolo
shell=powershell                             ; direct | powershell | cmd
app=%LOCALAPPDATA%\hermes\…\Hermes.exe;…     ; Desktop-App für mode=app: App-IDs oder exe-Pfade (;-getrennt)
appfallback=desktop                          ; ohne gefundene App: das Programm mit diesen Argumenten im Terminal
url=https://hermes-agent.nousresearch.com/   ; Website für mode=website
status=hermes                                ; claude | hermes | codex | leer
home=115                                     ; Abstand rechter Bildschirmrand → Zelle, in Pixeln bei Größe 1×
```

- **Einstellungen:** Die Werte hier sind Standardwerte. Was du in den Einstellungen änderst, landet in `settings.ini` und überschreibt sie. Die „zurücksetzen“-Links löschen diese Überschreibungen wieder.
- **`mode`:** `program`, `app` oder `website`. Fehlt der Schlüssel, gilt: nur `url` und kein `program` → `website`, sonst `program`. Unbekannte Werte werden `program`, `app` ohne `app=` auch (`PetSettings.UseMode`).
  - Umschalten speichert `mode` in `settings.ini`. Wird der pet.ini-Standard gewählt, wird der Schlüssel dort gelöscht.
  - Im Programm-Modus zeigen die Einstellungen Programm, Argumente, „Öffnen in“ und Arbeitsordner. Im Website-Modus zeigen sie „Link“, „Öffnen in: Standardbrowser“ und „Link zurücksetzen“.
  - Im Desktop-App-Modus zeigen sie „App“: die gefundene App mit Version, darunter das App-Paket oder den Pfad, sonst „nicht gefunden“ und was ein Klick dann tut. Dazu „…“ (eine exe wählen, landet als `app` in `settings.ini`) und „App zurücksetzen“.
    - Öffnet `appcommand` die App, steht darunter der Befehl, und der Arbeitsordner ist auch zu sehen.
- **`shell=direct`:** Das Terminal startet das Programm selbst, der Tab schließt mit dem Programm.
- **`shell=powershell` / `cmd`:** Das Programm läuft in dieser Shell, und die bleibt danach offen.
- **`app`:** die Desktop-App. Kandidaten mit `;` getrennt, Umgebungsvariablen erlaubt, der erste installierte gilt (`DesktopApp.Find`). Ohne `app=` bieten Einstellungen und Menü keine Desktop-App an.
  - **App-ID** (`Paketfamilie!App`, so wie `Get-StartApps` sie zeigt) für App-Pakete (MSIX/Store): Claude `Claude_pzs8sxrjxfjjc!Claude`, Codex `OpenAI.Codex_2p2nqsd0c76g0!App`.
    - Installiert ist sie, wenn `GetPackagesByPackageFamily` ein Paket liefert. Die Version kommt aus dessen vollem Namen, der Anzeigename vom Shell-Item `shell:AppsFolder\<App-ID>`.
    - Gestartet wird per Shell-Execute von `shell:AppsFolder\<App-ID>`, wie ein Klick im Startmenü.
  - **exe-Pfad** für normale Programme (Hermes Desktop): startet in ihrem eigenen Ordner mit frischer Logon-Umgebung, wie die Startmenü-Verknüpfung. Name und Version kommen aus der Versionsinfo der exe.
  - Vor dem Start gibt das Pet mit `AllowSetForegroundWindow(-1)` sein Vordergrundrecht aus dem Klick weiter, damit die App nach vorn kommen darf.
- **`appcommand`:** Das Programm öffnet die App selbst, mit diesen Argumenten statt `args` (Codex: `appcommand=app` → `codex app`).
  - Es läuft ohne Fenster im Arbeitsordner (npm-`.cmd` über `cmd.exe /c`), mit frischer Logon-Umgebung. `Launcher.RunHidden` wartet höchstens 60 s, die Ausgabe kommt ins Log, ein Exit-Code ≠ 0 als Meldung.
  - Das gilt nur für die App aus der pet.ini. Eine mit „…“ gewählte exe startet direkt, und ohne das Programm (CLI nicht installiert) startet die App aus `app=` direkt.
  - `app=` braucht es trotzdem: für Name und Version in den Einstellungen und für diesen direkten Start.
- **`appfallback`:** Wird keine App gefunden, startet ein Klick das Programm (`program`, `find`, `shell`, Arbeitsordner) mit diesen Argumenten statt `args` im Terminal.
  - Nur Hermes nutzt das: `hermes desktop` baut Hermes Desktop einmal (npm und Electron, dauert Minuten), startet sie und wartet, bis sie geschlossen wird. Danach findet `app=` die gebaute exe.
  - Ohne `appfallback` kommt die Meldung „Die Desktop-App wurde nicht gefunden“.
- **`url`:** Es gelten nur http(s)-Links. Eingaben wie `gemini.google.com` bekommen `https://` davor. Alles andere (Dateipfade, `file://`, `javascript:`) wird abgelehnt: im Einstellungsfeld springt der alte Wert zurück, beim Klick kommt eine Meldung.
- **Programm nicht installiert:** Ein Klick zeigt „… wurde nicht gefunden“ (z. B. Gemini oder Grok im Programm-Modus ohne CLI).
- **Mehrere Pets:** Die Home-Positionen müssen sich unterscheiden, sonst sitzen die Pets übereinander.
  - `home` wird mit der Größe malgenommen, gilt also für alle Pets gleicher Größe.
  - Home = Home des rechten Nachbarn + dessen Zellbreite × 162 / Figurenhöhe + 4, aufgerundet. Das ist seine Breite bei 1×, siehe Abschnitt 6, „Größe“.
  - Reihenfolge von rechts: Claude 12, Hermes 115, Astra 211, Gemini 334, Grok 482.

## 5. Art-Pipeline (`art/pixelkit.py` + `pets/<id>/art/make_sprites.py`)

1. **Freistellen** (bildspezifisch):
   - Hintergrund per Flood-Fill vom Rand entfernen, dann den größten zusammenhängenden Blob behalten.
   - Claude: `pockets` für eingeschlossene Hintergrundlöcher.
   - Hermes: grauen Bodenschatten wegfluten und weiße Randlichter in Haaren und Stiefeln schwarz malen. Beim Verkleinern würden sie zu Pünktchen.
   - Astra: dieselbe Vorlage wie Claude (Shirt-Schriftzug „ASTRA 6“, Ärmeltext, Wasserzeichen). Schrift wird mit Weiß zugeflossen, das Wasserzeichen auf dem Rock bekommt das glatte Lila (Zufließen würde die Faltenlinien hineinziehen).
   - Gemini: Der Hintergrund ist leicht verrauschtes Off-White (Referenz 250, Toleranz 12), dazu zwei `pockets` zwischen Haarsträhnen. Den Schriftzug „3.8 Flash“ (blaue Ziffern, dunkle Buchstaben) findet die Maske über Helligkeit *oder* Sättigung.
   - Grok (Vorlage `grok-chan-pixel-ohne-tablet.png`, 1024×900): Das Bild ist nur im Pixel-Stil gezeichnet, ohne sauberes Raster, und läuft deshalb durch die normale Pipeline.
     - Ihre Locken schließen viele Hintergrundlücken ein. Jede reinweiße Fläche (≥ 12 px) außerhalb von `KEEP_WHITE` (weiße Strähne, Gesicht, Schriftzug, Haarspange) wird als Loch entfernt.
     - Den weißen Schriftzug „grok“ auf dem schwarzen Shirt nicht zufließen lassen: Das zieht den dunklen Rand der Buchstaben mit und hinterlässt ein „grok“-Schattenbild. Stattdessen den Kasten flach mit dem Median-Schwarz der Zeilen darüber und darunter füllen.
2. **`kit.pixelise(rgb, fg, factor, palette, outline, outline_bottom)`:**
   - Blöcke verkleinern. Dünne dunkle Linien bleiben erhalten: Hat ein Block genug dunkle Pixel, bekommt er deren Farbe.
   - Palette per k-means (Lab), einzelne Pixel glätten, 1 px Außenlinie.
   - `outline_bottom=False`, wenn die Figur unten abgeschnitten ist (Claude, Astra, Gemini, Grok). Hermes sitzt komplett im Bild.
   - **Faktor:** Claude 3 bei 263×350 Vorlage. Hermes 6 bei 720×1280; kleiner geht nicht, sonst ist das Gesicht zu klein für Animationen.
   - **Maßstab zwischen den Pets:** Die Köpfe sollen etwa gleich groß sein (Astra: 46 px vom Haaransatz bis Kinn). Deshalb wird die Vorlage vor dem Faktor 3 skaliert (`ENLARGE`):
     - Astra: 252×304, `ENLARGE` 1,2, 28 Farben.
     - Gemini: 229×257, `ENLARGE` 1,5, 32 Farben für den Haarverlauf von Blau über Pink zu Lila.
     - Grok: 1024×900, `ENLARGE` 0,55 (verkleinern), 28 Farben. Damit ist sie etwa so groß wie Hermes (118×162).
   - Vorher Varianten nebeneinander mit den fertigen Pets vergleichen (Atlas-Frame `normal_0` der anderen daneben legen).
   - Die Gesamthöhe muss dabei nicht passen: Das Programm zeigt jede Figur gleich hoch an (Abschnitt 6, „Größe“). `ENLARGE` bestimmt nur, wie fein die Figur gepixelt ist.
3. **Handarbeit** mit `kit.patch(img, x, y, rows, colors)` (Zeichen → Farbe, `.` = unverändert). Was die Verkleinerung zerstört, wird neu gesetzt: Claude-Sternchen, Hermes-Kopfhörer und „N“-Halsband.
   - **Logos statt Schriftzug** (Wunsch des Users): immer aus dem echten Vektorlogo rastern, nie freihändig.
     - Astra, OpenAI-Logo: 12 px, punktsymmetrisch gemittelt, drei Tinten (voll, 62 %, 30 % auf Shirt-Weiß). Unter 12 px zerfällt der Knoten.
     - Gemini, Google-„G“: aus den vier farbigen Pfaden des Vektorlogos (`svg_polygon` kann M, L, H, V, C, S, Z). Jedes Pixel nimmt die Farbe mit der größten Abdeckung, schwach abgedeckte Randpixel werden mit Shirt-Weiß gemischt.
     - Grok, xAI-Zeichen (vom User statt „grok“ gewählt): vier Polygone, 12×13 px, hell auf Schwarz, Randpixel zu 55 % gemischt.
     - Vorgehen: Größen 11–13 px mit 1–3 Tinten direkt auf dem Shirt bei 2× und 4× vergleichen, dann wählen.
   - Gemini: goldene Brosche und die Haarspange als vierfarbiger Stern.
   - Grok: Das xAI-Logo sitzt dort, wo „grok“ stand. Die Farbe fürs Mischen der Randpixel wird aus dem fertigen Basis-Sprite gelesen.
4. **Gesichter:** Die Pipeline zerstört Augen und Mund praktisch immer, sie werden als Patches gezeichnet.
   - Koordinaten findest du über einen Symbol-Dump des Basis-Sprites plus Grid-Zoom mit Koordinatenlinien (großer Zoom, Beschriftung alle 2 px).
   - Umrechnung Vorlage → Basis-Sprite: `x = (ENLARGE·x_src − x0)/3 + 1`, `y` genauso. `x0`/`y0` ist der Rand der erodierten Maske im skalierten Bild.
   - Faces-Vorschau in `preview\faces.png`.
   - Gemini hat in der Vorlage die Augen zu: Augen und Mund werden erst mit Haut übermalt, dann offene Augen gezeichnet. Ihr Kopf ist geneigt, das rechte Auge sitzt 3 px höher. „happy“ ist das „^^“ mit Zunge aus dem Bild.
   - Grok zwinkert in der Vorlage mit offenem Mund. Das Bild selbst ist deshalb „hover“.
     - „normal“ bekommt ein gezeichnetes offenes rechtes Auge (x 54–61, y 30–34) und ein Lächeln. „look“ hat beide Augen offen und das Lachen aus dem Bild.
     - Geschlossene Augen links nur über x 38–44 malen: Die Randspalten gehören zu Haar und Wimpernspitze.
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
     - Links erst unterhalb des Peace-Zeichens, sonst würde die Hand mitwackeln.
     - `bob_peace` hebt Finger und Handfläche bei hover und happy um 1 px an (Knick am Handgelenk).
     - Gesichter: normal, blink, look (Lachen), hover (Zwinkern aus dem Bild), happy, sleep.
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
- **Figurenhöhe:** sichtbare Zeilen in `normal_0`. Danach richtet sich die angezeigte Größe (Abschnitt 6).
- **Partikel und Spinner:** kommen aus den `anim`-Zeilen, sonst gelten Claudes Standardnamen (`spark*`, `z*`, `spin0..4` pulsierend).
- **Blasenseite:** folgt der Blickrichtung (`facing` XOR gespiegelt). Anker `bubble` liegt deshalb auf der Seite, in die das Pet ungespiegelt schaut.

## 6. Laufzeit (C#)

- **Fenster:**
  - WinForms-Form mit `WS_EX_LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE`.
  - Gezeichnet wird in einen premultiplied 32-Bit-DIB, angezeigt per `UpdateLayeredWindow`.
  - `WM_MOUSEACTIVATE → MA_NOACTIVATE`: Anklicken klaut keinen Fokus.
- **Größe:** Bei Größe n ist jedes Pet n × 162 px hoch (`SizeUnit`, die höchste Figur: Grok), egal wie viele Pixel ihr Sprite hat.
  - n = gemeinsame Größe (`[app] size`, 1 bis 4, auch Zwischenwerte) × eigener Anteil des Pets (`[id] percent`, 50–200, Standard 100), begrenzt auf 0,5 bis 6 (`PetSettings.PetSize`).
  - Ohne `percent` sind also alle Pets gleich hoch. Ohne `size` gilt `PetSettings.DefaultSize()`: 2 bei 96 dpi, mehr bei skalierten Anzeigen.
  - Ändern geht über die beiden Regler (`TrayHost.SetSize` schickt allen Pets sofort `CmdReload`, `percent` geht über `ChangeSetting`) oder über das Pet-Menü (`ShareSize`, `OwnSize`). Beim Loslassen speichern Pets nur `x`/`y`.
  - `home` wächst nur mit der gemeinsamen Größe, damit die Plätze stehen bleiben, wenn ein Pet größer wird.
  - Figurenhöhe = Zeilen vom obersten bis zum untersten sichtbaren Pixel in `normal_0` (`Atlas.FigureHeight`): Claude 118, Astra 120, Gemini 130, Hermes 157, Grok 162.
  - `zoom = n × 162 / Figurenhöhe` Bildschirmpixel pro Sprite-Pixel, bei 2×: Claude 2,75, Astra 2,7, Gemini 2,49, Hermes 2,06, Grok 2.
  - Weil 162 die höchste Figur ist, liegt `zoom` schon bei 1× nie unter 1, kein Sprite-Pixel geht verloren.
  - Gezeichnet wird mit Nearest-Neighbor und `WrapMode.TileFlipXY`. Bei krummem `zoom` sind Sprite-Pixel 2 oder 3 Bildschirmpixel breit, die Bilder selbst bleiben unverändert.
  - Zelle und Ränder werden je einmal gerundet (`CellPx`, `CanvasPxW/H`), damit die Figur genau an der Fensterunterkante endet. Der Hover-Test rechnet mit derselben Streckung zurück.
  - Pets sind Per-Monitor-DPI-aware, Tray und Einstellungen System-DPI-aware.
- **Timer:** 33 ms bei Bewegung, 55 ms beim Spinner, sonst 80 ms. Neu gezeichnet wird nur bei Änderungen.
- **Gesicht nach Priorität:** ziehen > schlafen > happy (Klick, Aufwachen) > hover/fertig/Lächeln (mit Blinzeln, falls es ein hover-Gesicht gibt) > blinzeln > aufschauen > normal.
- **Klick-Aktion:** `Launcher.BuildStartInfo(pet, settings)`. `PetSettings.Mode` ist der wirksame Modus (`OpensProgram`, `OpensApp`, `OpensWebsite`).
  - Website-Modus: ein Shell-Execute-Start des geprüften Links.
  - Desktop-App-Modus: erst `appcommand` ohne Fenster (`CreateNoWindow` kennzeichnet das für `Launch`), dann die gefundene App (Abschnitt 4, `app`), dann `appfallback` im Terminal, sonst eine Meldung.
  - Programm-Modus (`TerminalStartInfo`): `wt.exe` mit frischer Logon-Umgebung.
- **Einstellungsfenster:** 700×540, eine Seite pro Pet.
  - Zeilen: Größe aller Pets, Größe von <Pet>, Klick öffnet, dann Programm-Zeilen (`programRows`), App-Zeilen (`appRows`) oder Website-Zeilen (`linkRows`), Statusanzeige, Buttons.
  - Zwei `TrackBar`s (`SetupSlider`), jeder Schritt gilt sofort:
    - „Größe aller Pets“: 4 bis 16, also Viertelschritte von 1× bis 4×, Striche bei ganzen Größen. Daneben der Wert („2,5×“).
    - „Größe von <Pet>“: 10 bis 40, also 5-%-Schritte von 50 % bis 200 %, Striche alle 50 %. Daneben Anteil und Ergebnis („125 % · 2,5×“, `ShowSizes`).
  - Der Arbeitsordner (`folderRows`) steht im Programm-Modus und im Desktop-App-Modus, wenn `appcommand` gilt (`Launcher.AppViaProgram`).
  - „Desktop-App“ steht nur bei Pets mit `app=`, sonst rückt „Website“ an seine Stelle (Abstand aus `PreferredSize`, damit es bei jeder DPI passt).
  - Jede Änderung wird sofort gespeichert (`TrayHost.ChangeSetting` → `settings.ini` → Nachricht an das Pet).
  - **Statusanzeige:** `HookText` sucht in der Config des Agents nach dem Pfad dieser exe, so geschrieben, wie er dort steht (JSON: `\\`; YAML/PowerShell: `''`).
    - Texte: „✓ Hooks eingerichtet“, „Keine Hooks in …“ oder „Hooks rufen eine andere aipets.exe auf“.
    - Bei den letzten beiden erscheint der Link „Hooks einrichten“ (`Setup.Hooks(quelle, App.ExePath, true)`, Ergebnis als Meldung).
- **Rechtsklick-Menü des Pets:** öffnen, Ordner (im Programm-Modus und bei einer App über `appcommand`), Klick öffnet → Programm/Desktop-App/Website, Größe (Gruppe „Alle Pets“ 1×–4×, Gruppe „Nur <Pet>“ 75–150 %), zurück in die Ecke, ausblenden, Einstellungen, beenden.
  - „Desktop-App“ gibt es nur bei Pets mit `app=`. Der Tooltip zeigt die gefundene App oder was ein Klick ohne sie tut.
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
- **Freigaben:** Hermes verlangt pro (Event, Befehl) eine einmalige Freigabe (`shell-hooks-allowlist.json`, `hermes hooks list`). Setup trägt sie selbst ein (siehe „Einrichten“ unten).
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
  - Ohne TUI geht es über `codex app-server` (JSON-RPC über stdio), genau wie `/hooks`: `initialize`, `initialized`, dann `hooks/list` (`cwds`) für `key` und `currentHash`, dann `config/batchWrite` mit `keyPath: "hooks.state"`, `value: {key: {trusted_hash}}`, `mergeStrategy: "upsert"`. So macht es Setup (`TrustCodexHooks`).
- **Stale-Timeout:** 15 min seit dem letzten Hook.

**Pet** (`StatusMonitor`, alle 500 ms):
- Stirbt der Agent-Prozess, wird die Datei gelöscht.
- Waiting schlägt Working schlägt Idle; der neueste Done-Zeitstempel zeigt den Haken.
- Der Haken bleibt, bis du ein paar Sekunden wieder am PC warst.

**Für eine andere Quelle:** eine neue `case` in `HookCommand.Run`, `status=<quelle>` in der pet.ini und ein Zweig in `Setup.Hooks` (sonst trägt `install.cmd` die Hooks nicht ein) plus in `SettingsForm.HookText`. Das Dateiformat ist bewusst simpel, notfalls schreibt ein Skript die Dateien direkt. Gemini und Grok (Websites) haben keine Statusquelle.

**Einrichten** (`src/Setup.cs`, aufgerufen von `--install`, `--uninstall` und „Hooks einrichten“):
- **Ablauf:** Autostart an bzw. aus, dann `Setup.Hooks(quelle, exe, install)` für `claude`, `codex` und `hermes`.
  - Jeder Schritt liefert einen `Step` (Name, ok, Text). Ausnahmen werden zum Text des Schritts und landen im Log.
- **Welche Agents:** nur die, deren Home existiert, sonst „nicht installiert“ (es wird nichts angelegt).
  - Claude: `~\.claude`.
  - Codex: `CODEX_HOME`, sonst `~\.codex`.
  - Hermes: `HERMES_HOME`, sonst `%LOCALAPPDATA%\hermes` (falls vorhanden), sonst `~\.hermes`. Fehlt dort die `config.yaml`, heißt es „Hermes einmal starten“.
- **Nur eigene Einträge:** Ein Handler gehört aipets, wenn der Befehl `aipets.exe` und `--hook <quelle>` enthält (Codex, Hermes) bzw. `args` mit `--hook`, `<quelle>` beginnt (Claude).
  - Einträge eines alten Pfads werden an derselben Stelle ersetzt, fremde Hooks bleiben, wo sie sind.
  - Beim Entfernen verschwinden leer gewordene Gruppen, Events und Blöcke.
- **Schreiben:** nur bei einer echten Änderung, vorher `<datei>.bak-aipets` mit dem alten Inhalt, UTF-8 ohne BOM.
  - JSON bleibt bei CRLF, wenn die Datei CRLF hatte. In YAML behält jede Zeile ihr eigenes Zeilenende.
- **JSON** (`src/Json.cs`): `JsonObject` (Schlüssel in Originalreihenfolge), `List<object>` und `JsonValue` (Rohtext). Zahlen, Escapes und fremde Einträge bleiben so, wie sie waren. Geschrieben wird mit 2 Leerzeichen Einrückung. Eine neue Codex-`hooks.json` bekommt eine `description`.
- **Hermes-`config.yaml`:** zeilenweise, ohne YAML-Bibliothek.
  - **Entfernen:** Sucht den Block `hooks:` auf oberster Ebene und entfernt `- command: …aipets.exe… --hook hermes`-Einträge samt tieferer Zeilen, aber nie über das nächste `-` hinaus.
  - **Einfügen:** direkt unter dem vorhandenen Event-Schlüssel (ein `[]` fällt weg) oder als neuer Schlüssel am Blockende. Die Einrückung kommt von den vorhandenen Einträgen, auch `- ` auf Höhe des Schlüssels (PyYAML-Stil). Ohne Block kommen Kommentar `# aipets: …` und Block ans Dateiende.
  - **Schon aktuell:** Stehen genau die Einträge dieser exe drin, bleibt die Datei unberührt (`HermesUpToDate`), auch wenn fremde Hooks davor stehen.
  - **Aufräumen beim Entfernen:** Eigene leere Event-Schlüssel gehen weg, ein leerer Block samt Kommentar und Leerzeile auch. Ein vorher leeres `pre_llm_call: []` kommt nicht zurück; für Hermes ist das gleichwertig.
  - Geprüft mit PyYAML aus Hermes' venv: eingerückt (2/4), breit (4/8), PyYAML-Stil, CRLF ohne Zeilenende am Schluss, jeweils nach Installieren, Umzug der exe und Entfernen.
- **Hermes-Freigabe:** `shell-hooks-allowlist.json` → `approvals: [{event, command, approved_at, script_mtime_at_approval}]`. Hermes vergleicht nur Event und Befehl. Setup ergänzt fehlende Einträge, lässt passende stehen und löscht veraltete aipets-Einträge.
- **Codex-Freigabe:** `TrustCodexHooks` startet `codex app-server` (bei npm über `cmd /c`, weil es eine `.cmd` ist), Ablauf wie oben.
  - Es zählt die gelisteten aipets-Hooks aus genau dieser `hooks.json`. Bei 0 kommt eine Fehlermeldung statt „freigegeben“.
  - Timeout 30 s pro Antwort. Schließt der App-Server stdout, gibt Setup sofort auf. Bei Fehlern kommt stderr ins Log.
  - Ohne `codex` (PATH oder `%APPDATA%\npm\codex.cmd`) werden die Hooks eingetragen, dazu ein Hinweis auf `/hooks`.
  - `--uninstall` lässt `[hooks.state]` in `config.toml` stehen.
- **Tray:** `--install` startet es über `explorer.exe`, falls es nicht läuft. `--uninstall` beendet es per IPC (`quit`) und wartet bis zu 5 s.

## 8. Neues Pet – Checkliste

1. Ordner `pets\<id>\` mit `art\` anlegen. Das `make_sprites.py` des ähnlichsten Pets als Vorlage kopieren:
   - Hermes = nur Gesicht (sitzend).
   - Claude = Körperanimation.
   - Astra = Funkeln, Ohren, Logo nach dem Spiegeln.
   - Gemini = gezeichnete Augen, Winken, farbiges SVG-Logo.
   - Grok = Hintergrundlücken in Locken, Zwinkern als Vorlage, wippende Hand, einfarbiges Polygon-Logo, dunkle Kleidung.
2. Bild nach `art\source.*`. Am besten eine freigestellte Figur auf einfarbigem Hintergrund, groß genug fürs Gesicht (nach dem Verkleinern ≥ 20 px Gesichtsbreite).
3. **Bildspezifisches** anpassen: Freistellen, `ENLARGE`/`FACTOR`, Details, Gesichts-Patches, Animation, Logo, `anchors`, Icon-Ausschnitt.
   - **Vorgehen:** erst nur pixelisieren und neben die anderen Pets legen, dann Symbol-Dump und Grid-Zoom anschauen, dann die Koordinaten eintragen.
   - Arbeitsskripte (Zooms, Dumps, Vergleiche) in einen Scratch-Ordner, nicht ins Repo.
4. `pet.ini` schreiben: `mode`, `program`/`find`/`args`/`shell` und `url` (beides, damit man umschalten kann), `status`, `home` links neben dem letzten Pet (Formel in Abschnitt 4, „Mehrere Pets“).
   - Hat der Agent eine Desktop-App, auch `app` eintragen: App-ID aus `Get-StartApps` (App-Pakete) oder den exe-Pfad, den die Startmenü-Verknüpfung nutzt.
5. `.\build.ps1 -Art -Pet <id>` (oder erst eine Test-exe), dann `aipets.exe --snapshot <ordner> --pet <id>` und die PNGs anschauen (alle Zustände, `lineup.png`, Einstellungsseiten).
6. `aipets.exe --command <id>` prüfen, mit Desktop-App auch `--command <id> --mode app`. Einmal echt klicken, wenn der Befehl stimmt.
7. Für den Status: Bei einer neuen Quelle `HookCommand.Run`, `Setup.Hooks` und `HookText` erweitern (Abschnitt 7). Dann „Hooks einrichten“ in den Einstellungen und mit `--status <quelle>` prüfen.
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
- **UTF-8-BOM vor der ersten Nachricht an `codex app-server`:** .NET Framework legt `Process.StandardInput` mit `Console.InputEncoding` an und schreibt dessen Präambel sofort (AutoFlush).
  - Bei Codepage 65001 ist das ein BOM. Der App-Server meldet „Failed to deserialize JSONRPCMessage“ und antwortet nie.
  - Das passiert in Konsolen mit UTF-8 (so fiel es im Test auf). Laut .NET-Quelltext passiert es auch in GUI-Prozessen, wenn Windows' Option „Unicode UTF-8 für weltweite Sprachunterstützung“ an ist.
  - Setup schickt deshalb in dem Fall zuerst eine Leerzeile und schreibt die Nachrichten als Bytes direkt in `BaseStream`.
- **Hermes-`config.yaml` mit gemischten Zeilenenden** (beim User CRLF und LF gemischt, ohne Zeilenende am Schluss): Zeilenenden nie vereinheitlichen, sonst ist jede Installation eine „Änderung“.
- **Fremde YAML-Einrückung:** nie feste 2/4 annehmen. Beim Entfernen an der nächsten `-`-Zeile aufhören, sonst verschwindet ein fremder Eintrag mit.
- **Codex-Test mit `CODEX_HOME` im Temp-Ordner:** Die Warnungen „Refusing to create helper binaries under temporary dir“ und „Project-local config … disabled“ sind harmlos. Die zweite kommt, weil `~\.codex` dann als Projektordner gilt.
- **Codex zum Testen ohne Spuren:** `codex exec --ephemeral --ignore-user-config --dangerously-bypass-hook-trust -c "hooks.<Event>=[{hooks=[{type=…,command=…}]}]"`. Ein zusätzlicher UserPromptSubmit-Hook mit `Start-Sleep -Seconds 4; [Console]::Error.WriteLine(1); exit 2` blockt den Prompt, dann gibt es keinen Modellaufruf.
- **Link öffnen:** `ProcessStartInfo` mit `UseShellExecute = true` darf nie `EnvironmentVariables` anfassen, auch nicht lesend fürs Log. Schon das Anlegen des Dictionarys lässt `Process.Start` werfen. `Process.Start` gibt `null` zurück, wenn der Browser schon läuft. Dasselbe gilt für App-Pakete (`shell:AppsFolder\…`), dort ist es immer `null`.
- **App-Pakete (MSIX, Store) nie über ihre exe starten:** Der Pfad in `C:\Program Files\WindowsApps\<Paket>_<Version>_…` ändert sich mit jedem Update. Die App-ID bleibt gleich; ob das Paket installiert ist, sagt `GetPackagesByPackageFamily` (ohne COM, geht auf jedem Thread).
  - Den Anzeigenamen liefert `SHCreateItemFromParsingName("shell:AppsFolder\<App-ID>")`. Das braucht COM. Der Klick selbst braucht den Namen nicht; im Test klappte die Abfrage auch im Thread-Pool (MTA). Scheitert sie, steht der Paketname da.
  - Der Marshaler macht aus dem HRESULT „nicht gefunden“ eine `FileNotFoundException`, keine `COMException`.
- **Die Codex-Desktop-App heißt unter Windows „ChatGPT“:** Paket `OpenAI.Codex`, exe `app\ChatGPT.exe`, Protokoll `codex://`. Im Startmenü und in den Einstellungen steht deshalb „ChatGPT“.
- **`codex app [PATH]`** (Codex CLI 0.154, laut Strings in der `codex.exe`):
  - Es sucht die App selbst per PowerShell (`Get-StartApps | Where-Object AppID -Like 'OpenAI.Codex_*!App'`) und öffnet den Workspace (Standard `.`) per `Start-Process`.
  - Ohne App lädt es den Store-Installer (`get.microsoft.com/installer/download/9PLM9XGG6VKS`).
  - Bis die App aufgeht, vergehen also ein paar Sekunden (node, codex.exe, zweimal PowerShell).
  - `--help` gibt nur die Hilfe aus und ist gefahrlos. Ohne `--help` öffnet der Befehl die App, also nie zum Testen aufrufen.
- **Umgeleitete Ausgabe von Befehlen, die etwas starten:** Kindprozesse erben die Pipes und halten sie offen. `ReadToEnd()` oder `WaitForExit()` ohne Zeitlimit hängen dann, bis das Kind endet.
  - `RunHidden` liest deshalb asynchron, wartet mit Zeitlimit und danach höchstens 1 s auf das Pipe-Ende.
  - Die Lese-Callbacks laufen auch nach `Dispose` weiter. Sie dürfen nichts Entsorgtes anfassen, sonst stürzt das Pet ab.
- **Hermes Desktop:**
  - `hermes desktop` prüft bei jedem Aufruf einen Hash über den Quellcode und baut bei Abweichung neu (npm, Electron). Danach wartet es, bis die App geschlossen wird. Deshalb startet der Klick die gebaute `Hermes.exe` direkt, so wie Hermes' eigene Startmenü-Verknüpfung.
  - Electron hängt sich beim Start per `AttachConsole` an die Konsole des Elternprozesses (steht in Hermes' `scripts\desktop-update\windows.ps1`). Aus einer Konsole gestartet, hält die App das Fenster offen, und Schließen beendet die App. Pets und Tray sind GUI-Prozesse ohne Konsole, der direkte Start sollte dort also unproblematisch sein (ungetestet: Hermes Desktop ist beim User nicht gebaut).
  - Das Protokoll `hermes://` kann auf eine `Hermes.exe` zeigen, die es nicht gibt (so beim User). Deshalb nie über das Protokoll starten.
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
  - `-match` ignoriert Groß- und Kleinschreibung: `'failures: 0' -match '^FAIL'` ist wahr. Testausgaben mit `-cmatch` auswerten.
  - Umgeleitete Ausgabe der GUI-exe zeigt ✓ und Umlaute als `?`. Das liegt nur an der Konsolen-Codepage; Log und Meldungsfenster sind richtig.
- **Premultiplied Alpha:** Für `UpdateLayeredWindow` in ein `Format32bppPArgb`-Bitmap über dem DIB zeichnen, nicht `GetHbitmap()` pro Frame.
- **`DrawImageUnscaled`** skaliert nach der DPI des PNG. Immer mit expliziten Pixel-Rechtecken zeichnen.
- **Testen, ohne den User zu stören:** nicht seine Maus bewegen. `--snapshot`, `--command`, `--status`, `--dry-run` genügen. Screenshots nur lesend per BitBlt.
- **`settings.ini` von außen ändern:** Pets lesen die Datei nur jede Sekunde neu (Zeitstempel). Nach `Store.Update` deshalb `Ipc.PostToPet(id, Ipc.CmdReload)` schicken, wie `TrayHost.ChangeSetting` es tut.
  - Früher speicherte ein Pet beim Loslassen auch seine Größe aus dem Speicher. Eine Größe, die ein Skript gerade von außen gesetzt hatte, ging so verloren.
  - Seit die Größe für alle gilt, schreiben Pets beim Loslassen nur noch `x`/`y`.
- **Autostart nur für die echte exe:** Eine Test-exe, die als Tray startet, würde sonst den Autostart auf sich umbiegen und beim Löschen kaputt hinterlassen. `ApplyDefault` prüft deshalb den Dateinamen `aipets.exe`.
- **Snapshots schreiben nichts:** Die `PetForm`s dort werden nie angezeigt. Ohne Fenster-Handle gibt es beim `Dispose` kein `FormClosed`, also auch kein `SavePosition`.
- **Pet „fehlt“ auf dem Desktop:** Erst Fensterliste und `settings.ini` prüfen. Der User verschiebt die Pets gern selbst, manchmal gleich nach dem Start an den Rand.
- **Ins Chat eingefügte Bilder können das falsche sein** (einmal kam das Gemini-Bild statt Grok): Hash mit bisherigen Vorlagen vergleichen und auf dem Desktop nach der passenden Datei schauen.
- **Die Bildvorlagen sind nicht selbst gezeichnet:** privat ok, vor dem Veröffentlichen fragen.
