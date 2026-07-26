using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Glass-console calculator: recessed display well with placeholder and live
    /// preview, tactile tiered keys (digits / operators / utility / accent equals) with
    /// press physics, and an expandable scientific ribbon.</summary>
    public static class CalculatorFeature
    {
        static bool _pro;

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "calc", L.T("f_calc"), Build, 320);
        }

        // key tiers: 0 digit · 1 operator · 2 equals · 3 utility ghost · 4 clear · 5 sci-fn
        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            // ---------------- recessed display well ----------------
            var preview = new TextBlock
            {
                Text = "",
                FontSize = 12,
                FontFamily = Theme.UiFont,
                Foreground = Theme.TextFaint,
                TextAlignment = TextAlignment.Right,
                MinHeight = 16,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var placeholder = Theme.MakeText("0", 30, Theme.TextFaint, Theme.LightFont);
            placeholder.HorizontalAlignment = HorizontalAlignment.Right;
            placeholder.IsHitTestVisible = false;
            var display = new TextBox
            {
                Text = "",
                FontSize = 30,
                FontFamily = Theme.LightFont,
                Background = Brushes.Transparent,
                Foreground = Theme.Text,
                CaretBrush = Theme.Accent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                TextAlignment = TextAlignment.Right,
            };
            var displayLayer = new Grid();
            displayLayer.Children.Add(placeholder);
            displayLayer.Children.Add(display);
            var wellInner = new StackPanel();
            wellInner.Children.Add(preview);
            wellInner.Children.Add(displayLayer);
            // recessed well: inset bevel makes the display look carved into the panel
            var wellLayers = new Grid();
            wellLayers.Children.Add(Theme.InsetBevel(13));
            wellLayers.Children.Add(new Border { Padding = new Thickness(14, 9, 14, 11), Child = wellInner });
            var well = new Border
            {
                Background = Theme.InputBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 0, 10),
                Child = wellLayers,
            };
            stack.Children.Add(well);

            // ---------------- key actions ----------------
            Action<string> append = s => { display.Text += s; display.CaretIndex = display.Text.Length; };
            Action equals = () =>
            {
                string expr = display.Text;
                try
                {
                    double v = ExpressionEvaluator.Eval(expr);
                    preview.Text = expr + "  =";
                    display.Text = v.ToString("G12", CultureInfo.InvariantCulture);
                }
                catch { display.Text = L.T("calc_error"); }
                display.CaretIndex = display.Text.Length;
            };
            Action clear = () => { display.Text = ""; preview.Text = ""; };
            Action back = () =>
            {
                if (display.Text == L.T("calc_error")) { display.Text = ""; return; }
                if (display.Text.Length > 0) display.Text = display.Text.Substring(0, display.Text.Length - 1);
                display.CaretIndex = display.Text.Length;
            };

            // live preview: evaluate silently while typing
            display.TextChanged += (s, e) =>
            {
                placeholder.Visibility = display.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                string t = display.Text;
                if (t.Length == 0 || t == L.T("calc_error") || !"0123456789(".Contains(t.Substring(0, 1))) { preview.Text = ""; return; }
                if (t.IndexOfAny(new[] { '+', '-', '×', '÷', '*', '/', '^', '%' }) < 0) { preview.Text = ""; return; }
                try
                {
                    double v = ExpressionEvaluator.Eval(t);
                    preview.Text = "=  " + v.ToString("G12", CultureInfo.InvariantCulture);
                }
                catch { preview.Text = ""; }
            };

            // ---------------- tiered key factory ----------------
            var opBg = new SolidColorBrush(Color.FromArgb((byte)(Theme.IsLight ? 0x30 : 0x38),
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var opHover = new SolidColorBrush(Color.FromArgb((byte)(Theme.IsLight ? 0x44 : 0x4E),
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var opBorder = new SolidColorBrush(Color.FromArgb(0x55,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var eqBg = new LinearGradientBrush(
                Color.FromRgb((byte)Math.Min(255, Theme.AccentColor.R + 26), (byte)Math.Min(255, Theme.AccentColor.G + 22), (byte)Math.Min(255, Theme.AccentColor.B + 20)),
                Theme.AccentColor, 90);
            var eqHover = new SolidColorBrush(
                Color.FromRgb((byte)Math.Min(255, Theme.AccentColor.R + 44), (byte)Math.Min(255, Theme.AccentColor.G + 38), (byte)Math.Min(255, Theme.AccentColor.B + 36)));
            var fnHover = new SolidColorBrush(Color.FromArgb((byte)(Theme.IsLight ? 0x38 : 0x40),
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var coral = new SolidColorBrush(Theme.IsLight ? Color.FromRgb(0xC8, 0x4A, 0x38) : Color.FromRgb(0xFF, 0x8A, 0x7A));

            Func<string, Action, int, Border> key = (label, action, kind) =>
            {
                Brush fg; double size; bool semi = true;
                switch (kind)
                {
                    case 1: fg = Theme.Accent; size = 19; break;
                    case 2: fg = Theme.IsLight ? Brushes.White : Brushes.Black; size = 19; break;
                    case 3: fg = Theme.TextDim; size = 16; semi = false; break;
                    case 4: fg = coral; size = 16; break;
                    case 5: fg = Theme.Accent; size = 11.5; break;
                    default: fg = Theme.Text; size = 17; break;
                }
                var tb = Theme.MakeText(label, size, fg);
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                if (semi) tb.FontWeight = FontWeights.SemiBold;

                Brush bg, hover, border = null;
                switch (kind)
                {
                    case 1: bg = opBg; hover = opHover; border = opBorder; break;
                    case 2: bg = eqBg; hover = eqHover; break;
                    case 3: case 4: bg = Brushes.Transparent; hover = Theme.HoverBg; break;
                    case 5: bg = Theme.AccentSoft; hover = fnHover; break;
                    default: bg = Theme.ButtonBg; hover = Theme.HoverBg; border = Theme.ButtonBorder; break;
                }
                var keyLayers = new Grid();
                keyLayers.Children.Add(Theme.Bevel(kind == 5 ? 9 : 13));
                keyLayers.Children.Add(tb);
                var b = new Border
                {
                    Background = bg,
                    BorderBrush = border,
                    BorderThickness = border == null ? new Thickness(0) : new Thickness(1),
                    CornerRadius = new CornerRadius(kind == 5 ? 10 : 14),
                    Height = kind == 5 ? 30 : 48,
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand,
                    Child = keyLayers,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                if (kind == 2)
                    b.Effect = new DropShadowEffect
                    {
                        Color = Theme.AccentColor, BlurRadius = 14, ShadowDepth = 0,
                        Opacity = Theme.IsLight ? 0.18 : 0.38,
                    };
                else if (kind <= 1)
                    // physical key lift: soft ambient shadow under digits and operators
                    b.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black, BlurRadius = 7, ShadowDepth = 2,
                        Opacity = Theme.IsLight ? 0.16 : 0.30, Direction = 270,
                    };

                // press physics: quick squash on down, spring back on up
                var zoom = new ScaleTransform(1, 1);
                b.RenderTransform = zoom;
                Theme.AttachHover(b, bg, hover);
                b.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    var dur = TimeSpan.FromMilliseconds(60);
                    zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, dur));
                    zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, dur));
                };
                b.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    var dur = TimeSpan.FromMilliseconds(110);
                    var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                    zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                    zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                    try { action(); } catch (Exception ex) { Log.Error("calckey", ex); }
                };
                return b;
            };

            // ---------------- scientific ribbon (2×5) ----------------
            var proGrid = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 5,
                Visibility = _pro ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 4),
            };
            foreach (var fn in new[] { "sin", "cos", "tan", "log", "ln", "sqrt", "abs", "^", "pi", "e" })
            {
                string f = fn;
                bool isConstOrOp = f == "^" || f == "pi" || f == "e";
                string label = f == "sqrt" ? "√" : f == "pi" ? "π" : f == "abs" ? "|x|" : f;
                proGrid.Children.Add(key(label, () => append(isConstOrOp ? f : f + "("), 5));
            }
            stack.Children.Add(proGrid);

            // ---------------- basic keypad ----------------
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
            grid.Children.Add(key("C", clear, 4));
            grid.Children.Add(key("( )", () =>
            {
                string t = display.Text;
                int open = 0; foreach (char c in t) { if (c == '(') open++; if (c == ')') open--; }
                bool lastIsOpenable = t.Length == 0 || "+-*/×÷^%(".IndexOf(t[t.Length - 1]) >= 0;
                append(open > 0 && !lastIsOpenable ? ")" : "(");
            }, 3));
            grid.Children.Add(key("%", () => append("%"), 1));
            grid.Children.Add(key("÷", () => append("÷"), 1));
            grid.Children.Add(key("7", () => append("7"), 0));
            grid.Children.Add(key("8", () => append("8"), 0));
            grid.Children.Add(key("9", () => append("9"), 0));
            grid.Children.Add(key("×", () => append("×"), 1));
            grid.Children.Add(key("4", () => append("4"), 0));
            grid.Children.Add(key("5", () => append("5"), 0));
            grid.Children.Add(key("6", () => append("6"), 0));
            grid.Children.Add(key("−", () => append("-"), 1));
            grid.Children.Add(key("1", () => append("1"), 0));
            grid.Children.Add(key("2", () => append("2"), 0));
            grid.Children.Add(key("3", () => append("3"), 0));
            grid.Children.Add(key("+", () => append("+"), 1));
            grid.Children.Add(key("⌫", back, 3));
            grid.Children.Add(key("0", () => append("0"), 0));
            grid.Children.Add(key(".", () => append("."), 0));
            grid.Children.Add(key("=", equals, 2));
            stack.Children.Add(grid);

            // ---------------- scientific toggle (ghost pill) ----------------
            var toggleFx = Theme.MakeText("fx", 11.5, Theme.Accent);
            toggleFx.FontWeight = FontWeights.Bold;
            toggleFx.VerticalAlignment = VerticalAlignment.Center;
            var toggleLabel = Theme.MakeText("", 11.5, Theme.TextDim);
            toggleLabel.VerticalAlignment = VerticalAlignment.Center;
            toggleLabel.Margin = new Thickness(5, 0, 0, 0);
            var toggleContent = new StackPanel { Orientation = Orientation.Horizontal };
            toggleContent.Children.Add(toggleFx);
            toggleContent.Children.Add(toggleLabel);
            var toggle = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Theme.ButtonBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 3.5, 11, 3.5),
                Margin = new Thickness(0, 7, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Child = toggleContent,
            };
            Action refreshToggle = () => toggleLabel.Text = _pro ? L.T("calc_simple") : L.T("calc_pro");
            refreshToggle();
            Theme.AttachHover(toggle, Brushes.Transparent, Theme.HoverBg);
            toggle.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            toggle.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _pro = !_pro;
                proGrid.Visibility = _pro ? Visibility.Visible : Visibility.Collapsed;
                refreshToggle();
            };
            stack.Children.Add(toggle);

            display.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { equals(); e.Handled = true; }
                else if (e.Key == Key.Escape) { clear(); e.Handled = true; }
            };
            host.Loaded += (s, e) => display.Focus();
            return stack;
        }
    }
}
