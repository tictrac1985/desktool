using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Countdowns to Hijri occasions (Ramadan, both Eids) plus one custom Gregorian date.</summary>
    public static class OccasionsFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "occasions", L.T("f_occasions"), Build, 310);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var today = DateTime.Now;

            var entries = new System.Collections.Generic.List<Tuple<string, DateTime?, string>>();
            foreach (var oc in OccasionCore.Builtin)
                entries.Add(Tuple.Create(L.IsAr ? oc.KeyAr : oc.KeyEn,
                    OccasionCore.NextHijri(oc.Month, oc.Day, today), "h"));

            DateTime custom;

            // nearest builtin gets the hero card
            int nearestIdx = -1;
            int best = int.MaxValue;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Item2 == null) continue;
                int left = OccasionCore.DaysLeft(today, entries[i].Item2.Value);
                if (left < best) { best = left; nearestIdx = i; }
            }

            if (nearestIdx >= 0)
            {
                var heroInner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var lbl = Theme.MakeText(L.T("oc_nearest"), 11.5, Theme.TextFaint);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                heroInner.Children.Add(lbl);
                var big = Theme.MakeText(entries[nearestIdx].Item1, 19, Theme.Accent);
                big.FontWeight = FontWeights.SemiBold;
                big.HorizontalAlignment = HorizontalAlignment.Center;
                heroInner.Children.Add(big);
                var cd = Theme.MakeText(LeftText(best), 13, Theme.TextDim);
                cd.HorizontalAlignment = HorizontalAlignment.Center;
                heroInner.Children.Add(cd);
                var hero = new Border
                {
                    Background = Theme.AccentSoft,
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 10),
                    Child = heroInner,
                };
                stack.Children.Add(hero);
            }

            bool hasStored = DateTime.TryParseExact(Settings.I.OccasionCustom, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out custom);
            DateTime? customNext = null;
            if (hasStored)
            {
                if (Settings.I.OccasionCustomYearly)
                    customNext = OccasionCore.NextGregorian(custom.Month, custom.Day, today);
                else if (custom.Date >= today.Date)
                    customNext = custom.Date; // one-time: only while still ahead
            }
            bool hasCustom = customNext != null;

            var ci = L.IsAr ? new CultureInfo("ar-EG") : new CultureInfo("en-US");
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Item2 == null) continue;
                var row = MakeRow(entries[i].Item1,
                    entries[i].Item2.Value.ToString("d MMMM yyyy", ci),
                    LeftText(OccasionCore.DaysLeft(today, entries[i].Item2.Value)),
                    i == nearestIdx, null);
                stack.Children.Add(row);
            }

            if (hasCustom)
            {
                string cname = string.IsNullOrWhiteSpace(Settings.I.OccasionCustomName)
                    ? L.T("oc_custom_short") : Settings.I.OccasionCustomName.Trim();
                var row = MakeRow(cname,
                    customNext.Value.ToString("d MMMM yyyy", ci),
                    LeftText(OccasionCore.DaysLeft(today, customNext.Value)),
                    false, () =>
                    {
                        Settings.I.OccasionCustom = "";
                        Settings.I.OccasionCustomName = "";
                        Settings.I.Save();
                        PanelWindow.CloseCurrent();
                        Open(MainWindow.Instance);
                    });
                stack.Children.Add(row);
            }

            // ---- custom occasion entry: name field + day/month/year pickers ----
            var feedback = Theme.MakeText("", 11.5, Theme.Accent);
            stack.Children.Add(Theme.SectionTitle(L.T("oc_custom")));

            var nameCap = Theme.MakeText(L.T("oc_name"), 11.5, Theme.TextFaint);
            stack.Children.Add(nameCap);
            var nameInput = Theme.MakeInput(Settings.I.OccasionCustomName);
            nameInput.Margin = new Thickness(0, 2, 0, 6);
            stack.Children.Add(nameInput);

            var dayC = Theme.MakeCombo(); dayC.MinWidth = 0; dayC.Width = 58;
            var monC = Theme.MakeCombo(); monC.MinWidth = 0; monC.Width = 116;
            var yrC = Theme.MakeCombo(); yrC.MinWidth = 0; yrC.Width = 84;
            string[] monthNames = ci.DateTimeFormat.MonthNames;
            for (int m = 0; m < 12; m++) monC.Items.Add(monthNames[m]);
            for (int y = today.Year; y <= today.Year + 10; y++)
                yrC.Items.Add(y.ToString(CultureInfo.InvariantCulture));

            Action refreshDays = () =>
            {
                if (monC.SelectedIndex < 0 || yrC.SelectedIndex < 0) return;
                int keep = dayC.SelectedIndex + 1;
                int dim = DateTime.DaysInMonth(today.Year + yrC.SelectedIndex, monC.SelectedIndex + 1);
                dayC.Items.Clear();
                for (int d = 1; d <= dim; d++) dayC.Items.Add(d.ToString());
                dayC.SelectedIndex = Math.Min(Math.Max(keep, 1), dim) - 1;
            };
            monC.SelectionChanged += (s, e) => refreshDays();
            yrC.SelectionChanged += (s, e) => refreshDays();

            // preselect the stored date (or today when empty)
            int initDay = hasStored ? custom.Day : today.Day;
            monC.SelectedIndex = (hasStored ? custom.Month : today.Month) - 1;
            int yrIdx = hasStored ? custom.Year - today.Year : 0;
            yrC.SelectedIndex = Math.Min(Math.Max(yrIdx, 0), 10);
            refreshDays();
            dayC.SelectedIndex = Math.Min(initDay, dayC.Items.Count) - 1;

            var pickRow = new StackPanel { Orientation = Orientation.Horizontal };
            dayC.Margin = new Thickness(0, 0, 4, 0);
            monC.Margin = new Thickness(0, 0, 4, 0);
            pickRow.Children.Add(dayC);
            pickRow.Children.Add(monC);
            pickRow.Children.Add(yrC);
            stack.Children.Add(pickRow);

            var actionRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var yearly = Theme.MakeCheck(L.T("oc_yearly"), Settings.I.OccasionCustomYearly, v => { });
            var addBtn = Theme.MakeButton(L.T("oc_add"), () =>
            {
                if (dayC.SelectedIndex < 0 || monC.SelectedIndex < 0 || yrC.SelectedIndex < 0)
                {
                    feedback.Text = L.T("oc_bad");
                    return;
                }
                DateTime d;
                try { d = new DateTime(today.Year + yrC.SelectedIndex, monC.SelectedIndex + 1, dayC.SelectedIndex + 1); }
                catch { feedback.Text = L.T("oc_bad"); return; }
                Settings.I.OccasionCustom = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Settings.I.OccasionCustomName = nameInput.Text.Trim();
                Settings.I.OccasionCustomYearly = yearly.IsChecked == true;
                Settings.I.Save();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            }, padding: new Thickness(16, 4, 16, 4));
            DockPanel.SetDock(addBtn, Dock.Right);
            actionRow.Children.Add(addBtn);
            actionRow.Children.Add(yearly);
            stack.Children.Add(actionRow);

            feedback.HorizontalAlignment = HorizontalAlignment.Center;
            feedback.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(feedback);

            return stack;
        }

        static string LeftText(int days)
        {
            if (days <= 0) return L.T("oc_today");
            if (days == 1) return L.T("oc_tomorrow");
            return string.Format(L.T("oc_left"), days);
        }

        static Border MakeRow(string name, string date, string left, bool highlight, Action onRemove)
        {
            var dock = new DockPanel();
            var badge = new Border
            {
                Background = highlight ? Theme.Accent : Theme.AccentSoft,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 3, 9, 3),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var badgeText = Theme.MakeText(left, 11.5,
                highlight ? (Theme.IsLight ? Brushes.White : Brushes.Black) : Theme.Accent);
            badgeText.FontWeight = highlight ? FontWeights.SemiBold : FontWeights.Normal;
            badge.Child = badgeText;
            DockPanel.SetDock(badge, Dock.Right);
            dock.Children.Add(badge);

            if (onRemove != null)
            {
                var del = new Border
                {
                    Width = 22, Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = Brushes.Transparent,
                    Child = Theme.MakeGlyph("", 9, Theme.TextDim),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(2, 0, 2, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Theme.AttachHover(del, Brushes.Transparent, Theme.HoverBg);
                del.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                del.MouseLeftButtonUp += (s, e) => { e.Handled = true; onRemove(); };
                DockPanel.SetDock(del, Dock.Right);
                dock.Children.Add(del);
            }

            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameText = Theme.MakeText(name, 13.5, highlight ? Theme.Accent : Theme.Text);
            if (highlight) nameText.FontWeight = FontWeights.SemiBold;
            texts.Children.Add(nameText);
            var dateText = Theme.MakeText(date, 11, Theme.TextFaint);
            texts.Children.Add(dateText);
            dock.Children.Add(texts);

            var row = new Border
            {
                Background = highlight ? Theme.ButtonBg : Brushes.Transparent,
                BorderBrush = highlight ? Theme.Accent : null,
                BorderThickness = highlight ? new Thickness(1) : new Thickness(0),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 2, 0, 2),
                Child = dock,
            };
            return row;
        }
    }
}
