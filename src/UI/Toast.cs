using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>Topmost glass toast in a work-area corner; click dismisses, auto-closes.</summary>
    public static class Toast
    {
        public static void Show(string message, string glyph = "")
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
                    ShowActivated = false,
                    FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(glyph))
                {
                    var chip = Theme.MakeGlyphChip(glyph, 34);
                    chip.VerticalAlignment = VerticalAlignment.Center;
                    chip.Margin = new Thickness(0, 0, 12, 0);
                    row.Children.Add(chip);
                }
                var text = Theme.MakeText(message, 14.5, Theme.Text);
                text.VerticalAlignment = VerticalAlignment.Center;
                text.MaxWidth = 320;
                text.TextWrapping = TextWrapping.Wrap;
                row.Children.Add(text);
                var card = Theme.MakeCard(row, 18);
                w.Content = card;
                w.MouseLeftButtonDown += (s, e) => w.Close();
                w.Loaded += (s, e) =>
                {
                    var wa = SystemParameters.WorkArea;
                    w.Left = L.IsAr ? wa.Left + 16 : wa.Right - w.ActualWidth - 16;
                    w.Top = wa.Bottom - w.ActualHeight - 16;
                    // gentle entrance: rise + fade
                    var tt = new TranslateTransform(0, 10);
                    card.RenderTransform = tt;
                    w.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
                    tt.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
                        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
                };
                w.Opacity = 0;
                w.Show();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
                timer.Tick += (s, e) => { timer.Stop(); try { w.Close(); } catch { } };
                timer.Start();
            }
            catch (Exception ex) { Log.Error("toast", ex); }
        }
    }
}
