using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace AiPets
{
    /// <summary>Paths shared by the tray, the pets and the hook command. No WinForms here: the hook path stays fast.</summary>
    static class App
    {
        public const string Name = "aipets";
        public static readonly string ExePath = Assembly.GetExecutingAssembly().Location;
        public static readonly string Dir = Path.GetDirectoryName(ExePath);
        public static readonly string PetsDir = Path.Combine(Dir, "pets");
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
    }

    /// <summary>Minimal ini: [sections] with key=value lines, ';' and '#' start comments.</summary>
    sealed class Ini
    {
        readonly Dictionary<string, Dictionary<string, string>> sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public static Ini Parse(IEnumerable<string> lines, string defaultSection)
        {
            var ini = new Ini();
            string section = defaultSection;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                    continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq > 0)
                    ini.Set(section, line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim());
            }
            return ini;
        }

        public string Get(string section, string key)
        {
            Dictionary<string, string> values;
            string value;
            return sections.TryGetValue(section, out values) && values.TryGetValue(key, out value) ? value : null;
        }

        public int GetInt(string section, string key, int fallback)
        {
            int n;
            string s = Get(section, key);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        /// <summary>Sets a value; null removes the key.</summary>
        public void Set(string section, string key, string value)
        {
            Dictionary<string, string> values;
            if (!sections.TryGetValue(section, out values))
            {
                if (value == null)
                    return;
                values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                sections[section] = values;
            }
            if (value == null)
                values.Remove(key);
            else
                values[key] = value;
        }

        public string Format()
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, Dictionary<string, string>> section in sections)
            {
                if (section.Value.Count == 0)
                    continue;
                if (text.Length > 0)
                    text.Append("\r\n");
                text.Append('[').Append(section.Key).Append("]\r\n");
                foreach (KeyValuePair<string, string> kv in section.Value)
                    text.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            }
            return text.ToString();
        }
    }

    /// <summary>
    /// %APPDATA%\aipets\settings.ini, one section per pet. The tray and every pet process
    /// read and write it, always under one named mutex; changes are noticed by timestamp.
    /// </summary>
    static class Store
    {
        public static readonly string FilePath = Path.Combine(App.DataDir, "settings.ini");

        public static Ini Load()
        {
            Ini ini = null;
            Locked(delegate { ini = Read(); });
            return ini ?? new Ini();
        }

        /// <summary>Like Load, but false when the file exists and cannot be read: no defaults in place of real settings.</summary>
        public static bool TryLoad(out Ini ini)
        {
            Ini loaded = null;
            bool read = false;
            Locked(delegate
            {
                loaded = Read();
                read = true;
            });
            ini = read ? (loaded ?? new Ini()) : null;
            return read;
        }

        /// <summary>Changes keys of one section: Update("hermes", "x", "10", "y", null) — null removes.</summary>
        public static void Update(string section, params string[] keyValues)
        {
            Locked(delegate
            {
                Ini ini = Read() ?? new Ini();
                for (int i = 0; i + 1 < keyValues.Length; i += 2)
                    ini.Set(section, keyValues[i], keyValues[i + 1]);
                Directory.CreateDirectory(App.DataDir);
                File.WriteAllText(FilePath, ini.Format());
            });
        }

        public static DateTime Stamp()
        {
            try
            {
                return File.GetLastWriteTimeUtc(FilePath);
            }
            catch (IOException) { return DateTime.MinValue; }
            catch (UnauthorizedAccessException) { return DateTime.MinValue; }
        }

        static Ini Read()
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return File.Exists(FilePath) ? Ini.Parse(File.ReadAllLines(FilePath), "app") : null;
                }
                catch (IOException)
                {
                    if (attempt == 5)
                        throw;
                    Thread.Sleep(100);   // locked for a moment (virus scanner, backup): better late than defaults
                }
            }
        }

        static void Locked(Action action)
        {
            using (var mutex = new Mutex(false, "Local\\aipets.settings"))
            {
                bool owned;
                try
                {
                    owned = mutex.WaitOne(3000);
                }
                catch (AbandonedMutexException)
                {
                    owned = true;   // a process died while writing; the file is still whole or absent
                }
                try
                {
                    action();
                }
                catch (IOException ex)
                {
                    Log.Write("settings: " + ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Log.Write("settings: " + ex.Message);
                }
                finally
                {
                    if (owned)
                        mutex.ReleaseMutex();
                }
            }
        }
    }

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Command
        {
            get { return "\"" + App.ExePath + "\""; }
        }

        public static bool Enabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && string.Equals(key.GetValue(App.Name) as string, Command, StringComparison.OrdinalIgnoreCase);
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                        key.SetValue(App.Name, Command);
                    else
                        key.DeleteValue(App.Name, false);
                }
            }
        }

        /// <summary>The user's choice: switches autostart and remembers a "no" ([app] autostart=0 in settings.ini).</summary>
        public static void Choose(bool on)
        {
            Enabled = on;
            Store.Update("app", "autostart", on ? null : "0");
        }

        /// <summary>
        /// Autostart is on by default: the tray switches it on when it starts (also after the folder
        /// moved) unless the user switched it off. Never for a test copy like aipets-test.exe.
        /// </summary>
        public static void ApplyDefault(Ini settings)
        {
            if (settings.Get("app", "autostart") == "0"
                || !string.Equals(Path.GetFileName(App.ExePath), App.Name + ".exe", StringComparison.OrdinalIgnoreCase)
                || Enabled)
                return;
            Enabled = true;
            Log.Write("autostart switched on (default)");
        }
    }

    static class Log
    {
        public static readonly string FilePath = Path.Combine(App.DataDir, "aipets.log");

        /// <summary>Which process writes: "tray", a pet id or "hook claude".</summary>
        public static string Tag = "";

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(App.DataDir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 512 * 1024)
                    File.Delete(FilePath);
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + "  [" + Tag + "] " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
