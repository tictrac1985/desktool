using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Features;
using DeskMate.Logic;
using WF = System.Windows.Forms;

namespace DeskMate.UI
{
    /// <summary>MDL2 glyph codes as escapes (literal PUA chars are fragile in source files).</summary>
    static class Glyphs
    {
        public const string Cleaner = "";
        public const string Calc = "";
        public const string Wallpaper = "";
        public const string Power = "";
        public const string Clipboard = "";
        public const string Weather = "";
        public const string Apps = "";
        public const string Prayer = "";
        public const string SettingsGear = "";
        public const string Calendar = "";
        public const string Focus = "";
        public const string Occasions = "";
        public const string Note = "";
        public const string SysMon = "";
        public const string WorldClock = "";
    }

    /// <summary>The desktop widget shell. Split across:
    /// MainWindow.cs (core) · .Layouts.cs (six layouts) · .Icons.cs (tool cells + rays) · .Grip.cs (scale grip).</summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        /// <summary>Set when Windows is shutting down/logging off so Closing doesn't cancel.</summary>
        public static bool OsSessionEnding;

        readonly DispatcherTimer _timer = new DispatcherTimer();
        IClockView _clock;
        System.Windows.Controls.TextBlock _prayerLine;
        WF.NotifyIcon _tray;
        IntPtr _hwnd;
        bool _reallyExiting;
        bool _flashOnTop;
        int _lastPrayerLineMin = -1;

        FrameworkElement _rootEl;                       // scaled content root
        readonly List<System.Windows.Controls.Border> _cards = new List<System.Windows.Controls.Border>();
        System.Windows.Controls.Border _clockCard;      // for connector lines
        System.Windows.Controls.Canvas _connectors;
        readonly List<FrameworkElement> _floatIcons = new List<FrameworkElement>();
        System.Windows.Controls.TextBlock _gregText, _hijriText;  // tri-panel side cards
        string _dateKey;

        public MainWindow()
        {
            Instance = this;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = false;
            Title = "DeskMate";

            BuildUI();
            RestorePosition();

            SourceInitialized += OnSourceInitialized;
            Loaded += (s, e) =>
            {
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += (ts, te) => OnTick(DateTime.Now);
                _timer.Start();
                OnTick(DateTime.Now);
                if (Settings.I.AutoStart) Win32.SetAutoStart(true);
            };
            SizeChanged += (s, e) => ScheduleConnectorDraw();
            // Alt+F4 or any accidental close must only hide the widget — the process keeps
            // its tray icon and a later launch (desktop icon) brings the window back.
            Closing += (s, e) =>
            {
                if (_reallyExiting || OsSessionEnding) return;
                e.Cancel = true;
                Hide();
            };
            Closed += (s, e) => { if (_tray != null) _tray.Dispose(); };
            SetupTray();
        }

        void OnSourceInitialized(object sender, EventArgs e)
        {
            var src = (HwndSource)PresentationSource.FromVisual(this);
            _hwnd = src.Handle;
            Win32.MakeToolWindow(_hwnd);
            src.AddHook(WndProc);
            Win32.SendToBottom(_hwnd);
            Win32.ListenClipboard(_hwnd);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WM_WINDOWPOSCHANGING && !_flashOnTop)
            {
                Win32.EnforceBottom(lParam);
            }
            else if (msg == Win32.WM_CLIPBOARDUPDATE)
            {
                ClipboardKeeper.OnClipboardUpdate();
            }
            return IntPtr.Zero;
        }

        /// <summary>Full rebuild after settings change (language, style, layout, visibility).</summary>
        public void Reload()
        {
            _lastPrayerLineMin = -1;
            BuildUI();
            SetupTray();
            OnTick(DateTime.Now);
        }

        /// <summary>Applies the user's background transparency to all cards (text stays opaque).</summary>
        public void ApplyCardOpacity()
        {
            foreach (var card in _cards)
            {
                var b = Theme.CardBg.Clone();
                b.Opacity = Settings.I.BgOpacity;
                if (b.CanFreeze) b.Freeze();
                card.Background = b;
            }
        }

        // ---------------- position ----------------

        void RestorePosition()
        {
            var wa = SystemParameters.WorkArea;
            double x = Settings.I.PosX, y = Settings.I.PosY;
            if (x < 0 || y < 0 || x > wa.Right - 60 || y > wa.Bottom - 60)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = wa.Left + (wa.Width - 380) / 2;
                Top = wa.Top + wa.Height * 0.12;
            }
            else { Left = x; Top = y; }
        }

        void SavePosition()
        {
            Settings.I.PosX = Left;
            Settings.I.PosY = Top;
            Settings.I.Save();
        }

        // ---------------- tick ----------------

        void OnTick(DateTime now)
        {
            // auto day/night theme — flip the palette exactly when the boundary is crossed
            int eff = AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.AutoTheme, now.Hour);
            if (eff != Theme.AppliedIndex)
            {
                Theme.Apply(eff);
                Reload();
                return; // Reload already ticks once
            }

            // "Show desktop" (Win+D) minimizes even tool windows; restore automatically
            if (WindowState != WindowState.Normal && IsVisible)
            {
                WindowState = WindowState.Normal;
                Win32.SendToBottom(_hwnd);
            }
            if (_clock != null && IsVisible) _clock.Tick(now);

            // tri-panel side dates — refresh when the day or language changes
            if (_gregText != null)
            {
                string key = now.ToString("yyyyMMdd") + Settings.I.Lang;
                if (key != _dateKey)
                {
                    _dateKey = key;
                    _gregText.Text = DateText.Gregorian(now);
                    if (_hijriText != null) _hijriText.Text = DateText.Hijri(now);
                }
            }

            PrayerFeature.MinuteTick(now);
            // the countdown only changes once a minute — skip the recompute otherwise
            if (now.Minute != _lastPrayerLineMin)
            {
                _lastPrayerLineMin = now.Minute;
                if (Settings.I.ShowPrayer && Settings.I.ShowNextPrayerLine)
                {
                    string line = PrayerFeature.NextLine(now);
                    _prayerLine.Text = line;
                    _prayerLine.Visibility = string.IsNullOrEmpty(line) ? Visibility.Collapsed : Visibility.Visible;
                }
                else _prayerLine.Visibility = Visibility.Collapsed;
            }
        }

        // ---------------- tray ----------------

        void SetupTray()
        {
            if (_tray == null)
            {
                _tray = new WF.NotifyIcon();
                try
                {
                    _tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                        System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                catch { }
                _tray.Visible = true;
                _tray.DoubleClick += (s, e) => ToggleVisible();
            }
            _tray.Text = L.T("app_name");
            var menu = new WF.ContextMenuStrip();
            menu.Items.Add(L.T(IsVisible ? "tray_hide" : "tray_show"), null, (s, e) => ToggleVisible());
            menu.Items.Add(L.T("tray_settings"), null, (s, e) => Dispatcher.BeginInvoke(new Action(() => SettingsFeature.Open(this))));
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add(L.T("tray_exit"), null, (s, e) => ExitApp());
            menu.RightToLeft = L.IsAr ? WF.RightToLeft.Yes : WF.RightToLeft.No;
            menu.Opening += (s, e) => { menu.Items[0].Text = L.T(IsVisible ? "tray_hide" : "tray_show"); };
            _tray.ContextMenuStrip = menu;
        }

        public void ToggleVisible()
        {
            if (IsVisible) Hide();
            else ShowFromTray();
        }

        public void ShowFromTray()
        {
            WindowState = WindowState.Normal;
            Show();
            OnTick(DateTime.Now);
            // surface above other windows for a moment so the user sees it opened,
            // then settle back to the desktop layer
            _flashOnTop = true;
            Topmost = true;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                Topmost = false;
                _flashOnTop = false;
                Win32.SendToBottom(_hwnd);
            };
            t.Start();
        }

        public void ExitApp()
        {
            _reallyExiting = true;
            try { Settings.I.Save(); } catch { }
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            Application.Current.Shutdown();
        }
    }
}
