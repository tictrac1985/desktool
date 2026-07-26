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
                Child = Theme.MakeGlyph("", 10),
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
            Content = card;

            // quick, quiet entrance
            Opacity = 0;
            var slide = new TranslateTransform(0, 8);
            card.RenderTransform = slide;
            Loaded += (s, e) =>
            {
                BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
                slide.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(0, TimeSpan.FromMilliseconds(160))
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

        void PlaceNear(Window owner)
        {
            try
            {
                UpdateLayout();
                double w = ActualWidth, h = ActualHeight;
                var wa = SystemParameters.WorkArea;
                double left = owner.Left + (owner.ActualWidth - w) / 2;
                left = Math.Max(wa.Left + 4, Math.Min(left, wa.Right - w - 4));
                double top = owner.Top + owner.ActualHeight - 6;
                if (top + h > wa.Bottom - 4) top = owner.Top - h + 6;
                if (top < wa.Top + 4) top = wa.Top + 4;
                Left = left;
                Top = top;
            }
            catch { }
        }
    }
}
