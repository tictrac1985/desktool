using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class CleanerFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "cleaner", L.T("f_cleaner"), Build);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            bool userTemp = true, winTemp = true, inet = true, recycle = false;
            stack.Children.Add(Theme.MakeCheck(L.T("cl_temp"), userTemp, v => userTemp = v));
            stack.Children.Add(Theme.MakeCheck(L.T("cl_wintemp"), winTemp, v => winTemp = v));
            stack.Children.Add(Theme.MakeCheck(L.T("cl_inet"), inet, v => inet = v));
            stack.Children.Add(Theme.MakeCheck(L.T("cl_recycle"), recycle, v => recycle = v));

            var note = Theme.MakeText(L.T("cl_note"), 11, Theme.TextFaint);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 4, 0, 8);
            stack.Children.Add(note);

            var result = Theme.MakeText("", 13, Theme.Accent);
            result.HorizontalAlignment = HorizontalAlignment.Center;
            result.Margin = new Thickness(0, 8, 0, 0);

            Border runBtn = null;
            runBtn = Theme.MakeButton(L.T("cl_run"), () =>
            {
                runBtn.IsEnabled = false;
                result.Text = L.T("cl_running");
                var dirs = new List<string>();
                string temp = Path.GetTempPath();
                string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (userTemp && !string.IsNullOrEmpty(temp)) dirs.Add(temp.TrimEnd('\\'));
                if (winTemp && !string.IsNullOrEmpty(win)) dirs.Add(Path.Combine(win, "Temp"));
                if (inet && !string.IsNullOrEmpty(local)) dirs.Add(Path.Combine(local, @"Microsoft\Windows\INetCache"));
                bool doRecycle = recycle;
                Task.Run(() =>
                {
                    long freed = CleanEngine.Clean(dirs);
                    if (doRecycle) Win32.EmptyRecycleBin();
                    return freed;
                }).ContinueWith(t =>
                {
                    long freed = t.IsFaulted ? 0 : t.Result;
                    host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        result.Text = string.Format(L.T("cl_done"), CleanEngine.FormatBytes(freed));
                        runBtn.IsEnabled = true;
                    }));
                });
            }, accent: true);
            runBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(runBtn);
            stack.Children.Add(result);
            return stack;
        }
    }
}
