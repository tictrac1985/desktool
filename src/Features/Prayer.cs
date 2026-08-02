using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class PrayerFeature
    {
        static readonly string[] NameKeys = { "pr_fajr", "pr_sunrise", "pr_dhuhr", "pr_asr", "pr_maghrib", "pr_isha" };
        static string _lastNotifyKey;
        static DateTime _lastComputeDay;
        static double[] _today;

        public static PrayerCalc.Method EffectiveMethod
        {
            get
            {
                if (Settings.I.PrayerMethod >= 0 && Settings.I.PrayerMethod <= 4)
                    return (PrayerCalc.Method)Settings.I.PrayerMethod;
                return PrayerCalc.AutoMethod(Settings.I.Country);
            }
        }

        static double[] TimesFor(DateTime date)
        {
            double tz = TimeZoneInfo.Local.GetUtcOffset(date).TotalHours;
            return PrayerCalc.Times(date, Settings.I.Lat, Settings.I.Lon, tz, EffectiveMethod);
        }

        static double[] Today(DateTime now)
        {
            if (_today == null || _lastComputeDay.Date != now.Date)
            {
                if (!WeatherFeature.HasLocation) return null;
                _today = TimesFor(now.Date);
                _lastComputeDay = now.Date;
            }
            return _today;
        }

        /// <summary>Invalidate cached times after settings change.</summary>
        public static void Invalidate() { _today = null; }

        /// <summary>Next prayer (skips sunrise): returns index and DateTime, or -1 when unknown.</summary>
        public static int Next(DateTime now, out DateTime at)
        {
            at = DateTime.MinValue;
            var t = Today(now);
            if (t == null) return -1;
            double h = now.TimeOfDay.TotalHours;
            for (int i = 0; i < 6; i++)
            {
                if (i == 1) continue; // sunrise is informational, not a prayer
                if (!double.IsNaN(t[i]) && t[i] > h)
                {
                    at = now.Date.AddHours(t[i]);
                    return i;
                }
            }
            // tomorrow's fajr
            var tomorrow = TimesFor(now.Date.AddDays(1));
            if (tomorrow != null && !double.IsNaN(tomorrow[0]))
            {
                at = now.Date.AddDays(1).AddHours(tomorrow[0]);
                return 0;
            }
            return -1;
        }

        /// <summary>Widget line: "الصلاة القادمة: العشاء بعد 01:23".</summary>
        public static string NextLine(DateTime now)
        {
            DateTime at;
            int idx = Next(now, out at);
            if (idx < 0) return "";
            var left = at - now;
            string dur = string.Format("{0:0}:{1:00}", Math.Floor(left.TotalHours), left.Minutes);
            return L.T(NameKeys[idx]) + " " + L.T("pr_in") + " " + dur;
        }

        /// <summary>Top of the last minute that was already evaluated.</summary>
        static DateTime _lastMinuteHandled = DateTime.MinValue;

        /// <summary>Called every second from the widget timer; fires notifications once per event.</summary>
        public static void MinuteTick(DateTime now)
        {
            if (!Settings.I.ShowPrayer || !Settings.I.PrayerNotify || !WeatherFeature.HasLocation) return;

            // Do NOT gate on `now.Second == 0`: a DispatcherTimer runs at Background priority and
            // under load a whole second gets skipped, which silently dropped the alert for that
            // minute. Track the minute instead, so every minute is evaluated exactly once
            // whatever second the tick actually lands on.
            var minute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            if (minute == _lastMinuteHandled) return;
            _lastMinuteHandled = minute;

            var t = Today(now);
            if (t == null) return;
            // anchor on the top of the minute so the windows below tile without gaps
            double h = minute.TimeOfDay.TotalHours;
            for (int i = 0; i < 6; i++)
            {
                if (i == 1 || double.IsNaN(t[i])) continue;
                double diffMin = (t[i] - h) * 60.0;
                int pre = Settings.I.PrayerPreAlertMin;
                if (diffMin > -1.0 && diffMin <= 0.0)
                    Notify(i, 0, now);
                else if (pre > 0 && diffMin > pre - 1.0 && diffMin <= pre)
                    Notify(i, pre, now);
            }
        }

        static void Notify(int prayerIdx, int minutesBefore, DateTime now)
        {
            string key = now.ToString("yyyyMMdd") + "-" + prayerIdx + "-" + minutesBefore;
            if (key == _lastNotifyKey) return;
            _lastNotifyKey = key;
            string name = L.T(NameKeys[prayerIdx]);
            string msg = minutesBefore == 0
                ? string.Format(L.T("pr_now"), name)
                : string.Format(L.T("pr_pre"), name, minutesBefore);
            AlertSound.Play();
            Toast.Show(msg, Glyphs.Prayer);
        }

        static void PlaySoundRow(PanelWindow host, StackPanel stack)
        {
            stack.Children.Add(Theme.SectionTitle(L.T("pr_sound")));
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var testBtn = Theme.MakeButton(L.T("pr_snd_test"), () => AlertSound.Play(), padding: new Thickness(10, 5, 10, 5));
            DockPanel.SetDock(testBtn, Dock.Right);
            row.Children.Add(testBtn);

            var combo = Theme.MakeCombo();
            combo.Items.Add(L.T("pr_snd_system"));
            combo.Items.Add(L.T("pr_snd_chime"));
            combo.Items.Add(L.T("pr_snd_custom"));
            combo.SelectedIndex = Settings.I.AdhanSound;
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            combo.SelectionChanged += (s, e) =>
            {
                if (combo.SelectedIndex == 2)
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "WAV|*.wav",
                        CheckFileExists = true,
                    };
                    host.SuppressAutoClose = true;
                    try
                    {
                        if (dlg.ShowDialog(host) == true)
                        {
                            Settings.I.AdhanSound = 2;
                            Settings.I.AdhanPath = dlg.FileName;
                        }
                        else combo.SelectedIndex = Settings.I.AdhanSound; // cancelled → revert
                    }
                    finally
                    {
                        host.SuppressAutoClose = false;
                        try { host.Activate(); } catch { }
                    }
                }
                else Settings.I.AdhanSound = combo.SelectedIndex;
                Settings.I.Save();
            };
            row.Children.Add(combo);
            stack.Children.Add(row);
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "prayer", L.T("f_prayer"), Build);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            if (!WeatherFeature.HasLocation)
            {
                var msg = Theme.MakeText(L.T("pr_need_loc"), 13, Theme.TextDim);
                msg.TextWrapping = TextWrapping.Wrap;
                msg.Margin = new Thickness(0, 4, 0, 8);
                stack.Children.Add(msg);
                var status = Theme.MakeText("", 12, Theme.Accent);
                status.HorizontalAlignment = HorizontalAlignment.Center;
                var locBtn = Theme.MakeButton(L.T("pr_locate"), () =>
                {
                    status.Text = L.T("we_locating");
                    Task.Run(() => WeatherFeature.AutoLocate()).ContinueWith(t =>
                    {
                        host.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (!t.IsFaulted && t.Result != null)
                            {
                                Invalidate();
                                PanelWindow.CloseCurrent();
                                Open(MainWindow.Instance);
                            }
                            else status.Text = L.T("we_err");
                        }));
                    });
                }, accent: true);
                locBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
                stack.Children.Add(locBtn);
                stack.Children.Add(status);
                return stack;
            }

            var now = DateTime.Now;
            var times = Today(now);
            DateTime nextAt;
            int nextIdx = Next(now, out nextAt);

            if (nextIdx >= 0)
            {
                var nextCard = new Border
                {
                    Background = Theme.ButtonBg,
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 0, 10),
                };
                var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var lbl = Theme.MakeText(L.T("pr_next"), 11.5, Theme.TextFaint);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                inner.Children.Add(lbl);
                var big = Theme.MakeText(L.T(NameKeys[nextIdx]) + "  " +
                    PrayerCalc.FormatTime(nextAt.TimeOfDay.TotalHours, Settings.I.Use24h), 19, Theme.Accent);
                big.HorizontalAlignment = HorizontalAlignment.Center;
                big.FontWeight = FontWeights.SemiBold;
                inner.Children.Add(big);
                var left = nextAt - now;
                var cd = Theme.MakeText(L.T("pr_in") + " " +
                    string.Format("{0:0}:{1:00}", Math.Floor(left.TotalHours), left.Minutes), 12.5, Theme.TextDim);
                cd.HorizontalAlignment = HorizontalAlignment.Center;
                inner.Children.Add(cd);
                nextCard.Child = inner;
                stack.Children.Add(nextCard);
            }

            if (times != null)
            {
                for (int i = 0; i < 6; i++)
                {
                    var rowDock = new DockPanel { Margin = new Thickness(4, 3, 4, 3) };
                    bool isNext = i == nextIdx;
                    var timeText = Theme.MakeText(PrayerCalc.FormatTime(times[i], Settings.I.Use24h), 13.5,
                        isNext ? Theme.Accent : Theme.Text);
                    timeText.FlowDirection = FlowDirection.LeftToRight;
                    DockPanel.SetDock(timeText, Dock.Right);
                    rowDock.Children.Add(timeText);
                    var nameText = Theme.MakeText(L.T(NameKeys[i]), 13.5,
                        i == 1 ? Theme.TextFaint : (isNext ? Theme.Accent : Theme.TextDim));
                    if (isNext) nameText.FontWeight = FontWeights.SemiBold;
                    rowDock.Children.Add(nameText);
                    stack.Children.Add(rowDock);
                }
            }

            stack.Children.Add(Theme.SectionTitle(L.T("pr_method")));
            var combo = Theme.MakeCombo();
            combo.Items.Add(L.T("pr_m_auto"));
            foreach (var k in new[] { "pr_m_mwl", "pr_m_egypt", "pr_m_karachi", "pr_m_isna", "pr_m_umm" })
                combo.Items.Add(L.T(k));
            combo.SelectedIndex = Settings.I.PrayerMethod >= 0 && Settings.I.PrayerMethod <= 4 ? Settings.I.PrayerMethod + 1 : 0;
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            combo.SelectionChanged += (s, e) =>
            {
                Settings.I.PrayerMethod = combo.SelectedIndex - 1;
                Settings.I.Save();
                Invalidate();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            };
            stack.Children.Add(combo);

            stack.Children.Add(Theme.MakeCheck(L.T("pr_notify"), Settings.I.PrayerNotify, v =>
            {
                Settings.I.PrayerNotify = v;
                Settings.I.Save();
            }));

            var preRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var preCombo = Theme.MakeCombo();
            foreach (int m in new[] { 0, 5, 10, 15, 30 }) preCombo.Items.Add(m.ToString());
            int cur = Settings.I.PrayerPreAlertMin;
            preCombo.SelectedIndex = cur == 0 ? 0 : cur == 5 ? 1 : cur == 10 ? 2 : cur == 15 ? 3 : cur == 30 ? 4 : 2;
            preCombo.MinWidth = 60;
            preCombo.SelectionChanged += (s, e) =>
            {
                Settings.I.PrayerPreAlertMin = int.Parse((string)preCombo.SelectedItem);
                Settings.I.Save();
            };
            DockPanel.SetDock(preCombo, Dock.Right);
            preRow.Children.Add(preCombo);
            var preLbl = Theme.MakeText(L.T("pr_prealert"), 13, Theme.TextDim);
            preLbl.VerticalAlignment = VerticalAlignment.Center;
            preRow.Children.Add(preLbl);
            stack.Children.Add(preRow);

            PlaySoundRow(host, stack);

            return stack;
        }
    }
}
