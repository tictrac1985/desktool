using System;
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
    /// <summary>A month grid with the Hijri and Gregorian calendars side by side: the big
    /// number of each cell is the calendar the user put first, the small one underneath is
    /// the other. Steps month by month, jumps back to today, and spells both dates of any
    /// tapped day out in full. All calendar maths lives in <see cref="CalendarCore"/>.</summary>
    public static class CalendarFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "calendar", L.T("f_calendar"), Build, 340);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            bool ar = L.IsAr;
            var today = DateTime.Today;
            bool hijri = Settings.I.CalHijriFirst;
            int year = today.Year, month = today.Month;
            if (hijri)
            {
                int ty, tm, td;
                if (CalendarCore.TryHijri(today, out ty, out tm, out td)) { year = ty; month = tm; }
                else hijri = false;
            }
            CalDay selected = null;
            DayOfWeek weekStart = (DayOfWeek)Settings.I.WeekStart;
            string country = Settings.I.Country;

            var stack = new StackPanel();

            var title = Theme.MakeText("", 16, Theme.Text);
            title.FontWeight = FontWeights.Bold;
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.TextAlignment = TextAlignment.Center;
            var sub = Theme.MakeText("", 11.5, Theme.TextFaint);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            sub.TextAlignment = TextAlignment.Center;

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dayHeads = new UniformGrid { Columns = 7, Rows = 1, Margin = new Thickness(0, 10, 0, 0) };
            var grid = new UniformGrid { Columns = 7, Rows = 6, Margin = new Thickness(0, 0, 0, 4) };
            var detail = Theme.MakeText("", 12.5, Theme.Text);
            detail.TextWrapping = TextWrapping.Wrap;
            detail.TextAlignment = TextAlignment.Center;
            detail.HorizontalAlignment = HorizontalAlignment.Center;
            var detail2 = Theme.MakeText("", 11.5, Theme.TextDim);
            detail2.TextAlignment = TextAlignment.Center;
            detail2.HorizontalAlignment = HorizontalAlignment.Center;
            detail2.TextWrapping = TextWrapping.Wrap;

            Action render = null;
            Action<int> step = delta =>
            {
                int y = year, m = month;
                if (hijri)
                {
                    CalendarCore.Step(ref y, ref m, delta);
                    if (CalendarCore.BuildHijri(y, m, weekStart, today, country, ar) == null) return;
                }
                else CalendarCore.Step(ref y, ref m, delta);
                if (!hijri && (y < 1900 || y > 2200)) return;
                year = y; month = m; selected = null;
                render();
            };

            // header: ‹  title  ›   — the chevrons mirror with the layout direction
            bool rtl = ar;
            var prev = Theme.MakeIconButton(rtl ? "\uE76C" : "\uE76B", L.T("cal_prev"), () => step(-1), 32, 12);
            var next = Theme.MakeIconButton(rtl ? "\uE76B" : "\uE76C", L.T("cal_next"), () => step(1), 32, 12);
            var titleBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleBox.Children.Add(title);
            titleBox.Children.Add(sub);
            Grid.SetColumn(prev, 0); Grid.SetColumn(titleBox, 1); Grid.SetColumn(next, 2);
            head.Children.Add(prev); head.Children.Add(titleBox); head.Children.Add(next);
            stack.Children.Add(head);

            // calendar-first switch + today
            var tools = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            Border modeBtn = null;
            Action setModeText = () => Theme.SetButtonText(modeBtn, hijri ? L.T("cal_show_greg") : L.T("cal_show_hijri"));
            modeBtn = Theme.MakeButton("", () =>
            {
                // the moment on screen stays the same when the calendars flip: the tapped day,
                // or else the middle of the month being viewed
                DateTime view;
                if (selected != null) view = selected.Date;
                else if (hijri) { if (!CalendarCore.TryFromHijri(year, month, 15, out view)) view = today; }
                else view = new DateTime(year, month, 15);
                hijri = !hijri;
                Settings.I.CalHijriFirst = hijri;
                Settings.I.Save();
                int hy, hm, hd;
                if (hijri)
                {
                    if (!CalendarCore.TryHijri(view, out hy, out hm, out hd)) { hijri = false; Settings.I.CalHijriFirst = false; setModeText(); return; }
                    year = hy; month = hm;
                }
                else { year = view.Year; month = view.Month; }
                setModeText();
                render();
            }, false, new Thickness(12, 5, 12, 5));
            setModeText();
            var todayBtn = Theme.MakeButton(L.T("cal_today"), () =>
            {
                if (hijri)
                {
                    int hy, hm, hd;
                    if (CalendarCore.TryHijri(today, out hy, out hm, out hd)) { year = hy; month = hm; }
                }
                else { year = today.Year; month = today.Month; }
                selected = null;
                render();
            }, false, new Thickness(12, 5, 12, 5));
            tools.Children.Add(modeBtn);
            tools.Children.Add(todayBtn);
            stack.Children.Add(tools);

            stack.Children.Add(dayHeads);
            stack.Children.Add(grid);
            stack.Children.Add(Theme.Divider(4, 6));
            stack.Children.Add(detail);
            stack.Children.Add(detail2);

            var note = Theme.MakeText(L.T("cal_note"), 10.5, Theme.TextFaint);
            note.TextWrapping = TextWrapping.Wrap;
            note.TextAlignment = TextAlignment.Center;
            note.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(note);

            Action showDay = () =>
            {
                var d = selected;
                if (d == null)
                {
                    detail.Text = CalendarCore.Describe(new CalDay { Date = today, HDay = 0 }, ar);
                    int hy, hm, hd;
                    if (CalendarCore.TryHijri(today, out hy, out hm, out hd))
                        detail.Text = CalendarCore.Describe(new CalDay { Date = today, HYear = hy, HMonth = hm, HDay = hd }, ar);
                    detail2.Text = CalendarCore.Relative(today, today, ar);
                    return;
                }
                detail.Text = CalendarCore.Describe(d, ar);
                string rel = CalendarCore.Relative(d.Date, today, ar);
                detail2.Text = string.IsNullOrEmpty(d.Occasion) ? rel : d.Occasion + "  ·  " + rel;
            };

            render = () =>
            {
                CalMonth cm = hijri
                    ? CalendarCore.BuildHijri(year, month, weekStart, today, country, ar)
                    : CalendarCore.BuildGregorian(year, month, weekStart, today, country, ar);
                if (cm == null) { hijri = false; year = today.Year; month = today.Month; cm = CalendarCore.BuildGregorian(year, month, weekStart, today, country, ar); }
                title.Text = cm.Title;
                sub.Text = cm.SubTitle;
                grid.Children.Clear();
                dayHeads.Children.Clear();
                foreach (var dow in CalendarCore.Columns(weekStart))
                {
                    var h = Theme.MakeText(CalendarCore.DayShort(dow, ar), 10.5, Theme.TextFaint);
                    h.HorizontalAlignment = HorizontalAlignment.Center;
                    h.VerticalAlignment = VerticalAlignment.Center;
                    h.Margin = new Thickness(0, 2, 0, 4);
                    dayHeads.Children.Add(h);
                }
                foreach (var cell in cm.Cells) grid.Children.Add(MakeCell(cell, hijri, selected, c => { selected = c; render(); }));
                showDay();
            };

            render();
            return stack;
        }

        static FrameworkElement MakeCell(CalDay d, bool hijriFirst, CalDay selected, Action<CalDay> onPick)
        {
            string big = hijriFirst ? (d.HDay > 0 ? d.HDay.ToString() : "") : d.Date.Day.ToString();
            string small = hijriFirst ? d.Date.Day.ToString() : (d.HDay > 0 ? d.HDay.ToString() : "");
            bool isSel = selected != null && selected.Date.Date == d.Date.Date;

            Brush fg = d.IsToday ? (Theme.IsLight ? Brushes.White : Brushes.Black)
                     : d.IsWeekend ? Theme.Accent : Theme.Text;
            var num = Theme.MakeText(big, 13.5, fg);
            num.HorizontalAlignment = HorizontalAlignment.Center;
            num.FontWeight = d.IsToday ? FontWeights.Bold : FontWeights.Normal;
            var sm = Theme.MakeText(small, 8.5, d.IsToday ? fg : Theme.TextFaint);
            sm.HorizontalAlignment = HorizontalAlignment.Center;
            sm.FlowDirection = FlowDirection.LeftToRight;

            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            col.Children.Add(num);
            col.Children.Add(sm);

            var layers = new Grid();
            layers.Children.Add(col);
            if (!string.IsNullOrEmpty(d.Occasion))
            {
                layers.Children.Add(new System.Windows.Shapes.Ellipse
                {
                    Width = 4, Height = 4,
                    Fill = d.IsToday ? fg : Theme.Accent,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 3, 4, 0),
                });
            }

            var b = new Border
            {
                Height = 40,
                Margin = new Thickness(1.5),
                CornerRadius = new CornerRadius(10),
                Background = d.IsToday ? (Theme.AccentGradient ?? Theme.Accent) : (Brush)Brushes.Transparent,
                BorderBrush = isSel ? Theme.Accent : Brushes.Transparent,
                BorderThickness = new Thickness(1.2),
                Opacity = d.InMonth ? 1 : 0.38,
                Cursor = Cursors.Hand,
                Child = layers,
                SnapsToDevicePixels = true,
            };
            if (!d.IsToday)
            {
                b.MouseEnter += (s, e) => b.Background = Theme.HoverBg;
                b.MouseLeave += (s, e) => b.Background = Brushes.Transparent;
            }
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; onPick(d); };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }
    }
}
