using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Pomodoro-style focus timer: circular progress ring, work/break phases,
    /// per-day session counter, calm completion toast + sound.</summary>
    public static class FocusFeature
    {
        static readonly FocusCore Core = new FocusCore();

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "focus", L.T("f_focus"), Build, 300);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            // ---- progress ring ----
            const double size = 156, stroke = 8;
            double r = (size - stroke) / 2;
            double circumference = 2 * Math.PI * r;

            var track = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = Theme.DialStroke,
                StrokeThickness = stroke,
            };
            var progress = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = Theme.Accent,
                StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeDashArray = new DoubleCollection { 0, circumference * 2 },
                RenderTransform = new RotateTransform(-90, size / 2, size / 2),
            };
            var centerText = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var timeText = Theme.MakeText(FocusCore.Format(TimeSpan.FromMinutes(Core.WorkMin)), 34, Theme.Text, Theme.LightFont);
            timeText.HorizontalAlignment = HorizontalAlignment.Center;
            timeText.FlowDirection = FlowDirection.LeftToRight;
            var phaseText = Theme.MakeText("", 12, Theme.TextDim);
            phaseText.HorizontalAlignment = HorizontalAlignment.Center;
            centerText.Children.Add(timeText);
            centerText.Children.Add(phaseText);

            var ring = new Grid
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 10),
            };
            ring.Children.Add(track);
            ring.Children.Add(progress);
            ring.Children.Add(centerText);
            stack.Children.Add(ring);

            // ---- length presets (only while idle) ----
            Action RefreshPresets = null;
            Action RefreshButtons = null;
            Action<DateTime> Render = null;
            var presetRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
            };
            Border[] presetBtns = new Border[3];
            int[] mins = { 15, 25, 45 };
            for (int i = 0; i < mins.Length; i++)
            {
                int m = mins[i];
                var chip = Theme.MakeButton(m + " " + (L.IsAr ? "د" : "m"), () =>
                {
                    if (Core.State != FocusCore.Phase.Idle) return;
                    Core.WorkMin = m;
                    timeText.Text = FocusCore.Format(TimeSpan.FromMinutes(m));
                    RefreshPresets();
                });
                chip.Padding = new Thickness(12, 4, 12, 4);
                presetBtns[i] = chip;
                presetRow.Children.Add(chip);
            }
            stack.Children.Add(presetRow);

            RefreshPresets = () =>
            {
                for (int i = 0; i < mins.Length; i++)
                {
                    bool sel = Core.WorkMin == mins[i] && Core.State == FocusCore.Phase.Idle;
                    ((TextBlock)presetBtns[i].Child).Foreground = sel ? Theme.Accent : Theme.Text;
                }
            };

            // ---- controls ----
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            Border mainBtn = null;
            mainBtn = Theme.MakeButton(L.T("fo_start"), () =>
            {
                var now = DateTime.Now;
                if (Core.State == FocusCore.Phase.Idle) Core.Start(FocusCore.Phase.Work, now);
                else Core.TogglePause(now);
                RefreshButtons();
            }, accent: true);
            mainBtn.Padding = new Thickness(22, 8, 22, 8);
            var resetBtn = Theme.MakeButton(L.T("fo_reset"), () =>
            {
                Core.Reset();
                RefreshButtons();
                Render(DateTime.Now);
            });
            btnRow.Children.Add(mainBtn);
            btnRow.Children.Add(resetBtn);
            stack.Children.Add(btnRow);

            RefreshButtons = () =>
            {
                string key = Core.State == FocusCore.Phase.Idle ? "fo_start" : (Core.Paused ? "fo_resume" : "fo_pause");
                ((TextBlock)mainBtn.Child).Text = L.T(key);
                RefreshPresets();
            };
            // ---- sessions today ----
            var todayRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0),
            };
            var leaf = Theme.MakeGlyph("", 12, Theme.Accent); // check: sessions done
            leaf.VerticalAlignment = VerticalAlignment.Center;
            leaf.Margin = new Thickness(0, 0, 6, 0);
            var todayText = Theme.MakeText("", 12, Theme.TextDim);
            todayText.VerticalAlignment = VerticalAlignment.Center;
            todayRow.Children.Add(leaf);
            todayRow.Children.Add(todayText);
            stack.Children.Add(todayRow);

            Action RefreshToday = () =>
            {
                string today = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                int n = Settings.I.FocusDoneDate == today ? Settings.I.FocusDoneCount : 0;
                todayText.Text = L.T("fo_today") + ": " + n;
            };

            // ---- render + tick ----
            Render = now =>
            {
                double frac = Core.Progress(now);
                progress.StrokeDashArray = new DoubleCollection { Math.Max(0.001, frac * circumference), circumference * 2 };
                progress.Opacity = frac <= 0 ? 0 : 1;
                timeText.Text = Core.State == FocusCore.Phase.Idle
                    ? FocusCore.Format(TimeSpan.FromMinutes(Core.WorkMin))
                    : FocusCore.Format(Core.Remaining(now));
                phaseText.Text = Core.State == FocusCore.Phase.Idle
                    ? L.T("fo_hint")
                    : (Core.State == FocusCore.Phase.Work ? L.T("fo_work") : L.T("fo_break"))
                      + (Core.Paused ? " — " + L.T("fo_resume") : "");
            };

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (s, e) =>
            {
                var now = DateTime.Now;
                if (Core.Tick(now))
                {
                    if (Core.State == FocusCore.Phase.Work)
                    {
                        string today = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                        if (Settings.I.FocusDoneDate == today) Settings.I.FocusDoneCount++;
                        else { Settings.I.FocusDoneDate = today; Settings.I.FocusDoneCount = 1; }
                        Settings.I.Save();
                        AlertSound.Play();
                        Toast.Show(L.T("fo_done_work"), "");
                        Core.Start(FocusCore.Phase.Break, now);
                    }
                    else
                    {
                        AlertSound.Play();
                        Toast.Show(L.T("fo_done_break"), "");
                        Core.Reset();
                    }
                    RefreshButtons();
                    RefreshToday();
                }
                Render(now);
            };
            timer.Start();
            host.Closed += (s, e) => timer.Stop();

            RefreshButtons();
            RefreshToday();
            Render(DateTime.Now);
            return stack;
        }
    }
}
