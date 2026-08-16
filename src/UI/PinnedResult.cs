using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>A calculator result parked on the desktop: a small glass card that stays
    /// topmost, drags anywhere, copies its number on click and closes from its X. Unlike
    /// panels it never auto-closes — the whole point of pinning is that it outlives the
    /// panel it came from.</summary>
    public static class PinnedResult
    {
        static int _live;   // cascades each new pin a little further down-right

        public static void Show(string value)
        {
            try
            {
                var w = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                };

                var val = Theme.MakeText(value, 17, Theme.Accent);
                val.FontWeight = FontWeights.SemiBold;
                val.VerticalAlignment = VerticalAlignment.Center;
                val.Cursor = Cursors.Hand;
                val.ToolTip = L.T("calc_copy");
                val.FlowDirection = FlowDirection.LeftToRight;   // numbers never mirror

                var close = new Border
                {
                    Width = 18, Height = 18,
                    CornerRadius = new CornerRadius(9),
                    Background = Theme.ButtonBg,
                    Child = Theme.MakeGlyph("\uE711", 8),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                Theme.AttachHover(close, Theme.ButtonBg, Theme.HoverBg);
                close.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                close.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { w.Close(); } catch { } };

                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(Theme.MakeGlyphChip("\uE8EF", 24));
                ((FrameworkElement)row.Children[0]).Margin = new Thickness(0, 0, 9, 0);
                row.Children.Add(val);
                row.Children.Add(close);

                var card = Theme.MakeCard(row, 14);
                Theme.SetCardPadding(card, new Thickness(10, 7, 10, 7));
                w.Content = card;

                val.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                val.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    try { Clipboard.SetText(value); val.ToolTip = L.T("calc_copied"); }
                    catch (Exception ex) { Log.Error("pin copy", ex); }
                };
                w.MouseLeftButtonDown += (s, e) => { try { w.DragMove(); } catch { } };

                w.Loaded += (s, e) =>
                {
                    var owner = MainWindow.Instance;
                    var wa = owner != null && owner.IsVisible
                        ? PanelWindow.WorkAreaOf(owner)
                        : SystemParameters.WorkArea;
                    int slot = _live % 6;
                    w.Left = L.IsAr ? wa.Left + 18 + slot * 26 : wa.Right - w.ActualWidth - 18 - slot * 26;
                    w.Top = wa.Top + 18 + slot * 44;
                };
                w.Closed += (s, e) => { _live = Math.Max(0, _live - 1); };
                _live++;
                w.Show();
            }
            catch (Exception ex) { Log.Error("pinned result", ex); }
        }
    }
}
