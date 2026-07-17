using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Features;
using WF = System.Windows.Forms;

namespace DeskMate.UI
{
    public class MainWindow : Window
    {
        public static MainWindow Instance;

        readonly DispatcherTimer _timer = new DispatcherTimer();
        StackPanel _stack;
        ContentControl _clockHost;
        TextBlock _prayerLine;
        WrapPanel _iconBar;
        IClockView _clock;
        WF.NotifyIcon _tray;
        IntPtr _hwnd;
        bool _reallyExiting;
        bool _flashOnTop;

        public MainWindow()
        {
            Instance = this;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
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
            // Alt+F4 or any accidental close must only hide the widget — the process keeps
            // its tray icon and a later launch (desktop icon) brings the window back.
            Closing += (s, e) =>
            {
                if (_reallyExiting) return;
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

        // ---------------- UI construction ----------------

        public void BuildUI()
        {
            FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            _clockHost = new ContentControl { HorizontalAlignment = HorizontalAlignment.Center, Focusable = false };
            switch (Settings.I.ClockStyle)
            {
                case 1: _clock = new AnalogClock(); break;
                case 2: _clock = new MinimalClock(); break;
                default: _clock = new DigitalClock(); break;
            }
            _clockHost.Content = _clock.Root;
            _stack.Children.Add(_clockHost);

            _prayerLine = Theme.MakeText("", 12.5, Theme.Accent);
            _prayerLine.HorizontalAlignment = HorizontalAlignment.Center;
            _prayerLine.Margin = new Thickness(0, 5, 0, 0);
            _prayerLine.Visibility = Visibility.Collapsed;
            _stack.Children.Add(_prayerLine);

            _iconBar = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
                MaxWidth = 392,
            };
            BuildIconBar();
            _stack.Children.Add(_iconBar);

            var card = Theme.MakeCard(_stack);
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement) { try { DragMove(); SavePosition(); } catch { } }
            };
            Content = card;
            if (_clock != null) _clock.Tick(DateTime.Now);
        }

        void BuildIconBar()
        {
            _iconBar.Children.Clear();
            var s = Settings.I;
            if (s.ShowCleaner) AddIcon("", "f_cleaner", () => CleanerFeature.Open(this));
            if (s.ShowCalc) AddIcon("", "f_calc", () => CalculatorFeature.Open(this));
            if (s.ShowWallpaper) AddIcon("", "f_wallpaper", () => WallpaperFeature.Open(this));
            if (s.ShowPower) AddIcon("", "f_power", () => PowerFeature.Open(this));
            if (s.ShowClipboard) AddIcon("", "f_clipboard", () => ClipboardKeeper.Open(this));
            if (s.ShowWeather) AddIcon("", "f_weather", () => WeatherFeature.Open(this));
            if (s.ShowApps) AddIcon("", "f_apps", () => AppsFeature.Open(this));
            if (s.ShowPrayer) AddIcon("", "f_prayer", () => PrayerFeature.Open(this));
            AddIcon("", "f_settings", () => SettingsFeature.Open(this));
        }

        void AddIcon(string glyph, string titleKey, Action open)
        {
            _iconBar.Children.Add(Theme.MakeIconButton(glyph, L.T(titleKey), open));
        }

        /// <summary>Full rebuild after settings change (language, style, visibility).</summary>
        public void Reload()
        {
            BuildUI();
            SetupTray();
            OnTick(DateTime.Now);
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
            // "Show desktop" (Win+D) minimizes even tool windows; restore automatically
            if (WindowState != WindowState.Normal && IsVisible)
            {
                WindowState = WindowState.Normal;
                Win32.SendToBottom(_hwnd);
            }
            if (_clock != null && IsVisible) _clock.Tick(now);
            PrayerFeature.MinuteTick(now);
            if (Settings.I.ShowPrayer && Settings.I.ShowNextPrayerLine)
            {
                string line = PrayerFeature.NextLine(now);
                _prayerLine.Text = line;
                _prayerLine.Visibility = string.IsNullOrEmpty(line) ? Visibility.Collapsed : Visibility.Visible;
            }
            else _prayerLine.Visibility = Visibility.Collapsed;
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
