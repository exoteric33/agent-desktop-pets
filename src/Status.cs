using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AiPets
{
    enum AgentState { Idle, Working, Waiting, Done }

    /// <summary>
    /// One file per agent session in %APPDATA%\aipets\status\&lt;source&gt;\, written by the agent's
    /// hooks (aipets.exe --hook &lt;source&gt; &lt;event&gt;) and read by the pets.
    /// Line format: state|unix ms|agent pid|extra (Claude, Codex: transcript path, Hermes: open turn ids)
    /// </summary>
    sealed class StatusEntry
    {
        public AgentState State;
        public long Time;
        public int Pid;
        public string Extra = "";

        public static string Folder(string source)
        {
            return Path.Combine(App.DataDir, "status", source);
        }

        public static long UnixNow()
        {
            return ToUnix(DateTime.UtcNow);
        }

        public static long ToUnix(DateTime utc)
        {
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public static StatusEntry Read(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                string[] p = File.ReadAllText(path, Encoding.UTF8).Trim().Split(new[] { '|' }, 4);
                AgentState state;
                long time;
                int pid;
                if (p.Length < 4 || !Enum.TryParse(p[0], out state)
                    || !long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out time)
                    || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                    return null;
                return new StatusEntry { State = state, Time = time, Pid = pid, Extra = p[3] };
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void Write(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
            File.WriteAllText(tmp, string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}", State, Time, Pid, Extra), Encoding.UTF8);
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Replace(tmp, path, null);
                    else
                        File.Move(tmp, path);
                    return;
                }
                catch (IOException)
                {
                    if (attempt == 3)
                    {
                        File.Delete(tmp);
                        throw;
                    }
                    Thread.Sleep(25);   // a pet may be reading the file this instant
                }
            }
        }
    }

    /// <summary>aipets.exe --hook &lt;source&gt; &lt;event&gt;, with the agent's hook JSON on stdin. Prints nothing.</summary>
    static class HookCommand
    {
        public static void Run(string source, string what)
        {
            Log.Tag = "hook " + source;
            try
            {
                // always drain stdin: the agent may still be writing and would see a broken pipe
                string input;
                using (var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
                    input = stdin.ReadToEnd();
                switch (source)
                {
                    case "claude":
                    case "codex":
                        Session(source, what, input);
                        break;
                    case "hermes":
                        Hermes(what, input);
                        break;
                    default:
                        Log.Write("unknown hook source");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Write(what + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Claude Code and Codex, one file per session_id.
        /// Claude Code: UserPromptSubmit → working, PostToolUse → resume, Notification → waiting,
        /// Stop → done, SessionEnd → end.
        /// Codex: UserPromptSubmit → working, PostToolUse → resume, PermissionRequest → waiting,
        /// Stop → done, Interrupt → idle, SessionEnd → end.
        /// </summary>
        static void Session(string source, string what, string input)
        {
            string id = JsonString(input, "session_id");
            if (string.IsNullOrEmpty(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return;
            string path = Path.Combine(StatusEntry.Folder(source), id + ".txt");
            if (what == "end")
            {
                File.Delete(path);
                return;
            }

            // the agents spawn hooks in event order; async ones may finish out of order, so a hook
            // never overwrites a state recorded by a later-spawned one (the mutex keeps reading and
            // writing together)
            long time = StatusEntry.ToUnix(Process.GetCurrentProcess().StartTime.ToUniversalTime());
            using (var mutex = new Mutex(false, "Local\\aipets.status." + source + "-" + id))
            {
                bool owned;
                try { owned = mutex.WaitOne(3000); }
                catch (AbandonedMutexException) { owned = true; }
                try
                {
                    StatusEntry current = StatusEntry.Read(path);
                    if (current != null && current.Time > time)
                        return;

                    AgentState state;
                    switch (what)
                    {
                        case "working": state = AgentState.Working; break;
                        case "waiting": state = AgentState.Waiting; break;
                        case "done": state = AgentState.Done; break;
                        case "idle": state = AgentState.Idle; break;   // Codex: Esc interrupted the turn
                        case "resume":
                            // a tool finished: ends a permission wait, but must not revive a finished
                            // turn (background agents keep running tools after Stop). Codex also
                            // refreshes a working turn: its hooks are the only sign of activity
                            if (current != null && current.State != AgentState.Waiting
                                && !(source == "codex" && current.State == AgentState.Working))
                                return;
                            state = AgentState.Working;
                            break;
                        default:
                            return;
                    }
                    new StatusEntry
                    {
                        State = state,
                        Time = time,
                        Pid = ProcessInfo.FindAncestor(source, 6),   // claude.exe / codex.exe
                        Extra = JsonString(input, "transcript_path") ?? "",
                    }.Write(path);
                }
                finally
                {
                    if (owned)
                        mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>
        /// Hermes Agent shell hooks: pre_llm_call → working, pre_approval_request → waiting,
        /// post_approval_response → resume, on_session_end → done, on_session_finalize → end.
        /// Hermes runs its hooks synchronously from the agent process, so there is one file per
        /// Hermes process. Subagents open their own turns inside it: the pet only shows "done"
        /// once every open turn has ended.
        /// </summary>
        static void Hermes(string what, string input)
        {
            int pid = ProcessInfo.ParentPid();
            if (pid <= 0)
                return;
            string key = "hermes-" + pid.ToString(CultureInfo.InvariantCulture);
            string path = Path.Combine(StatusEntry.Folder("hermes"), key + ".txt");
            using (var mutex = new Mutex(false, "Local\\aipets.status." + key))
            {
                bool owned;
                try { owned = mutex.WaitOne(3000); }
                catch (AbandonedMutexException) { owned = true; }
                try
                {
                    if (what == "end")
                    {
                        File.Delete(path);
                        return;
                    }
                    StatusEntry current = StatusEntry.Read(path);
                    AgentState state = current != null ? current.State : AgentState.Idle;
                    var turns = new List<string>();
                    if (current != null)
                        foreach (string t in current.Extra.Split(','))
                            if (t.Length > 0)
                                turns.Add(t);
                    string turn = (JsonString(input, "turn_id") ?? "").Replace(",", "").Replace("|", "");

                    switch (what)
                    {
                        case "working":
                            if (turn.Length == 0)
                                turn = "?";
                            if (!turns.Contains(turn))
                                turns.Add(turn);
                            if (state != AgentState.Waiting)
                                state = AgentState.Working;
                            break;
                        case "waiting":
                            state = AgentState.Waiting;
                            break;
                        case "resume":
                            if (state != AgentState.Waiting)
                                return;
                            state = turns.Count > 0 ? AgentState.Working : AgentState.Idle;
                            break;
                        case "done":
                            if (turn.Length == 0)
                                turns.Clear();
                            else if (!turns.Remove(turn))
                                turns.Remove("?");
                            if (turns.Count == 0)   // interrupted or failed turns end quietly
                                state = JsonBool(input, "completed") ? AgentState.Done : AgentState.Idle;
                            break;
                        default:
                            return;
                    }
                    new StatusEntry
                    {
                        State = state,
                        Time = StatusEntry.UnixNow(),
                        Pid = pid,
                        Extra = string.Join(",", turns.ToArray()),
                    }.Write(path);
                }
                finally
                {
                    if (owned)
                        mutex.ReleaseMutex();
                }
            }
        }

        /// <summary>First "name": "value" in the JSON. Hook payloads put their ids before any user content.</summary>
        static string JsonString(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success)
                return null;
            string s = m.Groups[1].Value;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(s[i]);
                    continue;
                }
                char c = s[++i];
                switch (c)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                        }
                        break;
                    default: sb.Append(c); break;   // \\ \" \/
                }
            }
            return sb.ToString();
        }

        static bool JsonBool(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*(true|false)");
            return m.Success && m.Groups[1].Value == "true";
        }
    }

    /// <summary>Folds all status files of one source into one state for the pet: waiting > working > idle.</summary>
    sealed class StatusMonitor
    {
        const int TailBytes = 16 * 1024;

        readonly string folder;
        readonly bool claudeTranscripts, codex;
        readonly long staleMs;
        readonly Dictionary<string, long> checkedTranscripts = new Dictionary<string, long>();

        public AgentState State { get; private set; }
        public long LatestDone { get; private set; }

        public StatusMonitor(string source)
        {
            folder = StatusEntry.Folder(source);
            claudeTranscripts = source == "claude";
            codex = source == "codex";
            // Claude's transcript and Codex's tool hooks show activity, so a quiet session is stale
            // soon; Hermes always reports the end of a turn, the timeout only catches lost hooks
            staleMs = claudeTranscripts || codex ? 15 * 60 * 1000L : 2 * 3600 * 1000L;
        }

        public void Scan()
        {
            bool working = false, waiting = false;
            long latestDone = 0, now = StatusEntry.UnixNow();
            string[] files;
            try
            {
                files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.txt") : new string[0];
            }
            catch (IOException) { files = new string[0]; }
            catch (UnauthorizedAccessException) { files = new string[0]; }

            foreach (string path in files)
            {
                StatusEntry s = StatusEntry.Read(path);
                if (s == null)
                    continue;
                bool gone = s.Pid > 0 ? !ProcessInfo.IsAlive(s.Pid) : now - s.Time > 12 * 3600 * 1000L;
                if (gone)
                {
                    // terminal closed without a session-end hook
                    TryDelete(path);
                    continue;
                }
                if (s.State == AgentState.Working || s.State == AgentState.Waiting)
                {
                    long activity = claudeTranscripts || codex ? Math.Max(s.Time, TranscriptTime(s.Extra)) : s.Time;
                    if ((claudeTranscripts && Interrupted(s)) || now - activity > staleMs)
                    {
                        // Esc in Claude Code does not fire Stop, a failed Codex turn neither; don't spin forever
                        s.State = AgentState.Idle;
                        if (!codex)
                        {
                            s.Time = now;
                            try { s.Write(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                        }
                        // Codex keeps its file: the next tool hook of a long-running turn brings the spinner back
                    }
                }
                if (s.State == AgentState.Working) working = true;
                else if (s.State == AgentState.Waiting) waiting = true;
                else if (s.State == AgentState.Done) latestDone = Math.Max(latestDone, s.Time);
            }
            State = waiting ? AgentState.Waiting : working ? AgentState.Working : AgentState.Idle;
            LatestDone = latestDone;
        }

        static long TranscriptTime(string transcript)
        {
            try
            {
                return transcript.Length > 0 && File.Exists(transcript) ? StatusEntry.ToUnix(File.GetLastWriteTimeUtc(transcript)) : 0;
            }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
            catch (ArgumentException) { return 0; }
        }

        /// <summary>The newest message in the transcript is Claude Code's "[Request interrupted by user…]" marker.</summary>
        bool Interrupted(StatusEntry s)
        {
            long written = TranscriptTime(s.Extra);
            long seen;
            if (written <= s.Time || (checkedTranscripts.TryGetValue(s.Extra, out seen) && seen == written))
                return false;
            checkedTranscripts[s.Extra] = written;
            try
            {
                string tail;
                using (var fs = new FileStream(s.Extra, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    fs.Seek(Math.Max(0, fs.Length - TailBytes), SeekOrigin.Begin);
                    using (var reader = new StreamReader(fs, Encoding.UTF8))
                        tail = reader.ReadToEnd();
                }
                string[] lines = tail.Split('\n');
                for (int i = lines.Length - 1; i > 0; i--)   // line 0 may be cut off
                {
                    string line = lines[i];
                    if (line.Contains("\"type\":\"assistant\""))
                        return false;
                    if (line.Contains("\"type\":\"user\""))
                        return line.Contains("[Request interrupted by user");
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }

        static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    static class ProcessInfo
    {
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const int STILL_ACTIVE = 259;

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus, PebBaseAddress, AffinityMask, BasePriority, UniqueProcessId, InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")] static extern int NtQueryInformationProcess(IntPtr process, int infoClass, ref PROCESS_BASIC_INFORMATION info, int size, out int returned);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr process, out int code);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        /// <summary>The process that started this one (for a hook: the agent), or 0.</summary>
        public static int ParentPid()
        {
            return ParentPid(GetCurrentProcess());
        }

        /// <summary>Nearest ancestor process with this image name (e.g. the claude.exe that ran the hook), or 0.</summary>
        public static int FindAncestor(string imageName, int maxDepth)
        {
            int pid = ParentPid(GetCurrentProcess());
            for (int depth = 0; pid > 0 && depth < maxDepth; depth++)
            {
                IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (h == IntPtr.Zero)
                    return 0;
                try
                {
                    var name = new StringBuilder(1024);
                    int size = name.Capacity;
                    if (QueryFullProcessImageName(h, 0, name, ref size)
                        && string.Equals(Path.GetFileNameWithoutExtension(name.ToString()), imageName, StringComparison.OrdinalIgnoreCase))
                        return pid;
                    pid = ParentPid(h);
                }
                finally
                {
                    CloseHandle(h);
                }
            }
            return 0;
        }

        public static bool IsAlive(int pid)
        {
            IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
                return false;
            try
            {
                int code;
                return GetExitCodeProcess(h, out code) && code == STILL_ACTIVE;
            }
            finally
            {
                CloseHandle(h);
            }
        }

        static int ParentPid(IntPtr process)
        {
            var info = new PROCESS_BASIC_INFORMATION();
            int returned;
            if (NtQueryInformationProcess(process, 0, ref info, Marshal.SizeOf(info), out returned) != 0)
                return 0;
            return info.InheritedFromUniqueProcessId.ToInt32();
        }
    }
}
