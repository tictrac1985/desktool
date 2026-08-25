using System;
using System.Collections.Generic;
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
        static bool _units;

        /// <summary>Recent "expression = result" pairs, newest first. A calculator without any
        /// memory of what you just worked out forces you to retype the number you are building
        /// on — the single most common thing anyone does with one.</summary>
        static readonly List<string[]> History = new List<string[]>();
        const int MaxHistory = 12;

        static void Remember(string expr, string result)
        {
            if (string.IsNullOrEmpty(expr) || string.IsNullOrEmpty(result)) return;
            History.RemoveAll(h => h[0] == expr);
            History.Insert(0, new[] { expr, result });
            while (History.Count > MaxHistory) History.RemoveAt(History.Count - 1);
        }

        /// <summary>Faint accent light pooling at the top of the display well — the "lit
        /// screen" cue that separates a glowing readout from a painted rectangle.</summary>
        static Brush ScreenGlowBrush()
        {
            var b = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.0),
                Center = new Point(0.5, 0.0),
                RadiusX = 0.95, RadiusY = 0.85,
            };
            byte a = (byte)(Theme.IsLight ? 0x16 : 0x22);
            b.GradientStops.Add(new GradientStop(
                Color.FromArgb(a, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B), 0));
            b.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            b.Freeze();
            return b;
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "calc", L.T("f_calc"), Build, 356);
        }

        // key tiers: 0 digit · 1 operator · 2 equals · 3 utility ghost · 4 clear · 5 sci-fn
        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel { FlowDirection = FlowDirection.LeftToRight };

            // ---------------- recessed display well ----------------
            var preview = new TextBlock
            {
                Text = "",
                FontSize = 13.5,
                FontFamily = Theme.UiFont,
                Foreground = Theme.Accent,
                TextAlignment = TextAlignment.Right,
                MinHeight = 19,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            preview.FontWeight = FontWeights.SemiBold;
            var placeholder = Theme.MakeText("0", 36, Theme.TextFaint, Theme.LightFont);
            placeholder.HorizontalAlignment = HorizontalAlignment.Right;
            placeholder.IsHitTestVisible = false;
            var display = new TextBox
            {
                Text = "",
                FontSize = 36,
                FontFamily = Theme.LightFont,
                Background = Brushes.Transparent,
                Foreground = Theme.Text,
                CaretBrush = Theme.Accent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MinHeight = 48,
                TextAlignment = TextAlignment.Right,
            };
            // copy sits inside the well, on the result itself — a result you cannot get out of
            // the calculator is only half useful
            var copyBtn = Theme.MakeIconButton("\uE8C8", L.T("calc_copy"), () => { }, 26, 11);
            copyBtn.VerticalAlignment = VerticalAlignment.Bottom;
            copyBtn.Opacity = 0.95;
            // pin the result straight onto the desktop: a number you keep glancing at
            // (a budget, a total) should not live inside a panel that closes itself
            var pinBtn = Theme.MakeIconButton("\uE718", L.T("calc_pin"), () =>
            {
                string t = display.Text;
                if (string.IsNullOrEmpty(t) || t == L.T("calc_error")) return;
                PinnedResult.Show(t);
            }, 26, 11);
            pinBtn.VerticalAlignment = VerticalAlignment.Bottom;
            pinBtn.Opacity = 0.95;
            var wellBtns = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            wellBtns.Children.Add(copyBtn);
            wellBtns.Children.Add(pinBtn);

            var displayLayer = new Grid();
            displayLayer.Children.Add(placeholder);
            displayLayer.Children.Add(display);
            var wellInner = new StackPanel();
            wellInner.Children.Add(preview);
            var displayRow = new DockPanel();
            DockPanel.SetDock(wellBtns, Dock.Left);
            displayRow.Children.Add(wellBtns);
            displayRow.Children.Add(displayLayer);
            wellInner.Children.Add(displayRow);
            // recessed well: inset bevel makes the display look carved into the panel
            var wellLayers = new Grid();
            wellLayers.Children.Add(Theme.InsetBevel(15));
            // screen glow: a faint accent light pooling at the top of the well, so the
            // display reads as a lit screen recessed in the pad rather than a painted box
            var screenGlow = new Border
            {
                CornerRadius = new CornerRadius(15),
                Background = ScreenGlowBrush(),
                IsHitTestVisible = false,
            };
            wellLayers.Children.Add(screenGlow);
            wellLayers.Children.Add(new Border { Padding = new Thickness(15, 10, 15, 12), Child = wellInner });
            var well = new Border
            {
                Background = Theme.InputBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(0, 0, 0, 10),
                Child = wellLayers,
            };
            stack.Children.Add(well);

            // ---------------- key actions ----------------
            Action<string> append = s => { display.Text += s; display.CaretIndex = display.Text.Length; };
            Action rebuildHistory = null;
            Action equals = () =>
            {
                string expr = display.Text;
                try
                {
                    double v = ExpressionEvaluator.Eval(ExpressionEvaluator.NormalizePercentPhrase(expr));
                    string result = v.ToString("G12", CultureInfo.InvariantCulture);
                    preview.Text = expr + "  =";
                    display.Text = result;
                    Remember(expr, result);
                    if (rebuildHistory != null) rebuildHistory();
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
                    double v = ExpressionEvaluator.Eval(ExpressionEvaluator.NormalizePercentPhrase(t));
                    preview.Text = "=  " + v.ToString("G12", CultureInfo.InvariantCulture);
                }
                catch { preview.Text = ""; }
            };

            // ---------------- tiered key factory ----------------
            // Operators wear a clearly visible accent tint; the old 0x30/0x38 alphas were so
            // faint the pad read as one flat grey wall and the user called the look unlovely
            var opBg = new SolidColorBrush(Color.FromArgb((byte)(Theme.IsLight ? 0x4E : 0x72),
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var opHover = new SolidColorBrush(Color.FromArgb((byte)(Theme.IsLight ? 0x68 : 0x8C),
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            var opBorder = new SolidColorBrush(Color.FromArgb(0xA8,
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
                    case 1: fg = Theme.Accent; size = 21; break;
                    case 2: fg = Theme.IsLight ? Brushes.White : Brushes.Black; size = 22; break;
                    case 3: fg = Theme.TextDim; size = 17; semi = false; break;
                    case 4: fg = coral; size = 17; break;
                    case 5: fg = Theme.Accent; size = 12; break;
                    default: fg = Theme.Text; size = 18; break;
                }
                var tb = Theme.MakeText(label, size, fg);
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                if (semi) tb.FontWeight = FontWeights.SemiBold;

                // Raised keys get a top-lit face rather than one flat colour — a keycap catches
                // light on its upper curve, and that gradient is most of what sells the depth.
                // Utility keys (3, 4) get a faint recessed fill of their own: fully transparent
                // they read as holes punched out of the pad rather than as a calmer third tier.
                Brush bg, hover, border = null;
                switch (kind)
                {
                    case 1: bg = Theme.KeyFace(opBg.Color, 18); hover = opHover; border = opBorder; break;
                    case 2: bg = eqBg; hover = eqHover; break;
                    case 3: case 4: bg = Theme.InputBg; hover = Theme.HoverBg; border = Theme.CardBorder; break;
                    case 5: bg = Theme.AccentSoft; hover = fnHover; break;
                    // digits ride the palette's raised-key ramp — the pad tints itself with
                    // every theme instead of sitting on neutral grey
                    default: bg = Theme.KeyGradient; hover = Theme.KeyGradientHover; border = Theme.ButtonBorder; break;
                }
                double radius = kind == 5 ? 10 : 15;
                var keyLayers = new Grid();
                keyLayers.Children.Add(Theme.Bevel(radius - 1));
                // keycaps get the same specular edge as the cards; ghost keys (3, 4) stay flat
                // so the pad reads as three tiers instead of one wall of buttons
                if (kind != 3 && kind != 4) keyLayers.Children.Add(Theme.RimLight(radius));
                keyLayers.Children.Add(tb);
                var b = new Border
                {
                    Background = bg,
                    BorderBrush = border,
                    BorderThickness = border == null ? new Thickness(0) : new Thickness(1),
                    CornerRadius = new CornerRadius(radius),
                    Height = kind == 5 ? 34 : 52,
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand,
                    Child = keyLayers,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                if (kind == 2)
                {
                    // equals glows in the accent instead of casting a shadow — it is the one
                    // key that should look lit rather than merely raised
                    var glow = new DropShadowEffect
                    {
                        Color = Theme.AccentColor, BlurRadius = 18, ShadowDepth = 0,
                        Opacity = Theme.IsLight ? 0.28 : 0.5,
                    };
                    glow.Freeze();
                    b.Effect = glow;
                }
                else if (kind <= 1) b.Effect = Theme.Elevation(1);

                // press physics: the cap travels down into the panel and its shadow collapses,
                // then springs back. Scale alone reads as a flat image being resized.
                var zoom = new ScaleTransform(1, 1);
                var drop = new TranslateTransform(0, 0);
                var moves = new TransformGroup();
                moves.Children.Add(zoom);
                moves.Children.Add(drop);
                b.RenderTransform = moves;
                Theme.AttachHover(b, bg, hover);
                b.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    var dur = TimeSpan.FromMilliseconds(60);
                    zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, dur));
                    zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, dur));
                    drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(1.5, dur));
                    if (kind <= 1) b.Effect =null;   // key is down: nothing under it to shade
                };
                b.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    var dur = TimeSpan.FromMilliseconds(110);
                    var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                    zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                    zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                    drop.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
                    if (kind <= 1) b.Effect =Theme.Elevation(1);
                    try { action(); } catch (Exception ex) { Log.Error("calckey", ex); }
                };
                // releasing outside the key must restore it too, or it stays visually stuck down
                b.MouseLeave += (s, e) =>
                {
                    zoom.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    zoom.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    drop.BeginAnimation(TranslateTransform.YProperty, null);
                    zoom.ScaleX = zoom.ScaleY = 1.0;
                    drop.Y = 0;
                    if (kind <= 1) b.Effect =Theme.Elevation(1);
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

            // ---------------- units ribbon (2x4) ----------------
            // converts the number on the display in place — the everyday "how many pounds
            // is that?" question, one tap instead of a search engine
            var unitsGrid = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4,
                Visibility = _units ? Visibility.Visible : Visibility.Collapsed,
                Margin = new Thickness(0, 0, 0, 4),
            };
            for (int ui = 0; ui < UnitConvert.Labels.Length; ui++)
            {
                int idx = ui;
                unitsGrid.Children.Add(key(UnitConvert.Labels[idx], () =>
                {
                    double v;
                    try { v = ExpressionEvaluator.Eval(ExpressionEvaluator.NormalizePercentPhrase(display.Text)); }
                    catch { return; }
                    preview.Text = display.Text + "  " + UnitConvert.Labels[idx];
                    display.Text = UnitConvert.Apply(idx, v).ToString("G10", CultureInfo.InvariantCulture);
                    display.CaretIndex = display.Text.Length;
                }, 5));
            }
            stack.Children.Add(unitsGrid);

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
                Background = Theme.AccentSoft,
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
            Theme.AttachHover(toggle, Theme.AccentSoft, Theme.HoverBg);
            toggle.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            toggle.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _pro = !_pro;
                proGrid.Visibility = _pro ? Visibility.Visible : Visibility.Collapsed;
                refreshToggle();
            };

            // units pill: same ghost style as fx, shares the row so the pad stays compact
            var unitsFx = Theme.MakeText("<>", 11.5, Theme.Accent);
            unitsFx.FontWeight = FontWeights.Bold;
            unitsFx.VerticalAlignment = VerticalAlignment.Center;
            var unitsLabel = Theme.MakeText(L.T("calc_units"), 11.5, Theme.TextDim);
            unitsLabel.VerticalAlignment = VerticalAlignment.Center;
            unitsLabel.Margin = new Thickness(5, 0, 0, 0);
            var unitsContent = new StackPanel { Orientation = Orientation.Horizontal };
            unitsContent.Children.Add(unitsFx);
            unitsContent.Children.Add(unitsLabel);
            var unitsToggle = new Border
            {
                Background = Theme.AccentSoft,
                BorderBrush = Theme.ButtonBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(11, 3.5, 11, 3.5),
                Margin = new Thickness(8, 7, 0, 0),
                Cursor = Cursors.Hand,
                Child = unitsContent,
            };
            Theme.AttachHover(unitsToggle, Theme.AccentSoft, Theme.HoverBg);
            unitsToggle.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            unitsToggle.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                _units = !_units;
                unitsGrid.Visibility = _units ? Visibility.Visible : Visibility.Collapsed;
            };

            var togglesRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            toggle.Margin = new Thickness(0, 7, 8, 0);
            togglesRow.Children.Add(toggle);
            togglesRow.Children.Add(unitsToggle);
            stack.Children.Add(togglesRow);

            // ---------------- history strip ----------------
            var historyRow = new StackPanel { Orientation = Orientation.Horizontal };
            var historyScroll = new ScrollViewer
            {
                Content = historyRow,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Margin = new Thickness(0, 0, 0, 8),
                MaxHeight = 40,
            };
            rebuildHistory = () =>
            {
                historyRow.Children.Clear();
                foreach (var h in History)
                {
                    string expr = h[0], result = h[1];
                    // ghost pill: quiet accent-tinted chip, not a full button — the strip is
                    // a memory aid, not a second keypad
                    var pillText = Theme.MakeText(result, 12, Theme.Accent);
                    pillText.VerticalAlignment = VerticalAlignment.Center;
                    var pill = new Border
                    {
                        Background = Theme.AccentSoft,
                        BorderBrush = Theme.ButtonBorder,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(11),
                        Padding = new Thickness(10, 3, 10, 3),
                        Margin = new Thickness(0, 0, 5, 0),
                        Cursor = Cursors.Hand,
                        Child = pillText,
                    };
                    pill.ToolTip = expr + " = " + result;
                    Theme.AttachHover(pill, Theme.AccentSoft, Theme.HoverBg);
                    pill.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                    pill.MouseLeftButtonUp += (s, e) =>
                    {
                        e.Handled = true;
                        // append rather than replace: the usual reason to reach for an earlier
                        // result is to keep calculating with it
                        append(result);
                        display.Focus();
                    };
                    historyRow.Children.Add(pill);
                }
                historyScroll.Visibility = History.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            };
            rebuildHistory();
            stack.Children.Insert(0, historyScroll);

            copyBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                string t = display.Text;
                if (string.IsNullOrEmpty(t) || t == L.T("calc_error")) return;
                try { Clipboard.SetText(t); preview.Text = L.T("calc_copied"); }
                catch (Exception ex) { Log.Error("calc copy", ex); }
            };

            // ---------------- keyboard ----------------
            // The display is a real TextBox, so digits already type. What was missing is the
            // operator keys: * and / are what a keyboard actually produces, while the evaluator
            // and the on-screen keys speak × and ÷.
            display.PreviewTextInput += (s, e) =>
            {
                string ch = e.Text;
                if (ch == "*") { append("×"); e.Handled = true; }
                else if (ch == "/") { append("÷"); e.Handled = true; }
                else if (ch == "=") { equals(); e.Handled = true; }
            };
            display.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { equals(); e.Handled = true; }
                else if (e.Key == Key.Escape) { clear(); e.Handled = true; }
                else if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None
                         && display.SelectionLength == 0 && display.CaretIndex == display.Text.Length)
                {
                    clear(); e.Handled = true;   // Del at the end clears, like a calculator's C
                }
            };
            host.Loaded += (s, e) => display.Focus();
            return stack;
        }
    }
}
