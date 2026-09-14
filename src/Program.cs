using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace AiPets
{
    /// <summary>
    /// aipets.exe                          tray (starts and watches the pets)
    /// aipets.exe --pet &lt;id&gt; [--host pid]   one pet window
    /// aipets.exe --hook &lt;source&gt; &lt;event&gt;  agent hook: record the state and exit
    /// aipets.exe --snapshot &lt;dir&gt; [--pet id]  render pets and settings window into PNGs
    /// aipets.exe --status &lt;source&gt;        print the folded agent state (diagnostics)
    /// aipets.exe --command &lt;id&gt;           print what a click on the pet would start (diagnostics)
    /// --dry-run: clicks only log what they would start
    /// </summary>
    static class Program
    {
        public const int ExitAlreadyRunning = 3;

        [STAThread]
        static int Main(string[] args)
        {
            // agent hook: kept out of Run so this path never loads WinForms and exits within milliseconds
            if (args.Length >= 3 && args[0] == "--hook")
            {
                HookCommand.Run(args[1], args[2]);
                return 0;
            }
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
            {
                PetInfo info = PetInfo.ById(command);
                ProcessStartInfo psi = Launcher.BuildStartInfo(info, PetSettings.From(info, Store.Load()));
                Console.WriteLine(psi.FileName + " " + psi.Arguments + "\n(in " + psi.WorkingDirectory + ")");
                return 0;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string snapshot = Option(args, "--snapshot");
            if (snapshot != null)
            {
                Native.EnableDpiAwareness();
                foreach (PetInfo pet in PetInfo.Discover())
                    if (petId == null || pet.Id == petId)
                        PetForm.Snapshot(pet, snapshot);
                SettingsForm.Snapshot(Path.Combine(snapshot, "settings.png"));
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

        static string Option(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
