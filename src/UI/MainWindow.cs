using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Features;
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
    }

    public class MainWindow : Window
    {
        public static MainWindow Instance;
        /// <summary>Set when Windows is shutting down/logging off so Closing doesn't cancel.</summary>
        public static bool OsSessionEnding;

        readonly DispatcherTimer _timer = new DispatcherTimer();
        IClockView _clock;
        TextBlock _prayerLine;
        WF.NotifyIcon _tray;
        IntPtr _hwnd;
        bool _reallyExiting;
        bool _flashOnTop;
        int _lastPrayerLineMin = -1;

        FrameworkElement _rootEl;                       // scaled content root
        readonly List<Border> _cards = new List<Border>();
        Border _clockCard;                              // for connector lines
        Canvas _connectors;
        readonly List<FrameworkElement> _floatIcons = new List<FrameworkElement>();
        TextBlock _gregText, _hijriText;                // tri-panel side cards
        string _dateKey;

        Point _gripStart;
        double _gripStartScale;
        bool _scaling;

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

        // ---------------- UI construction ----------------

        public void BuildUI()
        {
            FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _cards.Clear();
            _floatIcons.Clear();
            _clockCard = null;
            _connectors = null;
            _gregText = null;
            _hijriText = null;
            _dateKey = null;

            _prayerLine = Theme.MakeText("", 12.5, Theme.Accent);
            _prayerLine.HorizontalAlignment = HorizontalAlignment.Center;
            _prayerLine.Margin = new Thickness(0, 5, 0, 0);
            _prayerLine.Visibility = Visibility.Collapsed;

            switch (Settings.I.LayoutIndex)
            {
                case 1: _rootEl = BuildConnected(); break;
                case 2: _rootEl = BuildTriple(); break;
                case 3: _rootEl = BuildOrbit(); break;
                case 4: _rootEl = BuildSidebar(); break;
                case 5: _rootEl = BuildDock(); break;
                default: _rootEl = BuildClassic(); break;
            }
            _rootEl.LayoutTransform = new ScaleTransform(Settings.I.Scale, Settings.I.Scale);

            var host = new Grid();
            host.Children.Add(_rootEl);
            host.Children.Add(BuildScaleGrip());
            host.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement) { try { DragMove(); SavePosition(); } catch { } }
            };
            Content = host;

            ApplyCardOpacity();
            if (_clock != null) _clock.Tick(DateTime.Now);
            ScheduleConnectorDraw();
        }

        IClockView MakeClock(bool timeOnly)
        {
            if (timeOnly) return new TimeOnlyClock();
            switch (Settings.I.ClockStyle)
            {
                case 1: return new AnalogClock();
                case 2: return new MinimalClock();
                default: return new DigitalClock();
            }
        }

        Border MakeCardTracked(UIElement content, double radius = 24)
        {
            var c = Theme.MakeCard(content, radius);
            _cards.Add(c);
            return c;
        }

        /// <summary>Layout 0 — everything in one card (the original look).</summary>
        FrameworkElement BuildClassic()
        {
            _clock = MakeClock(false);
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(_prayerLine);
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = Theme.CardBorder,
                Margin = new Thickness(4, 12, 4, 0),
            });
            var iconBar = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                MaxWidth = 430,
            };
            foreach (var ic in BuildIcons(false)) iconBar.Children.Add(ic);
            stack.Children.Add(iconBar);
            return MakeCardTracked(stack);
        }

        /// <summary>Layout 1 — clock card with rays connecting to free-floating icons.</summary>
        FrameworkElement BuildConnected()
        {
            _clock = MakeClock(false);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(_prayerLine);
            _clockCard = MakeCardTracked(inner);
            _clockCard.Margin = new Thickness(12, 12, 12, 2);

            _connectors = new Canvas { Height = 26, IsHitTestVisible = false, ClipToBounds = false };

            var iconsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(14, 0, 14, 12),
            };
            foreach (var ic in BuildIcons(true)) { iconsRow.Children.Add(ic); _floatIcons.Add(ic); }

            var root = new StackPanel();
            root.Children.Add(_clockCard);
            root.Children.Add(_connectors);
            root.Children.Add(iconsRow);
            return root;
        }

        /// <summary>Layout 2 — big time in the middle, Hijri card on one side and
        /// Gregorian on the other, icon pill underneath.</summary>
        FrameworkElement BuildTriple()
        {
            _clock = MakeClock(true);
            var centerInner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            centerInner.Children.Add(_clock.Root);
            centerInner.Children.Add(_prayerLine);
            var centerCard = MakeCardTracked(centerInner, 26);
            centerCard.Margin = new Thickness(4, 12, 4, 6);

            var hijriCard = MakeDateCard("", out _hijriText);   // crescent
            var gregCard = MakeDateCard("", out _gregText);     // calendar
            if (!Settings.I.ShowHijri) hijriCard.Visibility = Visibility.Collapsed;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            // added first = starts at the visual right in RTL → Hijri right, Gregorian left
            row.Children.Add(hijriCard);
            row.Children.Add(centerCard);
            row.Children.Add(gregCard);

            var pillContent = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 470,
            };
            foreach (var ic in BuildIcons(false)) pillContent.Children.Add(ic);
            var pill = MakeCardTracked(pillContent, 30);
            pill.Padding = new Thickness(12, 4, 12, 4);
            pill.Margin = new Thickness(12, 0, 12, 12);
            pill.HorizontalAlignment = HorizontalAlignment.Center;

            var root = new StackPanel();
            root.Children.Add(row);
            root.Children.Add(pill);
            return root;
        }

        Border MakeDateCard(string glyph, out TextBlock text)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var g = Theme.MakeGlyph(glyph, 16, Theme.Accent);
            g.Margin = new Thickness(0, 0, 0, 5);
            stack.Children.Add(g);
            text = Theme.MakeText("", 12, Theme.TextDim);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.MaxWidth = 96;
            stack.Children.Add(text);
            var card = MakeCardTracked(stack, 18);
            card.Padding = new Thickness(12, 10, 12, 10);
            card.Margin = new Thickness(4, 12, 4, 6);
            card.VerticalAlignment = VerticalAlignment.Center;
            return card;
        }

        /// <summary>Layout 3 — circular clock card with icons orbiting around it.</summary>
        FrameworkElement BuildOrbit()
        {
            _clock = MakeClock(false);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(_prayerLine);
            var card = MakeCardTracked(inner);
            card.Margin = new Thickness(0);
            card.Padding = new Thickness(26);

            // measure, then size the circle so the content rectangle is fully inscribed
            // (diameter = diagonal of the measured content)
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double mw = card.DesiredSize.Width, mh = card.DesiredSize.Height;
            double size = Math.Sqrt(mw * mw + mh * mh);
            card.Width = size;
            card.Height = size;
            card.CornerRadius = new CornerRadius(size / 2);
            card.FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            var icons = BuildIcons(true);
            double ringR = size / 2 + 40;
            double pad = 44; // room for icon cells + labels outside the ring
            double canvasSize = 2 * (ringR + pad);
            var canvas = new Canvas
            {
                Width = canvasSize,
                Height = canvasSize,
                Background = Brushes.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(4),
            };
            double c = canvasSize / 2;

            var ringStroke = new SolidColorBrush(Color.FromArgb(0x34,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            ringStroke.Freeze();
            var ring = new Ellipse
            {
                Width = 2 * ringR,
                Height = 2 * ringR,
                Stroke = ringStroke,
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 3, 4 },
            };
            Canvas.SetLeft(ring, c - ringR);
            Canvas.SetTop(ring, c - ringR);
            canvas.Children.Add(ring);

            Canvas.SetLeft(card, c - size / 2);
            Canvas.SetTop(card, c - size / 2);
            canvas.Children.Add(card);

            for (int i = 0; i < icons.Count; i++)
            {
                double a = (-90 + i * 360.0 / icons.Count) * Math.PI / 180.0;
                double ix = c + ringR * Math.Cos(a);
                double iy = c + ringR * Math.Sin(a);
                // icon circle center sits at (22, 21) inside the 44x58 cell
                Canvas.SetLeft(icons[i], ix - 22);
                Canvas.SetTop(icons[i], iy - 21);
                canvas.Children.Add(icons[i]);
            }
            return canvas;
        }

        /// <summary>Layout 4 — vertical sidebar: compact clock on top, menu-style rows
        /// with always-visible names beneath.</summary>
        FrameworkElement BuildSidebar()
        {
            _clock = new MinimalClock();
            var stack = new StackPanel { MinWidth = 168 };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(_prayerLine);
            stack.Children.Add(new Border
            {
                Height = 1,
                Background = Theme.CardBorder,
                Margin = new Thickness(2, 10, 2, 6),
            });
            foreach (var def in IconDefs())
                stack.Children.Add(MakeSidebarRow(def.Item1, def.Item2, def.Item3));
            return MakeCardTracked(stack, 22);
        }

        FrameworkElement MakeSidebarRow(string glyph, string titleKey, Action onClick)
        {
            var row = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
                RenderTransform = new TranslateTransform(0, 0),
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var circle = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = Theme.ButtonBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                Child = Theme.MakeGlyph(glyph, 12),
            };
            content.Children.Add(circle);
            var name = Theme.MakeText(L.T(titleKey), 12.5, Theme.TextDim);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(9, 0, 4, 0);
            content.Children.Add(name);
            row.Child = content;

            var slide = (TranslateTransform)row.RenderTransform;
            row.MouseEnter += (s, e) =>
            {
                row.Background = Theme.HoverBg;
                circle.BorderBrush = Theme.Accent;
                name.Foreground = Theme.Text;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(L.IsAr ? -4 : 4, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeave += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                circle.BorderBrush = Theme.CardBorder;
                name.Foreground = Theme.TextDim;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                try { onClick(); } catch (Exception ex) { Log.Error("sidebar", ex); }
            };
            return row;
        }

        /// <summary>Layout 5 — slim horizontal dock pill: time + date on one side,
        /// zooming icons inline.</summary>
        FrameworkElement BuildDock()
        {
            _clock = new MinimalClock();
            var clockCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            clockCol.Children.Add(_clock.Root);
            clockCol.Children.Add(_prayerLine);
            clockCol.Margin = new Thickness(6, 0, 6, 0);

            var rowStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            rowStack.Children.Add(clockCol);
            rowStack.Children.Add(new Border
            {
                Width = 1,
                Height = 44,
                Background = Theme.CardBorder,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            foreach (var ic in BuildIcons(false))
            {
                ((FrameworkElement)ic).VerticalAlignment = VerticalAlignment.Center;
                rowStack.Children.Add(ic);
            }

            var pill = MakeCardTracked(rowStack, 40);
            pill.Padding = new Thickness(18, 8, 18, 6);
            return pill;
        }

        List<Tuple<string, string, Action>> IconDefs()
        {
            var s = Settings.I;
            var list = new List<Tuple<string, string, Action>>();
            if (s.ShowCleaner) list.Add(Tuple.Create(Glyphs.Cleaner, "f_cleaner", (Action)(() => CleanerFeature.Open(this))));
            if (s.ShowCalc) list.Add(Tuple.Create(Glyphs.Calc, "f_calc", (Action)(() => CalculatorFeature.Open(this))));
            if (s.ShowWallpaper) list.Add(Tuple.Create(Glyphs.Wallpaper, "f_wallpaper", (Action)(() => WallpaperFeature.Open(this))));
            if (s.ShowPower) list.Add(Tuple.Create(Glyphs.Power, "f_power", (Action)(() => PowerFeature.Open(this))));
            if (s.ShowClipboard) list.Add(Tuple.Create(Glyphs.Clipboard, "f_clipboard", (Action)(() => ClipboardKeeper.Open(this))));
            if (s.ShowWeather) list.Add(Tuple.Create(Glyphs.Weather, "f_weather", (Action)(() => WeatherFeature.Open(this))));
            if (s.ShowApps) list.Add(Tuple.Create(Glyphs.Apps, "f_apps", (Action)(() => AppsFeature.Open(this))));
            if (s.ShowPrayer) list.Add(Tuple.Create(Glyphs.Prayer, "f_prayer", (Action)(() => PrayerFeature.Open(this))));
            list.Add(Tuple.Create(Glyphs.SettingsGear, "f_settings", (Action)(() => SettingsFeature.Open(this))));
            return list;
        }

        // ---------------- icons ----------------

        List<FrameworkElement> BuildIcons(bool floating)
        {
            var s = Settings.I;
            var list = new List<FrameworkElement>();
            if (s.ShowCleaner) list.Add(MakeAnimatedIcon("", "f_cleaner", () => CleanerFeature.Open(this), floating));
            if (s.ShowCalc) list.Add(MakeAnimatedIcon("", "f_calc", () => CalculatorFeature.Open(this), floating));
            if (s.ShowWallpaper) list.Add(MakeAnimatedIcon("", "f_wallpaper", () => WallpaperFeature.Open(this), floating));
            if (s.ShowPower) list.Add(MakeAnimatedIcon("", "f_power", () => PowerFeature.Open(this), floating));
            if (s.ShowClipboard) list.Add(MakeAnimatedIcon("", "f_clipboard", () => ClipboardKeeper.Open(this), floating));
            if (s.ShowWeather) list.Add(MakeAnimatedIcon("", "f_weather", () => WeatherFeature.Open(this), floating));
            if (s.ShowApps) list.Add(MakeAnimatedIcon("", "f_apps", () => AppsFeature.Open(this), floating));
            if (s.ShowPrayer) list.Add(MakeAnimatedIcon("", "f_prayer", () => PrayerFeature.Open(this), floating));
            list.Add(MakeAnimatedIcon("", "f_settings", () => SettingsFeature.Open(this), floating));
            return list;
        }

        /// <summary>Circular icon that zooms on hover and reveals its name underneath.</summary>
        FrameworkElement MakeAnimatedIcon(string glyph, string titleKey, Action onClick, bool floating)
        {
            var circle = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = floating ? Theme.CardBg : Theme.ButtonBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                Child = Theme.MakeGlyph(glyph, 15),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var scale = new ScaleTransform(1, 1);
            circle.RenderTransform = scale;
            if (floating)
            {
                circle.Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 10, ShadowDepth = 2,
                    Opacity = Theme.IsLight ? 0.25 : 0.45, Direction = 270,
                };
            }

            var label = new TextBlock
            {
                Text = L.T(titleKey),
                FontSize = 10,
                FontFamily = Theme.UiFont,
                Foreground = Theme.TextDim,
                Opacity = 0,
                Width = 84,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(-20, 0, -20, 0),
                IsHitTestVisible = false,
            };

            var cell = new Grid
            {
                Width = 44,
                Height = 58,
                Margin = new Thickness(1, 0, 1, 0),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
            };
            cell.Children.Add(circle);
            cell.Children.Add(label);
            Panel.SetZIndex(label, 5);

            var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            cell.MouseEnter += (s, e) =>
            {
                circle.BorderBrush = Theme.Accent;
                var a = new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(140)) { EasingFunction = easeIn };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                label.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(160)));
            };
            cell.MouseLeave += (s, e) =>
            {
                circle.BorderBrush = Theme.CardBorder;
                var a = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140)) { EasingFunction = easeIn };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, a);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, a);
                label.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(140)));
            };
            cell.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                try { onClick(); } catch (Exception ex) { Log.Error("icon", ex); }
            };
            return cell;
        }

        // ---------------- connector rays (layout 1) ----------------

        void ScheduleConnectorDraw()
        {
            if (_connectors == null) return;
            Dispatcher.BeginInvoke(new Action(DrawConnectors), DispatcherPriority.Loaded);
        }

        void DrawConnectors()
        {
            if (_connectors == null || _clockCard == null) return;
            try
            {
                _connectors.Children.Clear();
                double h = _connectors.Height;
                Point from = _clockCard.TranslatePoint(
                    new Point(_clockCard.ActualWidth / 2, _clockCard.ActualHeight - 12), _connectors);
                var stroke = new SolidColorBrush(Color.FromArgb(0x58,
                    Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
                stroke.Freeze();
                foreach (var ic in _floatIcons)
                {
                    Point to = ic.TranslatePoint(new Point(ic.ActualWidth / 2, 4), _connectors);
                    _connectors.Children.Add(new Line
                    {
                        X1 = from.X, Y1 = Math.Max(0, from.Y),
                        X2 = to.X, Y2 = Math.Min(h + 6, to.Y),
                        Stroke = stroke,
                        StrokeThickness = 1.2,
                    });
                    var dot = new Ellipse { Width = 4, Height = 4, Fill = Theme.Accent };
                    Canvas.SetLeft(dot, to.X - 2);
                    Canvas.SetTop(dot, Math.Min(h + 4, to.Y) - 2);
                    _connectors.Children.Add(dot);
                }
            }
            catch (Exception ex) { Log.Error("connectors", ex); }
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

        // ---------------- scale grip ----------------

        Border BuildScaleGrip()
        {
            var lines = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 0,9 L 9,0 M 4.5,9 L 9,4.5"),
                Stroke = Theme.TextFaint,
                StrokeThickness = 1.6,
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var grip = new Border
            {
                Width = 22,
                Height = 22,
                Background = Brushes.Transparent,
                Child = lines,
                Cursor = Cursors.SizeNWSE,
                VerticalAlignment = VerticalAlignment.Bottom,
                // keep the grip at the VISUAL bottom-right in both flow directions
                HorizontalAlignment = L.IsAr ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(2),
            };
            grip.MouseEnter += (s, e) => lines.Stroke = Theme.Accent;
            grip.MouseLeave += (s, e) => lines.Stroke = Theme.TextFaint;
            grip.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _scaling = true;
                _gripStartScale = Settings.I.Scale;
                _gripStart = PointToScreen(e.GetPosition(this));
                grip.CaptureMouse();
            };
            grip.MouseMove += (s, e) =>
            {
                if (!_scaling) return;
                var cur = PointToScreen(e.GetPosition(this));
                double delta = (cur.X - _gripStart.X) + (cur.Y - _gripStart.Y);
                double ns = Math.Max(0.6, Math.Min(1.8, _gripStartScale + delta / 280.0));
                Settings.I.Scale = ns;
                if (_rootEl != null) _rootEl.LayoutTransform = new ScaleTransform(ns, ns);
            };
            grip.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (!_scaling) return;
                _scaling = false;
                grip.ReleaseMouseCapture();
                Settings.I.Save();
            };
            grip.LostMouseCapture += (s, e) =>
            {
                if (_scaling) { _scaling = false; Settings.I.Save(); }
            };
            return grip;
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
