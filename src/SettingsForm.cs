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
        readonly Label appName = new Label(), appWhere = new Label();
        readonly LinkLabel setupLink = new LinkLabel();
        readonly CheckBox showBox = new CheckBox(), autostartBox = new CheckBox();
        readonly TrackBar sizeBar = new TrackBar();      // all pets, quarter steps: 4 = 1× … 16 = 4×
        readonly TrackBar petSizeBar = new TrackBar();   // this pet, 5 % steps: 10 = 50 % … 40 = 200 %
        readonly Label sizeValue = new Label(), petSizeLabel = new Label(), petSizeValue = new Label();
        readonly RadioButton programMode = new RadioButton(), appMode = new RadioButton(), websiteMode = new RadioButton();
        readonly TextBox programBox = new TextBox(), argsBox = new TextBox(), dirBox = new TextBox(), urlBox = new TextBox();
        readonly ComboBox shellBox = new ComboBox();
        readonly Button openButton = new Button(), homeButton = new Button();
        // "Klick öffnet: Programm" shows program, arguments, terminal and folder; "Desktop-App" the app
        // (and the folder, if the program opens the app: codex app); "Website" only the link
        readonly List<Control> programRows = new List<Control>(), appRows = new List<Control>(), linkRows = new List<Control>();
        readonly List<Control> folderRows = new List<Control>();
        PetProcess current;
        Ini shownIni;
        bool loading;
        string snapshotMode;   // snapshot only: show the page in this mode instead of the saved one

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
            ClientSize = new Size(700, 540);
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
                if (loading || host == null)
                    return;
                try { Autostart.Choose(autostartBox.Checked); }
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

            // two sliders, both live: the pets follow while a slider moves
            int y = 128;
            AddLabel(page, "Größe aller Pets", y);
            SetupSlider(page, sizeBar, sizeValue, y, (int)(PetSettings.MinSize * 4), (int)(PetSettings.MaxSize * 4), 4);
            sizeBar.ValueChanged += delegate
            {
                ShowSizes();
                if (!loading && host != null)
                    host.SetSize(sizeBar.Value / 4.0);
            };

            y += 36;
            petSizeLabel.AutoSize = true;
            petSizeLabel.Location = new Point(22, y);
            petSizeLabel.ForeColor = Color.FromArgb(40, 40, 40);
            page.Controls.Add(petSizeLabel);
            SetupSlider(page, petSizeBar, petSizeValue, y, PetSettings.MinPercent / 5, PetSettings.MaxPercent / 5, 10);
            petSizeBar.ValueChanged += delegate
            {
                ShowSizes();
                if (loading || current == null || host == null)
                    return;
                int percent = petSizeBar.Value * 5;
                host.ChangeSetting(current, "percent", percent == 100 ? null : PetSettings.Number(percent));
            };

            y += 36;
            AddLabel(page, "Klick öffnet", y);
            // own panel: these radio buttons form one group, apart from anything else on the page
            var modes = new Panel { Location = new Point(136, y - 4), Size = new Size(336, 24) };
            programMode.Text = "Programm";
            appMode.Text = "Desktop-App";
            websiteMode.Text = "Website";
            // ShowPet puts "Website" behind "Desktop-App", or in its place for pets without an app
            programMode.Location = new Point(4, 3);
            appMode.Location = new Point(112, 3);
            websiteMode.Location = new Point(232, 3);
            foreach (RadioButton radio in new[] { programMode, appMode, websiteMode })
            {
                RadioButton r = radio;
                r.AutoSize = true;
                r.CheckedChanged += delegate
                {
                    if (loading || !r.Checked || current == null || host == null)
                        return;
                    string mode = r == websiteMode ? "website" : r == appMode ? "app" : "program";
                    host.ChangeSetting(current, "mode", mode == current.Info.Mode ? null : mode);
                    ShowPet(current);
                };
                modes.Controls.Add(r);
            }
            page.Controls.Add(modes);

            y += 34;
            programRows.Add(AddLabel(page, "Programm", y));
            programRows.Add(SetupBox(page, programBox, y, 330));
            linkRows.Add(AddLabel(page, "Link", y));
            linkRows.Add(SetupBox(page, urlBox, y, 330));
            appRows.Add(AddLabel(page, "App", y));
            appName.Location = new Point(140, y);
            appName.Size = new Size(292, 18);
            appName.AutoEllipsis = true;
            appRows.Add(appName);
            var pickApp = new Button { Text = "…", Location = new Point(436, y - 4), Size = new Size(34, 25) };
            pickApp.Click += delegate { BrowseApp(); };
            appRows.Add(pickApp);
            appWhere.Location = new Point(140, y + 20);
            appWhere.Size = new Size(292, 34);
            appWhere.ForeColor = Muted;
            appWhere.AutoEllipsis = true;
            appRows.Add(appWhere);
            var resetApp = new LinkLabel { Text = "App zurücksetzen", AutoSize = true, Location = new Point(139, y + 59), LinkColor = Accent };
            resetApp.LinkClicked += delegate
            {
                if (current == null || host == null)
                    return;
                host.ChangeSetting(current, "app", null);
                ShowPet(current);
            };
            appRows.Add(resetApp);
            page.Controls.AddRange(new Control[] { appName, pickApp, appWhere, resetApp });
            y += 34;
            programRows.Add(AddLabel(page, "Argumente", y));
            programRows.Add(SetupBox(page, argsBox, y, 330));
            linkRows.Add(AddLabel(page, "Öffnen in", y));
            var browser = new Label { Text = "Standardbrowser", AutoSize = true, Location = new Point(140, y), ForeColor = Muted };
            page.Controls.Add(browser);
            linkRows.Add(browser);
            var resetUrl = new LinkLabel { Text = "Link zurücksetzen", AutoSize = true, Location = new Point(139, y + 25), LinkColor = Accent };
            resetUrl.LinkClicked += delegate
            {
                if (current == null || host == null)
                    return;
                host.ChangeSetting(current, "url", null);
                ShowPet(current);
            };
            page.Controls.Add(resetUrl);
            linkRows.Add(resetUrl);
            y += 34;
            programRows.Add(AddLabel(page, "Öffnen in", y));
            programRows.Add(shellBox);
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
            programRows.Add(reset);

            y += 56;
            folderRows.Add(AddLabel(page, "Arbeitsordner", y));
            folderRows.Add(SetupBox(page, dirBox, y, 292));
            var browse = new Button { Text = "…", Location = new Point(436, y - 4), Size = new Size(34, 25) };
            browse.Click += delegate { BrowseFolder(); };
            page.Controls.Add(browse);
            folderRows.Add(browse);

            y += 38;
            AddLabel(page, "Statusanzeige", y);
            hooks.Location = new Point(140, y);
            hooks.Size = new Size(330, 18);
            page.Controls.Add(hooks);
            setupLink.Text = "Hooks einrichten";
            setupLink.AutoSize = true;
            setupLink.Location = new Point(139, y + 19);
            setupLink.LinkColor = Accent;
            setupLink.LinkClicked += delegate
            {
                if (current == null)
                    return;
                Setup.Step step;
                Cursor = Cursors.WaitCursor;
                try
                {
                    step = Setup.Hooks(current.Info.Status, App.ExePath, true);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
                MessageBox.Show(this, step.ToString(), Text, MessageBoxButtons.OK, step.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                ShowPet(current);
            };
            page.Controls.Add(setupLink);

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

        /// <summary>A slider in the value column with its value label behind it; ticks every tickEvery steps.</summary>
        static void SetupSlider(Control page, TrackBar bar, Label value, int y, int min, int max, int tickEvery)
        {
            bar.AutoSize = false;
            bar.Location = new Point(134, y - 6);
            bar.Size = new Size(240, 34);
            bar.BackColor = Color.White;
            bar.Minimum = min;
            bar.Maximum = max;
            bar.SmallChange = 1;
            bar.LargeChange = tickEvery;
            bar.TickFrequency = tickEvery;
            bar.TickStyle = TickStyle.BottomRight;
            value.AutoSize = true;
            value.Location = new Point(380, y);
            page.Controls.AddRange(new Control[] { bar, value });
        }

        /// <summary>The values behind the sliders: "2×" and "125 % · 2,5×", the size this pet ends up at.</summary>
        void ShowSizes()
        {
            double shared = sizeBar.Value / 4.0;
            int percent = petSizeBar.Value * 5;
            sizeValue.Text = PetSettings.SizeText(shared);
            petSizeValue.Text = PetSettings.PercentText(percent) + " · " + PetSettings.SizeText(Math.Round(PetSettings.PetSize(shared, percent), 2));
        }

        static void SetSlider(TrackBar bar, double value)
        {
            bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum, (int)Math.Round(value)));
        }

        static Label AddLabel(Control page, string text, int y)
        {
            var label = new Label { Text = text, AutoSize = true, Location = new Point(22, y), ForeColor = Color.FromArgb(40, 40, 40) };
            page.Controls.Add(label);
            return label;
        }

        TextBox SetupBox(Control page, TextBox box, int y, int width)
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
            return box;
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
            if (snapshotMode != null)
                s.UseMode(snapshotMode);
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
                petSizeLabel.Text = "Größe von " + p.Info.Name;
                SetSlider(sizeBar, (s.Size > 0 ? s.Size : PetSettings.DefaultSize()) * 4);
                SetSlider(petSizeBar, s.Percent / 5.0);
                ShowSizes();
                if (!programBox.Focused) programBox.Text = s.Program;
                if (!argsBox.Focused) argsBox.Text = s.Args;
                if (!dirBox.Focused) dirBox.Text = s.WorkDir;
                if (!urlBox.Focused) urlBox.Text = s.Url;
                shellBox.SelectedIndex = Math.Max(0, Array.IndexOf(ShellValues, s.Shell));
                bool hasApp = s.DesktopApp.Length > 0;
                int gap = appMode.Left - programMode.Left - programMode.PreferredSize.Width;
                appMode.Visible = hasApp;
                websiteMode.Left = hasApp ? appMode.Left + appMode.PreferredSize.Width + gap : appMode.Left;
                programMode.Checked = s.OpensProgram;
                appMode.Checked = s.OpensApp;
                websiteMode.Checked = s.OpensWebsite;
                bool viaProgram = s.OpensApp && Launcher.AppViaProgram(p.Info, s);
                foreach (Control c in programRows)
                    c.Visible = s.OpensProgram;
                foreach (Control c in appRows)
                    c.Visible = s.OpensApp;
                foreach (Control c in linkRows)
                    c.Visible = s.OpensWebsite;
                foreach (Control c in folderRows)
                    c.Visible = s.OpensProgram || viaProgram;
                if (s.OpensApp)
                    ShowApp(p.Info, s, viaProgram);
                openButton.Text = p.Info.OpenText;
                bool ok;
                hooks.Text = HookText(p.Info, out ok);
                hooks.ForeColor = ok ? Good : Muted;
                setupLink.Visible = !ok && p.Info.Status.Length > 0;
            }
            finally
            {
                loading = false;
            }
        }

        /// <summary>Which app a click opens and how, or what happens without one.</summary>
        void ShowApp(PetInfo pet, PetSettings s, bool viaProgram)
        {
            DesktopApp app = DesktopApp.Find(s.DesktopApp);
            appName.ForeColor = app != null ? Color.FromArgb(26, 26, 26) : Muted;
            appName.Text = app != null ? app.ToString() : "nicht gefunden";
            if (viaProgram)
                appWhere.Text = "Ein Klick startet „" + DesktopApp.ProgramCommand(s, pet.AppCommand)
                    + "“ im Arbeitsordner, ohne Terminalfenster.";
            else if (app != null)
                appWhere.Text = app.AppId != null ? "App-Paket " + app.Family : PetForm.ShortPath(app.Exe);
            else if (pet.AppFallback.Length > 0)
                appWhere.Text = "Ein Klick startet stattdessen „" + DesktopApp.ProgramCommand(s, pet.AppFallback)
                    + "“ im Terminal, das die App einrichtet und öffnet.";
            else
                appWhere.Text = "Installier die App oder wähl mit „…“ ihre exe aus.";
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
            string key = box == programBox ? "program" : box == argsBox ? "args" : box == urlBox ? "url" : "workdir";
            PetSettings saved = host.SettingsOf(current);
            bool shown = key == "url" ? saved.OpensWebsite
                : key == "workdir" ? saved.OpensProgram || (saved.OpensApp && Launcher.AppViaProgram(info, saved))
                : saved.OpensProgram;
            if (!shown)
                return;   // the fields of the other modes are hidden
            if (key == "workdir" && (value.Length == 0 || !Directory.Exists(value)))
            {
                box.Text = saved.WorkDir;   // not a folder: back to the saved one
                return;
            }
            if (key == "url")
            {
                value = Launcher.NormalizeUrl(value);
                if (value == null)
                {
                    box.Text = saved.Url;   // not a link: back to the saved one
                    return;
                }
                box.Text = value;
            }
            string fallback = key == "program" ? info.Program : key == "args" ? info.Args : key == "url" ? info.Url : null;
            string stored = value == fallback ? null : value;
            if (stored != host.Settings.Get(info.Id, key))
                host.ChangeSetting(current, key, stored);
        }

        void CommitAll()
        {
            Commit(programBox);
            Commit(argsBox);
            Commit(dirBox);
            Commit(urlBox);
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

        /// <summary>"…" in app mode: any exe as the pet's app (packaged apps come from pet.ini).</summary>
        void BrowseApp()
        {
            if (current == null || host == null)
                return;
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Welche App soll " + current.Info.Name + " öffnen?";
                dialog.Filter = "Programme (*.exe)|*.exe";
                DesktopApp app = DesktopApp.Find(host.SettingsOf(current).DesktopApp);
                if (app != null && app.Exe != null)
                {
                    dialog.InitialDirectory = Path.GetDirectoryName(app.Exe);
                    dialog.FileName = Path.GetFileName(app.Exe);
                }
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                host.ChangeSetting(current, "app", dialog.FileName == current.Info.DesktopApp ? null : dialog.FileName);
                ShowPet(current);
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
            string file, marker;   // the exe path as it is written in that file
            if (pet.Status == "claude")
            {
                file = Setup.ClaudeSettings;
                marker = App.ExePath.Replace("\\", "\\\\");
            }
            else if (pet.Status == "hermes")
            {
                file = Path.Combine(Setup.HermesHome, "config.yaml");
                marker = App.ExePath.Replace("'", "''");
            }
            else if (pet.Status == "codex")
            {
                file = Path.Combine(Setup.CodexHome, "hooks.json");
                marker = App.ExePath.Replace("'", "''").Replace("\\", "\\\\");
            }
            else
            {
                return "–";
            }
            bool other = false;   // aipets hooks, but for an exe somewhere else (folder moved or renamed)
            try
            {
                if (File.Exists(file))
                {
                    string text = File.ReadAllText(file);
                    bool hooked = text.IndexOf("--hook", StringComparison.Ordinal) >= 0 && text.IndexOf("aipets", StringComparison.OrdinalIgnoreCase) >= 0;
                    ok = hooked && text.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
                    other = hooked && !ok;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            string where = PetForm.ShortPath(file);
            if (ok)
                return "✓ Hooks eingerichtet (" + where + ")";
            return other ? "Hooks rufen eine andere aipets.exe auf" : "Keine Hooks in " + where;
        }

        bool snapshot;

        protected override bool ShowWithoutActivation
        {
            get { return snapshot; }
        }

        /// <summary>
        /// Layout check: renders the window (the page of petId, or the first pet) from an invisible,
        /// off-screen copy: settings.png as saved, settings-&lt;mode&gt;.png for each mode the pet offers.
        /// </summary>
        public static void Snapshot(string dir, string petId)
        {
            Directory.CreateDirectory(dir);
            using (var form = new SettingsForm(null))
            {
                form.snapshot = true;
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.Show();
                form.SelectPet(petId);
                form.SavePng(Path.Combine(dir, "settings.png"));
                foreach (string mode in new[] { "program", "app", "website" })
                {
                    if (form.current == null)
                        break;
                    PetSettings s = PetSettings.From(form.current.Info, Store.Load());
                    s.UseMode(mode);
                    if (s.Mode != mode)
                        continue;   // a pet without a desktop app has no app page
                    form.snapshotMode = mode;
                    form.ShowPet(form.current);
                    form.SavePng(Path.Combine(dir, "settings-" + mode + ".png"));
                }
                form.Close();
            }
        }

        void SavePng(string path)
        {
            Application.DoEvents();
            using (var bmp = new Bitmap(Width, Height))
            {
                DrawToBitmap(bmp, new Rectangle(0, 0, Width, Height));
                bmp.Save(path);
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
