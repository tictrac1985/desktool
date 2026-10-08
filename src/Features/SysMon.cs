using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Lightweight live meters: CPU (GetSystemTimes deltas), memory load, battery.</summary>
    public static class SysMonFeature
    {
        static long _pIdle, _pKernel, _pUser;
        static bool _hasPrev;

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "sysmon", L.T("f_sysmon"), Build, 300);
        }

        class Meter
        {
            public TextBlock Value;
            public Border Fill;
            public Border Track;
        }

        static Meter MakeMeter(StackPanel parent, string label)
        {
            var head = new DockPanel { Margin = new Thickness(0, 6, 0, 3) };
            var value = Theme.MakeText("…", 12.5, Theme.Text);
            value.FlowDirection = FlowDirection.LeftToRight;
            DockPanel.SetDock(value, Dock.Right);
            head.Children.Add(value);
            head.Children.Add(Theme.MakeText(label, 12.5, Theme.TextDim));
            parent.Children.Add(head);

            var fill = new Border
            {
                Background = Theme.Accent,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0,
            };
            var track = new Border
            {
                Background = Theme.InputBg,
                CornerRadius = new CornerRadius(3),
                Height = 6,
                Child = fill,
            };
            parent.Children.Add(track);
            return new Meter { Value = value, Fill = fill, Track = track };
        }

        static void Set(Meter m, double pct, string text)
        {
            double w = m.Track.ActualWidth > 0 ? m.Track.ActualWidth : 240;
            m.Fill.Width = Math.Max(0, Math.Min(1, pct / 100.0)) * w;
            m.Value.Text = text;
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel { MinWidth = 240 };
            var cpu = MakeMeter(stack, L.T("sm_cpu"));
            var ram = MakeMeter(stack, L.T("sm_ram"));
            var batt = MakeMeter(stack, L.T("sm_battery"));

            Action refresh = () =>
            {
                // CPU — delta between consecutive samples
                long idle, kernel, user;
                if (Win32.SampleSystemTimes(out idle, out kernel, out user))
                {
                    if (_hasPrev)
                    {
                        double pct = CpuSampler.Percent(
                            (ulong)(idle - _pIdle), (ulong)(kernel - _pKernel), (ulong)(user - _pUser));
                        Set(cpu, pct, pct.ToString("0.#") + "%");
                    }
                    _pIdle = idle; _pKernel = kernel; _pUser = user; _hasPrev = true;
                }

                double usedPct; ulong freeB, totalB;
                if (Win32.MemoryStatus(out usedPct, out freeB, out totalB))
                    Set(ram, usedPct, usedPct.ToString("0") + "%  ·  " +
                        string.Format(L.T("sm_free"), CleanEngine.FormatBytes((long)freeB)));

                try
                {
                    var ps = System.Windows.Forms.SystemInformation.PowerStatus;
                    if (ps.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery))
                        Set(batt, 0, L.T("sm_nobatt"));
                    else
                    {
                        double p = ps.BatteryLifePercent <= 1 ? ps.BatteryLifePercent * 100 : 0;
                        string txt = p.ToString("0") + "%";
                        if (ps.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online)
                            txt += "  ·  " + L.T("sm_charging");
                        Set(batt, p, txt);
                    }
                }
                catch { batt.Value.Text = "—"; }
            };

            // Live bars are the whole point of this panel, so the cadence stays at a second.
            // But panels are pinned open by default: one left behind a maximised window was
            // redrawing meters nobody was reading, once a second, for hours.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Settings.SaverActive ? 3 : 1) };
            timer.Tick += (s, e) => { if (host.IsVisible) refresh(); };
            host.Loaded += (s, e) => refresh();
            host.Closed += (s, e) => timer.Stop();
            timer.Start();
            return stack;
        }
    }
}
