using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace AiPets
{
    /// <summary>The tray's settings window: one page per pet, every change applies immediately.</summary>
    sealed class SettingsForm : Form
    {
        static readonly string[] ShellValues = { "direct", "powershell", "cmd" };
        static readonly string[] ShellNames = { "Windows Terminal", "Windows Terminal · PowerShell", "Windows Terminal · Eingabeaufforderung" };
        static readonly Color Pane = Color.FromArgb(243, 243, 243);
        static readonly Color Selection = Color.FromArgb(230, 230, 230);
        static readonly Color Accent = Color.FromArgb(0, 103, 192);
        static readonly Color Muted = Color.FromArgb(100, 100, 100);
        static readonly Color Good = Color.FromArgb(16, 124, 16);

        readonly TrayHost host;   // null in snapshot mode
        readonly List<PetProcess> pets;
        readonly float dpi;
        readonly Font nameFont = new Font("Segoe UI Semibold", 10F);
        readonly ListBox list = new ListBox();
        readonly PixelBox avatar = new PixelBox();
        readonly Label title = new Label(), state = new Label(), hooks = new Label();
        readonly CheckBox showBox = new CheckBox(), autostartBox = new CheckBox();
        readonly RadioButton[] sizes = new RadioButton[4];
        readonly TextBox programBox = new TextBox(), argsBox = new TextBox(), dirBox = new TextBox();
        readonly ComboBox shellBox = new ComboBox();
        readonly Button openButton = new Button(), homeButton = new Button();
        PetProcess current;
        Ini shownIni;
        bool loading;

        public SettingsForm(TrayHost host)
        {
            this.host = host;
            if (host != null)
            {
                pets = host.Pets;
            }
            else
            {
                pets = new List<PetProcess>();
                foreach (PetInfo info in PetInfo.Discover())
                    pets.Add(new PetProcess(info));
            }
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                dpi = g.DpiX / 96f;

            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "aipets – Einstellungen";
            ClientSize = new Size(700, 470);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Icon = host != null ? host.AppIcon : TrayHost.LoadAppIcon(new Size(32, 32));

            Controls.Add(BuildPage());
            Controls.Add(BuildSide());
            Controls.Add(BuildBar());
            ResumeLayout(false);

            loading = true;
            try { autostartBox.Checked = Autostart.Enabled; }
            catch (Exception) { autostartBox.Checked = false; }
            loading = false;
        }

        // ------------------------------------------------------------------ layout

        Control BuildBar()
        {
            var bar = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Pane };
            bar.Paint += delegate(object sender, PaintEventArgs e) { e.Graphics.DrawLine(SystemPens.ControlLight, 0, 0, bar.Width, 0); };

            autostartBox.Text = "Mit Windows starten";
            autostartBox.AutoSize = true;
            autostartBox.Location = new Point(20, 18);
            autostartBox.CheckedChanged += delegate
            {
                if (loading)
                    return;
                try { Autostart.Enabled = autostartBox.Checked; }
                catch (Exception ex) { Log.Write("autostart: " + ex.Message); }
            };

            var log = new LinkLabel { Text = "Log öffnen", AutoSize = true, Location = new Point(210, 19), LinkColor = Accent };
            log.LinkClicked += delegate
            {
                try { Process.Start(Log.FilePath); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); }
            };

            var close = new Button { Text = "Schließen", Size = new Size(104, 30), Location = new Point(576, 13) };
            close.Click += delegate { Close(); };
            CancelButton = close;

            bar.Controls.AddRange(new Control[] { autostartBox, log, close });
            return bar;
        }

        Control BuildSide()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 210, BackColor = Pane, Padding = new Padding(8, 10, 8, 10) };
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.BackColor = Pane;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = (int)(58 * dpi);
            list.IntegralHeight = false;
            foreach (PetProcess p in pets)
                list.Items.Add(p.Info.Name);
            list.DrawItem += DrawPetItem;
            list.SelectedIndexChanged += delegate
            {
                list.Invalidate();
                if (list.SelectedIndex >= 0)
                    ShowPet(pets[list.SelectedIndex]);
            };
            side.Controls.Add(list);
            return side;
        }

        Control BuildPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            avatar.Location = new Point(22, 16);
            avatar.Size = new Size(96, 96);
            title.Location = new Point(130, 26);
            title.AutoSize = true;
            title.Font = new Font("Segoe UI Semibold", 17F);
            state.Location = new Point(133, 64);
            state.AutoSize = true;
            state.ForeColor = Muted;
            showBox.Text = "Anzeigen";
            showBox.AutoSize = true;
            showBox.Location = new Point(135, 88);
            showBox.CheckedChanged += delegate
            {
                if (!loading && current != null && host != null)
                    host.SetEnabled(current, showBox.Checked);
            };
            page.Controls.AddRange(new Control[] { avatar, title, state, showBox });

            int y = 128;
            AddLabel(page, "Größe", y);
            for (int i = 0; i < sizes.Length; i++)
            {
                int n = i + 1;
                var radio = new RadioButton { Text = n + "×", AutoSize = true, Location = new Point(140 + i * 58, y - 1) };
                radio.CheckedChanged += delegate
                {
                    if (!loading && radio.Checked && current != null && host != null)
                        host.ChangeSetting(current, "scale", PetSettings.Number(n));
                };
                sizes[i] = radio;
                page.Controls.Add(radio);
            }

            y += 36;
            AddLabel(page, "Programm", y);
            SetupBox(page, programBox, y, 330);
            y += 34;
            AddLabel(page, "Argumente", y);
            SetupBox(page, argsBox, y, 330);
            y += 34;
            AddLabel(page, "Öffnen in", y);
            shellBox.DropDownStyle = ComboBoxStyle.DropDownList;
            shellBox.Items.AddRange(ShellNames);
            shellBox.Location = new Point(140, y - 3);
            shellBox.Width = 330;
            shellBox.SelectedIndexChanged += delegate
            {
                if (loading || current == null || host == null || shellBox.SelectedIndex < 0)
                    return;
                string value = ShellValues[shellBox.SelectedIndex];
                host.ChangeSetting(current, "shell", value == current.Info.Shell ? null : value);
            };
            page.Controls.Add(shellBox);
            var reset = new LinkLabel { Text = "Programm, Argumente und „Öffnen in“ zurücksetzen", AutoSize = true, Location = new Point(139, y + 25), LinkColor = Accent };
            reset.LinkClicked += delegate
            {
                if (current == null || host == null)
                    return;
                host.ChangeSetting(current, "program", null, "args", null, "shell", null);
                ShowPet(current);
            };
            page.Controls.Add(reset);

            y += 56;
            AddLabel(page, "Arbeitsordner", y);
            SetupBox(page, dirBox, y, 292);
            var browse = new Button { Text = "…", Location = new Point(436, y - 4), Size = new Size(34, 25) };
            browse.Click += delegate { BrowseFolder(); };
            page.Controls.Add(browse);

            y += 38;
            AddLabel(page, "Statusanzeige", y);
            hooks.Location = new Point(140, y);
            hooks.Size = new Size(330, 34);
            page.Controls.Add(hooks);

            y += 44;
            openButton.Location = new Point(140, y);
            openButton.Size = new Size(162, 30);
            openButton.Click += delegate
            {
                CommitAll();
                if (current != null && host != null)
                    host.Launch(current);
            };
            homeButton.Text = "Zurück in die Ecke";
            homeButton.Location = new Point(308, y);
            homeButton.Size = new Size(162, 30);
            homeButton.Click += delegate
            {
                if (current != null && host != null)
                    host.SendHome(current);
            };
            page.Controls.AddRange(new Control[] { openButton, homeButton });
            return page;
        }

        static void AddLabel(Control page, string text, int y)
        {
            page.Controls.Add(new Label { Text = text, AutoSize = true, Location = new Point(22, y), ForeColor = Color.FromArgb(40, 40, 40) });
        }

        void SetupBox(Control page, TextBox box, int y, int width)
        {
            box.Location = new Point(140, y - 3);
            box.Width = width;
            box.Leave += delegate { Commit(box); };
            box.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter)
                    return;
                Commit(box);
                e.SuppressKeyPress = true;
            };
            page.Controls.Add(box);
        }

        void DrawPetItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= pets.Count)
                return;
            PetProcess p = pets[e.Index];
            Graphics g = e.Graphics;
            Rectangle r = e.Bounds;
            using (var bg = new SolidBrush(Pane))
                g.FillRectangle(bg, r);
            if (e.Index == list.SelectedIndex)
            {
                var card = new Rectangle(r.X + 2, r.Y + 3, r.Width - 4, r.Height - 6);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = Rounded(card, (int)(6 * dpi)))
                using (var brush = new SolidBrush(Selection))
                    g.FillPath(brush, path);
                g.SmoothingMode = SmoothingMode.None;
                using (var accent = new SolidBrush(Accent))
                    g.FillRectangle(accent, card.X, card.Y + card.Height / 4, (int)Math.Max(3, 3 * dpi), card.Height / 2);
            }
            int icon = (int)(32 * dpi);
            Image img = host != null ? host.PetImage(p.Info, 32) : LoadIcon(p.Info, 32);
            if (img != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(img, new Rectangle(r.X + (int)(14 * dpi), r.Y + (r.Height - icon) / 2, icon, icon));
            }
            int textX = r.X + (int)(56 * dpi);
            TextRenderer.DrawText(g, p.Info.Name, nameFont, new Point(textX, r.Y + (int)(10 * dpi)), Color.FromArgb(26, 26, 26), TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, host != null ? host.StateText(p) : "–", Font, new Point(textX, r.Y + (int)(31 * dpi)), Muted, TextFormatFlags.NoPadding);
        }

        static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ------------------------------------------------------------------ data

        public void SelectPet(string id)
        {
            int index = -1;
            for (int i = 0; i < pets.Count; i++)
                if (string.Equals(pets[i].Info.Id, id, StringComparison.OrdinalIgnoreCase))
                    index = i;
            if (index < 0)
                index = list.SelectedIndex >= 0 ? list.SelectedIndex : 0;
            if (index < pets.Count)
                list.SelectedIndex = index;
        }

        void ShowPet(PetProcess p)
        {
            current = p;
            Ini ini = host != null ? host.Settings : Store.Load();
            shownIni = ini;
            PetSettings s = PetSettings.From(p.Info, ini);
            loading = true;
            try
            {
                if (title.Text != p.Info.Name)
                {
                    title.Text = p.Info.Name;
                    avatar.Image = LoadIcon(p.Info, 48);
                }
                state.Text = host != null ? host.StateText(p) : "–";
                showBox.Checked = s.Enabled;
                int scale = s.Scale > 0 ? s.Scale : 2;
                for (int i = 0; i < sizes.Length; i++)
                    sizes[i].Checked = i + 1 == scale;
                if (!programBox.Focused) programBox.Text = s.Program;
                if (!argsBox.Focused) argsBox.Text = s.Args;
                if (!dirBox.Focused) dirBox.Text = s.WorkDir;
                shellBox.SelectedIndex = Math.Max(0, Array.IndexOf(ShellValues, s.Shell));
                openButton.Text = p.Info.OpenText;
                bool ok;
                hooks.Text = HookText(p.Info, out ok);
                hooks.ForeColor = ok ? Good : Muted;
            }
            finally
            {
                loading = false;
            }
        }

        /// <summary>Called by the tray every second: process states, and settings changed elsewhere (e.g. a pet's own menu).</summary>
        public void RefreshStates()
        {
            list.Invalidate();
            if (current == null || host == null)
                return;
            if (host.Settings != shownIni)
                ShowPet(current);
            else
                state.Text = host.StateText(current);
        }

        void Commit(TextBox box)
        {
            if (loading || current == null || host == null)
                return;
            PetInfo info = current.Info;
            string value = box.Text.Trim();
            string key = box == programBox ? "program" : box == argsBox ? "args" : "workdir";
            if (key == "workdir" && (value.Length == 0 || !Directory.Exists(value)))
            {
                box.Text = host.SettingsOf(current).WorkDir;   // not a folder: back to the saved one
                return;
            }
            string fallback = key == "program" ? info.Program : key == "args" ? info.Args : null;
            string stored = value == fallback ? null : value;
            if (stored != host.Settings.Get(info.Id, key))
                host.ChangeSetting(current, key, stored);
        }

        void CommitAll()
        {
            Commit(programBox);
            Commit(argsBox);
            Commit(dirBox);
        }

        void BrowseFolder()
        {
            if (current == null)
                return;
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "In welchem Ordner soll " + current.Info.Name + " starten?";
                dialog.SelectedPath = dirBox.Text;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                dirBox.Text = dialog.SelectedPath;
                Commit(dirBox);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CommitAll();
            base.OnFormClosing(e);
        }

        readonly Dictionary<string, Image> icons = new Dictionary<string, Image>();

        Image LoadIcon(PetInfo pet, int size)
        {
            string key = pet.Id + "/" + size;
            Image image;
            if (!icons.TryGetValue(key, out image))
            {
                try
                {
                    using (var icon = new Icon(pet.IconPath, size, size))
                        image = icon.ToBitmap();
                }
                catch (Exception)
                {
                    image = null;
                }
                icons[key] = image;
            }
            return image;
        }

        /// <summary>Whether the agent's hooks call this aipets.exe (plain text search in its config).</summary>
        static string HookText(PetInfo pet, out bool ok)
        {
            ok = false;
            string file, marker;
            if (pet.Status == "claude")
            {
                file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".claude\settings.json");
                marker = App.ExePath.Replace("\\", "\\\\");
            }
            else if (pet.Status == "hermes")
            {
                file = HermesConfig();
                marker = App.ExePath;
            }
            else if (pet.Status == "codex")
            {
                file = Path.Combine(CodexHome(), "hooks.json");
                marker = App.ExePath.Replace("\\", "\\\\");
            }
            else
            {
                return "–";
            }
            try
            {
                if (File.Exists(file))
                {
                    string text = File.ReadAllText(file);
                    ok = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("--hook", StringComparison.Ordinal) >= 0;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            string where = PetForm.ShortPath(file);
            return ok ? "✓ Hooks eingerichtet (" + where + ")" : "Keine Hooks in " + where + " – siehe README";
        }

        public static string HermesConfig()
        {
            string home = Environment.GetEnvironmentVariable("HERMES_HOME");
            if (string.IsNullOrEmpty(home))
            {
                home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes");
                if (!Directory.Exists(home))
                    home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hermes");
            }
            return Path.Combine(home, "config.yaml");
        }

        static string CodexHome()
        {
            string home = Environment.GetEnvironmentVariable("CODEX_HOME");
            return string.IsNullOrEmpty(home)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
                : home;
        }

        bool snapshot;

        protected override bool ShowWithoutActivation
        {
            get { return snapshot; }
        }

        /// <summary>Layout check: renders the window into a PNG from an invisible, off-screen copy.</summary>
        public static void Snapshot(string path)
        {
            using (var form = new SettingsForm(null))
            {
                form.snapshot = true;
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                form.SelectPet(null);
                Application.DoEvents();
                using (var bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(path);
                }
                form.Close();
            }
        }

        /// <summary>Shows an image at the largest whole-number scale that fits, without smoothing.</summary>
        sealed class PixelBox : Control
        {
            Image image;

            public PixelBox()
            {
                SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            }

            public Image Image
            {
                get { return image; }
                set
                {
                    image = value;
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
                if (image == null)
                    return;
                int k = Math.Max(1, Math.Min(Width / image.Width, Height / image.Height));
                int w = image.Width * k, h = image.Height * k;
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
            }
        }
    }
}
