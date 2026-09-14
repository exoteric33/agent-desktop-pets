using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudePet
{
    sealed class PetForm : Form
    {
        // room around the character cell for the speech bubble and particles (cell pixels)
        const int PadX = 26, PadTop = 20;
        const int Phases = 8;
        const int IdleStepMs = 260, SleepStepMs = 620;
        const int SleepAfterMs = 60 * 1000;
        const int BubbleDelayMs = 120;

        static readonly int[] BounceSteps = { -1, -2, -1, 1, 2, 3, 3, 2, 1 };
        static readonly int[] BounceMs = { 40, 60, 40, 40, 40, 60, 60, 50, 50 };
        static readonly string[] SparkFrames = { "spark1", "spark2", "spark3", "spark2", "spark1", "spark0" };
        static readonly string[] ZFrames = { "z0", "z1", "z2" };
        static readonly int[] SpinFrames = { 0, 1, 2, 3, 4, 3, 2, 1 };

        sealed class Particle
        {
            public double X, Y, VX, VY, Gravity;
            public long Born;
            public int Life;
            public string[] Frames;
        }

        readonly Atlas atlas;
        readonly Settings settings;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random rng = new Random();
        readonly List<Particle> particles = new List<Particle>();

        Native.LayeredSurface surface;
        Graphics gfx;
        int scale;
        Point anchor;               // bottom-centre of the pet on screen
        bool mirrored;
        string lastRender;

        long now;
        int phase;
        long phaseAt, blinkUntil, nextBlink, secondBlinkAt = -1, happyUntil, bounceAt = -1;
        long nextIdleAction, nextZ, hoverSince, nextTopmost, nextFullscreenCheck;
        long hintUntil = 2800;      // show the ">_" bubble once after start
        long lastLaunch = long.MinValue / 2;
        bool sleeping, hovered, menuOpen, hiddenForFullscreen;
        bool pressed, dragging;
        Point pressCursor, pressAnchor;

        // Claude Code activity, reported by the hooks (see Sessions.cs); done times are unix ms
        readonly SessionMonitor sessions = new SessionMonitor();
        ClaudeState claude;
        long nextSessionScan, nextStatusFx;
        long doneShownSince, doneInputSince, doneAcknowledged = SessionStatus.UnixNow();

        public PetForm()
        {
            atlas = Atlas.FromResources();
            settings = Settings.Load();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "Claude Pet";
            Cursor = Cursors.Hand;

            scale = settings.Scale > 0 ? settings.Scale : DefaultScale();
            anchor = settings.HasPosition ? Clamp(new Point(settings.X, settings.Y), true) : HomeAnchor();
            mirrored = FacesLeft(anchor, false);
            menu = BuildMenu();

            nextBlink = 3200;
            nextIdleAction = 9000;
            happyUntil = hintUntil;
            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)Native.MA_NOACTIVATE;   // clicking the pet never steals focus
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyScale(scale);
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SavePosition();
            if (gfx != null) gfx.Dispose();
            if (surface != null) surface.Dispose();
            atlas.Dispose();
            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------------ geometry

        int CanvasW { get { return atlas.CellW + 2 * PadX; } }
        int CanvasH { get { return atlas.CellH + PadTop; } }

        Rectangle WindowRect()
        {
            int w = CanvasW * scale, h = CanvasH * scale;
            return new Rectangle(anchor.X - w / 2, anchor.Y - h, w, h);
        }

        static int DefaultScale()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                return Math.Max(1, Math.Min(4, (int)Math.Round(2 * g.DpiX / 96f)));
        }

        Point HomeAnchor()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Right - (atlas.CellW / 2) * scale - 12 * scale, wa.Bottom);
        }

        Point Clamp(Point p, bool snapToTaskbar)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int half = (atlas.CellW / 2 - 6) * scale;
            p.X = Math.Max(vs.Left + half, Math.Min(vs.Right - half, p.X));
            Rectangle wa = Screen.FromPoint(new Point(p.X, Math.Min(p.Y, vs.Bottom - 1))).WorkingArea;
            int minY = vs.Top + (CanvasH - PadTop - 4) * scale;   // keep the top of her head on screen
            p.Y = Math.Max(minY, Math.Min(wa.Bottom, p.Y));
            if (snapToTaskbar && wa.Bottom - p.Y < 18 * scale)
                p.Y = wa.Bottom;   // she stands on the taskbar edge, legs hidden behind it
            return p;
        }

        bool FacesLeft(Point p, bool current)
        {
            // look towards the middle of the screen she is on
            int mid = Screen.FromPoint(p).WorkingArea.Left + Screen.FromPoint(p).WorkingArea.Width / 2;
            int hysteresis = 12 * scale;
            if (current)
                return p.X > mid - hysteresis;
            return p.X > mid + hysteresis;
        }

        void ApplyScale(int newScale)
        {
            scale = newScale;
            anchor = Clamp(anchor, false);
            if (gfx != null) gfx.Dispose();
            if (surface != null) surface.Dispose();
            surface = new Native.LayeredSurface(CanvasW * scale, CanvasH * scale);
            gfx = Graphics.FromImage(surface.Bitmap);
            PixelArtMode(gfx);
            Bounds = WindowRect();
            lastRender = null;
            Render(true);
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                anchor = Clamp(anchor, true);
                Bounds = WindowRect();
                Render(true);
            });
        }

        // ------------------------------------------------------------------ animation

        void Tick()
        {
            now = clock.ElapsedMilliseconds;
            if (now >= nextSessionScan)
            {
                UpdateClaudeState();
                nextSessionScan = now + 500;
            }
            UpdateHover();
            UpdateSleep();

            if (now - phaseAt >= (sleeping ? SleepStepMs : IdleStepMs))
            {
                phase = (phase + 1) % Phases;
                phaseAt = now;
            }
            if (!sleeping && now >= nextBlink)
            {
                blinkUntil = now + 130;
                nextBlink = now + rng.Next(2200, 5600);
                if (rng.Next(4) == 0)
                    secondBlinkAt = now + 260;
            }
            if (secondBlinkAt >= 0 && now >= secondBlinkAt)
            {
                blinkUntil = now + 120;
                secondBlinkAt = -1;
            }
            if (!sleeping && !hovered && !dragging && now >= nextIdleAction)
            {
                IdleAction();
                nextIdleAction = now + rng.Next(7000, 16000);
            }
            if (sleeping && now >= nextZ)
            {
                SpawnZ();
                nextZ = now + 1300;
            }
            if (!sleeping && !dragging && now >= nextStatusFx)
            {
                if (claude == ClaudeState.Working)
                {
                    SpawnTwinkle();   // thinking sparkles from the shirt logo
                    nextStatusFx = now + 2600;
                }
                else if (claude == ClaudeState.Waiting)
                {
                    bounceAt = now;   // hop now and then so you notice she needs you
                    nextStatusFx = now + 3500;
                }
            }
            if (now >= nextFullscreenCheck)
            {
                // a fullscreen video, game or presentation: step aside instead of floating over it
                hiddenForFullscreen = !dragging && Native.FullscreenAppActive(Handle);
                nextFullscreenCheck = now + 1000;
            }
            if (!menuOpen && !dragging && !hiddenForFullscreen && now >= nextTopmost)
            {
                Native.KeepTopmost(Handle);
                nextTopmost = now + 3000;
            }
            particles.RemoveAll(p => now - p.Born >= p.Life);
            Render(false);

            // fast ticks only while something moves quickly; idling stays cheap
            int interval = particles.Count > 0 || bounceAt >= 0 || pressed ? 33 : claude == ClaudeState.Working ? 55 : 80;
            if (timer.Interval != interval)
                timer.Interval = interval;
        }

        void UpdateHover()
        {
            Rectangle win = WindowRect();
            Point c = Cursor.Position;
            bool over = false;
            if (win.Contains(c) && !hiddenForFullscreen)
            {
                int lx = (c.X - win.Left) / scale, ly = (c.Y - win.Top) / scale;
                over = atlas.HitCharacter(lx - PadX, ly - PadTop, mirrored) || (CurrentBubble() != null && BubbleRect().Contains(lx, ly));
            }
            if (over && !hovered)
                hoverSince = now;
            hovered = over;
        }

        /// <summary>Spinner while Claude works, "?" while it waits for you, a check mark when it is done.</summary>
        void UpdateClaudeState()
        {
            ClaudeState previous = claude;
            sessions.Scan();
            claude = sessions.State;
            if (claude == ClaudeState.Waiting && previous != ClaudeState.Waiting)
            {
                bounceAt = now;
                nextStatusFx = now + 3500;
            }

            long wall = SessionStatus.UnixNow();
            if (sessions.LatestDone > doneAcknowledged && sessions.LatestDone > doneShownSince)
            {
                doneShownSince = sessions.LatestDone;
                doneInputSince = 0;
                if (claude == ClaudeState.Idle)
                    bounceAt = now;
            }
            if (doneShownSince > 0 && wall - doneShownSince > 5000)
            {
                // the check mark stays until you have been back at the PC for a few seconds
                long lastInput = wall - Native.IdleMilliseconds();
                if (lastInput > doneShownSince + 5000 && doneInputSince == 0)
                    doneInputSince = wall;
                if (doneInputSince > 0 && wall - doneInputSince > 3000)
                    AcknowledgeDone();
            }
        }

        void AcknowledgeDone()
        {
            doneAcknowledged = Math.Max(doneAcknowledged, doneShownSince);
            doneShownSince = doneInputSince = 0;
        }

        void UpdateSleep()
        {
            uint idle = Native.IdleMilliseconds();
            bool claudeNeedsHer = claude != ClaudeState.Idle || doneShownSince > 0;
            if (!sleeping && idle > SleepAfterMs && !hovered && !dragging && !menuOpen && !claudeNeedsHer)
            {
                sleeping = true;
                nextZ = now + 500;
            }
            else if (sleeping && (idle < 1000 || hovered || claudeNeedsHer))
            {
                sleeping = false;
                happyUntil = now + 900;
                particles.RemoveAll(p => p.Frames == ZFrames);
            }
        }

        void IdleAction()
        {
            int roll = rng.Next(100);
            if (roll < 45)
                happyUntil = now + 1600;
            else if (roll < 80)
                SpawnTwinkle();
            else
                bounceAt = now;
        }

        string FrameName()
        {
            if (bounceAt >= 0)
            {
                long t = now - bounceAt;
                for (int i = 0; i < BounceSteps.Length; i++)
                {
                    if (t < BounceMs[i])
                        return "bounce_" + BounceSteps[i];
                    t -= BounceMs[i];
                }
                bounceAt = -1;
            }
            string face;
            if (dragging)
                face = "happy";
            else if (sleeping)
                face = "blink";
            else if (hovered || now < happyUntil || (doneShownSince > 0 && claude == ClaudeState.Idle))
                face = "happy";
            else if (now < blinkUntil)
                face = "blink";
            else
                face = "normal";
            return face + "_" + phase;
        }

        /// <summary>Bubble sprite to show, or null. The ">_" prompt on hover wins over Claude's state.</summary>
        string CurrentBubble()
        {
            if (dragging || sleeping)
                return null;
            string bubble = "bubble_" + (mirrored ? "l_" : "r_");
            if (hovered && now - hoverSince >= BubbleDelayMs || now < hintUntil)
                return bubble + ((now / 530) % 2 == 0 ? "on" : "off");
            if (claude == ClaudeState.Waiting)
                return bubble + "wait";
            if (claude == ClaudeState.Working)
                return bubble + "spin" + SpinFrames[(now / 110) % SpinFrames.Length];
            if (doneShownSince > 0)
                return bubble + "done";
            return null;
        }

        /// <summary>Bubble bounds in canvas pixels.</summary>
        Rectangle BubbleRect()
        {
            Point tip = atlas.Anchor("bubble", mirrored);
            Size size = atlas.SpriteSize("bubble_r_on");
            int left = mirrored ? PadX + tip.X - (size.Width - 1) : PadX + tip.X;
            return new Rectangle(left, PadTop + tip.Y - (size.Height - 1), size.Width, size.Height);
        }

        void Render(bool force)
        {
            if (surface == null)
                return;
            string frame = (mirrored ? "m_" : "") + FrameName();
            string bubble = CurrentBubble();
            string key = hiddenForFullscreen ? "hidden" : frame + bubble + particles.Count;
            if (!force && key == lastRender && (particles.Count == 0 || hiddenForFullscreen))
                return;
            lastRender = key;

            gfx.Clear(Color.Transparent);
            if (!hiddenForFullscreen)
                Compose(gfx, frame, bubble);
            Rectangle win = WindowRect();
            surface.Present(Handle, win.Left, win.Top);
        }

        void Compose(Graphics g, string frame, string bubble)
        {
            atlas.DrawFrame(g, frame, PadX * scale, PadTop * scale, scale);
            if (bubble != null)
            {
                Point tip = atlas.Anchor("bubble", mirrored);
                atlas.DrawSprite(g, bubble, (PadX + tip.X) * scale, (PadTop + tip.Y) * scale, scale);
            }
            foreach (Particle p in particles)
            {
                double t = (now - p.Born) / 1000.0;
                double f = (now - p.Born) / (double)p.Life;
                string sprite = p.Frames[Math.Min(p.Frames.Length - 1, (int)(f * p.Frames.Length))];
                int x = (int)Math.Round(p.X + p.VX * t);
                int y = (int)Math.Round(p.Y + p.VY * t + 0.5 * p.Gravity * t * t);
                atlas.DrawSprite(g, sprite, x * scale, y * scale, scale);
            }
        }

        static void PixelArtMode(Graphics g)
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.SmoothingMode = SmoothingMode.None;
        }

        /// <summary>
        /// Art check without touching the desktop: renders idle, hover, sleep and click
        /// (both facing directions) through the real drawing code into PNGs.
        /// </summary>
        public static void Snapshot(string dir)
        {
            Directory.CreateDirectory(dir);
            using (var pet = new PetForm())
            {
                pet.scale = 3;
                foreach (bool left in new[] { false, true })
                {
                    string side = left ? "_left" : "_right";
                    pet.mirrored = left;
                    pet.SnapshotState(dir, "idle" + side, delegate { });
                    pet.SnapshotState(dir, "hover" + side, delegate
                    {
                        pet.hovered = true;
                        pet.hoverSince = pet.now - 1000;
                    });
                    pet.SnapshotState(dir, "sleep" + side, delegate
                    {
                        pet.sleeping = true;
                        for (int i = 0; i < 3; i++)
                        {
                            pet.SpawnZ();
                            pet.now += 800;
                        }
                    });
                    pet.SnapshotState(dir, "click" + side, delegate
                    {
                        pet.bounceAt = pet.now;
                        pet.Burst();
                        pet.now += 260;
                    });
                    pet.SnapshotState(dir, "working" + side, delegate
                    {
                        pet.claude = ClaudeState.Working;
                        pet.now = 110 * 196;   // biggest spinner frame
                    });
                    pet.SnapshotState(dir, "waiting" + side, delegate { pet.claude = ClaudeState.Waiting; });
                    pet.SnapshotState(dir, "done" + side, delegate { pet.doneShownSince = 1; });
                }
            }
        }

        void SnapshotState(string dir, string name, Action setup)
        {
            now = 21200;   // bubble cursor phase: visible
            hovered = sleeping = false;
            bounceAt = -1;
            happyUntil = blinkUntil = 0;
            phase = 0;
            claude = ClaudeState.Idle;
            doneShownSince = 0;
            particles.Clear();
            setup();
            using (var bmp = new Bitmap(CanvasW * scale, CanvasH * scale, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(58, 74, 92));
                PixelArtMode(g);
                Compose(g, (mirrored ? "m_" : "") + FrameName(), CurrentBubble());
                bmp.Save(Path.Combine(dir, name + ".png"));
            }
        }

        // ------------------------------------------------------------------ particles

        Point CanvasAnchor(string name)
        {
            Point a = atlas.Anchor(name, mirrored);
            return new Point(PadX + a.X, PadTop + a.Y);
        }

        void Burst()
        {
            Point head = CanvasAnchor("head");
            int count = 8;
            double offset = rng.NextDouble() * Math.PI;
            for (int i = 0; i < count; i++)
            {
                double angle = offset + i * 2 * Math.PI / count;
                double speed = 38 + rng.Next(22);
                particles.Add(new Particle
                {
                    X = head.X, Y = head.Y - 16,   // above the hair, orange on orange disappears
                    VX = Math.Cos(angle) * speed, VY = Math.Sin(angle) * speed - 22,
                    Gravity = 40, Born = now, Life = 620 + rng.Next(160), Frames = SparkFrames,
                });
            }
        }

        void SpawnTwinkle()
        {
            Point logo = CanvasAnchor("logo");
            particles.Add(new Particle
            {
                X = logo.X, Y = logo.Y, VX = (rng.NextDouble() - 0.5) * 6, VY = -16,
                Born = now, Life = 1100, Frames = SparkFrames,
            });
        }

        void SpawnZ()
        {
            Point z = CanvasAnchor("zzz");
            particles.Add(new Particle
            {
                X = z.X, Y = z.Y, VX = mirrored ? -4 : 4, VY = -7,
                Born = now, Life = 2600, Frames = ZFrames,
            });
        }

        // ------------------------------------------------------------------ input

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            pressed = true;
            dragging = false;
            pressCursor = Cursor.Position;
            pressAnchor = anchor;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed)
                return;
            Point c = Cursor.Position;
            Size slop = SystemInformation.DragSize;
            if (!dragging && (Math.Abs(c.X - pressCursor.X) > slop.Width || Math.Abs(c.Y - pressCursor.Y) > slop.Height))
            {
                dragging = true;
                sleeping = false;
            }
            if (dragging)
            {
                anchor = Clamp(new Point(pressAnchor.X + c.X - pressCursor.X, pressAnchor.Y + c.Y - pressCursor.Y), false);
                mirrored = FacesLeft(anchor, mirrored);
                Bounds = WindowRect();
                Render(true);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                ShowMenu();
                return;
            }
            if (e.Button != MouseButtons.Left || !pressed)
                return;
            pressed = false;
            if (dragging)
                EndDrag();
            else
                OpenClaudeCode();
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (pressed && dragging && !Capture)
            {
                pressed = false;
                EndDrag();   // capture lost mid-drag (e.g. Alt+Tab): drop her where she is
            }
        }

        void EndDrag()
        {
            dragging = false;
            anchor = Clamp(anchor, true);
            Bounds = WindowRect();
            SavePosition();
            Render(true);
        }

        void OpenClaudeCode()
        {
            now = clock.ElapsedMilliseconds;
            sleeping = false;
            AcknowledgeDone();
            bounceAt = now;
            happyUntil = now + 1400;
            Burst();
            Render(true);
            if (now - lastLaunch < 1500)
                return;   // double clicks open one window, not two
            lastLaunch = now;
            string dir = settings.WorkDir;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Launcher.Launch(dir);
                }
                catch (Exception ex)
                {
                    Log.Write("launch failed: " + ex);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(this, ex.Message, "Claude Pet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            });
        }

        void SavePosition()
        {
            settings.X = anchor.X;
            settings.Y = anchor.Y;
            settings.Scale = scale;
            settings.HasPosition = true;
            settings.Save();
        }

        // ------------------------------------------------------------------ menu

        ContextMenuStrip BuildMenu()
        {
            var strip = new ContextMenuStrip();
            var open = new ToolStripMenuItem("Claude Code öffnen", null, delegate { OpenClaudeCode(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            var folder = new ToolStripMenuItem("Ordner", null, delegate { ChooseFolder(); });
            var size = new ToolStripMenuItem("Größe");
            for (int i = 1; i <= 4; i++)
            {
                int s = i;
                size.DropDownItems.Add(new ToolStripMenuItem(s + "×", null, delegate
                {
                    ApplyScale(s);
                    SavePosition();
                }) { Tag = s });
            }
            var home = new ToolStripMenuItem("Zurück in die Ecke", null, delegate
            {
                anchor = HomeAnchor();
                mirrored = FacesLeft(anchor, false);
                Bounds = WindowRect();
                SavePosition();
                Render(true);
            });
            var autostart = new ToolStripMenuItem("Mit Windows starten", null, delegate
            {
                try { Autostart.Enabled = !Autostart.Enabled; }
                catch (Exception ex) { Log.Write("autostart: " + ex.Message); }
            });
            var quit = new ToolStripMenuItem("Beenden", null, delegate { Close(); });

            strip.Items.AddRange(new ToolStripItem[]
            {
                open, folder, new ToolStripSeparator(), size, home, autostart, new ToolStripSeparator(), quit,
            });
            strip.Opening += delegate
            {
                folder.Text = "Ordner: " + ShortPath(settings.WorkDir) + " …";
                folder.ToolTipText = settings.WorkDir;
                foreach (ToolStripMenuItem item in size.DropDownItems)
                    item.Checked = (int)item.Tag == scale;
                try { autostart.Checked = Autostart.Enabled; }
                catch (Exception) { autostart.Checked = false; }
            };
            strip.Closed += delegate { menuOpen = false; };
            return strip;
        }

        void ShowMenu()
        {
            menuOpen = true;
            menu.Show(Cursor.Position);
        }

        void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "In welchem Ordner soll Claude Code starten?";
                dialog.SelectedPath = settings.WorkDir;
                if (dialog.ShowDialog(this) == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                {
                    settings.WorkDir = dialog.SelectedPath;
                    SavePosition();
                }
            }
        }

        static string ShortPath(string path)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                path = "~" + path.Substring(home.Length);
            return path.Length <= 34 ? path : "…" + path.Substring(path.Length - 33);
        }
    }
}
