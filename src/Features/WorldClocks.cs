using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Live clocks for user-chosen world cities, with day-offset badges.</summary>
    public static class WorldClocksFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "worldclock", L.T("f_worldclock"), Build, 300);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var listHost = new StackPanel();
            stack.Children.Add(listHost);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            host.Closed += (s, e) => timer.Stop();

            Action rebuild = null;

            // ---- live time labels ----
            var timeLabels = new System.Collections.Generic.List<Tuple<TextBlock, Func<DateTime>>>();
            Action TickTimes = () =>
            {
                string fmt = Settings.I.Use24h ? "HH:mm" : "h:mm tt";
                foreach (var pair in timeLabels)
                {
                    try { pair.Item1.Text = pair.Item2().ToString(fmt, CultureInfo.InvariantCulture); }
                    catch { pair.Item1.Text = "--:--"; }
                }
            };
            timer.Tick += (s, e) => TickTimes();

            // ---- add-city row ----
            var combo = Theme.MakeCombo();
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            var addBtn = Theme.MakeButton("+", () =>
            {
                var city = combo.SelectedItem as WorldClockCore.City;
                if (city == null) return;
                if (!Settings.I.WorldClocks.Contains(city.TzId))
                {
                    Settings.I.WorldClocks.Add(city.TzId);
                    Settings.I.Save();
                    rebuild();
                }
            }, padding: new Thickness(12, 4, 12, 4));
            var addRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(addBtn, Dock.Right);
            addRow.Children.Add(addBtn);
            addRow.Children.Add(combo);

            rebuild = () =>
            {
                listHost.Children.Clear();
                timeLabels.Clear();

                // local row first
                listHost.Children.Add(MakeRow(
                    string.IsNullOrEmpty(Settings.I.City) ? L.T("wc_here") : Settings.I.City,
                    () => DateTime.Now, 0, null));

                foreach (var tzId in Settings.I.WorldClocks.ToArray())
                {
                    var city = WorldClockCore.Find(tzId);
                    if (city == null) continue;
                    string id = tzId;
                    listHost.Children.Add(MakeRow(
                        L.IsAr ? city.Ar : city.En,
                        () => WorldClockCore.TimeIn(id, DateTime.UtcNow) ?? DateTime.Now,
                        WorldClockCore.DayDiff(id, DateTime.UtcNow, DateTime.Now),
                        () =>
                        {
                            Settings.I.WorldClocks.Remove(id);
                            Settings.I.Save();
                            rebuild();
                        }));
                }
                if (Settings.I.WorldClocks.Count == 0)
                {
                    var hint = Theme.MakeText(L.T("wc_empty"), 12, Theme.TextFaint);
                    hint.TextWrapping = TextWrapping.Wrap;
                    hint.HorizontalAlignment = HorizontalAlignment.Center;
                    hint.Margin = new Thickness(4, 8, 4, 10);
                    listHost.Children.Add(hint);
                }

                // refill the combo with cities not yet added
                combo.Items.Clear();
                foreach (var c in WorldClockCore.Cities)
                    if (!Settings.I.WorldClocks.Contains(c.TzId))
                        combo.Items.Add(c);
                if (combo.Items.Count > 0) combo.SelectedIndex = 0;
                TickTimes();
            };

            FrameworkElement MakeRow(string name, Func<DateTime> timeOf, int dayDiff, Action onRemove)
            {
                var dock = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

                var right = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (onRemove != null)
                {
                    var del = new Border
                    {
                        Width = 20, Height = 20,
                        CornerRadius = new CornerRadius(10),
                        Background = Brushes.Transparent,
                        Child = Theme.MakeGlyph("\uE711", 8.5, Theme.TextDim),
                        Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Theme.AttachHover(del, Brushes.Transparent, Theme.HoverBg);
                    del.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                    del.MouseLeftButtonUp += (s, e) => { e.Handled = true; onRemove(); };
                    right.Children.Add(del);
                }
                var timeText = Theme.MakeText("--:--", 15, Theme.Text, Theme.LightFont);
                timeText.FlowDirection = FlowDirection.LeftToRight;
                timeText.VerticalAlignment = VerticalAlignment.Center;
                right.Children.Add(timeText);
                timeLabels.Add(Tuple.Create(timeText, timeOf));

                if (dayDiff != 0)
                {
                    var badge = new Border
                    {
                        Background = Theme.AccentSoft,
                        CornerRadius = new CornerRadius(7),
                        Padding = new Thickness(6, 1.5, 6, 1.5),
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = Theme.MakeText(dayDiff > 0 ? L.T("wc_plus") : L.T("wc_minus"), 10, Theme.Accent),
                    };
                    right.Children.Add(badge);
                }
                DockPanel.SetDock(right, Dock.Right);
                dock.Children.Add(right);

                var nameText = Theme.MakeText(name, 13, Theme.TextDim);
                nameText.VerticalAlignment = VerticalAlignment.Center;
                dock.Children.Add(nameText);
                return dock;
            }

            timer.Start();

            rebuild();
            stack.Children.Add(addRow);
            return stack;
        }
    }
}
