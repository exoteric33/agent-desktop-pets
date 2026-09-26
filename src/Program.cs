using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace AiPets
{
    /// <summary>
    /// aipets.exe                          tray (starts and watches the pets)
    /// aipets.exe --pet &lt;id&gt; [--host pid]   one pet window
    /// aipets.exe --hook &lt;source&gt; &lt;event&gt;  agent hook: record the state and exit
    /// aipets.exe --hook cursor            Cursor hook, the event comes with the payload
    /// aipets.exe (started by Cursor)      also a Cursor hook: CURSOR_VERSION is set and the payload is piped in
    /// aipets.exe --snapshot &lt;dir&gt; [--pet id]  render pets, all pets side by side and the settings window (that pet's page, once per mode) into PNGs
    /// aipets.exe --status &lt;source&gt;        print the folded agent state (diagnostics)
    /// aipets.exe --command &lt;id&gt; [--mode m] print what a click on the pet would start (diagnostics; m = program, app, website)
    /// aipets.exe --install [--quiet]      autostart + status hooks of the installed agents, then start the tray
    /// aipets.exe --uninstall [--quiet]    quit the tray, remove autostart and hooks
    /// --dry-run: clicks only log what they would start
    /// </summary>
    static class Program
    {
        public const int ExitAlreadyRunning = 3;

        [STAThread]
        static int Main(string[] args)
        {
            // agent hook: kept out of Run so this path never loads WinForms and exits within milliseconds
            if (args.Length > 0 && args[0] == "--hook" && (args.Length >= 3 || (args.Length == 2 && args[1] == "cursor")))
            {
                HookCommand.Run(args[1], args.Length >= 3 ? args[2] : null);
                return 0;
            }
            // Cursor runs its hooks and Claude Code's without arguments: never start the tray (or open its settings) for them
            if (args.Length == 0 && HookCommand.RunCursorHook())
                return 0;
            return Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Run(string[] args)
        {
            string petId = Option(args, "--pet");
            bool dryRun = Array.IndexOf(args, "--dry-run") >= 0;

            string status = Option(args, "--status");
            if (status != null)
            {
                var monitor = new StatusMonitor(status);
                monitor.Scan();
                Console.WriteLine(monitor.State + (monitor.LatestDone > 0 ? " (last done " + monitor.LatestDone + ")" : ""));
                return 0;
            }

            string command = Option(args, "--command");
            if (command != null)
                return PrintCommand(command, Option(args, "--mode"));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool install = Array.IndexOf(args, "--install") >= 0;
            if (install || Array.IndexOf(args, "--uninstall") >= 0)
                return RunSetup(install, Array.IndexOf(args, "--quiet") >= 0);

            string snapshot = Option(args, "--snapshot");
            if (snapshot != null)
            {
                Log.Tag = "snapshot";
                Native.EnableDpiAwareness();
                foreach (PetInfo pet in PetInfo.Discover())
                    if (petId == null || pet.Id == petId)
                        PetForm.Snapshot(pet, snapshot);
                PetForm.SnapshotLineup(PetInfo.Discover(), Path.Combine(snapshot, "lineup.png"));
                SettingsForm.Snapshot(snapshot, petId);
                return 0;
            }

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Log.Write("crash: " + e.ExceptionObject);
            };
            return petId != null ? RunPet(petId, dryRun, Option(args, "--host")) : RunTray(dryRun);
        }

        static int RunTray(bool dryRun)
        {
            Log.Tag = "tray";
            bool first;
            using (var mutex = new Mutex(true, "Local\\aipets.tray", out first))
            {
                if (!first)
                {
                    // started again (double-click on the exe): open the running tray's settings instead
                    Ipc.SendToHost("settings");
                    return ExitAlreadyRunning;
                }
                Native.EnableSystemDpiAwareness();
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Log.Write("error: " + e.Exception);   // the tray keeps running
                };
                Application.Run(new TrayHost(dryRun));
                return 0;
            }
        }

        static int RunPet(string id, bool dryRun, string hostPid)
        {
            Log.Tag = id;
            PetInfo pet = PetInfo.ById(id);
            if (pet == null || !pet.HasSprites)
            {
                Log.Write("pet not found or without sprites: " + id);
                return 2;
            }
            bool first;
            using (var mutex = new Mutex(true, "Local\\aipets.pet." + pet.Id, out first))
            {
                if (!first)
                    return ExitAlreadyRunning;

                Process host = null;
                int pid;
                if (hostPid != null && int.TryParse(hostPid, NumberStyles.Integer, CultureInfo.InvariantCulture, out pid))
                {
                    try
                    {
                        host = Process.GetProcessById(pid);
                    }
                    catch (ArgumentException)
                    {
                        return 0;   // the tray that started us is already gone
                    }
                }

                Launcher.DryRun = dryRun;
                Native.EnableDpiAwareness();
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    // a broken pet exits; the tray starts a fresh one
                    Log.Write("crash: " + e.Exception);
                    Environment.Exit(1);
                };
                Application.Run(new PetForm(pet, host));
                return 0;
            }
        }

        /// <summary>install.cmd / uninstall.cmd: everything a user would otherwise set up by hand, with a summary at the end.</summary>
        static int RunSetup(bool install, bool quiet)
        {
            Log.Tag = install ? "install" : "uninstall";
            if (!install && !quiet && MessageBox.Show(
                    "Remove aipets?\n\nThis quits aipets and removes \"Start with Windows\" as well as the status hooks from Claude Code, Codex, Hermes and Cursor.",
                    App.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return 1;
            if (!install)
                QuitTray();

            List<Setup.Step> steps = install ? Setup.Install(App.ExePath) : Setup.Uninstall(App.ExePath);
            if (install)
            {
                if (TrayRunning())
                {
                    steps.Add(new Setup.Step("aipets", true, "running"));
                }
                else
                {
                    // through explorer, so the tray does not inherit the installer's environment
                    Process.Start("explorer.exe", "\"" + App.ExePath + "\"");
                    steps.Add(new Setup.Step("aipets", true, "started (icon in the notification area, left click = settings)"));
                }
            }

            bool ok = steps.TrueForAll(s => s.Ok || s.Text == "not installed");
            var text = new StringBuilder(install ? "aipets is set up." : "aipets is removed.");
            text.Append(ok ? "\n\n" : " Not everything worked, see below.\n\n");
            foreach (Setup.Step step in steps)
                text.Append(step).Append('\n');
            if (!install)
                text.Append("\nYou can now delete the folder ").Append(App.Dir).Append(".");
            Log.Write(text.ToString().Replace('\n', ' '));
            Console.WriteLine(text);
            if (!quiet)
                MessageBox.Show(text.ToString(), App.Name, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return ok ? 0 : 1;
        }

        /// <summary>--command: what a click would open, in the saved mode or the given one; errors are printed, not thrown.</summary>
        static int PrintCommand(string id, string mode)
        {
            PetInfo info = PetInfo.ById(id);
            if (info == null)
            {
                Console.WriteLine("No pet with the id " + id);
                return 2;
            }
            PetSettings s = PetSettings.From(info, Store.Load());
            if (mode != null)
                s.UseMode(mode);
            try
            {
                Console.WriteLine(Launcher.Describe(info, s));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
        }

        static bool TrayRunning()
        {
            bool created;
            using (new Mutex(false, "Local\\aipets.tray", out created))
                return !created;
        }

        static void QuitTray()
        {
            if (!TrayRunning() || !Ipc.SendToHost("quit"))
                return;
            for (int i = 0; i < 50 && TrayRunning(); i++)
                Thread.Sleep(100);
        }

        static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
