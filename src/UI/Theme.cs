using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>Shared visual language: dark glass card, amber accent, MDL2 glyph buttons.</summary>
    public static class Theme
    {
        public static readonly Color AccentColor = Color.FromRgb(0xFF, 0xC6, 0x6E);
        public static readonly Brush Accent = new SolidColorBrush(AccentColor);
        public static readonly Brush Text = Brushes.White;
        public static readonly Brush TextDim = new SolidColorBrush(Color.FromArgb(0xB4, 0xFF, 0xFF, 0xFF));
        public static readonly Brush TextFaint = new SolidColorBrush(Color.FromArgb(0x6E, 0xFF, 0xFF, 0xFF));
        public static readonly Brush CardBg = new SolidColorBrush(Color.FromArgb(0xD2, 0x12, 0x16, 0x20));
        public static readonly Brush CardBorder = new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF));
        public static readonly Brush HoverBg = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
        public static readonly Brush ButtonBg = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF));
        public static readonly Brush InputBg = new SolidColorBrush(Color.FromArgb(0x28, 0x00, 0x00, 0x00));

        public static readonly FontFamily UiFont = new FontFamily("Segoe UI");
        public static readonly FontFamily LightFont = new FontFamily("Segoe UI Light, Segoe UI");
        public static readonly FontFamily Mdl2 = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");

        static Theme()
        {
            foreach (var b in new[] { Accent, Text, TextDim, TextFaint, CardBg, CardBorder, HoverBg, ButtonBg, InputBg })
                if (b.CanFreeze) b.Freeze();
        }

        public static Border MakeCard(UIElement content, double radius = 22)
        {
            return new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = new Thickness(18, 14, 18, 14),
                Child = content,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black, BlurRadius = 18, ShadowDepth = 3, Opacity = 0.45, Direction = 270
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
                Child = MakeGlyph(glyph, glyphSize),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            AttachHover(b, ButtonBg, HoverBg);
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("iconbtn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        /// <summary>Rounded text button for panels.</summary>
        public static Border MakeButton(string text, Action onClick, bool accent = false)
        {
            var tb = MakeText(text, 13, accent ? Brushes.Black : Text);
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
            else AttachHover(b, Accent, new SolidColorBrush(Color.FromRgb(0xFF, 0xD5, 0x90)));
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
            return new TextBox
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
        }

        public static ComboBox MakeCombo()
        {
            return new ComboBox
            {
                FontSize = 13,
                FontFamily = UiFont,
                MinWidth = 140,
                VerticalContentAlignment = VerticalAlignment.Center,
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
            };
            cb.Checked += (s, e) => onChange(true);
            cb.Unchecked += (s, e) => onChange(false);
            return cb;
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
