using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AiPets
{
    /// <summary>
    /// One pet on the desktop. What she can do comes from her atlas: faces (normal, blink, happy
    /// and optionally hover, look, sleep, drag), idle phases, bounce frames, particle anims.
    /// </summary>
    sealed class PetForm : Form
    {
        // room around the character cell for the speech bubble and particles (cell pixels)
        const int PadX = 26, PadTop = 20;
        const int IdleStepMs = 260, SleepStepMs = 620;
        const int SleepAfterMs = 60 * 1000;
        const int BubbleDelayMs = 120;

        static readonly int[] BounceSteps = { -1, -2, -1, 1, 2, 3, 3, 2, 1 };
        static readonly int[] BounceMs = { 40, 60, 40, 40, 40, 60, 60, 50, 50 };
        static readonly string[] SparkFrames = { "spark1", "spark2", "spark3", "spark2", "spark1", "spark0" };
        static readonly string[] ZFrames = { "z0", "z1", "z2" };
        static readonly string[] PulseSpinner = { "spin0", "spin1", "spin2", "spin3", "spin4", "spin3", "spin2", "spin1" };

        sealed class Particle
        {
            public double X, Y, VX, VY, Gravity;
            public long Born;
            public int Life;
            public string[] Frames;
        }

        readonly PetInfo pet;
        readonly Process host;          // the tray that started this pet; null when run on its own
        readonly Atlas atlas;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random rng = new Random();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<string> layerOrder = new List<string>();   // all pet ids in menu order: the first one stands in front
        readonly List<int> ownProcesses = new List<int>();       // this pet and the tray: their menus stay above every pet
        readonly int layerRank;

        // what this atlas offers
        readonly bool canBounce, hasHover, hasLook, hasSleep, hasDrag;
        readonly string[] burstFrames, zFrames, spinFrames;
        readonly List<string[]> twinkles;

        PetSettings settings;
        DateTime settingsStamp;
        Native.LayeredSurface surface;
        Graphics gfx;
        int height;                 // how tall the figure is on screen, whatever her sprite height
        double zoom;                // screen pixels per sprite pixel at that height (rarely a whole number)
        Point anchor;               // bottom-centre of the pet on screen
        bool mirrored;
        string lastRender;

        long now;
        int phase;
        long phaseAt, blinkUntil, nextBlink, secondBlinkAt = -1, happyUntil, smileUntil, lookUntil, bounceAt = -1;
        long nextIdleAction, nextZ, hoverSince, nextFullscreenCheck, nextHousekeeping;
        long nextRestack;           // unix ms (Layers.NextCheck): the pets take turns
        long hintUntil = 2800;      // show the prompt bubble once after start
        long lastLaunch = long.MinValue / 2;
        bool sleeping, hovered, menuOpen, hiddenForFullscreen;
        bool pressed, dragging;
        bool restacking;
        Point pressCursor, pressAnchor;

        // agent activity reported by hooks (see Status.cs); done times are unix ms
        readonly StatusMonitor status;
        AgentState agent;
        long nextStatusScan, nextStatusFx;
        long doneShownSince, doneInputSince, doneAcknowledged = StatusEntry.UnixNow();

        public PetForm(PetInfo pet, Process host)
        {
            this.pet = pet;
            this.host = host;
            settingsStamp = Store.Stamp();
            Ini initial;
            if (!Store.TryLoad(out initial))
                throw new IOException("Die Pet-Einstellungen sind momentan nicht lesbar.");
            settings = PetSettings.From(pet, initial);
            atlas = Atlas.Load(pet.SpritesDir);
            status = pet.Status.Length > 0 ? new StatusMonitor(pet.Status) : null;

            canBounce = atlas.HasFrame("bounce_1");
            hasHover = atlas.HasFrame("hover_0");
            hasLook = atlas.HasFrame("look_0");
            hasSleep = atlas.HasFrame("sleep_0");
            hasDrag = atlas.HasFrame("drag_0");
            burstFrames = atlas.Anim("burst", SparkFrames);
            zFrames = atlas.Anim("z", ZFrames);
            spinFrames = atlas.Anim("spin", PulseSpinner);
            twinkles = atlas.AnimsWithPrefix("twinkle");
            if (twinkles.Count == 0)
                twinkles.Add(SparkFrames);
            foreach (PetInfo other in PetInfo.Discover())
                layerOrder.Add(other.Id);
            layerRank = layerOrder.IndexOf(pet.Id);
            ownProcesses.Add(Process.GetCurrentProcess().Id);
            if (host != null)
                ownProcesses.Add(host.Id);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = Ipc.PetTitle(pet.Id);
            Cursor = Cursors.Hand;

            var saved = new Point(settings.X, settings.Y);
            SetHeight(FitScreen(settings.HasPosition ? saved : Screen.PrimaryScreen.WorkingArea.Location, settings.PetHeight()));
            anchor = settings.HasPosition ? Clamp(saved, true) : HomeAnchor();
            mirrored = WantsMirror(anchor, false);
            menu = BuildMenu();
            Log.Write("start at " + anchor.X + "," + anchor.Y + " (home " + HomeAnchor().X + ", height " + height
                + ", zoom " + Math.Round(zoom, 3).ToString(CultureInfo.InvariantCulture)
                + ", work area " + Screen.PrimaryScreen.WorkingArea + ")");

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
            if (m.Msg == Ipc.CommandMessage)
            {
                if ((int)m.WParam == Ipc.CmdReload)
                    ReloadSettings();
                else if ((int)m.WParam == Ipc.CmdLaunch)
                    OpenAgent();
                return;
            }
            base.WndProc(ref m);
            // Windows moved her in the z-order, e.g. to the top together with her menu or a dialog: back into her layer at once
            if (m.Msg == Native.WM_WINDOWPOSCHANGED && !restacking && !dragging && !hiddenForFullscreen && Native.ZOrderChanged(m.LParam))
                Restack();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyHeight(height);
            Restack();   // a new window starts on top of everything: into her layer before she shows up
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

        /// <summary>Screen pixels for a length in cell pixels.</summary>
        int Px(double cellPixels)
        {
            return (int)Math.Round(cellPixels * zoom);
        }

        // the cell and the room around it, each rounded once, so the figure ends exactly at the window's bottom edge
        Rectangle CellPx { get { return new Rectangle(Px(PadX), Px(PadTop), Px(atlas.CellW), Px(atlas.CellH)); } }
        int CanvasPxW { get { return Px(atlas.CellW) + 2 * Px(PadX); } }
        int CanvasPxH { get { return Px(atlas.CellH) + Px(PadTop); } }

        /// <summary>She currently looks to the left (art direction combined with mirroring).</summary>
        bool LooksLeft { get { return atlas.FacingLeft != mirrored; } }

        Rectangle WindowRect()
        {
            int w = CanvasPxW, h = CanvasPxH;
            return new Rectangle(anchor.X - w / 2, anchor.Y - h, w, h);
        }

        /// <summary>
        /// "Back to the corner": pet.ini home is the gap from the screen's right edge to the cell's, in pixels
        /// at 162 px height. It grows with the height of all pets only, so the places stay put when one pet has her own.
        /// </summary>
        Point HomeAnchor()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int gap = (int)Math.Round((double)pet.Home * settings.SharedHeight() / PetSettings.HeightUnit);
            return new Point(wa.Right - Px(atlas.CellW) / 2 - gap, wa.Bottom);
        }

        Point Clamp(Point p, bool snapToTaskbar)
        {
            Rectangle vs = SystemInformation.VirtualScreen;
            int half = Px(atlas.CellW / 2 - 6);
            p.X = Math.Max(vs.Left + half, Math.Min(vs.Right - half, p.X));
            Rectangle wa = Screen.FromPoint(new Point(p.X, Math.Min(p.Y, vs.Bottom - 1))).WorkingArea;
            int minY = vs.Top + Px(atlas.CellH - 4);   // keep the top of her head on screen
            p.Y = Math.Max(minY, Math.Min(wa.Bottom, p.Y));
            if (snapToTaskbar && wa.Bottom - p.Y < Px(18))
                p.Y = wa.Bottom;   // she sits / stands on the taskbar edge
            return p;
        }

        /// <summary>Mirror the frames so she looks towards the middle of the screen she is on.</summary>
        bool WantsMirror(Point p, bool currentlyMirrored)
        {
            Rectangle wa = Screen.FromPoint(p).WorkingArea;
            int mid = wa.Left + wa.Width / 2;
            int hysteresis = Px(12);
            bool lookLeft = (atlas.FacingLeft != currentlyMirrored)
                ? p.X > mid - hysteresis
                : p.X > mid + hysteresis;
            return lookLeft != atlas.FacingLeft;
        }

        /// <summary>Height and zoom: the figure is px screen pixels tall, whatever her sprite height.</summary>
        void SetHeight(int px)
        {
            height = px;
            zoom = (double)px / atlas.FigureHeight;
        }

        /// <summary>A height that fits the work area of the screen at p: a pet can grow up to the full screen height.</summary>
        public static int FitScreen(Point p, int px)
        {
            return Math.Max(1, Math.Min(px, Screen.FromPoint(p).WorkingArea.Height));
        }

        int ScreenHeight()
        {
            return Screen.FromPoint(anchor).WorkingArea.Height;
        }

        void ApplyHeight(int px)
        {
            SetHeight(px);
            anchor = Clamp(anchor, false);
            if (gfx != null) gfx.Dispose();
            if (surface != null) surface.Dispose();
            surface = new Native.LayeredSurface(CanvasPxW, CanvasPxH);
            gfx = Graphics.FromImage(surface.Bitmap);
            PixelArtMode(gfx);
            Bounds = WindowRect();
            lastRender = null;
            Render(true);
        }

        void MoveTo(Point p, bool snap)
        {
            anchor = Clamp(p, snap);
            mirrored = WantsMirror(anchor, mirrored);
            Bounds = WindowRect();
            Render(true);
        }

        void OnDisplayChanged(object sender, EventArgs e)
        {
            BeginInvoke((MethodInvoker)delegate
            {
                FitHeight();   // the screen may be lower now
                MoveTo(settings.HasPosition ? anchor : HomeAnchor(), true);
            });
        }

        /// <summary>After a move or a display change: the height she asks for, as far as her screen allows.</summary>
        void FitHeight()
        {
            int fitted = FitScreen(anchor, settings.PetHeight());
            if (fitted != height)
                ApplyHeight(fitted);
        }

        /// <summary>settings.ini changed (tray settings window, "back to the corner", another size).</summary>
        void ReloadSettings()
        {
            DateTime stamp = Store.Stamp();
            Ini loaded;
            if (!Store.TryLoad(out loaded))
                return;
            settingsStamp = stamp;
            PetSettings s = PetSettings.From(pet, loaded);
            int newHeight = FitScreen(anchor, s.PetHeight());
            settings = s;
            if (newHeight != height)
            {
                ApplyHeight(newHeight);
                if (!s.HasPosition)
                    MoveTo(HomeAnchor(), true);
                SavePosition();
                return;
            }
            if (dragging)
                return;
            if (!s.HasPosition)
            {
                MoveTo(HomeAnchor(), true);
                SavePosition();
            }
            else if (s.X != anchor.X || s.Y != anchor.Y)
            {
                MoveTo(new Point(s.X, s.Y), true);
            }
        }

        // ------------------------------------------------------------------ animation

        void Tick()
        {
            now = clock.ElapsedMilliseconds;
            if (now >= nextHousekeeping)
            {
                nextHousekeeping = now + 1000;
                if (host != null && host.HasExited)
                {
                    Close();   // the tray is gone (quit or killed): don't linger without it
                    return;
                }
                if (Store.Stamp() != settingsStamp)
                    ReloadSettings();
            }
            if (status != null && now >= nextStatusScan)
            {
                UpdateAgentState();
                nextStatusScan = now + 500;
            }
            UpdateHover();
            UpdateSleep();

            if (now - phaseAt >= (sleeping ? SleepStepMs : IdleStepMs))
            {
                phase = (phase + 1) % atlas.Phases;
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
                if (agent == AgentState.Working)
                {
                    SpawnTwinkle();   // thinking sparkles / music while the agent works
                    nextStatusFx = now + 2600;
                }
                else if (agent == AgentState.Waiting)
                {
                    CallForAttention();
                    nextStatusFx = now + 3500;
                }
            }
            if (now >= nextFullscreenCheck)
            {
                // a fullscreen video, game or presentation: step aside instead of floating over it
                hiddenForFullscreen = !dragging && Native.FullscreenAppActive(Handle);
                nextFullscreenCheck = now + 1000;
            }
            long wall = StatusEntry.UnixNow();
            if (!dragging && !hiddenForFullscreen && wall >= nextRestack)
            {
                nextRestack = Layers.NextCheck(wall, layerRank);
                Restack();
            }
            particles.RemoveAll(p => now - p.Born >= p.Life);
            Render(false);

            // fast ticks only while something moves quickly; idling stays cheap
            int interval = particles.Count > 0 || bounceAt >= 0 || pressed ? 33 : agent == AgentState.Working ? 55 : 80;
            if (timer.Interval != interval)
                timer.Interval = interval;
        }

        /// <summary>
        /// Her fixed layer: above other programs' topmost windows, below the pets before her in the menu
        /// and below every menu and dialog of aipets (see Layers).
        /// </summary>
        void Restack()
        {
            restacking = true;   // her own move is no reason to restack again
            try
            {
                if (!Layers.Restack(Handle, pet.Id, layerOrder, ownProcesses))
                    nextRestack = StatusEntry.UnixNow() + Layers.RetryMs;   // windows were still moving: again shortly
            }
            finally
            {
                restacking = false;
            }
        }

        void UpdateHover()
        {
            Rectangle win = WindowRect();
            Point c = Cursor.Position;
            bool over = false;
            if (win.Contains(c) && !hiddenForFullscreen)
            {
                // back to cell pixels, the way Compose stretches the cell
                Rectangle cell = CellPx;
                int cx = (int)Math.Floor((c.X - win.Left - cell.X) * (double)atlas.CellW / cell.Width);
                int cy = (int)Math.Floor((c.Y - win.Top - cell.Y) * (double)atlas.CellH / cell.Height);
                over = atlas.HitCharacter(cx, cy, mirrored) || (CurrentBubble() != null && BubbleRect().Contains(cx + PadX, cy + PadTop));
            }
            if (over && !hovered)
                hoverSince = now;
            hovered = over;
        }

        /// <summary>Spinner while the agent works, "?" while it waits for you, a check mark when it is done.</summary>
        void UpdateAgentState()
        {
            AgentState previous = agent;
            status.Scan();
            agent = status.State;
            if (agent == AgentState.Waiting && previous != AgentState.Waiting)
            {
                CallForAttention();
                nextStatusFx = now + 3500;
            }

            long wall = StatusEntry.UnixNow();
            if (status.LatestDone > doneAcknowledged && status.LatestDone > doneShownSince)
            {
                doneShownSince = status.LatestDone;
                doneInputSince = 0;
                if (agent == AgentState.Idle)
                    Cheer();
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
            bool agentNeedsHer = agent != AgentState.Idle || doneShownSince > 0;
            if (!sleeping && idle > SleepAfterMs && !hovered && !dragging && !menuOpen && !agentNeedsHer)
            {
                sleeping = true;
                nextZ = now + 500;
            }
            else if (sleeping && (idle < 1000 || hovered || agentNeedsHer))
            {
                sleeping = false;
                happyUntil = now + 900;
                particles.RemoveAll(p => p.Frames == zFrames);
            }
        }

        void IdleAction()
        {
            // smile 35, twinkle 30, hop 15 (if she can), look up 30 (if she can)
            int roll = rng.Next(65 + (canBounce ? 15 : 0) + (hasLook ? 30 : 0));
            if (roll < 35)
                smileUntil = now + 1600;
            else if (roll < 65)
                SpawnTwinkle();
            else if (canBounce && roll < 80)
                bounceAt = now;
            else
                lookUntil = now + 1400 + rng.Next(1200);
        }

        /// <summary>The agent needs you: hop, or (a pet without a body animation) look up with a twinkle.</summary>
        void CallForAttention()
        {
            if (canBounce)
            {
                bounceAt = now;
                return;
            }
            lookUntil = now + 1500;
            SpawnTwinkle();
        }

        void Cheer()
        {
            if (canBounce)
                bounceAt = now;
            else
                happyUntil = now + 1400;
        }

        string FaceName()
        {
            if (dragging)
                return hasDrag ? "drag" : "happy";
            if (sleeping)
                return hasSleep ? "sleep" : "blink";
            if (now < happyUntil)
                return "happy";
            bool done = doneShownSince > 0 && agent == AgentState.Idle;
            if (hovered || done || now < smileUntil)
                return hasHover ? (now < blinkUntil ? "blink" : "hover") : "happy";
            if (now < blinkUntil)
                return "blink";
            if (now < lookUntil && hasLook)
                return "look";
            return "normal";
        }

        string FrameName()
        {
            if (bounceAt >= 0)
            {
                long t = now - bounceAt;
                for (int i = 0; i < BounceSteps.Length && canBounce; i++)
                {
                    if (t < BounceMs[i])
                        return "bounce_" + BounceSteps[i];
                    t -= BounceMs[i];
                }
                bounceAt = -1;
            }
            string face = FaceName();
            string name = face + "_" + phase;
            return atlas.HasFrame(name) ? name : face + "_0";
        }

        /// <summary>Bubble sprite to show, or null. The prompt on hover wins over the agent's state.</summary>
        string CurrentBubble()
        {
            if (dragging || sleeping)
                return null;
            string bubble = "bubble_" + (LooksLeft ? "l_" : "r_");
            if (hovered && now - hoverSince >= BubbleDelayMs || now < hintUntil)
                return bubble + ((now / 530) % 2 == 0 ? "on" : "off");
            if (agent == AgentState.Waiting)
                return bubble + "wait";
            if (agent == AgentState.Working)
                return bubble + spinFrames[(now / 110) % spinFrames.Length];
            if (doneShownSince > 0)
                return bubble + "done";
            return null;
        }

        /// <summary>Bubble bounds in canvas pixels.</summary>
        Rectangle BubbleRect()
        {
            Point tip = atlas.Anchor("bubble", mirrored);
            Size size = atlas.SpriteSize("bubble_r_on");
            int left = LooksLeft ? PadX + tip.X - (size.Width - 1) : PadX + tip.X;
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
            atlas.DrawFrame(g, frame, CellPx);
            if (bubble != null)
            {
                Point tip = atlas.Anchor("bubble", mirrored);
                atlas.DrawSprite(g, bubble, Px(PadX + tip.X), Px(PadTop + tip.Y), zoom);
            }
            foreach (Particle p in particles)
            {
                double t = (now - p.Born) / 1000.0;
                double f = (now - p.Born) / (double)p.Life;
                string sprite = p.Frames[Math.Min(p.Frames.Length - 1, (int)(f * p.Frames.Length))];
                int x = (int)Math.Round(p.X + p.VX * t);
                int y = (int)Math.Round(p.Y + p.VY * t + 0.5 * p.Gravity * t * t);
                atlas.DrawSprite(g, sprite, Px(x), Px(y), zoom);
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
        /// Art check without touching the desktop: renders idle, hover, sleep, click, working,
        /// waiting and done (both looking directions) through the real drawing code into PNGs.
        /// </summary>
        public static void Snapshot(PetInfo info, string dir)
        {
            Directory.CreateDirectory(dir);
            using (var pet = new PetForm(info, null))
            {
                pet.zoom = 3;   // whole cell pixels, for checking the art
                foreach (bool mirror in new[] { false, true })
                {
                    pet.mirrored = mirror;
                    string side = pet.LooksLeft ? "_left" : "_right";
                    string prefix = Path.Combine(dir, info.Id + "_");
                    pet.SnapshotState(prefix + "idle" + side, delegate { });
                    pet.SnapshotState(prefix + "hover" + side, delegate
                    {
                        pet.hovered = true;
                        pet.hoverSince = pet.now - 1000;
                    });
                    pet.SnapshotState(prefix + "look" + side, delegate { pet.lookUntil = pet.now + 1000; });
                    pet.SnapshotState(prefix + "sleep" + side, delegate
                    {
                        pet.sleeping = true;
                        for (int i = 0; i < 3; i++)
                        {
                            pet.SpawnZ();
                            pet.now += 800;
                        }
                    });
                    pet.SnapshotState(prefix + "click" + side, delegate
                    {
                        pet.bounceAt = pet.now;
                        pet.happyUntil = pet.now + 1400;
                        pet.Burst();
                        pet.now += 260;
                    });
                    pet.SnapshotState(prefix + "working" + side, delegate
                    {
                        pet.agent = AgentState.Working;
                        pet.now = 110 * 196 - 360;
                        pet.SpawnTwinkle();
                        pet.now = 110 * 196;   // biggest frame of the pulse spinner
                    });
                    pet.SnapshotState(prefix + "waiting" + side, delegate { pet.agent = AgentState.Waiting; });
                    pet.SnapshotState(prefix + "done" + side, delegate { pet.doneShownSince = 1; });
                }
            }
        }

        /// <summary>
        /// Size check: every pet idle at 324 px (without her own height), side by side on one baseline.
        /// All heads should touch the line 324 px above the ground line.
        /// </summary>
        public static void SnapshotLineup(List<PetInfo> infos, string path)
        {
            var pets = new List<PetForm>();
            try
            {
                int width = 0, height = 0;
                foreach (PetInfo info in infos)
                {
                    if (!info.HasSprites)
                        continue;
                    var pet = new PetForm(info, null);
                    pets.Add(pet);
                    pet.SetHeight(2 * PetSettings.HeightUnit);
                    pet.mirrored = false;
                    width += pet.CanvasPxW;
                    height = Math.Max(height, pet.CanvasPxH);
                }
                using (var bmp = new Bitmap(Math.Max(1, width), height + 2, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(58, 74, 92));
                    int x = 0;
                    foreach (PetForm pet in pets)
                    {
                        pet.ResetState();
                        GraphicsState state = g.Save();
                        g.TranslateTransform(x, height - pet.CanvasPxH);
                        PixelArtMode(g);
                        pet.Compose(g, pet.FrameName(), null);
                        g.Restore(state);
                        x += pet.CanvasPxW;
                    }
                    using (var pen = new Pen(Color.FromArgb(255, 110, 110)))
                    {
                        g.DrawLine(pen, 0, height, width, height);
                        int top = height - 2 * PetSettings.HeightUnit - 1;
                        g.DrawLine(pen, 0, top, width, top);
                    }
                    bmp.Save(path);
                }
            }
            finally
            {
                foreach (PetForm pet in pets)
                    pet.Dispose();
            }
        }

        void ResetState()
        {
            now = 21200;   // bubble cursor phase: visible
            hovered = sleeping = false;
            bounceAt = -1;
            happyUntil = smileUntil = lookUntil = blinkUntil = 0;
            phase = 0;
            agent = AgentState.Idle;
            doneShownSince = 0;
            particles.Clear();
        }

        void SnapshotState(string path, Action setup)
        {
            ResetState();
            setup();
            using (var bmp = new Bitmap(CanvasPxW, CanvasPxH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(58, 74, 92));
                PixelArtMode(g);
                Compose(g, (mirrored ? "m_" : "") + FrameName(), CurrentBubble());
                bmp.Save(path + ".png");
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
                    X = head.X, Y = head.Y - 16,   // above the hair, where the sparks stand out
                    VX = Math.Cos(angle) * speed, VY = Math.Sin(angle) * speed - 22,
                    Gravity = 40, Born = now, Life = 620 + rng.Next(160), Frames = burstFrames,
                });
            }
        }

        void SpawnTwinkle()
        {
            Point logo = CanvasAnchor("logo");
            particles.Add(new Particle
            {
                X = logo.X, Y = logo.Y, VX = (rng.NextDouble() - 0.5) * 6, VY = -16,
                Born = now, Life = 1100, Frames = twinkles[rng.Next(twinkles.Count)],
            });
        }

        void SpawnZ()
        {
            Point z = CanvasAnchor("zzz");
            particles.Add(new Particle
            {
                X = z.X, Y = z.Y, VX = LooksLeft ? -4 : 4, VY = -7,
                Born = now, Life = 2600, Frames = zFrames,
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
                MoveTo(new Point(pressAnchor.X + c.X - pressCursor.X, pressAnchor.Y + c.Y - pressCursor.Y), false);
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
                OpenAgent();
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
            MoveTo(anchor, true);
            int before = height;
            FitHeight();   // on another screen she may have to be smaller, or may grow back
            if (height != before)
                MoveTo(anchor, true);
            SavePosition();
        }

        void OpenAgent()
        {
            now = clock.ElapsedMilliseconds;
            sleeping = false;
            AcknowledgeDone();
            if (canBounce)
                bounceAt = now;
            happyUntil = now + 1400;
            Burst();
            Render(true);
            if (now - lastLaunch < 1500)
                return;   // double clicks open one window, not two
            lastLaunch = now;
            PetSettings s = ReadSettings();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Launcher.Launch(pet, s);
                }
                catch (Exception ex)
                {
                    Log.Write("launch failed: " + ex);
                    BeginInvoke((MethodInvoker)delegate
                    {
                        MessageBox.Show(this, ex.Message, pet.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            });
        }

        /// <summary>Only the position: heights are written where they change.</summary>
        void SavePosition()
        {
            Store.Update(pet.Id, "x", PetSettings.Number(anchor.X), "y", PetSettings.Number(anchor.Y));
            settings.HasPosition = true;
            settings.X = anchor.X;
            settings.Y = anchor.Y;
            // Do not acknowledge unrelated changes that were saved before this position write.
        }

        PetSettings ReadSettings()
        {
            Ini loaded;
            return Store.TryLoad(out loaded) ? PetSettings.From(pet, loaded) : settings;
        }

        // ------------------------------------------------------------------ menu

        ContextMenuStrip BuildMenu()
        {
            var strip = new ContextMenuStrip();
            var open = new ToolStripMenuItem(pet.OpenText, null, delegate { OpenAgent(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            var folder = new ToolStripMenuItem("Ordner", null, delegate { ChooseFolder(); });
            var opens = new ToolStripMenuItem("Klick öffnet");
            var openProgram = new ToolStripMenuItem("Programm", null, delegate { SetMode("program"); });
            var openApp = new ToolStripMenuItem("Desktop-App", null, delegate { SetMode("app"); });
            var openWebsite = new ToolStripMenuItem("Website", null, delegate { SetMode("website"); });
            opens.DropDownItems.AddRange(new ToolStripItem[] { openProgram, openApp, openWebsite });
            // heights of all pets (Tag = px), then this pet's own, each group under a grey heading
            var sizes = new ToolStripMenuItem("Größe");
            var shared = new List<ToolStripMenuItem>();
            for (int i = 1; i <= 4; i++)
            {
                int px = i * PetSettings.HeightUnit;
                shared.Add(new ToolStripMenuItem(PetSettings.HeightText(px), null, delegate { SetSharedHeight(px); }) { Tag = px });
            }
            var likeAll = new ToolStripMenuItem("Wie alle", null, delegate { SetOwnHeight(0); });
            var smaller = new ToolStripMenuItem("Kleiner", null, delegate { SetOwnHeight(height * 4 / 5); });
            var bigger = new ToolStripMenuItem("Größer", null, delegate { SetOwnHeight(height * 5 / 4); });
            var screenHigh = new ToolStripMenuItem("So hoch wie der Bildschirm", null, delegate { SetOwnHeight(ScreenHeight()); });
            sizes.DropDownItems.Add(new ToolStripMenuItem("Alle Pets") { Enabled = false });
            sizes.DropDownItems.AddRange(shared.ToArray());
            sizes.DropDownItems.Add(new ToolStripSeparator());
            sizes.DropDownItems.Add(new ToolStripMenuItem("Nur " + pet.Name) { Enabled = false });
            sizes.DropDownItems.AddRange(new ToolStripItem[] { likeAll, smaller, bigger, screenHigh });
            var home = new ToolStripMenuItem("Zurück in die Ecke", null, delegate
            {
                MoveTo(HomeAnchor(), true);
                SavePosition();
            });
            var hide = new ToolStripMenuItem("Ausblenden", null, delegate
            {
                // saved here as well: she stays hidden after a restart even if the tray does not answer now
                Store.Update(pet.Id, "enabled", "0");
                if (!Ipc.SendToHost("hide " + pet.Id))
                    Close();
            });
            var prefs = new ToolStripMenuItem("Einstellungen …", null, delegate { Ipc.SendToHost("settings " + pet.Id); });
            var quit = new ToolStripMenuItem("aipets beenden", null, delegate
            {
                if (!Ipc.SendToHost("quit"))
                    Close();
            });

            strip.Items.AddRange(new ToolStripItem[]
            {
                open, folder, opens, new ToolStripSeparator(), sizes, home, hide, new ToolStripSeparator(), prefs, quit,
            });
            strip.Opening += delegate
            {
                PetSettings s = ReadSettings();
                folder.Text = "Ordner: " + ShortPath(s.WorkDir) + " …";
                folder.ToolTipText = s.WorkDir;
                // only the terminal and an app opened by the program (codex app) use the working folder
                folder.Visible = s.OpensProgram || (s.OpensApp && Launcher.AppViaProgram(pet, s));
                openProgram.Checked = s.OpensProgram;
                openApp.Checked = s.OpensApp;
                openWebsite.Checked = s.OpensWebsite;
                openProgram.ToolTipText = s.Program;
                openApp.Visible = s.DesktopApp.Length > 0;   // only pets that know a desktop app
                openApp.ToolTipText = AppText(s);
                openWebsite.ToolTipText = s.Url;
                foreach (ToolStripMenuItem entry in shared)
                    entry.Checked = (int)entry.Tag == s.SharedHeight();
                likeAll.Checked = s.OwnHeight == 0;
                smaller.Enabled = height > PetSettings.MinHeight;
                bigger.Enabled = height < ScreenHeight();
                screenHigh.Checked = s.OwnHeight > 0 && height == ScreenHeight();
                prefs.Visible = host != null;
            };
            strip.Closed += delegate { menuOpen = false; };
            return strip;
        }

        void ShowMenu()
        {
            menuOpen = true;
            menu.Show(Cursor.Position);
        }

        /// <summary>The height of all pets from the menu, like the first slider in the settings: every pet snaps to it.</summary>
        void SetSharedHeight(int px)
        {
            Store.Change(delegate(Ini ini) { PetSettings.ShareHeight(ini, px); });
            TakeOwnChange();
            foreach (PetInfo other in PetInfo.Discover())
                if (other.Id != pet.Id)
                    Ipc.PostToPet(other.Id, Ipc.CmdReload);
        }

        /// <summary>This pet's own height (0: the one of all pets again), like the second slider.</summary>
        void SetOwnHeight(int px)
        {
            px = px <= 0 ? 0 : Math.Max(PetSettings.MinHeight, Math.Min(ScreenHeight(), px));
            Store.Update(pet.Id, "height", px == 0 ? null : PetSettings.Number(px));
            TakeOwnChange();
        }

        /// <summary>A height this pet wrote itself: apply it now instead of waiting for the file stamp.</summary>
        void TakeOwnChange()
        {
            DateTime stamp = Store.Stamp();
            Ini loaded;
            if (!Store.TryLoad(out loaded))
                return;
            settingsStamp = stamp;
            settings = PetSettings.From(pet, loaded);
            FitHeight();
            SavePosition();
        }

        /// <summary>"program", "app" or "website"; the pet.ini default is stored as no override.</summary>
        void SetMode(string mode)
        {
            Store.Update(pet.Id, "mode", mode == pet.Mode ? null : mode);
        }

        /// <summary>Tooltip of "Desktop-App": which app a click opens and how, or what happens without one.</summary>
        string AppText(PetSettings s)
        {
            if (s.DesktopApp.Length == 0)
                return null;
            DesktopApp app = DesktopApp.Find(s.DesktopApp);
            string text = app != null ? app.ToString() : "nicht gefunden";
            if (Launcher.AppViaProgram(pet, s))
                return text + ", über „" + DesktopApp.ProgramCommand(s, pet.AppCommand) + "“";
            if (app == null && pet.AppFallback.Length > 0)
                return text + ", ein Klick startet „" + DesktopApp.ProgramCommand(s, pet.AppFallback) + "“";
            return text;
        }

        void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "In welchem Ordner soll " + pet.Name + " starten?";
                dialog.SelectedPath = ReadSettings().WorkDir;
                if (dialog.ShowDialog(this) == DialogResult.OK && Directory.Exists(dialog.SelectedPath))
                {
                    Store.Update(pet.Id, "workdir", dialog.SelectedPath);
                }
            }
        }

        public static string ShortPath(string path)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                path = "~" + path.Substring(home.Length);
            return path.Length <= 34 ? path : "…" + path.Substring(path.Length - 33);
        }
    }
}
