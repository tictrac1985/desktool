using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>Shared chrome for all feature popups: glass card with a glyph-chip header
    /// near the widget, topmost while open, closes on deactivate. One panel at a time.</summary>
    public class PanelWindow : Window
    {
        static PanelWindow _current;
        static string _currentKey;

        /// <summary>Currently open panel, if any (used by tests and callers that reposition).</summary>
        public static Window Current { get { return _current; } }

        readonly Grid _contentHost = new Grid();
        Window _owner;

        /// <summary>The icon whose click is about to open a panel. Set by the tool cell right
        /// before it invokes the feature, consumed once by <see cref="Toggle"/>. A field rather
        /// than a parameter so all fourteen features keep their one-argument Open(owner).</summary>
        public static FrameworkElement PendingAnchor;

        FrameworkElement _anchor;
        FrameworkElement _caret;
        /// <summary>True when the panel sits below its anchor, so the caret points up.</summary>
        bool _caretUp = true;

        /// <summary>Set while a modal dialog owned by this panel is open, so losing
        /// activation to the dialog does not auto-close the panel (and kill the dialog).</summary>
        public bool SuppressAutoClose;

        static string GlyphFor(string key)
        {
            switch (key)
            {
                case "cleaner": return Glyphs.Cleaner;
                case "calc": return Glyphs.Calc;
                case "wallpaper": return Glyphs.Wallpaper;
                case "power": return Glyphs.Power;
                case "clipboard": return Glyphs.Clipboard;
                case "weather": return Glyphs.Weather;
                case "apps": return Glyphs.Apps;
                case "prayer": return Glyphs.Prayer;
                case "settings": return Glyphs.SettingsGear;
                case "focus": return Glyphs.Focus;
                case "occasions": return Glyphs.Occasions;
                case "note": return Glyphs.Note;
                case "sysmon": return Glyphs.SysMon;
                case "worldclock": return Glyphs.WorldClock;
                default: return "";
            }
        }

        PanelWindow(string key, string title, FrameworkElement content, double width)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowActivated = true;
            FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            // implicit theme-matched scrollbars for everything inside the panel
            Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar), ControlStyles.Scroll());

            var stack = new StackPanel { Width = width };

            // header: close circle · glyph chip + title
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var close = new Border
            {
                Width = 26, Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Theme.ButtonBg,
                Child = Theme.MakeGlyph("\uE711", 10),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Theme.AttachHover(close, Theme.ButtonBg, Theme.HoverBg);
            // handle Down too — otherwise the window's DragMove swallows the click before Up fires
            close.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            close.MouseLeftButtonUp += (s, e) => { e.Handled = true; Close(); };
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);

            string glyph = GlyphFor(key);
            if (!string.IsNullOrEmpty(glyph))
            {
                var chip = Theme.MakeGlyphChip(glyph, 26);
                chip.Margin = new Thickness(0, 0, 9, 0);
                DockPanel.SetDock(chip, Dock.Left);
                header.Children.Add(chip);
            }
            var titleText = Theme.MakeText(title, 14.5, Theme.Text);
            titleText.FontWeight = FontWeights.SemiBold;
            titleText.VerticalAlignment = VerticalAlignment.Center;
            header.Children.Add(titleText);
            stack.Children.Add(header);
            stack.Children.Add(Theme.Divider(0, 10));

            _contentHost.Children.Add(content);
            stack.Children.Add(_contentHost);

            var card = Theme.MakeCard(stack, 18);

            // A tail pointing at the icon reads as "this panel belongs to that button".
            // It is a real triangle drawn OVER the card edge, not a square tucked behind it:
            // the card is translucent glass, so anything hidden underneath shows straight
            // through, and the card's 1px border would cut across it. The figure is left
            // unclosed so WPF fills the triangle but strokes only the two slanted sides —
            // the base then merges into the card instead of drawing a line across it.
            _caret = new System.Windows.Shapes.Path
            {
                Fill = Theme.CardBg,
                Stroke = Theme.CardBorder,
                StrokeThickness = 1,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
            };

            // The root inherits the window's FlowDirection on purpose. Forcing it to LTR and
            // flipping the card back nests two mirror transforms, which renders the whole
            // panel mirrored. PlaceCaret mirrors its own margin instead — one place to reason
            // about, and the visual tree stays a single direction throughout.
            var root = new Grid();
            root.Children.Add(card);
            root.Children.Add(_caret);   // above the card: it covers the border it grows out of
            Content = root;

            // quick, quiet entrance
            Opacity = 0;
            var slide = new TranslateTransform(0, 8);
            root.RenderTransform = slide;
            Loaded += (s, e) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
                // rise into place when below the anchor, settle down when above it, so the
                // motion always starts from the icon's side
                slide.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(_caretUp ? 8 : -8, 0, TimeSpan.FromMilliseconds(160))
                    { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            };

            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            Deactivated += (s, e) => { if (!SuppressAutoClose) { try { Close(); } catch { } } };
            Closed += (s, e) => { if (_current == this) { _current = null; _currentKey = null; } };
            SourceInitialized += (s, e) =>
            {
                var h = new WindowInteropHelper(this).Handle;
                Win32.MakeToolWindow(h);
            };
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
        }

        /// <summary>Opens (or toggles) a panel. <paramref name="build"/> receives the panel
        /// window so features can close it or trigger re-layout.</summary>
        public static void Toggle(Window owner, string key, string title, Func<PanelWindow, FrameworkElement> build, double width = 320)
        {
            // consume it immediately: a panel reopened later from the tray or from a settings
            // change must not inherit the anchor of whichever icon was clicked last
            var anchor = PendingAnchor;
            PendingAnchor = null;

            if (_current != null)
            {
                bool same = _currentKey == key;
                try { _current.Close(); } catch { }
                if (same) return;
            }
            PanelWindow p = null;
            FrameworkElement content;
            try
            {
                p = new PanelWindow(key, title, new Grid(), width);
                content = build(p);
            }
            catch (Exception ex)
            {
                Log.Error("panel:" + key, ex);
                return;
            }
            p._contentHost.Children.Clear();
            p._contentHost.Children.Add(content);
            p._owner = owner;
            p._anchor = anchor;
            _current = p;
            _currentKey = key;
            p.Loaded += (s, e) => p.PlaceNear(owner);
            p.Show();
            p.Activate();
        }

        public static void CloseCurrent()
        {
            if (_current != null) { try { _current.Close(); } catch { } }
        }

        /// <summary>Shadow padding baked into <see cref="Theme.MakeCard"/>: the visible card
        /// edge sits this far inside the window on every side.</summary>
        const double CardInset = 12;

        void PlaceNear(Window owner)
        {
            try
            {
                UpdateLayout();
                double w = ActualWidth, h = ActualHeight;
                // work area of the screen ACTUALLY showing the widget — SystemParameters.WorkArea
                // is the primary monitor only, so panels opened next to a widget on a second
                // screen jumped back to the primary one
                var wa = WorkAreaOf(owner);

                // anchor to the icon that was clicked; fall back to the whole widget for panels
                // reopened without one (tray menu, or a settings change re-running Open)
                Rect a = AnchorRect(owner);

                // centre on the anchor, then keep the card fully on screen. The clamp is what
                // stops a panel opened from an edge icon hanging off the monitor.
                double left = a.Left + (a.Width - w) / 2;
                left = Math.Max(wa.Left - CardInset + 4, Math.Min(left, wa.Right - w + CardInset - 4));

                // prefer below the anchor; flip above when it would not fit
                double below = a.Bottom - CardInset + 2;
                double above = a.Top - h + CardInset - 2;
                _caretUp = true;
                double top = below;
                if (below + h > wa.Bottom + CardInset - 4 && above >= wa.Top - CardInset + 4)
                {
                    top = above;
                    _caretUp = false;
                }
                top = Math.Max(wa.Top - CardInset + 4, Math.Min(top, wa.Bottom - h + CardInset - 4));

                Left = left;
                Top = top;
                PlaceCaret(a, left, w, h);
            }
            catch { }
        }

        /// <summary>Screen rect of the clicked icon in the same units as Window.Left/Top.
        /// PointToScreen returns physical pixels, so it is divided by the window's own DPI —
        /// matching how <see cref="WorkAreaOf"/> converts, which is what keeps the two
        /// comparable on a per-monitor-DPI setup. Both corners are transformed because an
        /// element inside an RTL widget has its local origin on the visual right.</summary>
        Rect AnchorRect(Window owner)
        {
            var el = _anchor;
            if (el != null && el.IsVisible && PresentationSource.FromVisual(el) != null)
            {
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(el);
                    Point p1 = el.PointToScreen(new Point(0, 0));
                    Point p2 = el.PointToScreen(new Point(el.ActualWidth, el.ActualHeight));
                    double x1 = Math.Min(p1.X, p2.X) / dpi.DpiScaleX;
                    double x2 = Math.Max(p1.X, p2.X) / dpi.DpiScaleX;
                    double y1 = Math.Min(p1.Y, p2.Y) / dpi.DpiScaleY;
                    double y2 = Math.Max(p1.Y, p2.Y) / dpi.DpiScaleY;
                    if (x2 > x1 && y2 > y1) return new Rect(x1, y1, x2 - x1, y2 - y1);
                }
                catch { /* detached by a Reload mid-click — fall through to the widget */ }
            }
            return new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);
        }

        /// <summary>Points the caret at the anchor's centre, clamped away from the rounded
        /// corners so it never straddles one.</summary>
        void PlaceCaret(Rect anchor, double windowLeft, double w, double h)
        {
            if (_caret == null) return;
            if (_anchor == null) { _caret.Visibility = Visibility.Collapsed; return; }

            const double tw = 17, th = 8;   // tail footprint
            double centre = anchor.Left + anchor.Width / 2 - windowLeft;
            double min = CardInset + 22, max = w - CardInset - 22;
            if (max <= min) { _caret.Visibility = Visibility.Collapsed; return; }
            centre = Math.Max(min, Math.Min(centre, max));

            // HorizontalAlignment.Left measures from the visual right under RTL, so the offset
            // is mirrored here rather than by forcing the tree into one direction
            double fromLeft = centre - tw / 2;
            double offset = FlowDirection == FlowDirection.RightToLeft ? w - fromLeft - tw : fromLeft;

            // the base overlaps the card by 1px so it hides the border segment it sits on
            var fig = new PathFigure { IsClosed = false, IsFilled = true };
            if (_caretUp)
            {
                fig.StartPoint = new Point(0, th);
                fig.Segments.Add(new LineSegment(new Point(tw / 2, 0), true));
                fig.Segments.Add(new LineSegment(new Point(tw, th), true));
            }
            else
            {
                fig.StartPoint = new Point(0, 0);
                fig.Segments.Add(new LineSegment(new Point(tw / 2, th), true));
                fig.Segments.Add(new LineSegment(new Point(tw, 0), true));
            }
            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            ((System.Windows.Shapes.Path)_caret).Data = geo;

            double top = _caretUp ? CardInset - th + 1 : h - CardInset - 1;
            _caret.Margin = new Thickness(offset, top, 0, 0);
            _caret.Visibility = Visibility.Visible;
        }

        /// <summary>Work area (in WPF device-independent units) of the monitor that hosts
        /// the given window. Screen.WorkArea is in physical pixels, so it is converted with
        /// the window's own DPI (the app is PerMonitorV2-aware).</summary>
        public static Rect WorkAreaOf(Window w)
        {
            try
            {
                var h = new WindowInteropHelper(w).Handle;
                var r = System.Windows.Forms.Screen.FromHandle(h).WorkingArea;
                var dpi = VisualTreeHelper.GetDpi(w);
                return new Rect(
                    r.Left * 96.0 / dpi.PixelsPerInchX, r.Top * 96.0 / dpi.PixelsPerInchY,
                    r.Width * 96.0 / dpi.PixelsPerInchX, r.Height * 96.0 / dpi.PixelsPerInchY);
            }
            catch { return SystemParameters.WorkArea; }
        }
    }
}
