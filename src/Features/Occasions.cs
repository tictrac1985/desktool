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
            bool hasCustom = DateTime.TryParseExact(Settings.I.OccasionCustom, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out custom);
            if (hasCustom && custom.Date < today.Date) hasCustom = false; // past custom date is stale

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
                var row = MakeRow(L.T("oc_custom_short"),
                    custom.ToString("d MMMM yyyy", ci),
                    LeftText(OccasionCore.DaysLeft(today, custom)),
                    false, () =>
                    {
                        Settings.I.OccasionCustom = "";
                        Settings.I.Save();
                        PanelWindow.CloseCurrent();
                        Open(MainWindow.Instance);
                    });
                stack.Children.Add(row);
            }

            // ---- custom date entry ----
            var feedback = Theme.MakeText("", 11.5, Theme.Accent);
            stack.Children.Add(Theme.SectionTitle(L.T("oc_custom")));
            var inputRow = new DockPanel();
            var input = Theme.MakeInput(Settings.I.OccasionCustom);
            var addBtn = Theme.MakeButton("+", () =>
            {
                DateTime d;
                if (!DateTime.TryParseExact(input.Text.Trim(), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                {
                    feedback.Text = L.T("oc_bad");
                    return;
                }
                Settings.I.OccasionCustom = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                Settings.I.Save();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            }, padding: new Thickness(12, 4, 12, 4));
            DockPanel.SetDock(addBtn, Dock.Right);
            inputRow.Children.Add(addBtn);
            inputRow.Children.Add(input);
            stack.Children.Add(inputRow);

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
