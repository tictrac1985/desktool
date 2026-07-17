using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DeskMate.Core;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class PowerFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "power", L.T("f_power"), Build);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var hint = Theme.MakeText(L.T("pw_hint"), 11.5, Theme.TextFaint);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 0, 0, 4);
            stack.Children.Add(hint);

            var result = Theme.MakeText("", 12.5, Theme.Accent);
            result.HorizontalAlignment = HorizontalAlignment.Center;
            result.Margin = new Thickness(0, 8, 0, 0);

            stack.Children.Add(Theme.SectionTitle(L.T("pw_ac")));
            stack.Children.Add(MakeRow("ac", result));
            stack.Children.Add(Theme.SectionTitle(L.T("pw_dc")));
            stack.Children.Add(MakeRow("dc", result));
            stack.Children.Add(result);
            return stack;
        }

        static WrapPanel MakeRow(string acdc, TextBlock result)
        {
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            foreach (int minutes in new[] { 0, 5, 10, 15, 30, 60 })
            {
                int m = minutes;
                string label = m == 0 ? L.T("pw_never") : string.Format(L.T("pw_min"), m);
                var b = Theme.MakeButton(label, () =>
                {
                    SetTimeout(acdc, m);
                    result.Text = L.T("pw_applied");
                });
                b.Padding = new Thickness(10, 5, 10, 5);
                row.Children.Add(b);
            }
            return row;
        }

        static void SetTimeout(string acdc, int minutes)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/change standby-timeout-" + acdc + " " + minutes,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                };
                using (var p = Process.Start(psi)) p.WaitForExit(4000);
            }
            catch (Exception ex) { Log.Error("powercfg", ex); }
        }
    }
}
