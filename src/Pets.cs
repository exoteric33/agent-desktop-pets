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

        public bool HasOriginal
        {
            get { return File.Exists(Path.Combine(SpritesDir, "original", "atlas.png"))
                && File.Exists(Path.Combine(SpritesDir, "original", "atlas.txt")); }
        }

        public string StyleDir(string style)
        {
            return style == "original" && HasOriginal ? Path.Combine(SpritesDir, "original") : SpritesDir;
        }

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
        // the tallest figure (Grok) at 1:1: no pet is smaller, so no sprite loses pixels;
        // old settings counted sizes in steps of this (1×, 2× …)
        public const int HeightUnit = 162, MinHeight = HeightUnit;

        public bool Enabled;        // the pet's own choice ([id] enabled)
        public bool AllHidden;      // "Alle Pets ausblenden" ([app] hidden=1); Enabled stays as it was
        public int Height;          // height of all pets in screen pixels ([app] height); 0 = from the display DPI
        public int OwnHeight;       // this pet's own height ([id] height); 0 = the one of all pets
        public bool HasPosition;
        public int X, Y;            // bottom-centre of the pet in screen pixels
        public string WorkDir, Program, Args, Shell, Url, DesktopApp, Mode, Style;

        /// <summary>The pet is on the desktop: shown herself, and not all pets hidden.</summary>
        public bool Shown
        {
            get { return Enabled && !AllHidden; }
        }

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
            s.AllHidden = HidesAll(ini);
            s.Height = HeightOf(ini, "app");
            s.OwnHeight = HeightOf(ini, id);
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
            s.Style = ini.Get(id, "style") == "original" && pet.HasOriginal ? "original" : "pixel";
            s.UseMode(ini.Get(id, "mode") ?? pet.Mode);
            return s;
        }

        /// <summary>All pets are hidden at once; each pet's own "Anzeigen" comes back when this is off again.</summary>
        public static bool HidesAll(Ini ini)
        {
            return ini.Get("app", "hidden") == "1";
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

        /// <summary>A height key: 0 if unset or not a number, otherwise at least MinHeight (the screen limit is the pet's business).</summary>
        static int HeightOf(Ini ini, string section)
        {
            int px = ini.GetInt(section, "height", 0);
            return px <= 0 ? 0 : Math.Max(MinHeight, px);
        }

        /// <summary>The height of all pets without their own: the setting or the display default.</summary>
        public int SharedHeight()
        {
            return Height > 0 ? Height : DefaultHeight();
        }

        /// <summary>The height this pet asks for; the pet keeps it within the screen she stands on.</summary>
        public int PetHeight()
        {
            return OwnHeight > 0 ? OwnHeight : SharedHeight();
        }

        /// <summary>Without a setting: 324 px on a 96-dpi display, more on scaled ones.</summary>
        public static int DefaultHeight()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                return Math.Max(1, (int)Math.Round(2 * g.DpiX / 96.0)) * HeightUnit;
        }

        /// <summary>The height for all pets. It replaces every pet's own height: they all snap to it.</summary>
        public static void ShareHeight(Ini ini, int px)
        {
            foreach (string section in ini.SectionNames())
                ini.Set(section, "height", null);
            ini.Set("app", "height", Number(px));
        }

        /// <summary>"324 px"</summary>
        public static string HeightText(int px)
        {
            return Number(px) + " px";
        }

        public static string Number(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
