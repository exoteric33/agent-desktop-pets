# Status hooks

[← Back to the README](../README.md) · [Setup and troubleshooting](setup.md)

`install.cmd` and **Set up hooks** configure these integrations automatically. Use the examples below to inspect or configure them by hand. Replace `C:\path\to\aipets.exe` with the real path and merge entries with your existing hooks instead of replacing unrelated configuration.

Hooks write local status files under `%APPDATA%\aipets\status`. Each pet combines the sessions for its agent: waiting takes priority over working. Gemini, Grok and Microsoft Copilot have no status integration. Cursor supports working and completion, but no waiting indicator.

## Claude Code

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

## Hermes Agent

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

## Codex

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

Codex question tracking needs no additional hook. The normal Codex hooks identify the session; Astra reads its local transcript to detect question-tool requests and shows "?" until you answer. Transcript updates are read incrementally in the background, keeping questions visible during long sessions and retrying if the file is temporarily locked.

## Cursor

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
