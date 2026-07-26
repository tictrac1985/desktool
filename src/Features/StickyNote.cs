using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>One persistent sticky note; auto-saves shortly after typing stops.</summary>
    public static class StickyNoteFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "note", L.T("f_note"), Build, 300);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();

            var box = Theme.MakeInput(Settings.I.StickyNote);
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 160;
            box.VerticalContentAlignment = VerticalAlignment.Top;
            box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            var placeholder = Theme.MakeText(L.T("nt_hint"), 12.5, Theme.TextFaint);
            placeholder.Margin = new Thickness(11, 9, 11, 0);
            placeholder.IsHitTestVisible = false;
            placeholder.TextWrapping = TextWrapping.Wrap;
            placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

            var host2 = new Grid();
            host2.Children.Add(box);
            host2.Children.Add(placeholder);
            stack.Children.Add(host2);

            var saved = Theme.MakeText("", 11, Theme.Accent);
            saved.HorizontalAlignment = HorizontalAlignment.Right;
            saved.Margin = new Thickness(0, 4, 2, 0);
            stack.Children.Add(saved);

            var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            debounce.Tick += (s, e) =>
            {
                debounce.Stop();
                Settings.I.StickyNote = box.Text;
                Settings.I.Save();
                saved.Text = L.T("nt_saved");
                var fade = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
                fade.Tick += (s2, e2) => { fade.Stop(); saved.Text = ""; };
                fade.Start();
            };
            box.TextChanged += (s, e) =>
            {
                placeholder.Visibility = box.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                saved.Text = "";
                debounce.Stop();
                debounce.Start();
            };
            host.Closed += (s, e) =>
            {
                debounce.Stop();
                if (Settings.I.StickyNote != box.Text)
                {
                    Settings.I.StickyNote = box.Text;
                    Settings.I.Save();
                }
            };
            host.Loaded += (s, e) => box.Focus();
            return stack;
        }
    }
}
