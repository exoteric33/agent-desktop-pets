# Desktop-Pet – Bauanleitung (am Beispiel ClaudePet)

Diese Datei beschreibt, wie ClaudePet aufgebaut ist und wie man daraus ein neues Pet für etwas anderes baut.
Sie ist auch als Kontext für Claude gedacht: „Lies `PET-BAUANLEITUNG.md` und bau mir ein Pet für X.“

---

## 1. Was ClaudePet macht

- Pixel-Art-Figur aus einer Bildvorlage. Sie steht unten rechts auf der Taskleiste, immer im Vordergrund, und transparente Stellen lassen Klicks durch.
- **Animation:** Atmen, wehende Haare, wippende Antenne, Blinzeln, zufälliges Lächeln, Hüpfer und Funken. Nach 1 Minute ohne Eingabe schläft sie ein (Zzz).
- **Maus drüber:** happy Gesicht plus Sprechblase `>_`.
- **Klick:** Hüpfer und Funken, dann öffnet sich Claude Code in einem neuen Windows Terminal: `wt.exe -d <Ordner> claude.exe --dangerously-skip-permissions`.
- **Ziehen:** frei verschiebbar. In Taskleistennähe rastet sie ein und schaut zur Bildschirmmitte (Sprites gespiegelt).
- **Rechtsklick-Menü:** Claude Code öffnen, Arbeitsordner, Größe 1×–4×, zurück in die Ecke, Autostart, Beenden.
- **Claude-Status über Hooks:** Spinner-Blase = arbeitet, „?“ plus Hüpfen = braucht dich, grüner Haken = fertig.
- **Vollbild:** Bei Vollbild-Apps (Videos, Spiele) blendet sie sich aus, bei „Desktop anzeigen“ nicht.

## 2. Ordner

```
ClaudePet\
├─ ClaudePet.exe          fertiges Programm (eine Datei, Sprites eingebettet)
├─ build.ps1              baut die exe  (-Art = Sprites vorher neu erzeugen)
├─ PET-BAUANLEITUNG.md    diese Datei
├─ art\
│  ├─ source.png          Bildvorlage
│  ├─ make_sprites.py     Bild → Pixel-Art → Animations-Frames → Atlas
│  └─ build\              atlas.png, atlas.txt, icon.ico, preview_*.png
└─ src\
   ├─ Program.cs          Einstieg: --hook / --snapshot / --dry-run / normal
   ├─ PetForm.cs          Fenster, Animation, Maus, Menü, Status-Anzeige
   ├─ Atlas.cs            lädt atlas.png/atlas.txt, zeichnet Frames und Sprites
   ├─ Native.cs           Win32: Layered Window, DPI, Idle-Zeit, Vollbild-Erkennung, Logon-Umgebung
   ├─ Launcher.cs         Klick-Aktion (Claude Code im Terminal starten)
   ├─ Sessions.cs         Hook-Befehl + Auswertung der Statusdateien
   └─ Settings.cs         settings.ini, Autostart (Registry), pet.log
```

Laufzeitdaten liegen in `%APPDATA%\ClaudePet\`: `settings.ini`, `pet.log` und `sessions\<id>.txt`.

## 3. Bauen

- **Voraussetzung Code:** nichts extra. Der C#-Compiler von .NET Framework 4.x ist in Windows enthalten (`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`).
  - **Achtung:** nur C#-5-Syntax, also kein `$"..."`, kein `?.`, keine `=>`-Properties, kein `nameof`.
- **Voraussetzung Sprites:** Python mit `numpy` und `Pillow`.
- `.\build.ps1` baut nur den Code, `.\build.ps1 -Art` erzeugt vorher die Sprites neu. Das Skript beendet ein laufendes Pet selbst.
- Aus einer Claude-Session heraus startest du das Pet mit `explorer.exe "…\ClaudePet.exe"`. So hängt es nicht an der Session und erbt nicht deren Umgebung.

## 4. Art-Pipeline (`art/make_sprites.py`)

1. **`cut_out`:** Hintergrund per Flood-Fill vom Rand entfernen (Toleranz zu #FDFDFD). Dann den größten zusammenhängenden Blob behalten, das entfernt UI-Reste im Bild. `pockets` sind Startpunkte für eingeschlossene Hintergrundlöcher, zum Beispiel zwischen Arm und Hüfte. **→ bildspezifisch**
2. **`remove_lettering`:** Schrift auf dem Shirt übermalen, indem die umgebende Farbe hineindiffundiert. `boxes` = Rechtecke plus Helligkeitsschwelle. **→ bildspezifisch**
3. **`pixelise`:**
   - Um `FACTOR` (3) verkleinern. Dünne dunkle Linien bleiben dabei erhalten: Hat ein Block genug dunkle Pixel, bekommt er deren Farbe.
   - Palette per k-means auf `PALETTE_SIZE` (20) Farben reduzieren und einzelne Pixel glätten.
   - 1 px dunkle Außenlinie ziehen, unten keine (Figur ist abgeschnitten → steht „hinter der Taskleiste“).
4. **`shirt_logo`:** Details, die das Verkleinern zerstört, per Hand neu setzen (hier ein 7×7-Sternchen).
5. **Gesichter** `face_normal/blink/happy`: kleine Pixel-Patches mit festen Koordinaten (`patch(img, x, y, rows)`, Zeichen → Farbe über `COLORS`). **→ bildspezifisch**
   - Koordinaten findest du am besten über einen Symbol-Dump des Basis-Sprites (jede Palettenfarbe = ein Zeichen) plus einer Grid-Zoom-Vorschau.
6. **Animation**, bei allen die Zeilen/Spalten **→ bildspezifisch**:
   - `hair_wave`: Außenkanten der Haare pro Zeile ±1 px als Welle verschieben; die Tupel geben den Zeilenbereich je Seite an.
   - `wiggle_ahoge`: obere Zeilen der Antenne verschieben.
   - `restretch`: Atmen und Hüpfen durch Verdoppeln/Löschen einzelner Zeilen (`BREATH_ROW`, `STRETCH_ROWS`). Nimm Zeilen, in denen nur senkrechte Kanten verlaufen.
   - 8 Phasen je Gesicht, dazu `bounce_-2..3`.
   - `mirror` erzeugt alle Frames gespiegelt (`m_…`) und dreht das Haarspangen-Logo zurück.
7. **`effects`:** Funken, Zzz, Sprechblasen. `bubble_contents()` ist die Liste, was die Blase anzeigen kann (Prompt, Spinner-Frames, „?“, Haken); der Rahmen wird automatisch drumherum gebaut.
8. **Ausgabe:** `atlas.png`, `atlas.txt`, `icon.ico` und Vorschaubilder.

### `atlas.txt`

```
cell 72 122                          # Größe jeder Figur-Zelle
frame <name> <x> <y>                 # z.B. normal_0 … happy_7, bounce_3, m_normal_0 (gespiegelt)
sprite <name> <x> <y> <w> <h> <px> <py>   # Effekte; (px,py) = Pivot (Mitte bzw. Blasen-Spitze)
anchor <name> <x> <y>                # Punkte in der ungespiegelten Zelle: bubble, zzz, logo, head
```

Der C#-Code kennt nur diese Namen. Ein neues Pet braucht also dieselben Frame- und Sprite-Namen, oder du passt `PetForm.cs` an.

## 5. Laufzeit (C#)

- **Fenster:**
  - WinForms-Form mit `WS_EX_LAYERED | TOOLWINDOW | TOPMOST | NOACTIVATE`.
  - Gezeichnet wird in einen premultiplied 32-Bit-DIB (`Native.LayeredSurface`), angezeigt per `UpdateLayeredWindow` mit Alpha pro Pixel.
  - Pixel mit Alpha 0 lassen Klicks durch. `WM_MOUSEACTIVATE → MA_NOACTIVATE`: Anklicken klaut keinen Fokus.
- **Skalierung:** ganzzahlig, Nearest-Neighbor mit `WrapMode.TileFlipXY`, damit keine Nachbarzellen durchbluten. Der Prozess ist Per-Monitor-DPI-aware, sonst wird die Pixel-Art unscharf.
- **Timer:** 33 ms bei Bewegung, 55 ms beim Spinner, sonst 80 ms. Neu gezeichnet wird nur, wenn sich Frame, Blase oder Partikel ändern. Idle-CPU liegt unter 1 %.
- **Zustände:**
  - Gesicht nach Priorität: ziehen > schlafen > hover/happy/fertig > blinzeln > normal.
  - Blase nach Priorität: Hover-Prompt > wartet > arbeitet > fertig.
- **Position:** gespeichert als unterer Mittelpunkt, dadurch bleibt sie beim Größenwechsel auf der Taskleiste.
- **Aktion bei Klick:** `Launcher.Launch()`. Für ein anderes Pet ersetzt du nur diese Methode (Programm starten, URL öffnen, Skript ausführen …).

## 6. Status-Protokoll (Hooks → Datei → Pet)

Hooks in `~/.claude/settings.json` (Exec-Form mit `args`, also ohne Shell):

| Claude-Code-Event | Aufruf | Wirkung |
|---|---|---|
| UserPromptSubmit | `--hook working` | arbeitet |
| PostToolUse / PostToolUseFailure (async) | `--hook resume` | nur Waiting → Working (Permission beantwortet) |
| Notification `permission_prompt\|elicitation_dialog\|elicitation_url_dialog\|agent_needs_input` | `--hook waiting` | braucht dich |
| Stop | `--hook done` | fertig |
| SessionEnd | `--hook end` | Datei löschen |

- **Datei:** `%APPDATA%\ClaudePet\sessions\<session_id>.txt` mit `State|Unix-ms|claude-PID|transcript_path`.
- **Hook-Befehl** (`HookCommand`):
  - Er liest das JSON von stdin, gibt **nichts** auf stdout aus (bei UserPromptSubmit landet stdout sonst in Claudes Kontext) und schreibt die Datei atomar.
  - Als Zeitstempel dient die **Prozess-Startzeit**. Dadurch überschreiben verspätete async-Hooks keinen neueren Zustand.
- **Pet** (`SessionMonitor`, alle 500 ms):
  - Ist der Claude-Prozess tot, wird die Datei gelöscht.
  - Ein Esc-Abbruch wird am Transcript-Marker `[Request interrupted by user` erkannt.
  - Nach 15 min ohne Aktivität gilt die Session als idle.

**Für ein Pet mit anderer Quelle** (Build-Server, Downloads, Mails …): Irgendein Programm schreibt Dateien in diesem Format in einen Ordner, das Pet liest sie. Das Protokoll ist bewusst simpel.

## 7. Neues Pet bauen – Checkliste

1. Ordner kopieren und umbenennen. **Eindeutig machen**, sonst kommen sich die Pets in die Quere:
   - Mutex `Local\ClaudePet.SingleInstance` (`Program.cs`)
   - AppData-Ordner `"ClaudePet"` (`Settings.cs`)
   - Autostart-Wert `ValueName` (`Settings.cs`)
   - Ressourcennamen `ClaudePet.atlas.*` (`Atlas.cs` + `build.ps1`), Exe-Name, Namespace
2. Neues Bild nach `art\source.png`. Am besten eine freigestellte Figur auf einfarbigem Hintergrund, nicht zu klein (bei FACTOR 3 wird aus 350 px Höhe eine Figur von ca. 118 px).
3. In `make_sprites.py` die **bildspezifischen Stellen** anpassen: `pockets`, `boxes`, `LOGO_CENTER`/`shirt_logo`, Gesichts-Patches, `hair_wave`-Bereiche, `wiggle_ahoge`, `BREATH_ROW`, `STRETCH_ROWS`, Spangen-Region in `mirror`, `anchors`, Icon-Ausschnitt `head = base[0:44, 17:61]`.
   - **Vorgehen:** erst nur `pixelise` laufen lassen, Symbol-Dump und Grid-Zoom anschauen, dann die Koordinaten eintragen.
4. `.\build.ps1 -Art`, danach `ClaudePet.exe --snapshot <ordner>`. Das rendert Idle, Hover, Schlaf, Klick, Arbeitet, Wartet und Fertig in beide Richtungen als PNG, ohne den Desktop anzufassen.
5. Klick-Aktion in `Launcher.cs` ersetzen. Statusquelle und Blasen-Inhalte (`bubble_contents`) nach Bedarf anpassen.
6. Mit `--dry-run` testen: Ein Klick protokolliert dann nur in `pet.log`, was gestartet würde.

## 8. Stolperfallen (alle schon einmal passiert)

- **„Desktop anzeigen“ (Win+D)** meldet über `SHQueryUserNotificationState` dasselbe wie Vollbild (QUNS_BUSY). Deshalb zusätzlich prüfen: Vordergrundfenster nicht Progman/WorkerW/Taskleiste, gleicher Monitor, deckt den ganzen Monitor ab.
- **Windows Terminal reicht die Umgebung des Aufrufers weiter.** Wurde das Pet aus einer Claude-Session gestartet, bekamen neue Sessions `NO_COLOR` und Session-Variablen mit. Lösung: frische Logon-Umgebung per `CreateEnvironmentBlock` (`Native.LogonEnvironment`).
- **`wt.exe`:** Ein Pfad mit `\` am Ende maskiert das schließende Anführungszeichen (`TerminalDir`). Argumente nach dem Programm reicht wt korrekt durch.
- **Esc löst kein `Stop` aus.** Ohne Transcript-Check würde der Spinner ewig laufen.
- **`PostToolUse` feuert auch nach `Stop`** (Hintergrund-Agents). Deshalb belebt `resume` nur einen Waiting-Zustand.
- **Hook-exe schnell halten:** Der `--hook`-Pfad in `Main` darf keine WinForms-Typen anfassen (`RunPet` mit `NoInlining`). Das spart Ladezeit, ein Aufruf dauert ca. 0,1 s.
- **Premultiplied Alpha:** Für `UpdateLayeredWindow` in einen `Format32bppPArgb`-Bitmap über dem DIB zeichnen, nicht `GetHbitmap()` pro Frame.
- **`DrawImageUnscaled`** skaliert nach der DPI des PNG. Immer mit expliziten Pixel-Rechtecken zeichnen.
- **Testen, ohne den User zu stören:** nicht seine Maus bewegen. `--snapshot`, `--dry-run`, `PostMessage`-Klicks und Screenshots per `BitBlt` mit `CAPTUREBLT` genügen.
- **Die Vorlage ist Fan-Art eines fremden Artists:** privat ok, vor dem Veröffentlichen fragen.
