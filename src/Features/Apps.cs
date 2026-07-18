using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class AppsFeature
    {
        static List<AppEntry> _cache;
        static DateTime _cacheTime;

        static List<AppEntry> GetApps()
        {
            if (_cache == null || (DateTime.Now - _cacheTime).TotalMinutes > 5)
            {
                _cache = AppScanner.ScanAll();
                _cacheTime = DateTime.Now;
            }
            return _cache;
        }

        static void Launch(AppEntry app)
        {
            try
            {
                if (!string.IsNullOrEmpty(app.LnkPath))
                    Process.Start(new ProcessStartInfo(app.LnkPath) { UseShellExecute = true });
                else
                    Process.Start(new ProcessStartInfo("explorer.exe", "shell:AppsFolder\\" + app.AppsFolderPath) { UseShellExecute = true });
            }
            catch (Exception ex) { Log.Error("app open", ex); }
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "apps", L.T("f_apps"), Build, 330);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var search = Theme.MakeInput();
            var searchHost = new Grid();
            var placeholder = Theme.MakeText(L.T("ap_search"), 13, Theme.TextFaint);
            placeholder.Margin = new Thickness(10, 0, 10, 0);
            placeholder.VerticalAlignment = VerticalAlignment.Center;
            placeholder.IsHitTestVisible = false;
            searchHost.Children.Add(search);
            searchHost.Children.Add(placeholder);
            stack.Children.Add(searchHost);

            var listHost = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = listHost,
                Height = 340,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 8, 0, 0),
            };
            stack.Children.Add(scroll);

            var feedback = Theme.MakeText("", 12, Theme.Accent);
            feedback.HorizontalAlignment = HorizontalAlignment.Center;
            feedback.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(feedback);

            Action refill = () =>
            {
                string q = search.Text.Trim();
                listHost.Children.Clear();
                int shown = 0;
                foreach (var app in GetApps())
                {
                    if (q.Length > 0 && app.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    listHost.Children.Add(MakeRow(app, feedback));
                    if (++shown >= 200) break;
                }
                if (shown == 0)
                {
                    var none = Theme.MakeText(L.T("ap_none"), 12.5, Theme.TextFaint);
                    none.Margin = new Thickness(4, 8, 4, 0);
                    listHost.Children.Add(none);
                }
            };
            search.TextChanged += (s, e) =>
            {
                placeholder.Visibility = search.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible;
                refill();
            };
            refill();
            host.Loaded += (s, e) => search.Focus();
            return stack;
        }

        static FrameworkElement MakeRow(AppEntry app, TextBlock feedback)
        {
            var row = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand,
            };
            Theme.AttachHover(row, Brushes.Transparent, Theme.HoverBg);
            var dock = new DockPanel();

            var openBtn = MakeMini("", L.T("ap_open"), () =>
            {
                Launch(app);
                PanelWindow.CloseCurrent();
            });
            var addBtn = MakeMini("", L.T("ap_add"), () =>
            {
                try
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!string.IsNullOrEmpty(app.LnkPath))
                    {
                        string dest = Path.Combine(desktop, Path.GetFileName(app.LnkPath));
                        int n = 2;
                        while (File.Exists(dest))
                            dest = Path.Combine(desktop, Path.GetFileNameWithoutExtension(app.LnkPath) + " (" + n++ + ").lnk");
                        File.Copy(app.LnkPath, dest);
                    }
                    else
                    {
                        // Store app: shortcut that launches it through the shell apps folder
                        string safe = string.Join("", app.Name.Split(Path.GetInvalidFileNameChars()));
                        string dest = Path.Combine(desktop, safe + ".lnk");
                        int n = 2;
                        while (File.Exists(dest)) dest = Path.Combine(desktop, safe + " (" + n++ + ").lnk");
                        Win32.CreateShortcut(dest,
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                            "shell:AppsFolder\\" + app.AppsFolderPath);
                    }
                    feedback.Text = L.T("ap_added");
                }
                catch (Exception ex) { Log.Error("app add", ex); }
            });
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(openBtn);
            btns.Children.Add(addBtn);
            DockPanel.SetDock(btns, Dock.Right);
            dock.Children.Add(btns);

            var name = Theme.MakeText(app.Name, 12.5, Theme.Text);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(name);
            row.Child = dock;

            // double-click anywhere on the row opens the app
            row.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount >= 2)
                {
                    Launch(app);
                    PanelWindow.CloseCurrent();
                }
            };
            return row;
        }

        static Border MakeMini(string glyph, string tip, Action onClick)
        {
            var b = new Border
            {
                Width = 26, Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Theme.ButtonBg,
                Child = Theme.MakeGlyph(glyph, 11, Theme.Text),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0),
                ToolTip = tip,
            };
            Theme.AttachHover(b, Theme.ButtonBg, Theme.HoverBg);
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; onClick(); };
            return b;
        }
    }
}
