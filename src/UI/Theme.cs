using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>Shared visual language. All brushes are (re)assigned by Apply(themeIndex);
    /// UI is rebuilt after a theme change so factories pick up the new palette.</summary>
    public static class Theme
    {
        public class Palette
        {
            public string KeyAr, KeyEn;
            public Color Accent;
            public Color CardTop, CardBottom;
            public bool Light;
        }

        public static readonly Palette[] Palettes =
        {
            new Palette { KeyAr = "كهرماني", KeyEn = "Amber",
                Accent = Color.FromRgb(0xFF, 0xC6, 0x6E),
                CardTop = Color.FromArgb(0xD6, 0x23, 0x2A, 0x3B), CardBottom = Color.FromArgb(0xDE, 0x0D, 0x11, 0x19) },
            new Palette { KeyAr = "أزرق", KeyEn = "Ocean",
                Accent = Color.FromRgb(0x6E, 0xC1, 0xFF),
                CardTop = Color.FromArgb(0xD6, 0x1D, 0x2D, 0x44), CardBottom = Color.FromArgb(0xDE, 0x0A, 0x12, 0x1E) },
            new Palette { KeyAr = "زمردي", KeyEn = "Emerald",
                Accent = Color.FromRgb(0x7B, 0xE3, 0xA6),
                CardTop = Color.FromArgb(0xD6, 0x19, 0x2D, 0x23), CardBottom = Color.FromArgb(0xDE, 0x08, 0x13, 0x0D) },
            new Palette { KeyAr = "وردي", KeyEn = "Rose",
                Accent = Color.FromRgb(0xFF, 0x9F, 0xB8),
                CardTop = Color.FromArgb(0xD6, 0x30, 0x1D, 0x2B), CardBottom = Color.FromArgb(0xDE, 0x17, 0x0B, 0x13) },
            new Palette { KeyAr = "فاتح", KeyEn = "Light", Light = true,
                Accent = Color.FromRgb(0xB8, 0x6B, 0x00),
                CardTop = Color.FromArgb(0xEE, 0xF8, 0xFB, 0xFE), CardBottom = Color.FromArgb(0xF2, 0xE0, 0xE7, 0xF1) },
        };

        /// <summary>Index of the palette that was actually applied (differs from the chosen
        /// one when auto day/night theme is active).</summary>
        public static int AppliedIndex { get; private set; }

        public static bool IsLight;
        public static Color AccentColor;
        public static Brush Accent, Text, TextDim, TextFaint, CardBg, CardBorder, HoverBg, ButtonBg, InputBg;
        public static Brush DialStroke, DialFill;
        /// <summary>Soft accent-tinted fill for chips, selected rows and operator keys.</summary>
        public static Brush AccentSoft;
        /// <summary>Slightly stronger border for buttons and keys.</summary>
        public static Brush ButtonBorder;
        /// <summary>Top-light gradient painted inside cards to fake glass thickness.</summary>
        public static Brush GlassTop;

        public static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        public static readonly FontFamily LightFont = new FontFamily("Segoe UI Light, Segoe UI");
        public static readonly FontFamily Mdl2 = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");

        static Theme() { Apply(0); }

        public static void Apply(int index)
        {
            if (index < 0 || index >= Palettes.Length) index = 0;
            AppliedIndex = index;
            var p = Palettes[index];
            IsLight = p.Light;
            AccentColor = p.Accent;
            Accent = Freeze(new SolidColorBrush(p.Accent));
            AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb((byte)(p.Light ? 0x20 : 0x24), p.Accent.R, p.Accent.G, p.Accent.B)));

            if (p.Light)
            {
                Text = Freeze(new SolidColorBrush(Color.FromArgb(0xE8, 0x16, 0x1E, 0x2A)));
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xAC, 0x16, 0x1E, 0x2A)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x68, 0x16, 0x1E, 0x2A)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x10, 0x18, 0x24)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3C, 0x10, 0x18, 0x24)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x10, 0x18, 0x24)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x13, 0x10, 0x18, 0x24)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0x10, 0x18, 0x24)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0x18, 0x24)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x10, 0x10, 0x18, 0x24)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x5A, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
            }
            else
            {
                Text = Brushes.White;
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2C, 0xFF, 0xFF, 0xFF)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x00, 0x00, 0x00)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
            }

            CardBg = Freeze(new LinearGradientBrush(p.CardTop, p.CardBottom, 90));
            ControlStyles.Invalidate();
        }

        static Brush Freeze(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }

        static Color Lift(Color c, int dr, int dg, int db)
        {
            return Color.FromRgb(
                (byte)Math.Min(255, c.R + dr), (byte)Math.Min(255, c.G + dg), (byte)Math.Min(255, c.B + db));
        }

        // ---------------- cards & chrome ----------------

        /// <summary>Glass card: gradient body + 1px edge + inner top light + soft shadow.</summary>
        public static Border MakeCard(UIElement content, double radius = 24)
        {
            var layers = new Grid();
            layers.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(Math.Max(1, radius - 1)),
                Background = GlassTop,
                IsHitTestVisible = false,
                Margin = new Thickness(1),
            });
            layers.Children.Add(content);
            return new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = new Thickness(18, 14, 18, 14),
                Child = layers,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 24, ShadowDepth = 3,
                    Opacity = IsLight ? 0.24 : 0.46, Direction = 270,
                },
                Margin = new Thickness(12), // room for the shadow inside the transparent window
            };
        }

        /// <summary>Full-width hairline used to separate panel sections.</summary>
        public static Border Divider(double topMargin = 8, double bottomMargin = 8)
        {
            return new Border
            {
                Height = 1,
                Background = CardBorder,
                Margin = new Thickness(0, topMargin, 0, bottomMargin),
            };
        }

        /// <summary>Small rounded-square accent-tinted badge holding a glyph (panel headers).</summary>
        public static Border MakeGlyphChip(string glyph, double size = 26)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.32),
                Background = AccentSoft,
                Child = MakeGlyph(glyph, size * 0.48, Accent),
            };
        }

        // ---------------- text ----------------

        public static TextBlock MakeText(string text, double size, Brush brush = null, FontFamily font = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = brush ?? Text,
                FontFamily = font ?? UiFont,
            };
        }

        public static TextBlock MakeGlyph(string glyph, double size, Brush brush = null)
        {
            return new TextBlock
            {
                Text = glyph,
                FontSize = size,
                Foreground = brush ?? Text,
                FontFamily = Mdl2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }

        // ---------------- tool cell: icon + permanent label ----------------

        /// <summary>The widget's signature button: a 40px circle with its name always
        /// visible underneath. Hover lifts the circle and lights glyph + label in the accent.
        /// cellWidth shrinks the cell for dense single-row layouts (label scales with it).</summary>
        public static FrameworkElement MakeToolCell(string glyph, string label, Action onClick, bool floating, double cellWidth = 58)
        {
            var glyphText = MakeGlyph(glyph, 16);
            var circle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = floating ? CardBg : ButtonBg,
                BorderBrush = ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = glyphText,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var lift = new TranslateTransform(0, 0);
            var zoom = new ScaleTransform(1, 1);
            var group = new TransformGroup();
            group.Children.Add(zoom);
            group.Children.Add(lift);
            circle.RenderTransform = group;
            if (floating)
            {
                circle.Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 10, ShadowDepth = 2,
                    Opacity = IsLight ? 0.25 : 0.45, Direction = 270,
                };
            }

            var name = new TextBlock
            {
                Text = label,
                FontSize = cellWidth < 52 ? 9.5 : 10.5,
                FontFamily = UiFont,
                Foreground = TextDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = cellWidth + 20,
                Margin = new Thickness(-10, 4, -10, 0),
            };

            var cell = new StackPanel
            {
                Width = cellWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(1, 0, 1, 0),
            };
            // StackPanel Background only paints with a panel hit-test surface
            cell.Children.Add(circle);
            cell.Children.Add(name);

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(130);
            cell.MouseEnter += (s, e) =>
            {
                circle.BorderBrush = Accent;
                glyphText.Foreground = Accent;
                name.Foreground = Accent;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.1, dur) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.1, dur) { EasingFunction = ease });
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, dur) { EasingFunction = ease });
            };
            cell.MouseLeave += (s, e) =>
            {
                circle.BorderBrush = ButtonBorder;
                glyphText.Foreground = Text;
                name.Foreground = TextDim;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
            };
            cell.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
            };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(90)));
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(90)));
                try { onClick(); } catch (Exception ex) { Log.Error("toolcell", ex); }
            };
            return cell;
        }

        // ---------------- buttons & inputs ----------------

        /// <summary>Round icon button used in panels (small actions).</summary>
        public static Border MakeIconButton(string glyph, string tooltip, Action onClick, double diameter = 36, double glyphSize = 15)
        {
            var b = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = new CornerRadius(diameter / 2),
                Background = ButtonBg,
                BorderBrush = ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = MakeGlyph(glyph, glyphSize),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            var normalBorder = ButtonBorder;
            var hoverBorder = Accent;
            b.MouseEnter += (s, e) => { b.Background = HoverBg; b.BorderBrush = hoverBorder; };
            b.MouseLeave += (s, e) => { b.Background = ButtonBg; b.BorderBrush = normalBorder; };
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("iconbtn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        /// <summary>Rounded text button for panels; accent variant gets a lifted accent gradient.</summary>
        public static Border MakeButton(string text, Action onClick, bool accent = false)
        {
            var tb = MakeText(text, 13, accent ? (IsLight ? Brushes.White : Brushes.Black) : Text);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal;
            Brush accentBg = Accent;
            if (accent)
                accentBg = Freeze(new LinearGradientBrush(Lift(AccentColor, 26, 22, 20), AccentColor, 90));
            var b = new Border
            {
                Background = accent ? accentBg : ButtonBg,
                BorderBrush = accent ? null : ButtonBorder,
                BorderThickness = accent ? new Thickness(0) : new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 7, 14, 7),
                Child = tb,
                Cursor = Cursors.Hand,
                Margin = new Thickness(3),
            };
            if (!accent) AttachHover(b, ButtonBg, HoverBg);
            else AttachHover(b, accentBg, Freeze(new SolidColorBrush(Lift(AccentColor, 40, 36, 34))));
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("btn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        public static void AttachHover(Border b, Brush normal, Brush hover)
        {
            b.MouseEnter += (s, e) => b.Background = hover;
            b.MouseLeave += (s, e) => b.Background = normal;
        }

        public static TextBox MakeInput(string initial = "")
        {
            var t = new TextBox
            {
                Text = initial,
                Background = InputBg,
                Foreground = Text,
                CaretBrush = Text,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 7, 9, 7),
                FontSize = 13,
                FontFamily = UiFont,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            t.GotKeyboardFocus += (s, e) => t.BorderBrush = Accent;
            t.LostKeyboardFocus += (s, e) => t.BorderBrush = CardBorder;
            return t;
        }

        public static ComboBox MakeCombo()
        {
            return new ComboBox
            {
                FontSize = 13,
                FontFamily = UiFont,
                MinWidth = 140,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = ControlStyles.Combo(),
            };
        }

        public static CheckBox MakeCheck(string text, bool isChecked, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Content = new TextBlock { Text = text, Foreground = Text, FontSize = 13, FontFamily = UiFont, TextWrapping = TextWrapping.Wrap },
                IsChecked = isChecked,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = ControlStyles.Check(),
            };
            cb.Checked += (s, e) => onChange(true);
            cb.Unchecked += (s, e) => onChange(false);
            return cb;
        }

        public static Slider MakeSlider(double min, double max, double value, Action<double> onChange)
        {
            var s = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Style = ControlStyles.Slider(),
                IsMoveToPointEnabled = true,
            };
            s.ValueChanged += (o, e) => onChange(e.NewValue);
            return s;
        }

        public static TextBlock SectionTitle(string text)
        {
            var t = MakeText(text, 11.5, Accent);
            t.FontWeight = FontWeights.SemiBold;
            t.Margin = new Thickness(0, 10, 0, 4);
            return t;
        }
    }
}
