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
        public const string Command = "\uE721";
        public const string Capture = "\uE722";
        public const string Media = "\uE767";
        public const string Update = "\uE896";
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
        /// <summary>Non-rectangular card surfaces (the Orbit bezel ring) that take the same
        /// background brush and transparency as the cards.</summary>
        readonly List<System.Windows.Shapes.Shape> _plates = new List<System.Windows.Shapes.Shape>();
        System.Windows.Controls.Border _clockCard;      // for connector lines
        System.Windows.Controls.Canvas _connectors;
        readonly List<FrameworkElement> _floatIcons = new List<FrameworkElement>();
        System.Windows.Controls.TextBlock _gregText, _hijriText;  // tri-panel side cards
        string _dateKey;
        // seasonal occasion line on the widget face (Ramadan countdown / Eid greeting /
        // nearest occasion) — glyph and text split like the weather line so the emoji
        // can render in the symbol font while the text stays in the UI font
        System.Windows.Controls.StackPanel _occasionLine;
        System.Windows.Controls.TextBlock _occasionGlyph, _occasionText;
        int _lastOccasionMin = -1;

        // ---- repaint gating (see OnTick) ----
        /// <summary>Set while the foreground window completely hides the widget.</summary>
        bool _covered;
        DateTime _coverChecked = DateTime.MinValue;
        /// <summary>Something moved on while nothing was being drawn, so the next painted tick
        /// must refresh every line rather than trusting the per-minute markers.</summary>
        bool _repaintPending = true;
        int _lastPaintedMin = -1;
        /// <summary>Minute of the last weather freshness check. Separate from _lastWeatherMin
        /// because fetching happens even while the widget is invisible; drawing does not.</summary>
        int _lastWeatherFetchMin = -1;
        /// <summary>Power-saving state the current visual tree was built for.</summary>
        bool _saverApplied;

        /// <summary>State shared with the Halo layout: its dashed ring is rotated a step on
        /// every tick so the halo slowly revolves. Null in every other layout.</summary>
        static class HaloBrushes
        {
            public static System.Windows.Shapes.Ellipse Halo;
        }

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
                _saverApplied = Settings.SaverActive;   // BuildUI already ran for this state
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
            // The command bar's global shortcut. Win+Alt+D (the old fixed choice) is reserved by
            // Windows for the taskbar clock, so the setting now walks a list of free combinations.
            RegisterSummonHotkey();
        }

        /// <summary>Label of the shortcut that actually got registered ("" = none).</summary>
        public static string ActiveHotkey = "";

        public void RegisterSummonHotkey()
        {
            ActiveHotkey = "";
            Win32.UnregisterSummonHotKey(_hwnd, HotkeyIdCommand);
            foreach (int idx in HotkeyCore.Candidates(Settings.I.Hotkey))
            {
                var c = HotkeyCore.Choices[idx];
                if (Win32.RegisterSummonHotKey(_hwnd, HotkeyIdCommand, c.Modifiers, c.Key))
                {
                    ActiveHotkey = c.Label;
                    Log.Write("command-bar shortcut: " + c.Label);
                    return;
                }
            }
            if (Settings.I.Hotkey != HotkeyCore.Off)
                Log.Write("command-bar shortcut: every candidate is taken; the bar stays icon-only");
        }

        const int HotkeyIdCommand = 0x444D;   // 'DM'

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
            else if (msg == Win32.WM_HOTKEY && wParam.ToInt32() == HotkeyIdCommand)
            {
                handled = true;
                // the widget usually sits hidden behind every window; the bar it opens must
                // not appear next to an invisible anchor — surface the widget first
                if (!IsVisible) ShowFromTray();
                CommandBarFeature.Summon(this);
            }
            return IntPtr.Zero;
        }

        /// <summary>Full rebuild after settings change (language, style, layout, visibility).</summary>
        public void Reload()
        {
            _lastPrayerLineMin = -1;
            _lastWeatherMin = -1;   // the rebuilt line starts empty; force a repaint on the next tick
            ForceRepaint();
            BuildUI();
            SetupTray();
            if (_hwnd != IntPtr.Zero) RegisterSummonHotkey();
            // the tree was just built for the current power-saving state, so the tick below
            // must not read a stale flag and immediately rebuild it again
            _saverApplied = Settings.SaverActive;
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
            foreach (var plate in _plates)
            {
                var b = Theme.CardBg.Clone();
                // the bezel sits a touch more transparent than the clock core, so the core
                // reads as the raised centre and the ring as the dial around it
                b.Opacity = Settings.I.BgOpacity * 0.94;
                if (b.CanFreeze) b.Freeze();
                plate.Fill = b;
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

        /// <summary>Forces the next tick to repaint everything. Called after a rebuild and
        /// whenever the widget comes back into view, where the cached per-minute markers no
        /// longer describe what is actually on screen.</summary>
        public void ForceRepaint()
        {
            _repaintPending = true;
            _lastPaintedMin = -1;
            _coverChecked = DateTime.MinValue;
            _covered = false;
        }

        /// <summary>Is the widget worth drawing at all right now? Hidden from the tray,
        /// minimised, or completely covered by the foreground window all answer no. The
        /// occlusion probe runs at most every two seconds — it is three cheap syscalls, but
        /// there is no reason to pay for them sixty times a minute.</summary>
        bool CanBeSeen(DateTime now)
        {
            if (!IsVisible) return false;
            // "Show widget" lifts the widget above everything for a couple of seconds without
            // activating it, so the app that still owns the foreground would otherwise be read
            // as covering it — and the user would be shown a stale face at the exact moment
            // they asked to see it.
            if (_flashOnTop || Topmost) return true;
            if ((now - _coverChecked).TotalSeconds >= 2)
            {
                _coverChecked = now;
                _covered = Win32.IsCovered(_hwnd);
            }
            return !_covered;
        }

        /// <summary>Does anything on the face actually differ from one second to the next?
        /// Only the seconds readout and the two decorative rotations do; every other element
        /// moves on the minute. With all three off the widget repaints sixty times an hour
        /// instead of three thousand six hundred — the same picture, a sixtieth of the work.</summary>
        bool NeedsSecondCadence()
        {
            if (Settings.I.ShowSeconds) return true;
            if (Settings.SaverActive) return false;
            return HaloBrushes.Halo != null || SkyBodies.HasSpin;
        }

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

            // ---- work that happens whether or not a single pixel is drawn ----
            // Alerts are the whole point of a prayer widget: they must fire while it sits
            // buried under a full-screen window exactly as they do in plain sight.
            PrayerFeature.MinuteTick(now);
            FocusFeature.BackgroundTick(now);
            StickyNoteFeature.BackgroundTick(now);   // note reminders fire whether the panel is open or not
            UpdateFeature.BackgroundTick();          // cheap gate; one tiny request every few hours
            Cinema.OnMinute(_cards, now);            // the minute glint (cinematic motion only)
            if (now.Minute != _lastWeatherFetchMin)
            {
                _lastWeatherFetchMin = now.Minute;
                WeatherFeature.EnsureFresh();        // no-op unless enabled, located and stale
            }

            // "Show desktop" (Win+D) minimizes even tool windows; restore automatically
            if (WindowState != WindowState.Normal && IsVisible)
            {
                WindowState = WindowState.Normal;
                Win32.SendToBottom(_hwnd);
            }

            // Power saving followed the charger out or in. The blur ramp is baked into the
            // visual tree, so this one costs a rebuild — which is fine: it happens when you
            // unplug, not on a timer.
            bool saver = Settings.SaverActive;
            if (saver != _saverApplied)
            {
                _saverApplied = saver;
                Theme.InvalidateEffects();
                Reload();
                return;
            }

            // ---- everything below this line paints ----
            //
            // AllowsTransparency makes this a layered window, which takes it off the GPU:
            // Windows composes it on the CPU through UpdateLayeredWindow. A repaint is
            // therefore never free, and one nobody can see is pure heat. Two gates follow —
            // is anyone looking, and has anything actually changed.
            if (!CanBeSeen(now)) { _repaintPending = true; return; }

            if (!_repaintPending && now.Minute == _lastPaintedMin && !NeedsSecondCadence()) return;

            if (_repaintPending)
            {
                // back from hidden or covered: the "already drawn this minute" markers no
                // longer describe what is on screen, so let every line paint once.
                _repaintPending = false;
                _lastPrayerLineMin = _lastWeatherMin = _lastOccasionMin = -1;
                _dateKey = null;
            }
            _lastPaintedMin = now.Minute;

            if (_clock != null) _clock.Tick(now);

            if (!saver)
            {
                // Halo layout: revolve the dashed ring one step per painted tick (1° a tick =
                // 6 min/rev — a slow drift you only notice glancing back after a while)
                if (HaloBrushes.Halo != null && HaloBrushes.Halo.RenderTransform is RotateTransform rt)
                    rt.Angle = (rt.Angle + 1.0) % 360.0;
                // the sun's rays turn the same way, and by the same one degree
                SkyBodies.Advance();
            }

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
                UpdateWeatherLine();            // the fetch itself ran above, gate or no gate
            }

            // seasonal occasion line — refresh per minute: during Ramadan it carries a
            // live H:MM countdown that must tick along like the weather line
            if (now.Minute != _lastOccasionMin)
            {
                _lastOccasionMin = now.Minute;
                UpdateOccasionLine(now);
            }
        }

        /// <summary>Paints the widget's occasion line from OccasionCore.SeasonalLine:
        /// Ramadan countdown when in season, Eid greeting on the days themselves, else the
        /// nearest builtin occasion. Collapsed entirely when occasions are off or there is
        /// genuinely nothing to say.</summary>
        void UpdateOccasionLine(DateTime now)
        {
            if (_occasionLine == null) return;
            if (!Settings.I.ShowOccasions) { _occasionLine.Visibility = Visibility.Collapsed; return; }
            var times = DeskMate.Features.PrayerFeature.PublicToday();
            var info = Logic.OccasionCore.SeasonalLine(now, times, L.IsAr, Settings.I.ShowOccasions);
            if (info == null || string.IsNullOrEmpty(info.Text))
            {
                _occasionLine.Visibility = Visibility.Collapsed;
                return;
            }
            // split leading emoji/glyph (🌙 ☪ 🕋 …) into the symbol-font run
            string text = info.Text.TrimStart();
            int split = 0;
            while (split < text.Length && !char.IsLetterOrDigit(text[split]) && text[split] != '٪')
                split++;
            _occasionGlyph.Text = split > 0 ? text.Substring(0, split).Trim() : "";
            _occasionGlyph.Visibility = split > 0 ? Visibility.Visible : Visibility.Collapsed;
            _occasionText.Text = split > 0 ? text.Substring(split).TrimStart() : text;
            _occasionLine.Visibility = Visibility.Visible;
        }

        /// <summary>Fills every status line right away (normally they wait for the first
        /// tick). Layouts that size themselves around their content call this before
        /// measuring, so a circle is cut for the lines that will actually be there.</summary>
        void PrimeStatusLines(DateTime now)
        {
            try
            {
                if (Settings.I.ShowPrayer && Settings.I.ShowNextPrayerLine)
                {
                    string line = PrayerFeature.NextLine(now);
                    _prayerLine.Text = line;
                    _prayerLine.Visibility = string.IsNullOrEmpty(line) ? Visibility.Collapsed : Visibility.Visible;
                }
                UpdateWeatherLine();
                UpdateOccasionLine(now);
            }
            catch (Exception ex) { Log.Error("prime lines", ex); }
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
            string temp = Math.Round(w.Temp).ToString(System.Globalization.CultureInfo.InvariantCulture) + "°";
            // The text runs left-to-right in both languages so the degree sign stays glued to
            // its number (inside an RTL run the bidi rules floated it to the wrong side).
            // Arabic reads from the right: glyph, temperature, then the city — so the city is
            // written first here and lands on the left.
            _weatherText.FlowDirection = FlowDirection.LeftToRight;
            _weatherText.Text = string.IsNullOrEmpty(city) ? temp
                : L.IsAr ? city + "  ·  " + temp : temp + "  ·  " + city;
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
            ForceRepaint();     // it was invisible a moment ago; nothing on it is current
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
            // a leaked global hotkey would keep answering Win+Alt+D with a dead window
            if (_hwnd != IntPtr.Zero) Win32.UnregisterSummonHotKey(_hwnd, HotkeyIdCommand);
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            Application.Current.Shutdown();
        }
    }
}
