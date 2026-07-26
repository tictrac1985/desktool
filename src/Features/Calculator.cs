using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Calculator with a two-line inset display (live preview above, editable
    /// expression below), categorized keys and an expandable scientific row set.</summary>
    public static class CalculatorFeature
    {
        static bool _pro;

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "calc", L.T("f_calc"), Build, 300);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            // ---- inset display well: live preview on top, editable expression below ----
            var preview = new TextBlock
            {
                Text = "",
                FontSize = 12.5,
                FontFamily = Theme.UiFont,
                Foreground = Theme.TextFaint,
                TextAlignment = TextAlignment.Right,
                MinHeight = 17,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var display = new TextBox
            {
                Text = "",
                FontSize = 26,
                FontFamily = Theme.LightFont,
                Background = Brushes.Transparent,
                Foreground = Theme.Text,
                CaretBrush = Theme.Accent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 2, 0, 0),
                TextAlignment = TextAlignment.Right,
            };
            var wellInner = new StackPanel();
            wellInner.Children.Add(preview);
            wellInner.Children.Add(display);
            var well = new Border
            {
                Background = Theme.InputBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 10),
                Margin = new Thickness(0, 0, 0, 10),
                Child = wellInner,
            };
            stack.Children.Add(well);

            // ---- key actions ----
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

            // ---- key factory ----
            Func<string, Action, int, Border> key = (label, action, kind) =>
            {
                var tb = Theme.MakeText(label, kind == 3 ? 17 : 15,
                    kind == 1 ? Theme.Accent :
                    kind == 2 ? (Theme.IsLight ? Brushes.White : Brushes.Black) :
                    kind == 4 ? Theme.Accent : Theme.Text);
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                if (kind == 2) tb.FontWeight = FontWeights.SemiBold;
                if (kind == 1 || kind == 4) tb.FontWeight = FontWeights.SemiBold;

                Brush bg, hover;
                switch (kind)
                {
                    case 1: bg = Theme.AccentSoft; hover = new SolidColorBrush(Color.FromArgb(0x40, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B)); break;
                    case 2:
                        bg = new LinearGradientBrush(
                            Color.FromRgb((byte)Math.Min(255, Theme.AccentColor.R + 26), (byte)Math.Min(255, Theme.AccentColor.G + 22), (byte)Math.Min(255, Theme.AccentColor.B + 20)),
                            Theme.AccentColor, 90);
                        hover = new SolidColorBrush(Color.FromRgb((byte)Math.Min(255, Theme.AccentColor.R + 44), (byte)Math.Min(255, Theme.AccentColor.G + 38), (byte)Math.Min(255, Theme.AccentColor.B + 36)));
                        break;
                    case 4: bg = Brushes.Transparent; hover = Theme.AccentSoft; break;
                    default: bg = Theme.ButtonBg; hover = Theme.HoverBg; break;
                }
                var b = new Border
                {
                    Background = bg,
                    BorderBrush = kind == 2 || kind == 4 ? null : Theme.ButtonBorder,
                    BorderThickness = kind == 2 || kind == 4 ? new Thickness(0) : new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Height = kind == 5 ? 32 : 44,
                    Margin = new Thickness(2.5),
                    Cursor = Cursors.Hand,
                    Child = tb,
                };
                if (kind == 5) tb.FontSize = 12;
                Theme.AttachHover(b, bg, hover);
                b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { action(); } catch (Exception ex) { Log.Error("calckey", ex); } };
                return b;
            };

            // ---- pro keypad (2×5) ----
            var proGrid = new UniformGrid { Columns = 5, Visibility = _pro ? Visibility.Visible : Visibility.Collapsed };
            foreach (var fn in new[] { "sin", "cos", "tan", "log", "ln", "sqrt", "abs", "^", "pi", "e" })
            {
                string f = fn;
                bool isConstOrOp = f == "^" || f == "pi" || f == "e";
                string label = f == "sqrt" ? "√" : f == "pi" ? "π" : f == "abs" ? "|x|" : f;
                proGrid.Children.Add(key(label, () => append(isConstOrOp ? f : f + "("), 5));
            }
            proGrid.Margin = new Thickness(0, 0, 0, 2);
            stack.Children.Add(proGrid);

            // ---- basic keypad ----
            var grid = new UniformGrid { Columns = 4 };
            grid.Children.Add(key("C", clear, 4));
            grid.Children.Add(key("( )", () =>
            {
                string t = display.Text;
                int open = 0; foreach (char c in t) { if (c == '(') open++; if (c == ')') open--; }
                bool lastIsOpenable = t.Length == 0 || "+-*/×÷^%(".IndexOf(t[t.Length - 1]) >= 0;
                append(open > 0 && !lastIsOpenable ? ")" : "(");
            }, 4));
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
            grid.Children.Add(key("⌫", back, 4));
            grid.Children.Add(key("0", () => append("0"), 0));
            grid.Children.Add(key(".", () => append("."), 0));
            grid.Children.Add(key("=", equals, 2));
            stack.Children.Add(grid);

            // ---- pro toggle (text link under the keypad) ----
            Border toggle = null;
            toggle = Theme.MakeButton(_pro ? L.T("calc_simple") : L.T("calc_pro"), () =>
            {
                _pro = !_pro;
                proGrid.Visibility = _pro ? Visibility.Visible : Visibility.Collapsed;
                ((TextBlock)toggle.Child).Text = _pro ? L.T("calc_simple") : L.T("calc_pro");
            });
            ((TextBlock)toggle.Child).Foreground = Theme.Accent;
            toggle.Background = Brushes.Transparent;
            toggle.BorderThickness = new Thickness(0);
            toggle.HorizontalAlignment = HorizontalAlignment.Center;
            toggle.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(toggle);

            display.KeyDown += (s, e) => { if (e.Key == Key.Enter) { equals(); e.Handled = true; } };
            host.Loaded += (s, e) => display.Focus();
            return stack;
        }
    }
}
