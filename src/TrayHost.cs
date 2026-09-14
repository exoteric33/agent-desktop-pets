using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AiPets
{
    /// <summary>One pet process as the tray sees it.</summary>
    sealed class PetProcess
    {
        public readonly PetInfo Info;
        public Process Proc;
        public bool Stopping;
        public long StartedAt, RetryAt, StopDeadline;
        public int Failures;
        public string Problem;

        public PetProcess(PetInfo info)
        {
            Info = info;
        }

        public bool Running
        {
            get { return Proc != null && !Proc.HasExited; }
        }
    }

    /// <summary>
    /// The part that runs in the background: tray icon with menu and settings window. Starts
    /// every enabled pet as its own process (aipets.exe --pet id) and restarts a pet that
    /// crashed or was killed. Pets close themselves when the tray is gone.
    /// </summary>
    sealed class TrayHost : ApplicationContext
    {
        static readonly int[] RetryMs = { 1000, 2000, 5000, 15000, 30000, 60000 };

        public readonly List<PetProcess> Pets = new List<PetProcess>();
        public readonly Icon AppIcon;

        readonly NotifyIcon tray = new NotifyIcon();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly HostWindow window;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Dictionary<string, Image> images = new Dictionary<string, Image>();
        readonly bool dryRun;

        Ini ini = new Ini();
        DateTime iniStamp = DateTime.MinValue;
        SettingsForm settingsForm;
        bool quitting;

        public TrayHost(bool dryRun)
        {
            this.dryRun = dryRun;
            Launcher.DryRun = dryRun;
            foreach (PetInfo info in PetInfo.Discover())
                Pets.Add(new PetProcess(info));
            ReloadIni();

            AppIcon = LoadAppIcon(new Size(32, 32));
            tray.Icon = LoadAppIcon(SystemInformation.SmallIconSize);
            tray.Text = App.Name;
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    OpenSettings(null);
            };
            menu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                FillMenu();
                e.Cancel = false;
            };
            tray.Visible = true;

            window = new HostWindow(this);
            SystemEvents.SessionEnding += OnSessionEnding;
            timer.Interval = 1000;
            timer.Tick += delegate { Supervise(); };
            timer.Start();
            Supervise();
            Log.Write("tray started with " + Pets.Count + " pets" + (dryRun ? " (dry run)" : ""));
            if (Pets.Count == 0)
                tray.ShowBalloonTip(6000, App.Name, "Keine Pets gefunden: " + App.PetsDir + " ist leer.", ToolTipIcon.Warning);
        }

        public Ini Settings
        {
            get { return ini; }
        }

        public PetSettings SettingsOf(PetProcess pet)
        {
            return PetSettings.From(pet.Info, ini);
        }

        // ------------------------------------------------------------------ pet processes

        void ReloadIni()
        {
            iniStamp = Store.Stamp();
            ini = Store.Load();
        }

        void Supervise()
        {
            if (Store.Stamp() != iniStamp)
                ReloadIni();
            long now = clock.ElapsedMilliseconds;
            foreach (PetProcess p in Pets)
            {
                bool wanted = !quitting && p.Info.HasSprites && SettingsOf(p).Enabled;
                if (p.Proc != null && p.Proc.HasExited)
                    Exited(p, now, wanted);
                if (p.Proc == null)
                {
                    if (wanted && now >= p.RetryAt)
                        Start(p, now);
                }
                else if (!wanted && !p.Stopping)
                {
                    Stop(p, now);
                }
                else if (p.Stopping && now > p.StopDeadline)
                {
                    Kill(p);
                }
                else if (p.Failures > 0 && now - p.StartedAt > 60000)
                {
                    p.Failures = 0;   // running steadily again
                    p.Problem = null;
                }
            }
            if (settingsForm != null)
                settingsForm.RefreshStates();
        }

        void Exited(PetProcess p, long now, bool wanted)
        {
            int code = p.Proc.ExitCode;
            p.Proc.Dispose();
            p.Proc = null;
            if (p.Stopping || !wanted)
            {
                p.Stopping = false;
                return;
            }
            if (code == Program.ExitAlreadyRunning)
            {
                // an older instance still holds this pet (it closes once it notices the old tray is gone)
                p.RetryAt = now + 2000;
                return;
            }
            p.Failures = now - p.StartedAt > 60000 ? 1 : p.Failures + 1;
            p.RetryAt = now + RetryMs[Math.Min(p.Failures, RetryMs.Length) - 1];
            p.Problem = "beendet mit Code " + code;
            Log.Write(p.Info.Id + " exited with code " + code + ", restart #" + p.Failures);
            if (p.Failures == 4)
                tray.ShowBalloonTip(6000, App.Name, p.Info.Name + " stürzt immer wieder ab. Details stehen im Log.", ToolTipIcon.Warning);
        }

        void Start(PetProcess p, long now)
        {
            try
            {
                var psi = new ProcessStartInfo(App.ExePath,
                    "--pet " + p.Info.Id + " --host " + Process.GetCurrentProcess().Id + (dryRun ? " --dry-run" : ""));
                psi.UseShellExecute = false;
                psi.WorkingDirectory = App.Dir;
                p.Proc = Process.Start(psi);
                p.StartedAt = now;
                p.Stopping = false;
            }
            catch (Exception ex)
            {
                p.RetryAt = now + 30000;
                p.Problem = ex.Message;
                Log.Write("start " + p.Info.Id + ": " + ex.Message);
            }
        }

        void Stop(PetProcess p, long now)
        {
            p.Stopping = true;
            p.StopDeadline = now + 3000;
            if (!Ipc.ClosePet(p.Info.Id))
                Kill(p);
        }

        static void Kill(PetProcess p)
        {
            try
            {
                if (p.Proc != null && !p.Proc.HasExited)
                    p.Proc.Kill();
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        public string StateText(PetProcess p)
        {
            if (!p.Info.HasSprites)
                return "Sprites fehlen";
            if (!SettingsOf(p).Enabled)
                return "ausgeblendet";
            if (p.Running)
                return p.Stopping ? "wird beendet …" : p.Failures > 0 ? "läuft wieder" : "läuft";
            if (p.Failures > 0)
                return "abgestürzt, startet neu …";
            return p.Problem != null ? "Fehler: " + p.Problem : "startet …";
        }

        // ------------------------------------------------------------------ actions

        public void SetEnabled(PetProcess p, bool enabled)
        {
            Store.Update(p.Info.Id, "enabled", enabled ? null : "0");
            p.Failures = 0;
            p.RetryAt = 0;
            p.Problem = null;
            ReloadIni();
            Supervise();
        }

        /// <summary>ChangeSetting(pet, "args", "--yolo", "shell", null) — null goes back to the pet.ini default.</summary>
        public void ChangeSetting(PetProcess p, params string[] keyValues)
        {
            Store.Update(p.Info.Id, keyValues);
            ReloadIni();
            Ipc.PostToPet(p.Info.Id, Ipc.CmdReload);
        }

        public void SendHome(PetProcess p)
        {
            Store.Update(p.Info.Id, "x", null, "y", null);
            ReloadIni();
            Ipc.PostToPet(p.Info.Id, Ipc.CmdReload);
        }

        /// <summary>A visible pet opens it herself (with her little animation); a hidden one is launched from here.</summary>
        public void Launch(PetProcess p)
        {
            if (p.Running && Ipc.PostToPet(p.Info.Id, Ipc.CmdLaunch))
                return;
            PetInfo info = p.Info;
            PetSettings s = SettingsOf(p);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Launcher.Launch(info, s);
                }
                catch (Exception ex)
                {
                    Log.Write("launch failed: " + ex);
                    window.Post(delegate
                    {
                        MessageBox.Show(ex.Message, info.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
            });
        }

        public void OpenSettings(string petId)
        {
            if (settingsForm == null)
            {
                settingsForm = new SettingsForm(this);
                settingsForm.FormClosed += delegate { settingsForm = null; };
            }
            settingsForm.SelectPet(petId);
            settingsForm.Show();
            if (settingsForm.WindowState == FormWindowState.Minimized)
                settingsForm.WindowState = FormWindowState.Normal;
            settingsForm.Activate();
            Native.SetForegroundWindow(settingsForm.Handle);
        }

        void RunCommand(string text)
        {
            string[] parts = text.Split(new[] { ' ' }, 2);
            string arg = parts.Length > 1 ? parts[1].Trim() : null;
            PetProcess pet = null;
            foreach (PetProcess p in Pets)
                if (string.Equals(p.Info.Id, arg, StringComparison.OrdinalIgnoreCase))
                    pet = p;
            switch (parts[0])
            {
                case "settings":
                    OpenSettings(arg);
                    break;
                case "hide":
                    if (pet != null)
                        SetEnabled(pet, false);
                    break;
                case "quit":
                    Quit();
                    break;
            }
        }

        public void Quit()
        {
            if (quitting)
                return;
            quitting = true;
            timer.Stop();
            Log.Write("tray quits");
            if (settingsForm != null)
                settingsForm.Close();
            foreach (PetProcess p in Pets)
                if (p.Running && !Ipc.ClosePet(p.Info.Id))
                    Kill(p);
            var waited = Stopwatch.StartNew();
            foreach (PetProcess p in Pets)
                if (p.Proc != null && !p.Proc.WaitForExit((int)Math.Max(0, 2500 - waited.ElapsedMilliseconds)))
                    Kill(p);
            ExitThread();
        }

        void OnSessionEnding(object sender, SessionEndingEventArgs e)
        {
            quitting = true;   // Windows closes the pets itself; don't bring them back
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.SessionEnding -= OnSessionEnding;
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            window.DestroyHandle();
            base.ExitThreadCore();
        }

        // ------------------------------------------------------------------ menu and icons

        void FillMenu()
        {
            ReloadIni();
            menu.Items.Clear();
            var prefs = new ToolStripMenuItem("Einstellungen …", null, delegate { OpenSettings(null); });
            prefs.Font = new Font(prefs.Font, FontStyle.Bold);
            menu.Items.Add(prefs);
            menu.Items.Add(new ToolStripSeparator());
            foreach (PetProcess pet in Pets)
            {
                PetProcess p = pet;
                bool enabled = SettingsOf(p).Enabled;
                var item = new ToolStripMenuItem(p.Info.Name, PetImage(p.Info, 16));
                var show = new ToolStripMenuItem("Anzeigen", null, delegate { SetEnabled(p, !SettingsOf(p).Enabled); });
                show.Checked = enabled;
                var home = new ToolStripMenuItem("Zurück in die Ecke", null, delegate { SendHome(p); });
                home.Enabled = enabled;
                item.DropDownItems.AddRange(new ToolStripItem[]
                {
                    show, new ToolStripMenuItem(p.Info.OpenText, null, delegate { Launch(p); }), home,
                });
                if (!enabled)
                    item.ForeColor = SystemColors.GrayText;
                menu.Items.Add(item);
            }
            if (Pets.Count > 0)
                menu.Items.Add(new ToolStripSeparator());
            var autostart = new ToolStripMenuItem("Mit Windows starten", null, delegate
            {
                try { Autostart.Enabled = !Autostart.Enabled; }
                catch (Exception ex) { Log.Write("autostart: " + ex.Message); }
            });
            try { autostart.Checked = Autostart.Enabled; }
            catch (Exception) { autostart.Checked = false; }
            menu.Items.Add(autostart);
            menu.Items.Add(new ToolStripMenuItem("Beenden", null, delegate { Quit(); }));
        }

        public Image PetImage(PetInfo pet, int size)
        {
            string key = pet.Id + "/" + size;
            Image image;
            if (images.TryGetValue(key, out image))
                return image;
            try
            {
                using (var icon = new Icon(pet.IconPath, size, size))
                    image = icon.ToBitmap();
            }
            catch (Exception)
            {
                image = null;   // no icon.ico yet: menus and lists simply show none
            }
            images[key] = image;
            return image;
        }

        public static Icon LoadAppIcon(Size size)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("aipets.ico"))
                return s != null ? new Icon(s, size) : SystemIcons.Application;
        }

        /// <summary>Hidden window "aipets.host" that receives the pets' WM_COPYDATA commands.</summary>
        sealed class HostWindow : NativeWindow
        {
            const int WM_RUN = Native.WM_APP + 1;

            readonly TrayHost host;
            readonly Queue<MethodInvoker> posted = new Queue<MethodInvoker>();

            public HostWindow(TrayHost host)
            {
                this.host = host;
                CreateHandle(new CreateParams { Caption = Ipc.HostTitle, Style = Native.WS_POPUP, ExStyle = Native.WS_EX_TOOLWINDOW });
            }

            /// <summary>Run on the tray's UI thread (callable from any thread).</summary>
            public void Post(MethodInvoker action)
            {
                lock (posted)
                    posted.Enqueue(action);
                Native.PostMessage(Handle, WM_RUN, IntPtr.Zero, IntPtr.Zero);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == Native.WM_COPYDATA)
                {
                    // answer right away; a dialog opened here would block the sending pet
                    string text = Ipc.ReadCopyData(m.LParam);
                    Post(delegate { host.RunCommand(text); });
                    m.Result = (IntPtr)1;
                    return;
                }
                if (m.Msg == WM_RUN)
                {
                    while (true)
                    {
                        MethodInvoker action;
                        lock (posted)
                        {
                            if (posted.Count == 0)
                                break;
                            action = posted.Dequeue();
                        }
                        action();
                    }
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
