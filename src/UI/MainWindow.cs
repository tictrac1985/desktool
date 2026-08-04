using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
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
        public const string Cleaner = "\uE74D";
        public const string Calc = "\uE8EF";
        public const string Wallpaper = "\uE91B";
        public const string Power = "\uE7E8";
        public const string Clipboard = "\uE77F";
        public const string Weather = "\uE706";
        public const string Apps = "\uE71D";
        public const string Prayer = "\uE708";
        public const string SettingsGear = "\uE713";
        public const string Calendar = "\uE787";
        public const string Focus = "\uE916";
        public const string Occasions = "\uE735";
        public const string Note = "\uE70F";
        public const string SysMon = "\uE772";
        public const string WorldClock = "\uE774";
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
        // widget weather line: glyph and text are separate because the WMO glyph needs
        // Segoe UI Symbol while the temperature must stay in the UI font
        System.Windows.Controls.StackPanel _weatherLine;
        System.Windows.Controls.TextBlock _weatherGlyph, _weatherText;
        int _lastWeatherMin = -1;
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
            _lastWeatherMin = -1;   // the rebuilt line starts empty; force a repaint on the next tick
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
            WindowStartupLocation = WindowStartupLocation.Manual;
            if (IsOnAnyScreen(x, y)) { Left = x; Top = y; return; }
            Left = wa.Left + (wa.Width - 380) / 2;
            Top = wa.Top + wa.Height * 0.12;
        }

        /// <summary>Validates a saved position against the whole virtual desktop, not just the
        /// primary monitor's work area — otherwise a widget parked on a second screen (where
        /// coordinates are legitimately negative or beyond the primary bounds) was treated as
        /// off-screen and recentred on every start.</summary>
        static bool IsOnAnyScreen(double x, double y)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return false;
            if (x == -1 && y == -1) return false;   // the "never positioned" default
            // VirtualScreen* are device-independent units covering every monitor, including
            // screens left of or above the primary where coordinates are legitimately negative.
            double left = SystemParameters.VirtualScreenLeft;
            double top = SystemParameters.VirtualScreenTop;
            double right = left + SystemParameters.VirtualScreenWidth;
            double bottom = top + SystemParameters.VirtualScreenHeight;
            // keep enough of the widget on-screen to stay grabbable
            return x >= left - 40 && x <= right - 80 && y >= top - 10 && y <= bottom - 60;
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
            int eff = AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.LightThemeIndex, Settings.I.AutoTheme, now.Hour);

            // Material You: derive the palette from the wallpaper. The override is sticky
            // until the user unchecks the option, but we still re-derive on every tick in
            // case the wallpaper changed (cheap because MaterialYouCore caches by path+mtime).
            //
            // Each branch that calls Reload() must also update Theme.AppliedIndex so the next
            // tick does NOT take the same branch again. The earlier code kept AppliedIndex at
            // the old value (so the "eff != AppliedIndex" branch fired every tick when Material
            // You was on), which rebuilt the visual tree every second and froze the clock —
            // the clock object was recreated on every Reload() and lost its per-tick state.
            if (Settings.I.MaterialYou && !Theme.MaterialYouActive)
            {
                var mp = Logic.MaterialYouCore.GenerateFromWallpaper();
                Theme.ApplyMaterial(Color.FromRgb((byte)mp.Accent.R, (byte)mp.Accent.G, (byte)mp.Accent.B),
                    Color.FromRgb((byte)mp.CardTop.R, (byte)mp.CardTop.G, (byte)mp.CardTop.B),
                    Color.FromRgb((byte)mp.CardBottom.R, (byte)mp.CardBottom.G, (byte)mp.CardBottom.B),
                    eff);
                Reload();
                return;
            }
            if (!Settings.I.MaterialYou && Theme.MaterialYouActive)
            {
                // user just turned Material You off — fall back to the normal theme
                Theme.Apply(eff);
                Reload();
                return;
            }
            if (eff != Theme.AppliedIndex)
            {
                // normal theme flip (ThemeIndex change, or day/night boundary crossed)
                Theme.Apply(eff);
                Reload();
                return;
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
            FocusFeature.BackgroundTick(now);
            StickyNoteFeature.BackgroundTick(now);   // note reminders fire whether the panel is open or not
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

            if (now.Minute != _lastWeatherMin)
            {
                _lastWeatherMin = now.Minute;
                WeatherFeature.EnsureFresh();   // no-op unless enabled, located and stale
                UpdateWeatherLine();
            }
        }

        /// <summary>Repaints the next-prayer line immediately. Called when a per-prayer
        /// correction changes, so the widget agrees with the panel without waiting for the
        /// next minute boundary.</summary>
        public void RefreshPrayerLine()
        {
            if (_prayerLine == null) return;
            _lastPrayerLineMin = -1;
            OnTick(DateTime.Now);
        }

        /// <summary>Paints the widget weather line from whatever is already cached. Never
        /// requests anything itself, so it is safe on the UI thread every minute.</summary>
        void UpdateWeatherLine()
        {
            if (_weatherLine == null) return;
            var w = Settings.I.ShowWeather && Settings.I.ShowWeatherLine ? WeatherFeature.Cached : null;
            if (w == null || double.IsNaN(w.Temp))
            {
                _weatherLine.Visibility = Visibility.Collapsed;
                return;
            }
            bool night = DateTime.Now.Hour < 6 || DateTime.Now.Hour >= 19;
            _weatherGlyph.Text = WeatherCore.WmoGlyph(w.Code, night);
            string city = Settings.I.City;
            _weatherText.Text = Math.Round(w.Temp) + "°" +
                (string.IsNullOrEmpty(city) ? "" : "  ·  " + city);
            _weatherLine.Visibility = Visibility.Visible;
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
            // Reload() runs on every settings change and on each auto-theme flip; without
            // disposing the previous menu each rebuild leaked its native handles.
            var old = _tray.ContextMenuStrip;
            var menu = new WF.ContextMenuStrip();
            menu.Items.Add(L.T(IsVisible ? "tray_hide" : "tray_show"), null, (s, e) => ToggleVisible());
            menu.Items.Add(L.T("tray_settings"), null, (s, e) => Dispatcher.BeginInvoke(new Action(() => SettingsFeature.Open(this))));
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add(L.T("tray_exit"), null, (s, e) => ExitApp());
            menu.RightToLeft = L.IsAr ? WF.RightToLeft.Yes : WF.RightToLeft.No;
            menu.Opening += (s, e) => { menu.Items[0].Text = L.T(IsVisible ? "tray_hide" : "tray_show"); };
            _tray.ContextMenuStrip = menu;
            if (old != null) { try { old.Dispose(); } catch { } }
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
