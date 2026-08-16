using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Quick-command bar: one line that becomes a task with a reminder, a
    /// calculation, or an app launch. The live preview under the box always says what
    /// Enter will do, so the bar can never surprise — the parse is visible before it runs.</summary>
    public static class CommandBarFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "command", L.T("f_command"), Build, 360);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            var box = Theme.MakeInput("");
            box.FontSize = 14;
            var boxHost = new Grid();
            var placeholder = Theme.MakeText(L.T("cb_hint"), 12, Theme.TextFaint);
            placeholder.Margin = new Thickness(10, 0, 10, 0);
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.IsHitTestVisible = false;
            boxHost.Children.Add(box);
            boxHost.Children.Add(placeholder);
            stack.Children.Add(boxHost);

            // the parse, spoken aloud before anything happens
            var preview = Theme.MakeText("", 12.5, Theme.Accent);
            preview.TextWrapping = TextWrapping.Wrap;
            preview.Margin = new Thickness(2, 8, 2, 0);
            stack.Children.Add(preview);

            Action execute = () =>
            {
                var cmd = CommandParse.Parse(box.Text, DateTime.Now);
                switch (cmd.Kind)
                {
                    case CommandParse.Calc:
                        try
                        {
                            double v = ExpressionEvaluator.Eval(
                                ExpressionEvaluator.NormalizePercentPhrase(cmd.Text));
                            box.Text = v.ToString("G12", CultureInfo.InvariantCulture);
                            box.CaretIndex = box.Text.Length;
                        }
                        catch { preview.Text = L.T("calc_error"); }
                        return;   // stay open: a calculation usually continues
                    case CommandParse.OpenApp:
                        if (cmd.Text.Length == 0) return;
                        if (AppsFeature.LaunchBest(cmd.Text))
                        {
                            PanelWindow.CloseCurrent();
                        }
                        else preview.Text = L.T("cb_no_app");
                        return;
                    default:   // task
                        if (cmd.Text.Length == 0) return;
                        var notes = StickyNoteFeature.Load();
                        notes.Insert(0, new NoteItem { Text = cmd.Text, RemindAt = cmd.When });
                        StickyNoteFeature.Save(notes);
                        PanelWindow.CloseCurrent();
                        Toast.Show(L.T("cb_added"), Glyphs.Note);
                        return;
                }
            };

            Action refresh = () =>
            {
                placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                string t = box.Text.Trim();
                if (t.Length == 0) { preview.Text = ""; return; }
                var cmd = CommandParse.Parse(t, DateTime.Now);
                switch (cmd.Kind)
                {
                    case CommandParse.Calc:
                        try
                        {
                            double v = ExpressionEvaluator.Eval(
                                ExpressionEvaluator.NormalizePercentPhrase(cmd.Text));
                            preview.Text = "=  " + v.ToString("G12", CultureInfo.InvariantCulture);
                        }
                        catch { preview.Text = t == "=" ? "" : L.T("calc_error"); }
                        break;
                    case CommandParse.OpenApp:
                        preview.Text = cmd.Text.Length == 0 ? "" :
                            L.T("cb_will_open") + " " + cmd.Text;
                        break;
                    default:
                        if (cmd.Text.Length == 0) { preview.Text = ""; break; }
                        preview.Text = cmd.When == DateTime.MinValue
                            ? string.Format(L.T("cb_will_task"), cmd.Text)
                            : string.Format(L.T("cb_will_remind"), cmd.Text, DescribeWhen(cmd.When));
                        break;
                }
            };

            box.TextChanged += (s, e) => refresh();
            box.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; execute(); }
            };
            host.Loaded += (s, e) => box.Focus();
            return stack;
        }

        static string DescribeWhen(DateTime at)
        {
            var ci = L.IsAr ? new CultureInfo("ar-EG") : new CultureInfo("en-US");
            var today = DateTime.Now.Date;
            string time = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            int days = (int)(at.Date - today).TotalDays;
            if (days == 0) return L.T("nt_today") + " · " + time;
            if (days == 1) return L.T("nt_tomorrow") + " · " + time;
            return at.ToString("dddd d MMMM", ci) + " · " + time;
        }
    }
}
