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
        public const int WM_HOTKEY = 0x0312;

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_NOZORDER = 0x0004;

        public const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>Registers a global shortcut on the widget window. False when the
        /// combination is already claimed (by Windows or another app) — the caller then
        /// tries its next candidate; nothing is thrown at startup.</summary>
        public static bool RegisterSummonHotKey(IntPtr hwnd, int id, uint modifiers, uint key)
        {
            try { return RegisterHotKey(hwnd, id, modifiers | MOD_NOREPEAT, key); }
            catch (Exception ex) { Log.Error("RegisterHotKey", ex); return false; }
        }

        // ---------------- media keys ----------------

        public const byte VK_VOLUME_MUTE = 0xAD, VK_VOLUME_DOWN = 0xAE, VK_VOLUME_UP = 0xAF,
                          VK_MEDIA_NEXT = 0xB0, VK_MEDIA_PREV = 0xB1, VK_MEDIA_STOP = 0xB2, VK_MEDIA_PLAY_PAUSE = 0xB3;

        [DllImport("user32.dll")] static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        /// <summary>Presses and releases one of the keyboard's media keys. Whatever player is
        /// listening for them (Spotify, a browser tab, the system mixer) answers exactly as it
        /// would to a real keyboard — DeskMate needs no knowledge of the player.</summary>
        public static void TapMediaKey(byte vk)
        {
            const uint KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_KEYUP = 0x0002;
            try
            {
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                keybd_event(vk, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex) { Log.Error("media key", ex); }
        }

        public static void UnregisterSummonHotKey(IntPtr hwnd, int id)
        {
            try { UnregisterHotKey(hwnd, id); } catch { }
        }

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

        // ---------------- visibility & power probes ----------------

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder buf, int max);

        /// <summary>True when the foreground window covers <paramref name="self"/> completely.
        /// The widget is pinned to the bottom of the z-order, so a foreground window whose
        /// rectangle contains it means not one pixel of it is on screen. That matters more here
        /// than in a normal app: the widget is a layered (per-pixel alpha) window, which takes
        /// it off the GPU — Windows composes it on the CPU through UpdateLayeredWindow — so a
        /// repaint nobody can see is pure heat and fan noise.
        ///
        /// Only the foreground window is examined on purpose. Walking the whole z-order to
        /// catch every partial overlap would cost more than the repaints it saves, and the
        /// case that actually matters (working in a maximised window) is exactly the one a
        /// single rectangle test catches.</summary>
        public static bool IsCovered(IntPtr self)
        {
            try
            {
                if (self == IntPtr.Zero) return false;
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero || fg == self) return false;
                if (IsIconic(fg) || !IsWindowVisible(fg)) return false;
                // Progman and WorkerW ARE the desktop, and the taskbar sits beside the widget,
                // not over it. Their rectangles span the screen, so without this the widget
                // would decide it was hidden every time the user clicked the desktop.
                var sb = new System.Text.StringBuilder(64);
                GetClassName(fg, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
                RECT f, w;
                if (!GetWindowRect(fg, out f) || !GetWindowRect(self, out w)) return false;
                if (w.Right <= w.Left || w.Bottom <= w.Top) return false;
                return f.Left <= w.Left && f.Top <= w.Top && f.Right >= w.Right && f.Bottom >= w.Bottom;
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus, BatteryFlag, BatteryLifePercent, Reserved1;
            public int BatteryLifeTime, BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

        /// <summary>True only when the machine is demonstrably running on battery. Desktops,
        /// unknown states and any failure all answer false: power saving may switch itself on
        /// for a laptop on battery, never by accident on a machine that is plugged in.</summary>
        public static bool OnBattery()
        {
            try
            {
                SYSTEM_POWER_STATUS st;
                if (!GetSystemPowerStatus(out st)) return false;
                if (st.BatteryFlag == 128 || st.BatteryFlag == 255) return false;   // no battery / unknown
                return st.ACLineStatus == 0;
            }
            catch { return false; }
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
