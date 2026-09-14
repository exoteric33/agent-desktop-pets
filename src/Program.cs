using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

namespace ClaudePet
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Claude Code hook: record the session state and exit. Kept out of RunPet so
            // this path never loads WinForms and returns within a few milliseconds.
            if (args.Length >= 2 && args[0] == "--hook")
            {
                HookCommand.Run(args[1]);
                return;
            }
            RunPet(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RunPet(string[] args)
        {
            int snapshot = Array.IndexOf(args, "--snapshot");
            if (snapshot >= 0 && snapshot + 1 < args.Length)
            {
                PetForm.Snapshot(args[snapshot + 1]);
                return;
            }

            bool first;
            using (var mutex = new Mutex(true, "Local\\ClaudePet.SingleInstance", out first))
            {
                if (!first)
                    return;

                Launcher.DryRun = Array.IndexOf(args, "--dry-run") >= 0;
                Native.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Write("crash: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("crash: " + e.ExceptionObject);
                Application.Run(new PetForm());
            }
        }
    }
}
