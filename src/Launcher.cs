using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace AiPets
{
    /// <summary>
    /// Opens a pet's program in a new Windows Terminal window (plain console as fallback),
    /// its desktop app, or its link in the browser.
    /// </summary>
    static class Launcher
    {
        public static bool DryRun;

        public static void Launch(PetInfo pet, PetSettings s)
        {
            ProcessStartInfo psi = BuildStartInfo(pet, s);
            // a shell-execute start must never touch EnvironmentVariables: that alone makes Process.Start throw
            Log.Write((DryRun ? "dry-run: " : "launch: ") + psi.FileName + " " + psi.Arguments
                + (!psi.UseShellExecute && psi.EnvironmentVariables.ContainsKey("CLAUDECODE") ? "  [inherited env!]" : ""));
            if (DryRun)
                return;
            if (s.OpensApp)
                Native.AllowSetForegroundWindow(-1);   // this process just got the click: let the app come to the front
            if (!psi.CreateNoWindow)
            {
                using (Process.Start(psi)) { }   // null for a packaged app or a browser that was already running
                return;
            }
            // "codex app" has no window of its own: its messages go to the log, a failure to the user
            string output;
            int? code = RunHidden(psi, out output);
            Log.Write("app command " + (code.HasValue ? "exit " + code.Value : "still running after 60 s")
                + (output.Length > 0 ? ": " + output.Replace(Environment.NewLine, " | ") : ""));
            if (code.HasValue && code.Value != 0)
                throw new InvalidOperationException(pet.Name + ": „" + DesktopApp.ProgramCommand(s, pet.AppCommand)
                    + "“ ist mit Code " + code.Value + " fehlgeschlagen." + (output.Length > 0 ? "\n\n" + output : ""));
        }

        /// <summary>
        /// Program mode: see TerminalStartInfo. Website mode opens the link in the default browser.
        /// App mode: a pet with appcommand lets its program open the app (codex app, in the working
        /// folder, no window); otherwise the app starts directly. Without an app, a pet with appfallback
        /// runs its program with those arguments in the terminal (hermes desktop builds Hermes Desktop once, then starts it).
        /// </summary>
        public static ProcessStartInfo BuildStartInfo(PetInfo pet, PetSettings s)
        {
            if (s.OpensWebsite)
            {
                string url = NormalizeUrl(s.Url);
                if (url == null)
                    throw new UriFormatException(pet.Name + ": \"" + s.Url + "\" ist kein Link.\n\n"
                        + "Trag in den Einstellungen einen Link mit http:// oder https:// ein.");
                return new ProcessStartInfo(url) { UseShellExecute = true };
            }
            if (s.OpensApp)
            {
                Dictionary<string, string> env = Native.LogonEnvironment();
                string program = UsesAppCommand(pet, s) ? ProgramPath(pet, s, env) : null;
                if (program != null)
                    return HiddenStartInfo(program, pet.AppCommand, WorkDirOf(s), env);
                DesktopApp app = DesktopApp.Find(s.DesktopApp);
                if (app != null)
                {
                    ProcessStartInfo psi = app.StartInfo();
                    if (!psi.UseShellExecute)
                        UseEnvironment(psi, env);
                    return psi;
                }
                if (pet.AppFallback.Length == 0)
                    throw new FileNotFoundException(pet.Name + ": Die Desktop-App wurde nicht gefunden.\n\n"
                        + "Installier sie oder wähl in den Einstellungen unter „App“ ihre exe aus.");
                return TerminalStartInfo(pet, s, pet.AppFallback);
            }
            return TerminalStartInfo(pet, s, s.Args);
        }

        /// <summary>What a click would open, in words (--command).</summary>
        public static string Describe(PetInfo pet, PetSettings s)
        {
            ProcessStartInfo psi = BuildStartInfo(pet, s);
            if (s.OpensWebsite)
                return psi.FileName + "\n(im Standardbrowser)";
            DesktopApp app = s.OpensApp ? DesktopApp.Find(s.DesktopApp) : null;
            if (psi.CreateNoWindow)
                return psi.FileName + " " + psi.Arguments + "\n(ohne Fenster, in " + psi.WorkingDirectory
                    + (app != null ? "; öffnet die Desktop-App " + app : "; Desktop-App nicht gefunden") + ")";
            if (app != null)
                return (psi.FileName + " " + psi.Arguments).TrimEnd() + "\n(Desktop-App " + app + ", "
                    + (app.AppId != null ? "App-Paket " + app.Family : "in " + psi.WorkingDirectory) + ")";
            return psi.FileName + " " + psi.Arguments + "\n(in " + psi.WorkingDirectory + ")"
                + (s.OpensApp ? "\n(Desktop-App nicht gefunden, deshalb „" + DesktopApp.ProgramCommand(s, pet.AppFallback) + "“)" : "");
        }

        /// <summary>
        /// App mode goes through the program (appcommand): the pet has one, the app is not one the
        /// user picked, and the program is installed. The working folder matters then.
        /// </summary>
        public static bool AppViaProgram(PetInfo pet, PetSettings s)
        {
            return UsesAppCommand(pet, s) && ProgramPath(pet, s, Native.LogonEnvironment()) != null;
        }

        static bool UsesAppCommand(PetInfo pet, PetSettings s)
        {
            return pet.AppCommand.Length > 0 && s.DesktopApp == pet.DesktopApp;
        }

        /// <summary>The pet's program, found with the PATH of this environment (null: the pet's own PATH).</summary>
        static string ProgramPath(PetInfo pet, PetSettings s, Dictionary<string, string> env)
        {
            string path = env != null && env.ContainsKey("PATH") ? env["PATH"] : Environment.GetEnvironmentVariable("PATH");
            return Resolve(s.Program, pet.Find, path);
        }

        static string WorkDirOf(PetSettings s)
        {
            return Directory.Exists(s.WorkDir) ? s.WorkDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        /// <summary>A command without any window, run in the working folder: .cmd shims (npm installs) through cmd.exe.</summary>
        static ProcessStartInfo HiddenStartInfo(string program, string arguments, string workDir, Dictionary<string, string> env)
        {
            string args = (arguments ?? "").Trim();
            ProcessStartInfo psi = IsBatch(program)
                ? new ProcessStartInfo("cmd.exe", "/c " + Quote(program) + (args.Length > 0 ? " " + args : ""))
                : new ProcessStartInfo(program, args);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = workDir;
            UseEnvironment(psi, env);
            return psi;
        }

        /// <summary>
        /// Runs a windowless command to its end, at most 60 s: its exit code (null if it still runs)
        /// and what it printed. Output pipes that its children keep open are not waited for.
        /// </summary>
        public static int? RunHidden(ProcessStartInfo psi, out string output)
        {
            psi.RedirectStandardOutput = psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = psi.StandardErrorEncoding = Encoding.UTF8;
            var text = new StringBuilder();
            int open = 2;
            // never disposed: the readers may still call in after this method has returned
            var closed = new ManualResetEvent(false);
            DataReceivedEventHandler collect = delegate(object sender, DataReceivedEventArgs e)
            {
                if (e.Data != null)
                {
                    lock (text)
                        text.AppendLine(e.Data);
                }
                else if (Interlocked.Decrement(ref open) == 0)
                {
                    closed.Set();
                }
            };
            using (var process = new Process { StartInfo = psi })
            {
                process.OutputDataReceived += collect;
                process.ErrorDataReceived += collect;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                bool exited = process.WaitForExit(60000);
                if (exited)
                    closed.WaitOne(1000);   // the last lines, unless a child holds the pipes
                lock (text)
                    output = text.ToString().Trim();
                return exited ? process.ExitCode : (int?)null;
            }
        }

        /// <summary>
        /// shell=direct: the terminal runs the program itself (tab closes when it exits);
        /// shell=powershell / cmd: the program runs inside that shell, which stays open afterwards.
        /// </summary>
        static ProcessStartInfo TerminalStartInfo(PetInfo pet, PetSettings s, string arguments)
        {
            // wt hands the caller's environment to the new tab, so start from a clean
            // logon environment (also picks up PATH changes made while the pet runs)
            Dictionary<string, string> env = Native.LogonEnvironment();
            string path = env != null && env.ContainsKey("PATH") ? env["PATH"] : Environment.GetEnvironmentVariable("PATH");

            string program = ProgramPath(pet, s, env);
            if (program == null)
                throw new FileNotFoundException(pet.Name + ": \"" + s.Program + "\" wurde nicht gefunden.\n\n"
                    + "Trag in den Einstellungen den vollen Pfad ein oder nimm den Ordner in den PATH auf.");
            string workDir = WorkDirOf(s);
            string args = (arguments ?? "").Trim();

            string command;
            switch (s.Shell)
            {
                case "powershell":
                    string call = "& '" + program.Replace("'", "''") + "'" + (args.Length > 0 ? " " + args : "");
                    command = "powershell.exe -NoLogo -NoExit -Command \"" + call.Replace("\"", "\\\"") + "\"";
                    break;
                case "cmd":
                    command = "cmd.exe /k \"" + Quote(program) + (args.Length > 0 ? " " + args : "") + "\"";
                    break;
                default:
                    // .cmd shims (npm installs) need cmd.exe; a real exe runs directly
                    command = (IsBatch(program) ? "cmd.exe /c " + Quote(program) : Quote(program)) + (args.Length > 0 ? " " + args : "");
                    break;
            }

            ProcessStartInfo psi;
            string terminal = FindWindowsTerminal(path);
            if (terminal != null)
                psi = new ProcessStartInfo(terminal, "-d " + Quote(TerminalDir(workDir)) + " " + command);
            else if (s.Shell == "powershell" || s.Shell == "cmd")
                psi = new ProcessStartInfo(command.Substring(0, command.IndexOf(' ')), command.Substring(command.IndexOf(' ') + 1));
            else
                psi = new ProcessStartInfo("cmd.exe", "/k \"" + command + "\"");
            psi.UseShellExecute = false;
            psi.WorkingDirectory = workDir;
            UseEnvironment(psi, env);
            return psi;
        }

        /// <summary>Gives a CreateProcess start (not shell execute) this environment instead of the pet's own.</summary>
        static void UseEnvironment(ProcessStartInfo psi, Dictionary<string, string> env)
        {
            if (env == null)
                return;
            psi.EnvironmentVariables.Clear();
            foreach (KeyValuePair<string, string> kv in env)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        /// <summary>
        /// Full path of the program: a path as given, else the pet's known install locations
        /// ("find" in pet.ini, only if they are this program), else the PATH.
        /// </summary>
        public static string Resolve(string program, string find, string path)
        {
            program = Environment.ExpandEnvironmentVariables((program ?? "").Trim().Trim('"'));
            if (program.Length == 0)
                return null;
            if (program.IndexOf('\\') >= 0 || program.IndexOf('/') >= 0)
                return File.Exists(program) ? Path.GetFullPath(program) : null;

            string bare = Path.GetFileNameWithoutExtension(program);
            foreach (string hint in (find ?? "").Split(';'))
            {
                string candidate = Environment.ExpandEnvironmentVariables(hint.Trim());
                if (candidate.Length > 0 && File.Exists(candidate)
                    && string.Equals(Path.GetFileNameWithoutExtension(candidate), bare, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
            if (Path.HasExtension(program))
                return FindOnPath(path, program);
            foreach (string ext in new[] { ".exe", ".cmd", ".bat", ".com" })
            {
                string found = FindOnPath(path, program + ext);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>An absolute http(s) link from what was typed ("gemini.google.com" gets https://), or null.</summary>
        public static string NormalizeUrl(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length > 0 && text.IndexOf("://", StringComparison.Ordinal) < 0)
                text = "https://" + text;
            Uri uri;
            if (!Uri.TryCreate(text, UriKind.Absolute, out uri) || uri.Host.Length == 0
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return null;
            return uri.AbsoluteUri;
        }

        static bool IsBatch(string file)
        {
            return file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        }

        static string FindWindowsTerminal(string path)
        {
            string alias = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wt.exe");
            return File.Exists(alias) ? alias : FindOnPath(path, "wt.exe");
        }

        static string FindOnPath(string path, string file)
        {
            foreach (string dir in (path ?? "").Split(';'))
            {
                try
                {
                    string candidate = Path.Combine(Environment.ExpandEnvironmentVariables(dir.Trim()), file);
                    if (dir.Trim().Length > 0 && File.Exists(candidate))
                        return candidate;
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        // wt parses its own command line: a trailing backslash would escape the closing quote
        static string TerminalDir(string dir)
        {
            return dir.EndsWith("\\") ? dir + "." : dir;
        }

        static string Quote(string s)
        {
            return "\"" + s + "\"";
        }
    }
}
