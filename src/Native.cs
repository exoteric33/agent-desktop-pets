using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace AiPets
{
    static class Native
    {
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_TOPMOST = 0x8;
        public const int WS_EX_NOACTIVATE = 0x8000000;
        public const int WS_POPUP = unchecked((int)0x80000000);
        public const int WM_CLOSE = 0x10;
        public const int WM_COPYDATA = 0x4A;
        public const int WM_MOUSEACTIVATE = 0x21;
        public const int WM_WINDOWPOSCHANGED = 0x47;
        public const int WM_APP = 0x8000;
        public const int MA_NOACTIVATE = 3;

        const int ULW_ALPHA = 2;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_NOOWNERZORDER = 0x200;
        const uint GW_HWNDFIRST = 0, GW_HWNDLAST = 1, GW_HWNDNEXT = 2, GW_HWNDPREV = 3;
        const int GWL_EXSTYLE = -20;
        const int MaxWindows = 10000;   // z-order walks end here even if windows reshuffle meanwhile
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = new IntPtr(-2);
        static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int CX, CY; }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPOS { public IntPtr hwnd, hwndInsertAfter; public int x, y, cx, cy; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public int cbSize; public uint dwTime; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage,
            out IntPtr bits, IntPtr section, uint offset);
        [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetTopWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hwnd, int msg, IntPtr wParam,
            ref COPYDATASTRUCT lParam, int flags, int timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int processId);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public int dwFlags; }

        /// <summary>A fullscreen game, video or presentation is in front on the pet's monitor.</summary>
        public static bool FullscreenAppActive(IntPtr pet)
        {
            int state;
            if (SHQueryUserNotificationState(out state) != 0 || (state != 2 && state != 3 && state != 4))
                return false;   // only QUNS_BUSY, RUNNING_D3D_FULL_SCREEN, PRESENTATION_MODE can mean fullscreen

            // "Show desktop" (Win+D) also reports QUNS_BUSY, with the desktop in front
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || IsIconic(fg))
                return false;
            var cls = new StringBuilder(64);
            GetClassName(fg, cls, cls.Capacity);
            switch (cls.ToString())
            {
                case "Progman":
                case "WorkerW":
                case "Shell_TrayWnd":
                case "Shell_SecondaryTrayWnd":
                    return false;
            }

            const uint MONITOR_DEFAULTTONEAREST = 2;
            IntPtr monitor = MonitorFromWindow(fg, MONITOR_DEFAULTTONEAREST);
            if (monitor != MonitorFromWindow(pet, MONITOR_DEFAULTTONEAREST))
                return false;   // fullscreen on another screen: stay where she is
            if (state != 2)
                return true;

            var info = new MONITORINFO { cbSize = Marshal.SizeOf(typeof(MONITORINFO)) };
            RECT r;
            if (!GetWindowRect(fg, out r) || !GetMonitorInfo(monitor, ref info))
                return false;
            // maximised windows stop at the taskbar; fullscreen ones cover the whole monitor
            return r.Left <= info.rcMonitor.Left && r.Top <= info.rcMonitor.Top
                && r.Right >= info.rcMonitor.Right && r.Bottom >= info.rcMonitor.Bottom;
        }

        /// <summary>Physical pixels for the pets, otherwise Windows blurs the pixel art on scaled displays.</summary>
        public static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
                    return;
            }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }

        /// <summary>The tray and its settings window: WinForms scales them once for the system DPI.</summary>
        public static void EnableSystemDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_SYSTEM_AWARE))
                    return;
            }
            catch (EntryPointNotFoundException) { }
            SetProcessDPIAware();
        }

        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("userenv.dll", SetLastError = true)] static extern bool CreateEnvironmentBlock(out IntPtr block, IntPtr token, bool inherit);
        [DllImport("userenv.dll")] static extern bool DestroyEnvironmentBlock(IntPtr block);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// The user's environment as Windows builds it at logon (registry, not this process).
        /// Keeps variables of whoever started the pet — e.g. a Claude Code session's NO_COLOR
        /// or CLAUDE_CODE_CHILD_SESSION — out of the terminals it opens. Null if unavailable.
        /// </summary>
        public static Dictionary<string, string> LogonEnvironment()
        {
            const uint TOKEN_QUERY = 0x8, TOKEN_DUPLICATE = 0x2;
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, out token))
                return null;
            try
            {
                IntPtr block;
                if (!CreateEnvironmentBlock(out block, token, false))
                    return null;
                try
                {
                    var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    IntPtr p = block;
                    string entry;
                    while ((entry = Marshal.PtrToStringUni(p)).Length > 0)
                    {
                        int eq = entry.IndexOf('=', 1);   // names like "=C:" start with '='
                        if (eq > 0)
                            vars[entry.Substring(0, eq)] = entry.Substring(eq + 1);
                        p = IntPtr.Add(p, (entry.Length + 1) * 2);
                    }
                    return vars;
                }
                finally
                {
                    DestroyEnvironmentBlock(block);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }

        public static uint IdleMilliseconds()
        {
            var info = new LASTINPUTINFO { cbSize = Marshal.SizeOf(typeof(LASTINPUTINFO)) };
            if (!GetLastInputInfo(ref info))
                return 0;
            return unchecked((uint)Environment.TickCount - info.dwTime);
        }

        /// <summary>
        /// The shown topmost windows, from the top of the z-order down (see Layers), or null while windows move.
        /// A walk is no snapshot: a window that moves meanwhile can be missed or seen twice, so two walks must agree.
        /// </summary>
        public static List<Layers.Window> TopmostWindows()
        {
            List<Layers.Window> first = WalkTopmost(), second = WalkTopmost();
            if (first == null || second == null || first.Count != second.Count)
                return null;
            for (int i = 0; i < first.Count; i++)
                if (first[i].Handle != second[i].Handle)
                    return null;
            return first;
        }

        /// <summary>One walk over all windows; null if it broke off because a window vanished on the way.</summary>
        static List<Layers.Window> WalkTopmost()
        {
            var band = new List<Layers.Window>();
            IntPtr hwnd = GetTopWindow(IntPtr.Zero), last = IntPtr.Zero;
            for (int i = 0; hwnd != IntPtr.Zero && i < MaxWindows; i++)
            {
                // topmost windows can also sit further down, hidden ones even below normal windows: walk everything
                if (IsTopmost(hwnd) && Shown(hwnd))
                {
                    int pid;
                    GetWindowThreadProcessId(hwnd, out pid);
                    // no message for other processes' windows, so a hung program cannot stall the pet
                    var title = new StringBuilder(256);
                    GetWindowText(hwnd, title, title.Capacity);
                    band.Add(new Layers.Window { Handle = hwnd, ProcessId = pid, Title = title.ToString() });
                }
                last = hwnd;
                hwnd = GetWindow(hwnd, GW_HWNDNEXT);
            }
            bool complete = hwnd == IntPtr.Zero && last != IntPtr.Zero && GetWindow(last, GW_HWNDLAST) == last;
            return complete ? band : null;
        }

        public static bool IsTopmost(IntPtr hwnd)
        {
            return (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0;
        }

        /// <summary>Visible and not empty: helper windows without any area (input indicator) cover nothing.</summary>
        static bool Shown(IntPtr hwnd)
        {
            RECT r;
            return IsWindowVisible(hwnd) && GetWindowRect(hwnd, out r) && r.Right > r.Left && r.Bottom > r.Top;
        }

        /// <summary>
        /// The nearest shown window above this one in the z-order, IntPtr.Zero at the very top,
        /// or HWND_TOPMOST (never a real window) if the walk broke off.
        /// </summary>
        public static IntPtr VisibleWindowAbove(IntPtr hwnd)
        {
            for (int i = 0; i < MaxWindows; i++)
            {
                IntPtr above = GetWindow(hwnd, GW_HWNDPREV);
                if (above == IntPtr.Zero)
                    return GetWindow(hwnd, GW_HWNDFIRST) == hwnd ? IntPtr.Zero : HWND_TOPMOST;
                if (Shown(above))
                    return above;
                hwnd = above;
            }
            return HWND_TOPMOST;
        }

        /// <summary>WM_WINDOWPOSCHANGED: the window may have a new place in the z-order.</summary>
        public static bool ZOrderChanged(IntPtr windowPos)
        {
            var pos = (WINDOWPOS)Marshal.PtrToStructure(windowPos, typeof(WINDOWPOS));
            return (pos.flags & SWP_NOZORDER) == 0;
        }

        /// <summary>
        /// Moves a topmost window directly below another topmost window, or with IntPtr.Zero to the very top.
        /// Only this window moves (no owner or owned windows), and it is not activated.
        /// </summary>
        public static void PlaceBelow(IntPtr hwnd, IntPtr above)
        {
            const uint flags = SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER;
            if (above == IntPtr.Zero || !IsTopmost(hwnd))
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, flags);
            if (above != IntPtr.Zero)
                SetWindowPos(hwnd, above, 0, 0, 0, 0, flags);
        }

        /// <summary>
        /// A premultiplied 32-bit DIB that GDI+ draws into and UpdateLayeredWindow
        /// pushes to the screen with per-pixel alpha (transparent pixels click through).
        /// </summary>
        public sealed class LayeredSurface : IDisposable
        {
            public readonly int Width, Height;
            public readonly Bitmap Bitmap;
            readonly IntPtr dc, dib, oldBitmap;

            public LayeredSurface(int width, int height)
            {
                Width = width;
                Height = height;
                var header = new BITMAPINFOHEADER
                {
                    biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER)),
                    biWidth = width,
                    biHeight = -height,  // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                };
                IntPtr screen = GetDC(IntPtr.Zero);
                IntPtr bits;
                dib = CreateDIBSection(screen, ref header, 0, out bits, IntPtr.Zero, 0);
                dc = CreateCompatibleDC(screen);
                ReleaseDC(IntPtr.Zero, screen);
                if (dib == IntPtr.Zero || dc == IntPtr.Zero)
                    throw new InvalidOperationException("CreateDIBSection failed");
                oldBitmap = SelectObject(dc, dib);
                Bitmap = new Bitmap(width, height, width * 4, PixelFormat.Format32bppPArgb, bits);
            }

            public void Present(IntPtr hwnd, int x, int y)
            {
                var dst = new POINT { X = x, Y = y };
                var src = new POINT();
                var size = new SIZE { CX = Width, CY = Height };
                var blend = new BLENDFUNCTION { BlendOp = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, dc, ref src, 0, ref blend, ULW_ALPHA);
            }

            public void Dispose()
            {
                Bitmap.Dispose();
                SelectObject(dc, oldBitmap);
                DeleteObject(dib);
                DeleteDC(dc);
            }
        }
    }

    /// <summary>
    /// Tray ⇄ pet messages. Pets are top-level windows titled "aipets.pet.&lt;id&gt;", the tray owns a
    /// hidden window "aipets.host". Tray → pet: registered message (reload, launch) or WM_CLOSE.
    /// Pet → tray: WM_COPYDATA with a text command ("settings claude", "hide hermes", "quit").
    /// </summary>
    static class Ipc
    {
        public const string HostTitle = "aipets.host";
        public const int CmdReload = 1, CmdLaunch = 2;
        public static readonly int CommandMessage = Native.RegisterWindowMessage("aipets.command");

        public static string PetTitle(string id)
        {
            return "aipets.pet." + id;
        }

        public static bool PostToPet(string id, int command)
        {
            IntPtr hwnd = Native.FindWindow(null, PetTitle(id));
            return hwnd != IntPtr.Zero && Native.PostMessage(hwnd, CommandMessage, (IntPtr)command, IntPtr.Zero);
        }

        public static bool ClosePet(string id)
        {
            IntPtr hwnd = Native.FindWindow(null, PetTitle(id));
            return hwnd != IntPtr.Zero && Native.PostMessage(hwnd, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>Sends a text command to the running tray. False if there is none.</summary>
        public static bool SendToHost(string text)
        {
            IntPtr hwnd = Native.FindWindow(null, HostTitle);
            if (hwnd == IntPtr.Zero)
                return false;
            // this process just got the click: let the tray bring its settings window to the front
            Native.AllowSetForegroundWindow(-1);
            IntPtr data = Marshal.StringToHGlobalUni(text);
            try
            {
                var cds = new Native.COPYDATASTRUCT { dwData = IntPtr.Zero, cbData = (text.Length + 1) * 2, lpData = data };
                IntPtr result;
                const int SMTO_ABORTIFHUNG = 2;
                return Native.SendMessageTimeout(hwnd, Native.WM_COPYDATA, IntPtr.Zero, ref cds, SMTO_ABORTIFHUNG, 3000, out result) != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(data);
            }
        }

        public static string ReadCopyData(IntPtr lParam)
        {
            var cds = (Native.COPYDATASTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.COPYDATASTRUCT));
            return cds.cbData >= 2 ? Marshal.PtrToStringUni(cds.lpData, cds.cbData / 2 - 1) : "";
        }
    }
}
