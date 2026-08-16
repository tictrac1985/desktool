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
        TranslateTransform _slide;
        /// <summary>Which edge of the card carries the caret: 0 = top edge pointing up
        /// (panel below the anchor), 1 = bottom edge pointing down, 2 = left edge pointing
        /// left (panel right of the widget), 3 = right edge pointing right (panel left of
        /// the widget). Side carets are how panels opened from a vertical sidebar hug the
        /// widget instead of covering it.</summary>
        int _caretSide = 0;

        /// <summary>Slides the card in from the anchor's direction. Called by
        /// <see cref="PlaceNear"/> after <see cref="_caretSide"/> is final.</summary>
        void PlayEntrance()
        {
            if (_slide == null) return;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(160);
            if (_caretSide >= 2)
                _slide.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(_caretSide == 3 ? -8 : 8, 0, dur) { EasingFunction = ease });
            else
                _slide.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(_caretSide == 0 ? 8 : -8, 0, dur) { EasingFunction = ease });
        }

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
                case "command": return Glyphs.Command;
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

            // quick, quiet entrance — the motion always starts from the icon's side.
            // Opacity fades in on load; the slide is started by PlaceNear once the caret
            // side (and therefore the direction) is known.
            Opacity = 0;
            _slide = new TranslateTransform(0, 0);
            root.RenderTransform = _slide;
            Loaded += (s, e) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
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
                Rect wr = new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight);

                double minLeft = wa.Left - CardInset + 4, maxRight = wa.Right + CardInset - 4;
                double minTop = wa.Top - CardInset + 4, maxBottom = wa.Bottom + CardInset - 4;

                // A tall widget (the Sidebar) owns its strip of screen from top to bottom:
                // a panel opening above or below the clicked row lands ON the widget and
                // covers the neighbours the user is about to click. Beside it there is a
                // whole empty desktop — so side placement is the rule for vertical widgets,
                // and the rescue for any panel that would otherwise overlap the widget.
                bool verticalWidget = wr.Height > wr.Width * 1.25;
                if (verticalWidget && TryBeside(wr, a, w, h, minLeft, maxRight, minTop, maxBottom))
                {
                    PlayEntrance();
                    return;
                }

                // centre on the anchor, then keep the card fully on screen. The clamp is what
                // stops a panel opened from an edge icon hanging off the monitor.
                double left = a.Left + (a.Width - w) / 2;
                left = Math.Max(minLeft, Math.Min(left, maxRight - w));

                // prefer below the anchor; flip above when it would not fit
                double below = a.Bottom - CardInset + 2;
                double above = a.Top - h + CardInset - 2;
                _caretSide = 0;
                double top = below;
                if (below + h > maxBottom && above >= minTop)
                {
                    top = above;
                    _caretSide = 1;
                }
                top = Math.Max(minTop, Math.Min(top, maxBottom - h));

                Left = left;
                Top = top;

                // overlap rescue: compact widgets (Orbit, or a Classic card with centre-row
                // icons) can still end up under the panel — slide beside the widget when a
                // side has room, so the panel never hides the tool row it came from
                var placed = new Rect(Left, Top, w, h);
                if (!verticalWidget && OverlapArea(placed, wr) > w * h * 0.2)
                    TryBeside(wr, a, w, h, minLeft, maxRight, minTop, maxBottom);

                if (_caretSide < 2) PlaceCaret(a, Left, w, h);
                PlayEntrance();
            }
            catch { }
        }

        static double OverlapArea(Rect a, Rect b)
        {
            double x = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
            double y = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
            return x * y;
        }

        /// <summary>Places the panel beside the widget: the widget's screen-left side when it
        /// fits there (a sidebar usually hugs the screen's right edge), the right side
        /// otherwise. The panel's vertical centre follows the clicked icon. Returns false
        /// when neither side has room — the caller then falls back to below/above.</summary>
        bool TryBeside(Rect wr, Rect a, double w, double h,
                       double minLeft, double maxRight, double minTop, double maxBottom)
        {
            double top = a.Top + (a.Height - h) / 2;
            top = Math.Max(minTop, Math.Min(top, maxBottom - h));

            double leftSide = wr.Left - w + CardInset - 6;   // panel's right edge meets the widget
            double rightSide = wr.Right - CardInset + 6;
            if (leftSide >= minLeft)
            {
                Left = leftSide; Top = top; _caretSide = 3;
            }
            else if (rightSide + w <= maxRight)
            {
                Left = rightSide; Top = top; _caretSide = 2;
            }
            else return false;
            PlaceCaret(a, Left, w, h);
            return true;
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
        /// corners so it never straddles one. Handles all four edges: top/bottom for panels
        /// above or below the anchor, left/right for panels parked beside a vertical
        /// widget.</summary>
        void PlaceCaret(Rect anchor, double windowLeft, double w, double h)
        {
            if (_caret == null) return;
            if (_anchor == null) { _caret.Visibility = Visibility.Collapsed; return; }

            const double tw = 17, th = 8;   // tail footprint (length along the edge × depth)
            var fig = new PathFigure { IsClosed = false, IsFilled = true };
            double offset, top;

            if (_caretSide >= 2)
            {
                // caret on a vertical edge, apex pointing sideways at the widget
                double centre = anchor.Top + anchor.Height / 2 - Top;
                double min = CardInset + 22, max = h - CardInset - 22;
                if (max <= min) { _caret.Visibility = Visibility.Collapsed; return; }
                centre = Math.Max(min, Math.Min(centre, max));
                top = centre - tw / 2;

                if (_caretSide == 3)
                {
                    // on the screen-right edge, apex pointing right (panel left of widget)
                    fig.StartPoint = new Point(0, 0);
                    fig.Segments.Add(new LineSegment(new Point(th, tw / 2), true));
                    fig.Segments.Add(new LineSegment(new Point(0, tw), true));
                    double fromLeft = w - CardInset - th + 1;
                    offset = FlowDirection == FlowDirection.RightToLeft ? w - fromLeft - th : fromLeft;
                }
                else
                {
                    // on the screen-left edge, apex pointing left (panel right of widget)
                    fig.StartPoint = new Point(th, 0);
                    fig.Segments.Add(new LineSegment(new Point(0, tw / 2), true));
                    fig.Segments.Add(new LineSegment(new Point(th, tw), true));
                    double fromLeft = CardInset - th + 1;
                    offset = FlowDirection == FlowDirection.RightToLeft ? w - fromLeft - th : fromLeft;
                }
            }
            else
            {
                // caret on a horizontal edge, apex pointing up or down at the anchor
                double centre = anchor.Left + anchor.Width / 2 - windowLeft;
                double min = CardInset + 22, max = w - CardInset - 22;
                if (max <= min) { _caret.Visibility = Visibility.Collapsed; return; }
                centre = Math.Max(min, Math.Min(centre, max));

                // HorizontalAlignment.Left measures from the visual right under RTL, so the
                // offset is mirrored here rather than by forcing the tree into one direction
                double fromLeft = centre - tw / 2;
                offset = FlowDirection == FlowDirection.RightToLeft ? w - fromLeft - tw : fromLeft;

                if (_caretSide == 0)
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
                top = _caretSide == 0 ? CardInset - th + 1 : h - CardInset - 1;
            }

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            geo.Freeze();
            ((System.Windows.Shapes.Path)_caret).Data = geo;

            // the base overlaps the card by 1px so it hides the border segment it sits on
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
