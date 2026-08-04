using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DeskMate.Core
{
    /// <summary>All P/Invoke and shell interop in one place.</summary>
    public static class Win32
    {
        public const int WM_WINDOWPOSCHANGING = 0x0046;
        public const int WM_CLIPBOARDUPDATE = 0x031D;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOZORDER = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x, y, cx, cy;
            public uint flags;
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);
        [DllImport("kernel32.dll")]
        static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint Length;
            public uint MemoryLoad;          // 0..100 used
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
            public static MEMORYSTATUSEX Create()
            {
                var m = new MEMORYSTATUSEX();
                m.Length = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                return m;
            }
        }

        /// <summary>Raw system time counters (100-ns ticks). Kernel includes idle.</summary>
        public static bool SampleSystemTimes(out long idle, out long kernel, out long user)
        {
            idle = kernel = user = 0;
            try { return GetSystemTimes(out idle, out kernel, out user); }
            catch { return false; }
        }

        /// <summary>Physical memory: used percent 0..100 and free bytes. False on failure.</summary>
        public static bool MemoryStatus(out double usedPercent, out ulong freeBytes, out ulong totalBytes)
        {
            usedPercent = 0; freeBytes = totalBytes = 0;
            try
            {
                var m = MEMORYSTATUSEX.Create();
                if (!GlobalMemoryStatusEx(ref m)) return false;
                usedPercent = m.MemoryLoad;
                freeBytes = m.AvailPhys;
                totalBytes = m.TotalPhys;
                return true;
            }
            catch { return false; }
        }

        /// <summary>Removes the window from taskbar and Alt-Tab.</summary>
        public static void MakeToolWindow(IntPtr hwnd)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }

        public static void SendToBottom(IntPtr hwnd)
        {
            SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }

        /// <summary>WndProc helper: on WM_WINDOWPOSCHANGING, force the window to stay bottom-most.</summary>
        public static void EnforceBottom(IntPtr lParam)
        {
            var wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
            wp.hwndInsertAfter = HWND_BOTTOM;
            wp.flags &= ~SWP_NOZORDER;
            Marshal.StructureToPtr(wp, lParam, false);
        }

        public static void ListenClipboard(IntPtr hwnd)
        {
            try { AddClipboardFormatListener(hwnd); } catch { }
        }

        /// <summary>style: fill|fit|stretch|tile|center</summary>
        public static bool SetWallpaper(string imagePath, string style)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
                {
                    if (key != null)
                    {
                        string ws = "10", tile = "0";
                        switch (style)
                        {
                            case "fit": ws = "6"; break;
                            case "stretch": ws = "2"; break;
                            case "tile": ws = "0"; tile = "1"; break;
                            case "center": ws = "0"; break;
                        }
                        key.SetValue("WallpaperStyle", ws);
                        key.SetValue("TileWallpaper", tile);
                    }
                }
                const int SPI_SETDESKWALLPAPER = 20, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;
                return SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE) != 0;
            }
            catch (Exception ex) { Log.Error("SetWallpaper", ex); return false; }
        }

        /// <summary>Returns the path of the current desktop wallpaper, or "" if it cannot
        /// be resolved. Reads the registry key set by the system (and by SetWallpaper) — the
        /// actual displayed wallpaper is a transcoded copy in <c>%AppData%\Microsoft\Windows\
        /// Themes\TranscodedWallpaper</c> for Windows 10/11, but the source path lives here and
        /// is good enough for "extract a colour" purposes.</summary>
        public static string GetCurrentWallpaper()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                {
                    if (key != null)
                    {
                        string v = key.GetValue("Wallpaper") as string;
                        if (!string.IsNullOrEmpty(v) && System.IO.File.Exists(v)) return v;
                    }
                }
            }
            catch (Exception ex) { Log.Error("GetCurrentWallpaper", ex); }
            // fall back to the transcoded copy on Win10/11
            try
            {
                string transcoded = System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
                if (System.IO.File.Exists(transcoded)) return transcoded;
            }
            catch { }
            return "";
        }

        public static void EmptyRecycleBin()
        {
            const uint SHERB_NOCONFIRMATION = 1, SHERB_NOPROGRESSUI = 2, SHERB_NOSOUND = 4;
            try { SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND); }
            catch { }
        }

        /// <summary>Creates a .lnk via WScript.Shell (late-bound COM, no extra references).</summary>
        public static void CreateShortcut(string lnkPath, string targetPath, string arguments = null, string iconPath = null)
        {
            var t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type st = sc.GetType();
            st.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { targetPath });
            if (!string.IsNullOrEmpty(arguments))
                st.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { arguments });
            string dir = null;
            try { dir = System.IO.Path.GetDirectoryName(targetPath); } catch { }
            if (!string.IsNullOrEmpty(dir))
                st.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { dir });
            if (!string.IsNullOrEmpty(iconPath))
                st.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { iconPath });
            st.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
        }

        /// <summary>HKCU Run key for autostart.</summary>
        public static void SetAutoStart(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (enabled)
                        key.SetValue("DeskMate", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                    else
                        key.DeleteValue("DeskMate", false);
                }
            }
            catch (Exception ex) { Log.Error("SetAutoStart", ex); }
        }
    }
}
