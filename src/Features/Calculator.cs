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

            var display = new TextBox
            {
                Text = "",
                FontSize = 24,
                FontFamily = Theme.LightFont,
                Background = Theme.InputBg,
                Foreground = Theme.Text,
                CaretBrush = Theme.Text,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 0, 8),
            };
            stack.Children.Add(display);

            var grid = new UniformGrid { Columns = 4 };
            var proGrid = new UniformGrid { Columns = 5, Visibility = _pro ? Visibility.Visible : Visibility.Collapsed };

            Action<string> append = s => { display.Text += s; display.CaretIndex = display.Text.Length; };
            Action equals = () =>
            {
                try
                {
                    double v = ExpressionEvaluator.Eval(display.Text);
                    display.Text = v.ToString("G12", CultureInfo.InvariantCulture);
                }
                catch { display.Text = L.T("calc_error"); }
                display.CaretIndex = display.Text.Length;
            };
            Action clear = () => display.Text = "";
            Action back = () =>
            {
                if (display.Text == L.T("calc_error")) { display.Text = ""; return; }
                if (display.Text.Length > 0) display.Text = display.Text.Substring(0, display.Text.Length - 1);
                display.CaretIndex = display.Text.Length;
            };

            Action<Panel, string, Action, bool> add = (panel, label, action, accent) =>
            {
                var b = Theme.MakeButton(label, action, accent);
                b.Margin = new Thickness(2.5);
                b.Padding = new Thickness(0, 8, 0, 8);
                panel.Children.Add(b);
            };

            // basic keypad
            add(grid, "C", clear, false);
            add(grid, "( )", () =>
            {
                string t = display.Text;
                int open = 0; foreach (char c in t) { if (c == '(') open++; if (c == ')') open--; }
                bool lastIsOpenable = t.Length == 0 || "+-*/×÷^%(".IndexOf(t[t.Length - 1]) >= 0;
                append(open > 0 && !lastIsOpenable ? ")" : "(");
            }, false);
            add(grid, "%", () => append("%"), false);
            add(grid, "÷", () => append("÷"), false);
            add(grid, "7", () => append("7"), false);
            add(grid, "8", () => append("8"), false);
            add(grid, "9", () => append("9"), false);
            add(grid, "×", () => append("×"), false);
            add(grid, "4", () => append("4"), false);
            add(grid, "5", () => append("5"), false);
            add(grid, "6", () => append("6"), false);
            add(grid, "−", () => append("-"), false);
            add(grid, "1", () => append("1"), false);
            add(grid, "2", () => append("2"), false);
            add(grid, "3", () => append("3"), false);
            add(grid, "+", () => append("+"), false);
            add(grid, "⌫", back, false);
            add(grid, "0", () => append("0"), false);
            add(grid, ".", () => append("."), false);
            add(grid, "=", equals, true);

            // pro keypad
            foreach (var fn in new[] { "sin", "cos", "tan", "log", "ln", "sqrt", "abs", "^", "pi", "e" })
            {
                string f = fn;
                bool isConstOrOp = f == "^" || f == "pi" || f == "e";
                string label = f == "sqrt" ? "√" : (f == "pi" ? "π" : f);
                add(proGrid, label, () => append(isConstOrOp ? f : f + "("), false);
            }

            stack.Children.Add(proGrid);
            stack.Children.Add(grid);

            var toggle = Theme.MakeButton(_pro ? L.T("calc_simple") : L.T("calc_pro"), () => { }, false);
            ((TextBlock)toggle.Child).Foreground = Theme.Accent;
            toggle.HorizontalAlignment = HorizontalAlignment.Center;
            toggle.Margin = new Thickness(0, 6, 0, 0);
            toggle.MouseLeftButtonUp += (s, e) =>
            {
                _pro = !_pro;
                proGrid.Visibility = _pro ? Visibility.Visible : Visibility.Collapsed;
                ((TextBlock)toggle.Child).Text = _pro ? L.T("calc_simple") : L.T("calc_pro");
            };
            stack.Children.Add(toggle);

            display.KeyDown += (s, e) => { if (e.Key == Key.Enter) { equals(); e.Handled = true; } };
            host.Loaded += (s, e) => display.Focus();
            return stack;
        }
    }
}
