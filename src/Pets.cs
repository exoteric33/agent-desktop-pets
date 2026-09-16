using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace AiPets
{
    /// <summary>
    /// One pet = one folder pets\&lt;id&gt;\ with pet.ini (defaults) and sprites\ (atlas.png,
    /// atlas.txt, icon.ico). Adding a folder adds a pet; no code changes needed.
    /// </summary>
    sealed class PetInfo
    {
        public string Id, Dir, Name, OpenText;
        public int Order, Home;
        public string Program, Find, Args, Shell;
        public string Url;          // the pet's website, opened in the default browser in website mode
        public string DesktopApp;   // app mode: app ids and exe paths, ';'-separated (see DesktopApp.Find)
        public string AppCommand;   // app mode: the program with these arguments opens the app ("codex app"), without a window
        public string AppFallback;  // app mode without an installed app: run the program with these arguments in the terminal
        public string Mode;         // "program", "app" or "website": what a click opens unless the settings say otherwise
        public string Status;       // "claude", "hermes", "codex" or "" — which hook source the pet shows

        public string SpritesDir
        {
            get { return Path.Combine(Dir, "sprites"); }
        }

        public string IconPath
        {
            get { return Path.Combine(SpritesDir, "icon.ico"); }
        }

        public bool HasSprites
        {
            get { return File.Exists(Path.Combine(SpritesDir, "atlas.png")) && File.Exists(Path.Combine(SpritesDir, "atlas.txt")); }
        }

        public static List<PetInfo> Discover()
        {
            var pets = new List<PetInfo>();
            if (!Directory.Exists(App.PetsDir))
                return pets;
            foreach (string dir in Directory.GetDirectories(App.PetsDir))
            {
                string ini = Path.Combine(dir, "pet.ini");
                if (!File.Exists(ini))
                    continue;
                try
                {
                    pets.Add(Load(dir, Ini.Parse(File.ReadAllLines(ini), "pet")));
                }
                catch (IOException ex)
                {
                    Log.Write("pet.ini unreadable in " + dir + ": " + ex.Message);
                }
            }
            pets.Sort(delegate(PetInfo a, PetInfo b)
            {
                int c = a.Order.CompareTo(b.Order);
                return c != 0 ? c : string.CompareOrdinal(a.Id, b.Id);
            });
            return pets;
        }

        public static PetInfo ById(string id)
        {
            foreach (PetInfo pet in Discover())
                if (string.Equals(pet.Id, id, StringComparison.OrdinalIgnoreCase))
                    return pet;
            return null;
        }

        static PetInfo Load(string dir, Ini ini)
        {
            var pet = new PetInfo();
            pet.Id = Path.GetFileName(dir).ToLowerInvariant();
            pet.Dir = dir;
            pet.Name = ini.Get("pet", "name") ?? pet.Id;
            pet.OpenText = ini.Get("pet", "open") ?? pet.Name + " öffnen";
            pet.Order = ini.GetInt("pet", "order", 100);
            pet.Home = ini.GetInt("pet", "home", 12);
            pet.Program = ini.Get("pet", "program") ?? "";
            pet.Find = ini.Get("pet", "find") ?? "";
            pet.Args = ini.Get("pet", "args") ?? "";
            pet.Shell = ini.Get("pet", "shell") ?? "direct";
            pet.Url = ini.Get("pet", "url") ?? "";
            pet.DesktopApp = ini.Get("pet", "app") ?? "";
            pet.AppCommand = ini.Get("pet", "appcommand") ?? "";
            pet.AppFallback = ini.Get("pet", "appfallback") ?? "";
            // without mode=, a pet that only has a url is a website pet
            pet.Mode = PetSettings.CheckedMode(ini.Get("pet", "mode")
                ?? (pet.Program.Length == 0 && pet.Url.Length > 0 ? "website" : "program"));
            pet.Status = ini.Get("pet", "status") ?? "";
            return pet;
        }
    }

    /// <summary>A pet's effective settings: the pet.ini defaults overlaid with its section in settings.ini.</summary>
    sealed class PetSettings
    {
        public const double MinSize = 1, MaxSize = 4;
        public const int MinPercent = 50, MaxPercent = 200;
        const double MinPetSize = 0.5, MaxPetSize = 6;   // 6 × 162 px still fits a 1080p screen

        public bool Enabled;
        public double Size;         // shared by all pets ([app] size), 1 … 4; 0 = pick from display DPI
        public int Percent;         // this pet's size in percent of the shared one ([id] percent), 50 … 200
        public bool HasPosition;
        public int X, Y;            // bottom-centre of the pet in screen pixels
        public string WorkDir, Program, Args, Shell, Url, DesktopApp, Mode;

        /// <summary>A click starts the program in the terminal.</summary>
        public bool OpensProgram
        {
            get { return Mode == "program"; }
        }

        /// <summary>A click starts the pet's desktop app.</summary>
        public bool OpensApp
        {
            get { return Mode == "app"; }
        }

        /// <summary>A click opens the website in the browser.</summary>
        public bool OpensWebsite
        {
            get { return Mode == "website"; }
        }

        public static PetSettings From(PetInfo pet, Ini ini)
        {
            string id = pet.Id;
            var s = new PetSettings();
            s.Enabled = ini.Get(id, "enabled") != "0";
            s.Size = SizeOf(ini, id);
            s.Percent = Math.Max(MinPercent, Math.Min(MaxPercent, ini.GetInt(id, "percent", 100)));
            s.HasPosition = ini.Get(id, "x") != null && ini.Get(id, "y") != null;
            s.X = ini.GetInt(id, "x", 0);
            s.Y = ini.GetInt(id, "y", 0);
            s.WorkDir = ini.Get(id, "workdir") ?? "";
            if (s.WorkDir.Length == 0 || !Directory.Exists(s.WorkDir))
                s.WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            s.Program = ini.Get(id, "program") ?? pet.Program;
            s.Args = ini.Get(id, "args") ?? pet.Args;
            s.Shell = ini.Get(id, "shell") ?? pet.Shell;
            s.Url = ini.Get(id, "url") ?? pet.Url;
            s.DesktopApp = ini.Get(id, "app") ?? pet.DesktopApp;
            s.UseMode(ini.Get(id, "mode") ?? pet.Mode);
            return s;
        }

        /// <summary>Switches the mode; app mode needs an app to look for, otherwise it is program mode.</summary>
        public void UseMode(string mode)
        {
            Mode = CheckedMode(mode);
            if (Mode == "app" && DesktopApp.Length == 0)
                Mode = "program";
        }

        /// <summary>"website", "app" or "program" (anything unknown).</summary>
        public static string CheckedMode(string mode)
        {
            return mode == "website" || mode == "app" ? mode : "program";
        }

        /// <summary>
        /// The size all pets share ([app] size). Older settings had one per pet (scale): the tray moves
        /// it over when it starts, a pet started on its own still reads it.
        /// </summary>
        static double SizeOf(Ini ini, string id)
        {
            double size;
            string text = ini.Get("app", "size") ?? ini.Get(id, "scale");
            if (text == null || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out size) || size <= 0)
                return 0;
            return Math.Max(MinSize, Math.Min(MaxSize, size));
        }

        /// <summary>The size this pet is shown at: the shared one (or the display default) times her own percentage.</summary>
        public double PetSize()
        {
            return PetSize(Size > 0 ? Size : DefaultSize(), Percent);
        }

        public static double PetSize(double shared, int percent)
        {
            return Math.Max(MinPetSize, Math.Min(MaxPetSize, shared * percent / 100.0));
        }

        /// <summary>The size without a setting: 2 on a 96-dpi display, more on scaled ones.</summary>
        public static double DefaultSize()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                return Math.Max(MinSize, Math.Min(MaxSize, Math.Round(2 * g.DpiX / 96.0)));
        }

        /// <summary>"2,5×"</summary>
        public static string SizeText(double size)
        {
            return size.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + "×";
        }

        /// <summary>"125 %"</summary>
        public static string PercentText(int percent)
        {
            return Number(percent) + " %";
        }

        public static string Number(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }

        public static string Number(double n)
        {
            return n.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
