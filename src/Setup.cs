using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AiPets
{
    /// <summary>
    /// aipets.exe --install / --uninstall and "Hooks einrichten" in the settings window: autostart plus the
    /// status hooks of every installed agent (Claude Code, Codex, Hermes Agent), all pointing at this exe.
    /// Only aipets' own entries are touched, a file is only written when something changes, and the old
    /// version is kept as &lt;file&gt;.bak-aipets.
    /// </summary>
    static class Setup
    {
        public sealed class Step
        {
            public readonly string Name, Text;
            public readonly bool Ok;   // done or already so; false = skipped or failed

            public Step(string name, bool ok, string text)
            {
                Name = name;
                Ok = ok;
                Text = text;
            }

            public override string ToString()
            {
                return (Ok ? "✓ " : "– ") + Name + ": " + Text;
            }
        }

        static string UserProfile
        {
            get { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
        }

        public static string ClaudeSettings
        {
            get { return Path.Combine(UserProfile, @".claude\settings.json"); }
        }

        public static string CodexHome
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("CODEX_HOME");
                return string.IsNullOrEmpty(home) ? Path.Combine(UserProfile, ".codex") : home;
            }
        }

        public static string HermesHome
        {
            get
            {
                string home = Environment.GetEnvironmentVariable("HERMES_HOME");
                if (!string.IsNullOrEmpty(home))
                    return home;
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");
                return Directory.Exists(home) ? home : Path.Combine(UserProfile, ".hermes");
            }
        }

        public static List<Step> Install(string exe)
        {
            var steps = new List<Step>();
            try
            {
                Autostart.Choose(true);   // installing again also takes back an earlier "no"
                steps.Add(new Step("Mit Windows starten", true, "an"));
            }
            catch (Exception ex)
            {
                steps.Add(new Step("Mit Windows starten", false, ex.Message));
            }
            foreach (string source in new[] { "claude", "codex", "hermes" })
                steps.Add(Hooks(source, exe, true));
            return steps;
        }

        public static List<Step> Uninstall(string exe)
        {
            var steps = new List<Step>();
            try
            {
                Autostart.Enabled = false;
                steps.Add(new Step("Mit Windows starten", true, "aus"));
            }
            catch (Exception ex)
            {
                steps.Add(new Step("Mit Windows starten", false, ex.Message));
            }
            foreach (string source in new[] { "claude", "codex", "hermes" })
                steps.Add(Hooks(source, exe, false));
            return steps;
        }

        /// <summary>Adds (install) or removes the status hooks of one agent.</summary>
        public static Step Hooks(string source, string exe, bool install)
        {
            try
            {
                switch (source)
                {
                    case "claude": return Claude(ClaudeSettings, exe, install);
                    case "codex": return Codex(CodexHome, exe, install, true);
                    case "hermes": return Hermes(HermesHome, exe, install);
                }
                return new Step(source, false, "für diese Statusquelle gibt es keine Hooks");
            }
            catch (Exception ex)
            {
                Log.Write("setup " + source + ": " + ex);
                return new Step(source == "claude" ? "Claude Code" : source == "codex" ? "Codex" : "Hermes Agent", false, "Fehler: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ Claude Code

        // event, matcher (null = none), hook event word, "async" or a timeout
        static readonly string[][] ClaudeEvents =
        {
            new[] { "UserPromptSubmit", null, "working", "10" },
            new[] { "PostToolUse", "*", "resume", "async" },
            new[] { "PostToolUseFailure", "*", "resume", "async" },
            new[] { "Notification", "permission_prompt|elicitation_dialog|elicitation_url_dialog|agent_needs_input", "waiting", "10" },
            new[] { "Stop", null, "done", "10" },
            new[] { "SessionEnd", null, "end", "10" },
        };

        public static Step Claude(string settingsPath, string exe, bool install)
        {
            const string name = "Claude Code";
            if (!Directory.Exists(Path.GetDirectoryName(settingsPath)))
                return new Step(name, !install, "nicht installiert");
            var groups = new List<KeyValuePair<string, JsonObject>>();
            foreach (string[] e in ClaudeEvents)
            {
                var handler = new JsonObject();
                handler.Set("type", JsonValue.Of("command"));
                handler.Set("command", JsonValue.Of(exe));
                handler.Set("args", new List<object> { JsonValue.Of("--hook"), JsonValue.Of("claude"), JsonValue.Of(e[2]) });
                if (e[3] == "async")
                    handler.Set("async", JsonValue.Of(true));
                else
                    handler.Set("timeout", JsonValue.Of(int.Parse(e[3], CultureInfo.InvariantCulture)));
                groups.Add(new KeyValuePair<string, JsonObject>(e[0], Group(e[1], handler)));
            }
            bool changed = EditJsonHooks(settingsPath, "claude", install ? groups : null, null);
            return new Step(name, true, Outcome(changed, install) + " (" + PetForm.ShortPath(settingsPath) + ")");
        }

        // ------------------------------------------------------------------ Codex

        // event, matcher, hook event word, timeout, async; Codex runs hook commands through Windows PowerShell
        static readonly string[][] CodexEvents =
        {
            new[] { "UserPromptSubmit", null, "working", "10", "async" },
            new[] { "PermissionRequest", "*", "waiting", "10", "async" },
            new[] { "PostToolUse", "*", "resume", "10", "async" },
            new[] { "Stop", null, "done", "10", "async" },
            new[] { "Interrupt", null, "idle", "3", "async" },
            new[] { "SessionEnd", null, "end", "3", null },
        };

        public static Step Codex(string home, string exe, bool install, bool trust)
        {
            const string name = "Codex";
            if (!Directory.Exists(home))
                return new Step(name, !install, "nicht installiert");
            string path = Path.Combine(home, "hooks.json");
            var groups = new List<KeyValuePair<string, JsonObject>>();
            foreach (string[] e in CodexEvents)
            {
                var handler = new JsonObject();
                handler.Set("type", JsonValue.Of("command"));
                // without Out-Null PowerShell does not wait for the GUI exe
                handler.Set("command", JsonValue.Of("& '" + exe.Replace("'", "''") + "' --hook codex " + e[2] + " | Out-Null"));
                handler.Set("timeout", JsonValue.Of(int.Parse(e[3], CultureInfo.InvariantCulture)));
                if (e[4] != null)
                    handler.Set("async", JsonValue.Of(true));
                groups.Add(new KeyValuePair<string, JsonObject>(e[0], Group(e[1], handler)));
            }
            bool changed = EditJsonHooks(path, "codex", install ? groups : null,
                "aipets: the pets on the desktop show whether Codex is working, needs you or is done");
            string where = " (" + PetForm.ShortPath(path) + ")";
            if (!install)
                return new Step(name, true, Outcome(changed, false) + where);
            string problem = trust ? TrustCodexHooks(path) : null;
            if (problem != null)
                return new Step(name, false, Outcome(changed, true) + where + ", aber nicht freigegeben (" + problem + "). In Codex einmal /hooks öffnen und die aipets-Hooks freigeben.");
            return new Step(name, true, Outcome(changed, true) + " und freigegeben" + where);
        }

        /// <summary>
        /// Trusts aipets' hooks the way Codex' /hooks does: codex app-server (JSON-RPC over stdio), hooks/list
        /// for key and current hash, config/batchWrite on hooks.state. Null on success, otherwise the reason.
        /// </summary>
        static string TrustCodexHooks(string hooksPath)
        {
            string codex = Launcher.Resolve("codex", @"%APPDATA%\npm\codex.cmd", Environment.GetEnvironmentVariable("PATH"));
            if (codex == null)
                return "codex nicht gefunden";
            ProcessStartInfo psi = codex.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || codex.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                ? new ProcessStartInfo("cmd.exe", "/d /s /c \"\"" + codex + "\" app-server\"")
                : new ProcessStartInfo(codex, "app-server");
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.WorkingDirectory = UserProfile;

            var lines = new Queue<string>();   // stdout lines; null = stdout closed
            var errors = new StringBuilder();
            using (Process p = Process.Start(psi))
            {
                p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    lock (lines)
                    {
                        lines.Enqueue(e.Data);
                        Monitor.PulseAll(lines);
                    }
                };
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    lock (errors)
                        if (e.Data != null && errors.Length < 4000)
                            errors.AppendLine(e.Data);
                };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                try
                {
                    // StandardInput writes the encoding's preamble on start, a UTF-8 BOM when the console or ANSI
                    // code page is 65001; the app-server cannot parse that line, so give it one of its own
                    if (p.StandardInput.Encoding.GetPreamble().Length > 0)
                        Send(p, "");
                    Send(p, "{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"aipets\",\"title\":null,\"version\":\"1\"},\"capabilities\":null}}");
                    if (Reply(lines, 1) == null)
                    {
                        lock (errors)
                            Log.Write("codex app-server did not answer initialize: " + errors.ToString().Trim());
                        return "codex app-server antwortet nicht";
                    }
                    Send(p, "{\"method\":\"initialized\"}");
                    Send(p, "{\"id\":2,\"method\":\"hooks/list\",\"params\":{\"cwds\":[" + Json.Quote(UserProfile) + "]}}");
                    JsonObject listed = Reply(lines, 2);
                    var result = listed != null ? listed.Get("result") as JsonObject : null;
                    var data = result != null ? result.Get("data") as List<object> : null;
                    if (data == null)
                        return "hooks/list ohne Ergebnis";

                    var value = new StringBuilder();
                    int found = 0;
                    string full = Path.GetFullPath(hooksPath);
                    foreach (object entry in data)
                    {
                        var hooks = entry is JsonObject ? ((JsonObject)entry).Get("hooks") as List<object> : null;
                        foreach (object item in hooks ?? new List<object>())
                        {
                            var hook = item as JsonObject;
                            string source = hook != null ? Json.Text(hook.Get("sourcePath")) : null;
                            string command = hook != null ? Json.Text(hook.Get("command")) : null;
                            if (source == null || command == null || command.IndexOf("aipets.exe", StringComparison.OrdinalIgnoreCase) < 0
                                || !string.Equals(Path.GetFullPath(source), full, StringComparison.OrdinalIgnoreCase))
                                continue;
                            found++;
                            if (Json.Text(hook.Get("trustStatus")) == "trusted")
                                continue;
                            value.Append(value.Length == 0 ? "" : ",").Append(Json.Quote(Json.Text(hook.Get("key"))))
                                .Append(":{\"trusted_hash\":").Append(Json.Quote(Json.Text(hook.Get("currentHash")))).Append('}');
                        }
                    }
                    Log.Write("codex trust: " + found + " aipets hooks listed, " + (value.Length == 0 ? "all trusted" : "trusting the rest"));
                    if (found == 0)
                        return "Codex listet die Hooks nicht, ist das Feature „hooks“ aus?";
                    if (value.Length == 0)
                        return null;   // all trusted already
                    Send(p, "{\"id\":3,\"method\":\"config/batchWrite\",\"params\":{\"edits\":[{\"keyPath\":\"hooks.state\",\"value\":{"
                        + value + "},\"mergeStrategy\":\"upsert\"}],\"reloadUserConfig\":true}}");
                    JsonObject written = Reply(lines, 3);
                    if (written == null)
                        return "config/batchWrite antwortet nicht";
                    return written.Get("error") != null ? "config/batchWrite: " + Json.Write(written.Get("error")).Trim() : null;
                }
                finally
                {
                    try { p.StandardInput.Close(); } catch (IOException) { }
                    if (!p.WaitForExit(5000))
                        try { p.Kill(); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                }
            }
        }

        static void Send(Process p, string json)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(json + "\n");
            p.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
            p.StandardInput.BaseStream.Flush();
        }

        /// <summary>
        /// The response with this id (notifications and server requests are skipped), or null once the
        /// app-server has closed stdout or after 30 s.
        /// </summary>
        static JsonObject Reply(Queue<string> lines, int id)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);
            string wanted = id.ToString(CultureInfo.InvariantCulture);
            while (true)
            {
                string line;
                lock (lines)
                {
                    while (lines.Count == 0)
                    {
                        TimeSpan left = deadline - DateTime.UtcNow;
                        if (left <= TimeSpan.Zero || !Monitor.Wait(lines, left))
                            return null;
                    }
                    if (lines.Peek() == null)
                        return null;   // left in the queue, so later calls return at once too
                    line = lines.Dequeue();
                }
                JsonObject msg;
                try
                {
                    msg = Json.Parse(line) as JsonObject;
                }
                catch (FormatException)
                {
                    continue;
                }
                var msgId = msg != null ? msg.Get("id") as JsonValue : null;
                if (msgId != null && msgId.Raw == wanted && msg.Get("method") == null)
                    return msg;
            }
        }

        // ------------------------------------------------------------------ Claude / Codex hook files

        static JsonObject Group(string matcher, JsonObject handler)
        {
            var group = new JsonObject();
            if (matcher != null)
                group.Set("matcher", JsonValue.Of(matcher));
            group.Set("hooks", new List<object> { handler });
            return group;
        }

        static bool IsOurs(object item, string source)
        {
            var handler = item as JsonObject;
            string command = handler != null ? Json.Text(handler.Get("command")) : null;
            if (command == null || command.IndexOf("aipets.exe", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            if (command.IndexOf("--hook " + source, StringComparison.Ordinal) >= 0)
                return true;
            var args = handler.Get("args") as List<object>;
            return args != null && args.Count >= 2 && Json.Text(args[0]) == "--hook" && Json.Text(args[1]) == source;
        }

        /// <summary>
        /// Settings file with "hooks": { Event: [ { matcher, hooks: [handler] } ] } (Claude Code settings.json, Codex
        /// hooks.json): removes aipets' handlers for this source, then adds the desired groups where the old ones were.
        /// True if the file changed.
        /// </summary>
        public static bool EditJsonHooks(string path, string source, List<KeyValuePair<string, JsonObject>> desired, string description)
        {
            string original = File.Exists(path) ? File.ReadAllText(path) : "";
            bool empty = original.Trim().Length == 0;
            if (empty && desired == null)
                return false;
            var root = (empty ? new JsonObject() : Json.Parse(original)) as JsonObject;
            if (root == null)
                throw new FormatException(Path.GetFileName(path) + " enthält kein JSON-Objekt");
            string before = empty ? "" : Json.Write(Json.Parse(original));
            if (empty && description != null)
                root.Set("description", JsonValue.Of(description));

            var hooks = root.Get("hooks") as JsonObject;
            if (hooks == null)
            {
                if (desired == null)
                    return false;
                hooks = new JsonObject();
                root.Set("hooks", hooks);
            }
            var slots = new Dictionary<string, int>();   // event -> index of aipets' old group
            foreach (KeyValuePair<string, object> ev in hooks.Items)
            {
                var groups = ev.Value as List<object>;
                if (groups == null)
                    continue;
                for (int g = groups.Count - 1; g >= 0; g--)
                {
                    var group = groups[g] as JsonObject;
                    var handlers = group != null ? group.Get("hooks") as List<object> : null;
                    if (handlers == null || handlers.RemoveAll(h => IsOurs(h, source)) == 0)
                        continue;
                    if (handlers.Count == 0)
                        groups.RemoveAt(g);
                    slots[ev.Key] = g;
                }
            }
            if (desired != null)
            {
                foreach (KeyValuePair<string, JsonObject> d in desired)
                {
                    var groups = hooks.Get(d.Key) as List<object>;
                    if (groups == null)
                    {
                        groups = new List<object>();
                        hooks.Set(d.Key, groups);
                    }
                    int slot;
                    if (slots.TryGetValue(d.Key, out slot) && slot <= groups.Count)
                        groups.Insert(slot, d.Value);
                    else
                        groups.Add(d.Value);
                }
            }
            foreach (string ev in slots.Keys)
            {
                var groups = hooks.Get(ev) as List<object>;
                if (groups != null && groups.Count == 0)
                    hooks.Remove(ev);
            }
            if (hooks.Items.Count == 0)
                root.Remove("hooks");

            string after = Json.Write(root);
            if (after == before)
                return false;
            Save(path, original, after);
            return true;
        }

        // ------------------------------------------------------------------ Hermes Agent

        static readonly string[][] HermesEvents =
        {
            new[] { "pre_llm_call", "working" },
            new[] { "pre_approval_request", "waiting" },
            new[] { "post_approval_response", "resume" },
            new[] { "on_session_end", "done" },
            new[] { "on_session_finalize", "end" },
        };

        public static Step Hermes(string home, string exe, bool install)
        {
            const string name = "Hermes Agent";
            string config = Path.Combine(home, "config.yaml");
            if (!File.Exists(config))
                return new Step(name, !install, Directory.Exists(home) ? "config.yaml fehlt (Hermes einmal starten)" : "nicht installiert");
            bool changed = EditHermesConfig(config, exe, install);
            bool approved = EditHermesAllowlist(Path.Combine(home, "shell-hooks-allowlist.json"), exe, install);
            string text = Outcome(changed || approved, install) + (install ? " und freigegeben" : "") + " (" + PetForm.ShortPath(config) + ")";
            if (changed)
                text += ". Ein laufender Hermes-Gateway übernimmt das erst nach einem Neustart";
            return new Step(name, true, text);
        }

        static string HermesCommand(string exe, string what)
        {
            return exe + " --hook hermes " + what;
        }

        /// <summary>
        /// config.yaml, edited line by line (no YAML library): aipets' "- command:" items under the top-level hooks: block
        /// are removed, then one item per event is put right under the event key (added if missing). Every line keeps
        /// its own line ending (Hermes writes CRLF, hand edits often LF) and new lines copy the block's indentation.
        /// True if changed.
        /// </summary>
        public static bool EditHermesConfig(string path, string exe, bool install)
        {
            string original = File.ReadAllText(path);
            var lines = new List<string>(original.Split('\n'));   // a trailing \r stays part of its line
            int top = lines.FindIndex(l => Regex.IsMatch(l, @"^hooks:\s*(#.*)?$"));
            if (top < 0 && !install)
                return false;
            if (install && top >= 0 && HermesUpToDate(lines, top, exe))
                return false;   // removing and adding again could only move our items behind foreign ones

            var eventNames = new List<string>();
            foreach (string[] e in HermesEvents)
                eventNames.Add(e[0]);

            if (top >= 0)
            {
                int end = BlockEnd(lines, top);
                for (int i = top + 1; i < end;)
                {
                    Match m = Regex.Match(lines[i], @"^(\s*)-\s*command:.*aipets\.exe.*--hook\s+hermes\b", RegexOptions.IgnoreCase);
                    if (!m.Success)
                    {
                        i++;
                        continue;
                    }
                    // the item: its dash line plus deeper lines, but never the next list item
                    int indent = m.Groups[1].Value.Length, j = i + 1;
                    while (j < end && lines[j].Trim().Length > 0 && Indent(lines[j]) > indent && !lines[j].TrimStart().StartsWith("-"))
                        j++;
                    lines.RemoveRange(i, j - i);
                    end -= j - i;
                }
                if (!install)
                {
                    // event keys of ours left without items go, and the whole block if nothing is left
                    for (int i = end - 1; i > top; i--)
                    {
                        Match key = Regex.Match(lines[i], @"^(\s+)([A-Za-z_]+):\s*$");
                        if (!key.Success || !eventNames.Contains(key.Groups[2].Value))
                            continue;
                        int next = i + 1;
                        while (next < end && lines[next].Trim().Length == 0)
                            next++;
                        if (next >= end || !IsItemOf(lines[next], key.Groups[1].Value.Length))
                        {
                            lines.RemoveAt(i);
                            end--;
                        }
                    }
                    bool nothingLeft = true;
                    for (int i = top + 1; i < end; i++)
                        if (lines[i].Trim().Length > 0 && !lines[i].TrimStart().StartsWith("#"))
                            nothingLeft = false;
                    if (nothingLeft)
                    {
                        lines.RemoveRange(top, end - top);
                        if (top > 0 && lines[top - 1].StartsWith("# aipets"))
                        {
                            lines.RemoveAt(--top);
                            if (top > 0 && lines[top - 1].Trim().Length == 0)
                                lines.RemoveAt(--top);   // the blank line install put before the block
                        }
                        if (lines.Count > 0 && lines[lines.Count - 1].EndsWith("\r"))
                            lines.Add("");   // the block was the end of the file: end with a whole line break
                    }
                }
            }

            if (install)
            {
                if (top < 0)
                {
                    // new block at the end, in the file's line ending style
                    string eol = lines.Exists(l => l.EndsWith("\r")) ? "\r" : "";
                    int at = lines.Count - 1;   // before the empty rest after the final line break
                    if (lines[at].Length > 0)
                    {
                        // the file did not end with a line break: give its last line one
                        lines[at] = lines[at].TrimEnd('\r') + eol;
                        lines.Add("");
                        at++;
                    }
                    var block = new List<string>();
                    if (at > 0 && lines[at - 1].Trim().Length > 0)
                        block.Add(eol);
                    block.Add("# aipets: the pets on the desktop show whether Hermes is working, waiting for an approval or done" + eol);
                    block.Add("hooks:" + eol);
                    foreach (string[] e in HermesEvents)
                    {
                        block.Add("  " + e[0] + ":" + eol);
                        block.AddRange(HermesItem(exe, e[1], 4, eol));
                    }
                    lines.InsertRange(at, block);
                }
                else
                {
                    int end = BlockEnd(lines, top);
                    // indentation of event keys and of their items, as the block already uses them
                    int keyIndent = 2, itemOffset = 2;
                    for (int i = top + 1; i < end; i++)
                    {
                        Match any = Regex.Match(lines[i], @"^(\s+)[A-Za-z_]+:\s*$");
                        if (!any.Success)
                            continue;
                        keyIndent = any.Groups[1].Value.Length;
                        if (i + 1 < end && lines[i + 1].TrimStart().StartsWith("-") && Indent(lines[i + 1]) >= keyIndent)
                            itemOffset = Indent(lines[i + 1]) - keyIndent;   // 0: "- " right under the key, as PyYAML writes it
                        break;
                    }
                    foreach (string[] e in HermesEvents)
                    {
                        int key = -1;
                        for (int i = top + 1; i < end; i++)
                        {
                            if (Regex.IsMatch(lines[i], @"^\s+" + e[0] + @":\s*(\[\s*\])?\s*$"))
                            {
                                key = i;
                                break;
                            }
                        }
                        if (key >= 0)
                        {
                            string eol = lines[key].EndsWith("\r") ? "\r" : "";
                            lines[key] = Regex.Replace(lines[key].TrimEnd('\r'), @"\s*\[\s*\]\s*$", "") + eol;
                            // line up with the items already under this key
                            bool listed = key + 1 < end && lines[key + 1].TrimStart().StartsWith("-") && Indent(lines[key + 1]) >= Indent(lines[key]);
                            List<string> item = HermesItem(exe, e[1], listed ? Indent(lines[key + 1]) : Indent(lines[key]) + itemOffset, eol);
                            lines.InsertRange(key + 1, item);
                            end += item.Count;
                        }
                        else
                        {
                            int at = end;
                            while (at - 1 > top && lines[at - 1].Trim().Length == 0)
                                at--;
                            string eol = lines[top].EndsWith("\r") ? "\r" : "";   // "hooks:" always has a line break
                            var added = new List<string> { new string(' ', keyIndent) + e[0] + ":" + eol };
                            added.AddRange(HermesItem(exe, e[1], keyIndent + itemOffset, eol));
                            if (at == lines.Count)
                            {
                                // after the file's last line, which had no line break: the file ends with one now
                                lines[at - 1] = lines[at - 1].TrimEnd('\r') + eol;
                                added.Add("");
                            }
                            lines.InsertRange(at, added);
                            end += added.Count;
                        }
                    }
                }
            }

            string result = string.Join("\n", lines.ToArray());
            if (result == original)
                return false;
            Save(path, original, result, false);
            return true;
        }

        static List<string> HermesItem(string exe, string what, int indent, string eol)
        {
            // single quotes: in double quotes YAML would read \U... in a Windows path as an escape
            return new List<string>
            {
                new string(' ', indent) + "- command: '" + HermesCommand(exe, what).Replace("'", "''") + "'" + eol,
                new string(' ', indent + 2) + "timeout: 10" + eol,
            };
        }

        /// <summary>True if the hooks block holds exactly one aipets item for this exe under each event key and no other.</summary>
        static bool HermesUpToDate(List<string> lines, int top, string exe)
        {
            int end = BlockEnd(lines, top);
            var seen = new List<string>();
            for (int i = top + 1; i < end; i++)
            {
                Match m = Regex.Match(lines[i], @"^(\s*)-\s*command:.*aipets\.exe.*--hook\s+hermes\b", RegexOptions.IgnoreCase);
                if (!m.Success)
                    continue;
                string owner = OwnerKey(lines, top, i, m.Groups[1].Value.Length);
                string[] e = Array.Find(HermesEvents, x => x[0] == owner);
                if (e == null || seen.Contains(owner) || lines[i].Trim() != HermesItem(exe, e[1], 0, "")[0])
                    return false;
                seen.Add(owner);
            }
            return seen.Count == HermesEvents.Length;
        }

        /// <summary>The key a list item at this line belongs to, or null if it is not directly under a key.</summary>
        static string OwnerKey(List<string> lines, int top, int item, int indent)
        {
            for (int k = item - 1; k > top; k--)
            {
                string trimmed = lines[k].Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#") || Indent(lines[k]) > indent || (Indent(lines[k]) == indent && trimmed.StartsWith("-")))
                    continue;   // blank, comment, part of an earlier item, or an earlier item of the same list
                Match key = Regex.Match(lines[k], @"^\s+([A-Za-z_]+):\s*$");
                return key.Success ? key.Groups[1].Value : null;
            }
            return null;
        }

        /// <summary>Whether a line below a key at this indentation is part of its value (deeper, or a "- " item at the same depth).</summary>
        static bool IsItemOf(string line, int keyIndent)
        {
            return Indent(line) > keyIndent || (Indent(line) == keyIndent && line.TrimStart().StartsWith("-"));
        }

        static int BlockEnd(List<string> lines, int top)
        {
            int i = top + 1;
            while (i < lines.Count && (lines[i].Length == 0 || char.IsWhiteSpace(lines[i][0]) || lines[i].StartsWith("#")))
                i++;
            // trailing comment or blank lines belong to what follows
            while (i - 1 > top && (lines[i - 1].Trim().Length == 0 || lines[i - 1].StartsWith("#")))
                i--;
            return i;
        }

        static int Indent(string line)
        {
            int n = 0;
            while (n < line.Length && line[n] == ' ')
                n++;
            return n;
        }

        /// <summary>
        /// Hermes asks once per (event, command) before running a shell hook and records the answer in
        /// shell-hooks-allowlist.json; this records aipets' approvals the same way (and drops stale ones).
        /// </summary>
        public static bool EditHermesAllowlist(string path, string exe, bool install)
        {
            string original = File.Exists(path) ? File.ReadAllText(path) : "";
            bool empty = original.Trim().Length == 0;
            if (empty && !install)
                return false;
            var root = (empty ? new JsonObject() : Json.Parse(original)) as JsonObject ?? new JsonObject();
            string before = empty ? "" : Json.Write(Json.Parse(original));
            var approvals = root.Get("approvals") as List<object>;
            if (approvals == null)
            {
                approvals = new List<object>();
                root.Set("approvals", approvals);
            }

            var wanted = new List<string>();
            if (install)
                foreach (string[] e in HermesEvents)
                    wanted.Add(e[0] + "\n" + HermesCommand(exe, e[1]));
            approvals.RemoveAll(item =>
            {
                var o = item as JsonObject;
                string command = o != null ? Json.Text(o.Get("command")) : null;
                if (command == null || command.IndexOf("aipets.exe", StringComparison.OrdinalIgnoreCase) < 0 || command.IndexOf("--hook hermes", StringComparison.Ordinal) < 0)
                    return false;
                string pair = Json.Text(o.Get("event")) + "\n" + command;
                if (wanted.Contains(pair))
                {
                    wanted.Remove(pair);   // already approved: keep it as it is
                    return false;
                }
                return true;
            });
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
            string mtime = File.Exists(exe)
                ? File.GetLastWriteTimeUtc(exe).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture)
                : null;
            foreach (string pair in wanted)
            {
                var entry = new JsonObject();
                entry.Set("approved_at", JsonValue.Of(now));
                entry.Set("command", JsonValue.Of(pair.Substring(pair.IndexOf('\n') + 1)));
                entry.Set("event", JsonValue.Of(pair.Substring(0, pair.IndexOf('\n'))));
                entry.Set("script_mtime_at_approval", mtime != null ? (object)JsonValue.Of(mtime) : new JsonValue("null"));
                approvals.Add(entry);
            }

            string after = Json.Write(root);
            if (after == before)
                return false;
            Save(path, original, after);
            return true;
        }

        // ------------------------------------------------------------------ helpers

        static string Outcome(bool changed, bool install)
        {
            if (install)
                return changed ? "Hooks eingetragen" : "Hooks schon eingerichtet";
            return changed ? "Hooks entfernt" : "keine aipets-Hooks";
        }

        /// <summary>Writes the file, keeping the previous content as .bak-aipets. JSON written from scratch gets CRLF if the old file had it.</summary>
        static void Save(string path, string original, string text, bool matchLineEndings = true)
        {
            if (original.Length > 0)
                File.WriteAllText(path + ".bak-aipets", original, new UTF8Encoding(false));
            if (matchLineEndings && original.Contains("\r\n"))
                text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
