using System;
using System.Windows;
using System.Windows.Controls;
using DeskMate.Core;
using DeskMate.UI;
using WF = System.Windows.Forms;

namespace DeskMate.Features
{
    public static class WallpaperFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "wallpaper", L.T("f_wallpaper"), Build);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            string selectedPath = null;

            var fileLabel = Theme.MakeText(L.T("wp_pick"), 12.5, Theme.TextDim);
            fileLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            fileLabel.MaxWidth = 260;

            var pickBtn = Theme.MakeButton(L.T("wp_pick"), () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp",
                    CheckFileExists = true,
                };
                // keep the panel alive while the modal dialog steals activation
                host.SuppressAutoClose = true;
                try
                {
                    if (dlg.ShowDialog(host) == true)
                    {
                        selectedPath = dlg.FileName;
                        fileLabel.Text = System.IO.Path.GetFileName(selectedPath);
                        fileLabel.Foreground = Theme.Text;
                    }
                }
                finally
                {
                    host.SuppressAutoClose = false;
                    try { host.Activate(); } catch { }
                }
            });
            pickBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(pickBtn);
            fileLabel.Margin = new Thickness(2, 4, 2, 8);
            stack.Children.Add(fileLabel);

            stack.Children.Add(Theme.SectionTitle(L.T("wp_style")));
            var combo = Theme.MakeCombo();
            var styles = new[] { "fill", "fit", "stretch", "tile", "center" };
            foreach (var st in styles) combo.Items.Add(L.T("wp_" + st));
            combo.SelectedIndex = 0;
            combo.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(combo);

            var result = Theme.MakeText("", 12.5, Theme.Accent);
            result.HorizontalAlignment = HorizontalAlignment.Center;
            result.Margin = new Thickness(0, 8, 0, 0);

            var applyBtn = Theme.MakeButton(L.T("wp_apply"), () =>
            {
                if (string.IsNullOrEmpty(selectedPath)) { result.Text = L.T("wp_pick"); return; }
                bool ok = Win32.SetWallpaper(selectedPath, styles[Math.Max(0, combo.SelectedIndex)]);
                result.Text = ok ? L.T("wp_done") : L.T("wp_err");
            }, accent: true);
            applyBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            applyBtn.Margin = new Thickness(3, 12, 3, 0);
            stack.Children.Add(applyBtn);
            stack.Children.Add(result);
            return stack;
        }
    }
}
