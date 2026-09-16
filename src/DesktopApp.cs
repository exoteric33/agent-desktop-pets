using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace AiPets
{
    /// <summary>
    /// The desktop app a pet opens in app mode: an installed app package (Claude, the Codex app),
    /// started through its app id like a Start menu click, or a plain exe (Hermes Desktop).
    /// pet.ini lists candidates separated by ';', the first installed one wins.
    /// </summary>
    sealed class DesktopApp
    {
        public string AppId;     // "PackageFamilyName!App" of a packaged app, else null
        public string Package;   // its full package name (name_version_arch__publisher), else null
        public string Exe;       // the exe of a plain app, else null

        public static DesktopApp Find(string candidates)
        {
            string whole = Expand(candidates);
            if (whole.IndexOf(';') >= 0 && File.Exists(whole))
                return new DesktopApp { Exe = Path.GetFullPath(whole) };   // one exe whose path contains ';'
            foreach (string part in (candidates ?? "").Split(';'))
            {
                string candidate = Expand(part);
                if (candidate.Length == 0)
                    continue;
                if (IsAppId(candidate))
                {
                    string package = InstalledPackage(candidate.Substring(0, candidate.IndexOf('!')));
                    if (package != null)
                        return new DesktopApp { AppId = candidate, Package = package };
                }
                else if (File.Exists(candidate))
                {
                    return new DesktopApp { Exe = Path.GetFullPath(candidate) };
                }
            }
            return null;
        }

        /// <summary>"codex app", "hermes desktop": the pet's program with these arguments, as one would type it.</summary>
        public static string ProgramCommand(PetSettings s, string args)
        {
            string program = Path.GetFileNameWithoutExtension((s.Program ?? "").Trim().Trim('"'));
            return (program.Length > 0 ? program + " " : "") + args;
        }

        /// <summary>Package family ("Claude_pzs8sxrjxfjjc") of a packaged app, else null.</summary>
        public string Family
        {
            get { return AppId != null ? AppId.Substring(0, AppId.IndexOf('!')) : null; }
        }

        /// <summary>The name Windows shows: the Start menu entry of a package, the file description of an exe.</summary>
        public string Name
        {
            get
            {
                string name = AppId != null ? ShellName(@"shell:AppsFolder\" + AppId) : ExeInfo(true);
                if (!string.IsNullOrEmpty(name))
                    return name;
                return AppId != null ? Package.Split('_')[0] : Path.GetFileNameWithoutExtension(Exe);
            }
        }

        /// <summary>Package version or the exe's product version; null if it has none.</summary>
        public string Version
        {
            get { return AppId != null ? Package.Split('_')[1] : ExeInfo(false); }
        }

        public override string ToString()
        {
            string version = Version;
            return Name + (string.IsNullOrEmpty(version) ? "" : " " + version);
        }

        /// <summary>
        /// A package starts in its own app context, through the shell like a Start menu click.
        /// An exe starts in its own folder, as the Start menu shortcuts do.
        /// </summary>
        public ProcessStartInfo StartInfo()
        {
            if (AppId != null)
                return new ProcessStartInfo(@"shell:AppsFolder\" + AppId) { UseShellExecute = true };
            return new ProcessStartInfo(Exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(Exe) };
        }

        static string Expand(string text)
        {
            return Environment.ExpandEnvironmentVariables((text ?? "").Trim().Trim('"'));
        }

        // "Claude_pzs8sxrjxfjjc!Claude"; paths have separators, app ids never do
        static bool IsAppId(string candidate)
        {
            return candidate.IndexOf('!') > 0 && candidate.IndexOfAny(new[] { '\\', '/', ':' }) < 0;
        }

        string ExeInfo(bool name)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(Exe);
                string text = name
                    ? (string.IsNullOrEmpty(info.FileDescription) ? info.ProductName : info.FileDescription)
                    : (string.IsNullOrEmpty(info.ProductVersion) ? info.FileVersion : info.ProductVersion);
                return text != null ? text.Trim() : null;
            }
            catch (IOException) { return null; }
        }

        // ------------------------------------------------------------------ Windows

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern int GetPackagesByPackageFamily(string packageFamilyName, ref uint count,
            [Out] IntPtr[] packageFullNames, ref uint bufferLength, [Out] char[] buffer);

        /// <summary>Full name of the newest package of this family installed for the user, or null.</summary>
        static string InstalledPackage(string family)
        {
            const int ERROR_INSUFFICIENT_BUFFER = 122;
            try
            {
                uint count = 0, length = 0;
                if (GetPackagesByPackageFamily(family, ref count, null, ref length, null) != ERROR_INSUFFICIENT_BUFFER || count == 0)
                    return null;   // none installed, or not a valid family name
                var names = new IntPtr[count];
                var buffer = new char[length];
                if (GetPackagesByPackageFamily(family, ref count, names, ref length, buffer) != 0)
                    return null;
                // the names are zero-terminated strings one after another in the buffer
                string newest = null;
                foreach (string name in new string(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries))
                    if (newest == null || PackageVersion(name) > PackageVersion(newest))
                        newest = name;
                return newest;
            }
            catch (EntryPointNotFoundException)
            {
                return null;   // Windows 7: no app packages
            }
        }

        static System.Version PackageVersion(string fullName)
        {
            System.Version version;
            string[] parts = fullName.Split('_');
            return parts.Length > 1 && System.Version.TryParse(parts[1], out version) ? version : new System.Version();
        }

        [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, [In] ref Guid handler, [In] ref Guid riid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint sigdn, out IntPtr name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr bindContext, [In] ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

        /// <summary>Display name of a shell item ("shell:AppsFolder\…" = a Start menu app), or null. Needs an STA thread.</summary>
        static string ShellName(string parsingName)
        {
            IShellItem item = null;
            try
            {
                Guid iid = typeof(IShellItem).GUID;
                SHCreateItemFromParsingName(parsingName, IntPtr.Zero, ref iid, out item);
                IntPtr text;
                item.GetDisplayName(0, out text);   // SIGDN_NORMALDISPLAY
                try
                {
                    return Marshal.PtrToStringUni(text);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(text);
                }
            }
            catch (Exception)
            {
                // no such app (the marshaller turns its HRESULT into FileNotFoundException and the like),
                // or COM not usable on this thread: the caller falls back to the package name
                return null;
            }
            finally
            {
                if (item != null)
                    Marshal.ReleaseComObject(item);
            }
        }
    }
}
