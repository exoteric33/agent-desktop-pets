using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace ClaudePet
{
    enum ClaudeState { Idle, Working, Waiting, Done }

    /// <summary>
    /// One file per Claude Code session in %APPDATA%\ClaudePet\sessions, written by the
    /// hooks in ~/.claude/settings.json (ClaudePet.exe --hook ...) and read by the pet.
    /// Line format: state|unix ms|claude pid|transcript path
    /// </summary>
    sealed class SessionStatus
    {
        public static readonly string Folder = Path.Combine(Settings.Folder, "sessions");

        public ClaudeState State;
        public long Time;
        public int Pid;
        public string Transcript = "";

        public static long UnixNow()
        {
            return ToUnix(DateTime.UtcNow);
        }

        public static long ToUnix(DateTime utc)
        {
            return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        public static SessionStatus Read(string path)
        {
            try
            {
                string[] p = File.ReadAllText(path, Encoding.UTF8).Trim().Split(new[] { '|' }, 4);
                ClaudeState state;
                long time;
                int pid;
                if (p.Length < 4 || !Enum.TryParse(p[0], out state)
                    || !long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out time)
                    || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                    return null;
                return new SessionStatus { State = state, Time = time, Pid = pid, Transcript = p[3] };
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        public void Write(string path)
        {
            Directory.CreateDirectory(Folder);
            string tmp = path + "." + Process.GetCurrentProcess().Id + ".tmp";
            File.WriteAllText(tmp, string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}", State, Time, Pid, Transcript), Encoding.UTF8);
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
                    System.Threading.Thread.Sleep(25);   // the pet may be reading the file this instant
                }
            }
        }
    }

    /// <summary>ClaudePet.exe --hook working|resume|waiting|done|end, JSON from Claude Code on stdin.</summary>
    static class HookCommand
    {
        public static void Run(string what)
        {
            try
            {
                string input;
                using (var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
                    input = stdin.ReadToEnd();
                string id = JsonString(input, "session_id");
                if (string.IsNullOrEmpty(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    return;
                string path = Path.Combine(SessionStatus.Folder, id + ".txt");
                if (what == "end")
                {
                    File.Delete(path);
                    return;
                }

                // Claude Code spawns hooks in event order; async ones may finish out of order,
                // so a hook never overwrites a state recorded by a later-spawned one
                long time = SessionStatus.ToUnix(Process.GetCurrentProcess().StartTime.ToUniversalTime());
                SessionStatus current = File.Exists(path) ? SessionStatus.Read(path) : null;
                if (current != null && current.Time > time)
                    return;

                ClaudeState state;
                switch (what)
                {
                    case "working": state = ClaudeState.Working; break;
                    case "waiting": state = ClaudeState.Waiting; break;
                    case "done": state = ClaudeState.Done; break;
                    case "resume":
                        // a tool finished: ends a permission wait, but must not revive a finished
                        // turn (background agents keep running tools after Stop)
                        if (current != null && current.State != ClaudeState.Waiting)
                            return;
                        state = ClaudeState.Working;
                        break;
                    default:
                        return;
                }
                new SessionStatus
                {
                    State = state,
                    Time = time,
                    Pid = ProcessInfo.FindAncestor("claude", 6),
                    Transcript = JsonString(input, "transcript_path") ?? "",
                }.Write(path);
            }
            catch (Exception ex)
            {
                Log.Write("hook " + what + ": " + ex.Message);
            }
        }

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
    }

    /// <summary>Folds all session files into one state for the pet: waiting > working > idle.</summary>
    sealed class SessionMonitor
    {
        const long StaleMs = 15 * 60 * 1000;
        const int TailBytes = 16 * 1024;

        readonly Dictionary<string, long> checkedTranscripts = new Dictionary<string, long>();

        public ClaudeState State { get; private set; }
        public long LatestDone { get; private set; }

        public void Scan()
        {
            bool working = false, waiting = false;
            long latestDone = 0, now = SessionStatus.UnixNow();
            string[] files;
            try
            {
                files = Directory.Exists(SessionStatus.Folder) ? Directory.GetFiles(SessionStatus.Folder, "*.txt") : new string[0];
            }
            catch (IOException) { files = new string[0]; }
            catch (UnauthorizedAccessException) { files = new string[0]; }

            foreach (string path in files)
            {
                SessionStatus s = SessionStatus.Read(path);
                if (s == null)
                    continue;
                bool gone = s.Pid > 0 ? !ProcessInfo.IsAlive(s.Pid) : now - s.Time > 12 * 3600 * 1000L;
                if (gone)
                {
                    // terminal closed without SessionEnd
                    TryDelete(path);
                    continue;
                }
                if (s.State == ClaudeState.Working || s.State == ClaudeState.Waiting)
                {
                    long activity = Math.Max(s.Time, TranscriptTime(s.Transcript));
                    if (Interrupted(s) || now - activity > StaleMs)
                    {
                        // Esc does not fire Stop; don't spin forever
                        s.State = ClaudeState.Idle;
                        s.Time = now;
                        try { s.Write(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                if (s.State == ClaudeState.Working) working = true;
                else if (s.State == ClaudeState.Waiting) waiting = true;
                else if (s.State == ClaudeState.Done) latestDone = Math.Max(latestDone, s.Time);
            }
            State = waiting ? ClaudeState.Waiting : working ? ClaudeState.Working : ClaudeState.Idle;
            LatestDone = latestDone;
        }

        static long TranscriptTime(string transcript)
        {
            try
            {
                return transcript.Length > 0 && File.Exists(transcript) ? SessionStatus.ToUnix(File.GetLastWriteTimeUtc(transcript)) : 0;
            }
            catch (IOException) { return 0; }
            catch (UnauthorizedAccessException) { return 0; }
            catch (ArgumentException) { return 0; }
        }

        /// <summary>The newest message in the transcript is Claude Code's "[Request interrupted by user…]" marker.</summary>
        bool Interrupted(SessionStatus s)
        {
            long written = TranscriptTime(s.Transcript);
            long seen;
            if (written <= s.Time || (checkedTranscripts.TryGetValue(s.Transcript, out seen) && seen == written))
                return false;
            checkedTranscripts[s.Transcript] = written;
            try
            {
                string tail;
                using (var fs = new FileStream(s.Transcript, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
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
