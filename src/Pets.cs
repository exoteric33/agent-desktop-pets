using System;
using System.Collections.Generic;
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
        public string Status;   // "claude", "hermes", "codex" or "" — which hook source the pet shows

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
            pet.Status = ini.Get("pet", "status") ?? "";
            return pet;
        }
    }

    /// <summary>A pet's effective settings: the pet.ini defaults overlaid with its section in settings.ini.</summary>
    sealed class PetSettings
    {
        public bool Enabled;
        public int Scale;           // 0 = pick from display DPI
        public bool HasPosition;
        public int X, Y;            // bottom-centre of the pet in screen pixels
        public string WorkDir, Program, Args, Shell;

        public static PetSettings From(PetInfo pet, Ini ini)
        {
            string id = pet.Id;
            var s = new PetSettings();
            s.Enabled = ini.Get(id, "enabled") != "0";
            s.Scale = Math.Max(0, Math.Min(4, ini.GetInt(id, "scale", 0)));
            s.HasPosition = ini.Get(id, "x") != null && ini.Get(id, "y") != null;
            s.X = ini.GetInt(id, "x", 0);
            s.Y = ini.GetInt(id, "y", 0);
            s.WorkDir = ini.Get(id, "workdir") ?? "";
            if (s.WorkDir.Length == 0 || !Directory.Exists(s.WorkDir))
                s.WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            s.Program = ini.Get(id, "program") ?? pet.Program;
            s.Args = ini.Get(id, "args") ?? pet.Args;
            s.Shell = ini.Get(id, "shell") ?? pet.Shell;
            return s;
        }

        public static string Number(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}
