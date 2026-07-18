using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
                CardTop = Color.FromArgb(0xD6, 0x20, 0x27, 0x36), CardBottom = Color.FromArgb(0xDE, 0x0E, 0x12, 0x1A) },
            new Palette { KeyAr = "أزرق", KeyEn = "Ocean",
                Accent = Color.FromRgb(0x6E, 0xC1, 0xFF),
                CardTop = Color.FromArgb(0xD6, 0x1B, 0x2A, 0x3F), CardBottom = Color.FromArgb(0xDE, 0x0B, 0x13, 0x1F) },
            new Palette { KeyAr = "زمردي", KeyEn = "Emerald",
                Accent = Color.FromRgb(0x7B, 0xE3, 0xA6),
                CardTop = Color.FromArgb(0xD6, 0x17, 0x2A, 0x20), CardBottom = Color.FromArgb(0xDE, 0x09, 0x14, 0x0E) },
            new Palette { KeyAr = "وردي", KeyEn = "Rose",
                Accent = Color.FromRgb(0xFF, 0x9F, 0xB8),
                CardTop = Color.FromArgb(0xD6, 0x2C, 0x1B, 0x27), CardBottom = Color.FromArgb(0xDE, 0x17, 0x0C, 0x13) },
            new Palette { KeyAr = "فاتح", KeyEn = "Light", Light = true,
                Accent = Color.FromRgb(0xB8, 0x6B, 0x00),
                CardTop = Color.FromArgb(0xEE, 0xF7, 0xFA, 0xFD), CardBottom = Color.FromArgb(0xF2, 0xE2, 0xE9, 0xF2) },
        };

        public static bool IsLight;
        public static Color AccentColor;
        public static Brush Accent, Text, TextDim, TextFaint, CardBg, CardBorder, HoverBg, ButtonBg, InputBg;
        public static Brush DialStroke, DialFill;

        public static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        public static readonly FontFamily LightFont = new FontFamily("Segoe UI Light, Segoe UI");
        public static readonly FontFamily Mdl2 = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");

        static Theme() { Apply(0); }

        public static void Apply(int index)
        {
            if (index < 0 || index >= Palettes.Length) index = 0;
            var p = Palettes[index];
            IsLight = p.Light;
            AccentColor = p.Accent;
            Accent = Freeze(new SolidColorBrush(p.Accent));

            if (p.Light)
            {
                Text = Freeze(new SolidColorBrush(Color.FromArgb(0xE6, 0x18, 0x20, 0x2C)));
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xA8, 0x18, 0x20, 0x2C)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0x18, 0x20, 0x2C)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0x10, 0x18, 0x24)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0x10, 0x18, 0x24)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x12, 0x10, 0x18, 0x24)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0x10, 0x18, 0x24)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0x18, 0x24)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x10, 0x10, 0x18, 0x24)));
            }
            else
            {
                Text = Brushes.White;
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x6E, 0xFF, 0xFF, 0xFF)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
            }

            var grad = new LinearGradientBrush(p.CardTop, p.CardBottom, 90);
            CardBg = Freeze(grad);
            ControlStyles.Invalidate();
        }

        static Brush Freeze(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }

        public static Border MakeCard(UIElement content, double radius = 24)
        {
            return new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = new Thickness(20, 15, 20, 15),
                Child = content,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 22, ShadowDepth = 4, Opacity = IsLight ? 0.28 : 0.5, Direction = 270
                },
                Margin = new Thickness(12), // room for the shadow inside the transparent window
            };
        }

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

        /// <summary>Round icon button used in the widget's tool bar.</summary>
        public static Border MakeIconButton(string glyph, string tooltip, Action onClick, double diameter = 36, double glyphSize = 15)
        {
            var b = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = new CornerRadius(diameter / 2),
                Background = ButtonBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                Child = MakeGlyph(glyph, glyphSize),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            var normalBorder = CardBorder;
            var hoverBorder = Accent;
            b.MouseEnter += (s, e) => { b.Background = HoverBg; b.BorderBrush = hoverBorder; };
            b.MouseLeave += (s, e) => { b.Background = ButtonBg; b.BorderBrush = normalBorder; };
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("iconbtn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        /// <summary>Rounded text button for panels.</summary>
        public static Border MakeButton(string text, Action onClick, bool accent = false)
        {
            var tb = MakeText(text, 13, accent ? (IsLight ? Brushes.White : Brushes.Black) : Text);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal;
            var b = new Border
            {
                Background = accent ? Accent : ButtonBg,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(14, 7, 14, 7),
                Child = tb,
                Cursor = Cursors.Hand,
                Margin = new Thickness(3),
            };
            if (!accent) AttachHover(b, ButtonBg, HoverBg);
            else
            {
                var c = AccentColor;
                var lighter = Color.FromRgb(
                    (byte)Math.Min(255, c.R + 30), (byte)Math.Min(255, c.G + 25), (byte)Math.Min(255, c.B + 25));
                AttachHover(b, Accent, new SolidColorBrush(lighter));
            }
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
                Padding = new Thickness(8, 6, 8, 6),
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
            var t = MakeText(text, 12.5, Accent);
            t.FontWeight = FontWeights.SemiBold;
            t.Margin = new Thickness(0, 10, 0, 4);
            return t;
        }
    }
}
