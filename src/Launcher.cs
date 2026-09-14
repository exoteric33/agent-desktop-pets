using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ClaudePet
{
    /// <summary>Opens Claude Code in a new Windows Terminal window (plain console as fallback).</summary>
    static class Launcher
    {
        public static bool DryRun;

        // the user always works without permission prompts (their explicit choice)
        const string ClaudeArgs = "--dangerously-skip-permissions";

        public static void Launch(string workDir)
        {
            ProcessStartInfo psi = BuildStartInfo(workDir);
            Log.Write((DryRun ? "dry-run: " : "launch: ") + psi.FileName + " " + psi.Arguments
                + (psi.EnvironmentVariables.ContainsKey("CLAUDECODE") ? "  [inherited env!]" : ""));
            if (!DryRun)
                Process.Start(psi).Dispose();
        }

        public static ProcessStartInfo BuildStartInfo(string workDir)
        {
            // wt hands the caller's environment to the new tab, so start from a clean
            // logon environment (also picks up PATH changes made while the pet runs)
            Dictionary<string, string> env = Native.LogonEnvironment();
            string path = env != null && env.ContainsKey("PATH") ? env["PATH"] : Environment.GetEnvironmentVariable("PATH");

            string claude = FindClaude(path);
            if (claude == null)
                throw new FileNotFoundException(
                    "Claude Code wurde nicht gefunden.\n\nErwartet unter %USERPROFILE%\\.local\\bin\\claude.exe oder im PATH.");
            if (!Directory.Exists(workDir))
                workDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // .cmd shims (npm installs) need cmd.exe; the native claude.exe runs directly
            string command = claude.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)
                ? "cmd.exe /c " + Quote(claude)
                : Quote(claude);
            command += " " + ClaudeArgs;

            ProcessStartInfo psi;
            string terminal = FindWindowsTerminal(path);
            if (terminal != null)
                psi = new ProcessStartInfo(terminal, "-d " + Quote(TerminalDir(workDir)) + " " + command);
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

        static string FindClaude(string path)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string native = Path.Combine(home, @".local\bin\claude.exe");
            if (File.Exists(native))
                return native;
            return FindOnPath(path, "claude.exe") ?? FindOnPath(path, "claude.cmd");
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
