# aipets

*Agent desktop pets for Windows — pixel-art desktop pets, one per AI agent: Claude Code, Codex, Cursor, Copilot, Gemini, Grok, Hermes.*

Pixel-art desktop pets for Windows, one per AI agent. The pets sit on the taskbar. A click opens their agent in the terminal, as a desktop app or its website in the browser, and they show whether the agent is working, waiting for you or done.

| Pet | Click opens (default) | Can switch to | Status comes from |
|---|---|---|---|
| **Claude** | Claude Code in Windows Terminal | Claude desktop app, claude.ai | Claude Code hooks |
| **Hermes** | Hermes Agent in PowerShell (in Windows Terminal) | Hermes Desktop, hermes-agent.nousresearch.com | Hermes shell hooks |
| **Astra** | Codex CLI in Windows Terminal | Codex desktop app (called "ChatGPT" on Windows), chatgpt.com | Codex hooks |
| **Gemini** | gemini.google.com in the default browser | Gemini CLI (`gemini`) | – |
| **Grok** | grok.com in the default browser | Grok CLI (`grok`) | – |
| **Cursor** | Cursor desktop app | Cursor in the terminal (`cursor`), cursor.com | Cursor hooks |
| **Copilot** | Microsoft Copilot desktop app | copilot.microsoft.com | – |

**Cursor in two looks:** Under **Settings → Cursor → Appearance** or **right-click Cursor → Appearance** you choose **Pixel** (default, like Grok) or **Original** (the high-resolution draft image). Both are animated: breathing, flowing hair, blinking, a wink on hover, a small finger gesture, joy on click and sleeping. The switch takes effect immediately and keeps position, height and visibility. It stays a single pet. Cursor gets its status from Cursor's hooks (see below); there is no "?" for "waiting for you" with Cursor, because Cursor reports no event for it.

**Copilot in two looks:** Draft 1 with turquoise hair, a white shirt and a wave. Under **Settings → Copilot → Appearance** or **right-click → Appearance** you choose **Pixel** (default, matching Grok and Cursor) or **Original**. Both looks animate breathing, hair, blinking, a hover smile with both eyes closed and a wave, joy on click with a hop, and sleeping. The shirt logo follows the shading and movement of the shirt like a fabric print. Position, height, visibility and layers are kept when switching. A click opens the Microsoft Copilot app; alternatively choose the website. There is no status source and no preset terminal command; the general "Program" field remains available for a command of your choice. This pet belongs to Microsoft Copilot, not GitHub Copilot.

## Quick start

Requirements: Windows 10/11 with .NET Framework 4.8. Windows Terminal is optional; without it the Windows console is used. The ready-made package needs neither Python nor a separately installed .NET SDK.

1. **Install:** Put the folder where it should stay (clone or extract the ZIP) and **double-click `install.cmd`.** Autostart and hooks remember the path.
   - In the source folder, `aipets.exe` is built with the C# compiler that ships with Windows. The release ZIP contains the finished exe and skips the build.
   - Turns on "Start with Windows".
   - Adds the status hooks for Claude Code, Codex, Hermes Agent and Cursor, as far as they are installed, and sets up the approvals. Automatic Codex approval needs the Codex CLI; otherwise the summary shows the steps needed under `/hooks`.
   - Starts aipets and shows at the end what it did.

   Running it several times does no harm: whatever is already right stays unchanged. For every changed file, the previous version is kept next to it as `<file>.bak-aipets`. Restart agents that are currently running once so they load the hooks.
2. **Use:** The aipets icon appears in the taskbar's notification area (among the hidden icons behind `^`).
   - **Left click:** settings. There you can show and hide pets, choose the size with two sliders and switch **"Click opens"** between **Program**, **Desktop app** and **Website**. The desktop app exists for Claude, Hermes, Astra, Cursor and Copilot.
     - **"Size of all pets":** the height of all pets in pixels, from 162 px up to the screen height. The ticks sit at 162, 324, 486 … px. Moving it makes **all** pets jump to this height, including those with their own size, so they are all equally tall.
     - **"Size of <pet>":** a height of its own for just this pet, also up to the full screen height. It stays until you move the upper slider again. **"like all"** next to it resets the pet to the height of all pets right away.
     - Nothing is multiplied any more. Smaller than 162 px is not possible, taller than the screen neither. If a pet stands on a lower screen, it gets smaller to fit there.
     - The pets follow immediately while dragging. The pixel art is only stretched, not altered.
     - Program: program, arguments, terminal and working folder.
     - Desktop app: which app was found, with version and location. With "…" you pick a different exe instead.
       - Claude opens the Claude app, Astra the Codex app (Windows calls it "ChatGPT"), Hermes the Hermes desktop app.
       - Astra uses `codex app` for this, without a terminal window. That opens the working folder as a workspace in the app (which is why the page shows it), and if the app is missing, it opens its installer.
       - Hermes Desktop has to be built once. While it is missing, a click runs `hermes desktop` in the terminal. That builds the app (a few minutes the first time) and opens it; after that it starts directly.
     - Website: the link (http/https only; `grok.com` automatically becomes `https://grok.com/`).
     - At the bottom: "Start with Windows". It is on from the start: aipets turns it on by itself at startup, even after the folder was moved. If you turn it off, it stays off.
     - At the bottom: **"Hide all pets"** hides all pets at once, for example while screen sharing. When you turn it off again, exactly the pets that were visible before come back; individually hidden ones stay hidden. While it is on, each pet's "Show" is greyed out. It stays on after a restart.
   - **Right click:** menu with all pets, "Hide all pets", autostart and Quit.
3. **Remove:** **Double-click `uninstall.cmd`.** It quits aipets, turns "Start with Windows" off and removes the hooks again. Afterwards you can delete the folder, and `%APPDATA%\aipets` (settings, log) as well.

**Moved the folder or installed an agent later?** The settings show under "Status display" whether the hooks match this `aipets.exe`. If not, the **"Set up hooks"** link below adds them again with one click. After moving, `install.cmd` in the new folder does everything at once, including the autostart.

**Without double-clicking:** `.\build.ps1` only builds, `.\build.ps1 -Art` regenerates the app icon and sprites first (this needs Python with `numpy` and `Pillow`). `aipets.exe --install` and `aipets.exe --uninstall` do the same as the two cmd files, with `--quiet` without a window. The exe must sit next to the `pets\` folder.

**Background:** The tray program starts each pet as its own process (`aipets.exe --pet <id>`). If a pet crashes or is ended, the tray program restarts it. If you quit the tray program, the pets disappear too.

**Pet right-click:** open, working folder (program mode only), "Click opens" → Program / Desktop app / Website, Size (all pets at 162 to 648 px; only this pet: like all, smaller, bigger, as tall as the screen), back to the corner, hide, settings, quit aipets. A hidden pet stays hidden, even after a restart, until you turn it on again in the settings or the tray menu.

**Overlapping pets** are stacked in a fixed order. **If you drag a pet with the mouse, it comes to the front and stays there**, like a window on Windows. This way you set the order yourself; aipets remembers it across restarts (`layers=` under `[app]` in `%APPDATA%\aipets\settings.ini`, delete the line to reset). As long as you have not dragged anything, the order of the list applies (settings, tray menu, `order=` in `pets\<id>\pet.ini`): Claude in front, then Hermes, Astra, Gemini, Grok, Cursor and Copilot. Menus and dialogs of aipets are always above all pets. If another program with "always on top" moves in front, the pets are back above it after a good three seconds at most.

A click starts Claude Code with `--dangerously-skip-permissions`, Hermes with `--yolo` and Codex with `--dangerously-bypass-approvals-and-sandbox`. Gemini opens `https://gemini.google.com/app`, Grok `https://grok.com/`, both in the default browser. You can change all of this in the settings or in `pets\<id>\pet.ini`. Gemini CLI and Grok CLI are not included; if you want program mode, you have to install them yourself.

In desktop app mode, the pet starts the Claude app and Hermes Desktop the same way the Start menu does. It finds the installed apps by itself: Claude as an app package, Hermes Desktop in the folder `hermes desktop` builds it into. The Codex app is opened by `codex app`; without the Codex CLI the pet starts the app package directly. The Codex app's status comes through the same Codex hooks. Whether the Claude app and Hermes Desktop trigger the hooks has not been tried yet.

## Adding hooks by hand

You normally don't need this: `install.cmd` and "Set up hooks" add exactly what is shown here. The examples show what goes into the files, in case you want to check or adjust something.

### Claude Code

Add this to `%USERPROFILE%\.claude\settings.json` and adjust the path. Then open `/hooks` once or restart Claude Code.

```json
"hooks": {
  "UserPromptSubmit":   [{ "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "working"], "timeout": 10 }] }],
  "PostToolUse":        [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "resume"], "async": true }] }],
  "PostToolUseFailure": [{ "matcher": "*", "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "resume"], "async": true }] }],
  "Notification":       [{ "matcher": "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input", "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "waiting"], "timeout": 10 }] }],
  "Stop":               [{ "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "done"], "timeout": 10 }] }],
  "SessionEnd":         [{ "hooks": [{ "type": "command", "command": "C:\\path\\to\\aipets.exe", "args": ["--hook", "claude", "end"], "timeout": 10 }] }]
}
```

### Hermes Agent

Add this to Hermes' `config.yaml`. It lives in `%HERMES_HOME%`, which for the Windows install is usually `%LOCALAPPDATA%\hermes`, otherwise `~/.hermes/`. Put paths in single quotes, otherwise YAML reads the backslashes as escapes.

```yaml
hooks:
  pre_llm_call:
    - command: '"C:\path\to\aipets.exe" --hook hermes working'
      timeout: 10
  pre_approval_request:
    - command: '"C:\path\to\aipets.exe" --hook hermes waiting'
      timeout: 10
  post_approval_response:
    - command: '"C:\path\to\aipets.exe" --hook hermes resume'
      timeout: 10
  on_session_end:
    - command: '"C:\path\to\aipets.exe" --hook hermes done'
      timeout: 10
  on_session_finalize:
    - command: '"C:\path\to\aipets.exe" --hook hermes end'
      timeout: 10
```

On its next start, Hermes asks once per hook whether it may run. Alternatively, start Hermes once with `hermes --accept-hooks`. You can check this with `hermes hooks list`.

### Codex

Add this to `%USERPROFILE%\.codex\hooks.json` (or `%CODEX_HOME%\hooks.json`) and adjust the path. On Windows, Codex runs hook commands with PowerShell, hence `& '…'` and `| Out-Null`: without `Out-Null`, PowerShell does not wait for `aipets.exe`.

```json
{
  "hooks": {
    "UserPromptSubmit":  [{ "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex working | Out-Null", "timeout": 10, "async": true }] }],
    "PermissionRequest": [{ "matcher": "*", "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex waiting | Out-Null", "timeout": 10, "async": true }] }],
    "PostToolUse":       [{ "matcher": "*", "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex resume | Out-Null", "timeout": 10, "async": true }] }],
    "Stop":              [{ "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex done | Out-Null", "timeout": 10, "async": true }] }],
    "Interrupt":         [{ "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex idle | Out-Null", "timeout": 3, "async": true }] }],
    "SessionEnd":        [{ "hooks": [{ "type": "command", "command": "& 'C:\\path\\to\\aipets.exe' --hook codex end | Out-Null", "timeout": 3 }] }]
  }
}
```

Codex only runs new hooks once you have approved them: open `/hooks` in Codex and mark the six aipets hooks as trusted. If you change a command (e.g. because the exe moved), Codex asks again.

### Cursor

Add this to `%USERPROFILE%\.cursor\hooks.json` and adjust the path. On Windows, Cursor runs hook commands through PowerShell and passes the event in the data the hook receives. That is why only the path is given here, without arguments. If it contains spaces, put it in single quotes, e.g. `"command": "'C:\\Program Files\\aipets\\aipets.exe'"`.

```json
{
  "version": 1,
  "hooks": {
    "beforeSubmitPrompt": [{ "command": "C:\\path\\to\\aipets.exe", "timeout": 10 }],
    "stop":               [{ "command": "C:\\path\\to\\aipets.exe", "timeout": 10 }],
    "sessionEnd":         [{ "command": "C:\\path\\to\\aipets.exe", "timeout": 10 }]
  }
}
```

Cursor also picks up the hooks from Claude Code by itself (Cursor setting "Include Third-Party Plugins, Skills, and Other Configs"), but without their arguments. aipets recognizes these calls and shows them on Cursor instead of opening itself. If both files contain the same command, Cursor runs it only once. On Windows, every hook starts a PowerShell and costs about half a second, which Cursor waits for. That is why aipets adds no hook after every tool call for Cursor; the imported `PostToolUse` hook from Claude Code still runs as long as importing is on in Cursor. Which hooks Cursor runs is shown in Cursor's "Hooks" output channel.

## Building it yourself

```powershell
.\build.ps1                         # build, then restart running pets
.\build.ps1 -OutputDirectory C:\Temp\aipets-build  # build without touching running pets
```

With `-OutputDirectory`, the output contains only the exe; to run it, `pets\` must sit next to it. The version number is in `src/AssemblyInfo.cs`. The exe is not signed.

## Known limitations

- Gemini and Grok show no working status in website mode.
- Cursor does not report when it is waiting for your confirmation: the "?" is missing for Cursor. The Cursor hooks follow the public docs and the behavior of Cursor 3.5 and have not been tried in a real Cursor session yet.
- For aborted Codex turns without a closing hook, the spinner can stay until the 15-minute timeout.
- The status hooks of the Claude desktop app and Hermes Desktop, as well as the behavior on multiple monitors, still need to be checked by hand.
- The YAML editor supports indented hook lists, empty blocks and comments. Compact non-empty inline hook lists or duplicate hook keys are rejected with an error message; the file is left intact.

## License

The source code is under the [MIT License](LICENSE). The pet graphics in `pets/*/sprites/` as well as names and logos of Anthropic, OpenAI, Google, xAI, Nous Research, Anysphere (Cursor) and Microsoft are not covered by it. aipets is an unofficial fan project and is not affiliated with these companies.
