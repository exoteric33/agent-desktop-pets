using System;
using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace ClaudePet
{
    /// <summary>%APPDATA%\ClaudePet\settings.ini — position, size and the folder Claude Code starts in.</summary>
    sealed class Settings
    {
        public static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudePet");
        static readonly string FilePath = Path.Combine(Folder, "settings.ini");

        public int Scale;                 // 0 = pick from display DPI
        public bool HasPosition;
        public int X, Y;                  // bottom-centre of the pet in screen pixels
        public string WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return s;
                bool hasX = false, hasY = false;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                    int n;
                    bool isInt = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    if (key == "scale" && isInt) s.Scale = Math.Max(1, Math.Min(4, n));
                    else if (key == "x" && isInt) { s.X = n; hasX = true; }
                    else if (key == "y" && isInt) { s.Y = n; hasY = true; }
                    else if (key == "workdir" && Directory.Exists(value)) s.WorkDir = value;
                }
                s.HasPosition = hasX && hasY;
            }
            catch (Exception ex)
            {
                Log.Write("settings unreadable: " + ex.Message);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var text = new System.Text.StringBuilder();
                if (Scale > 0)
                    text.AppendFormat(CultureInfo.InvariantCulture, "scale={0}\r\n", Scale);
                if (HasPosition)
                    text.AppendFormat(CultureInfo.InvariantCulture, "x={0}\r\ny={1}\r\n", X, Y);
                text.AppendFormat("workdir={0}\r\n", WorkDir);
                File.WriteAllText(FilePath, text.ToString());
            }
            catch (Exception ex)
            {
                Log.Write("settings not saved: " + ex.Message);
            }
        }
    }

    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "ClaudePet";

        static string Command
        {
            get { return "\"" + System.Windows.Forms.Application.ExecutablePath + "\""; }
        }

        public static bool Enabled
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && string.Equals(key.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
            }
            set
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value)
                        key.SetValue(ValueName, Command);
                    else
                        key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    static class Log
    {
        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Settings.Folder);
                string path = Path.Combine(Settings.Folder, "pet.log");
                if (File.Exists(path) && new FileInfo(path).Length > 256 * 1024)
                    File.Delete(path);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + "  " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
