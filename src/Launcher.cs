using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AiPets
{
    /// <summary>Opens a pet's program in a new Windows Terminal window (plain console as fallback).</summary>
    static class Launcher
    {
        public static bool DryRun;

        public static void Launch(PetInfo pet, PetSettings s)
        {
            ProcessStartInfo psi = BuildStartInfo(pet, s);
            Log.Write((DryRun ? "dry-run: " : "launch: ") + psi.FileName + " " + psi.Arguments
                + (psi.EnvironmentVariables.ContainsKey("CLAUDECODE") ? "  [inherited env!]" : ""));
            if (!DryRun)
                Process.Start(psi).Dispose();
        }

        /// <summary>
        /// shell=direct: the terminal runs the program itself (tab closes when it exits);
        /// shell=powershell / cmd: the program runs inside that shell, which stays open afterwards.
        /// </summary>
        public static ProcessStartInfo BuildStartInfo(PetInfo pet, PetSettings s)
        {
            // wt hands the caller's environment to the new tab, so start from a clean
            // logon environment (also picks up PATH changes made while the pet runs)
            Dictionary<string, string> env = Native.LogonEnvironment();
            string path = env != null && env.ContainsKey("PATH") ? env["PATH"] : Environment.GetEnvironmentVariable("PATH");

            string program = Resolve(s.Program, pet.Find, path);
            if (program == null)
                throw new FileNotFoundException(pet.Name + ": \"" + s.Program + "\" wurde nicht gefunden.\n\n"
                    + "Trag in den Einstellungen den vollen Pfad ein oder nimm den Ordner in den PATH auf.");
            string workDir = Directory.Exists(s.WorkDir) ? s.WorkDir : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string args = (s.Args ?? "").Trim();

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
            if (env != null)
            {
                psi.EnvironmentVariables.Clear();
                foreach (KeyValuePair<string, string> kv in env)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
            }
            return psi;
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
