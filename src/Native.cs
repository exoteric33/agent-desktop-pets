using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ClaudePet
{
    static class Native
    {
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int WS_EX_TOPMOST = 0x8;
        public const int WS_EX_NOACTIVATE = 0x8000000;
        public const int WM_MOUSEACTIVATE = 0x21;
        public const int MA_NOACTIVATE = 3;

        const int ULW_ALPHA = 2;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
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
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int size);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

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
            var cls = new System.Text.StringBuilder(64);
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

        public static void EnableDpiAwareness()
        {
            // physical pixels, otherwise Windows blurs the pixel art on scaled displays
            try
            {
                if (SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2))
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
        public static System.Collections.Generic.Dictionary<string, string> LogonEnvironment()
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
                    var vars = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

        public static void KeepTopmost(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
}
