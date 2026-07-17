using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskMate.Core;
using DeskMate.UI;

namespace DeskMate.Features
{
    /// <summary>Keeps recent clipboard text snippets in memory; pinned ones persist in settings.</summary>
    public static class ClipboardKeeper
    {
        static readonly List<string> Recent = new List<string>();
        static string _lastSeen;
        static bool _settingOurselves;

        /// <summary>Called from MainWindow's WndProc on WM_CLIPBOARDUPDATE.</summary>
        public static void OnClipboardUpdate()
        {
            if (_settingOurselves) { _settingOurselves = false; return; }
            if (!Settings.I.ShowClipboard) return;
            try
            {
                if (!Clipboard.ContainsText()) return;
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text) || text == _lastSeen) return;
                _lastSeen = text;
                Recent.Remove(text);
                Recent.Insert(0, text);
                int max = Math.Max(5, Settings.I.ClipboardMax);
                while (Recent.Count > max) Recent.RemoveAt(Recent.Count - 1);
            }
            catch { /* clipboard is frequently locked by other apps; just skip */ }
        }

        static void CopyBack(string text)
        {
            try
            {
                _settingOurselves = true;
                Clipboard.SetText(text);
                _lastSeen = text;
            }
            catch { _settingOurselves = false; }
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "clipboard", L.T("f_clipboard"), Build, 330);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var listHost = new StackPanel();

            Action rebuild = null;
            rebuild = () =>
            {
                listHost.Children.Clear();
                bool any = false;
                if (Settings.I.PinnedClips.Count > 0)
                {
                    listHost.Children.Add(Theme.SectionTitle(L.T("cb_pinned")));
                    foreach (string clip in Settings.I.PinnedClips.ToArray())
                    {
                        listHost.Children.Add(MakeRow(clip, true, rebuild));
                        any = true;
                    }
                }
                if (Recent.Count > 0)
                {
                    listHost.Children.Add(Theme.SectionTitle(L.T("cb_recent")));
                    foreach (string clip in Recent.ToArray())
                    {
                        listHost.Children.Add(MakeRow(clip, false, rebuild));
                        any = true;
                    }
                }
                if (!any)
                {
                    var empty = Theme.MakeText(L.T("cb_empty"), 12.5, Theme.TextFaint);
                    empty.TextWrapping = TextWrapping.Wrap;
                    empty.Margin = new Thickness(4, 8, 4, 8);
                    listHost.Children.Add(empty);
                }
            };
            rebuild();

            var scroll = new ScrollViewer
            {
                Content = listHost,
                MaxHeight = 320,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            stack.Children.Add(scroll);

            var clearBtn = Theme.MakeButton(L.T("cb_clear"), () =>
            {
                Recent.Clear();
                rebuild();
            });
            clearBtn.HorizontalAlignment = HorizontalAlignment.Center;
            clearBtn.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(clearBtn);
            return stack;
        }

        static FrameworkElement MakeRow(string clip, bool pinned, Action rebuild)
        {
            var row = new Border
            {
                Background = Theme.ButtonBg,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand,
            };
            Theme.AttachHover(row, Theme.ButtonBg, Theme.HoverBg);
            var dock = new DockPanel();

            var pinBtn = MakeMini(pinned ? "" : "", () =>
            {
                if (pinned) Settings.I.PinnedClips.Remove(clip);
                else
                {
                    Recent.Remove(clip);
                    if (!Settings.I.PinnedClips.Contains(clip)) Settings.I.PinnedClips.Insert(0, clip);
                }
                Settings.I.Save();
                rebuild();
            });
            var delBtn = MakeMini("", () =>
            {
                Recent.Remove(clip);
                Settings.I.PinnedClips.Remove(clip);
                Settings.I.Save();
                rebuild();
            });
            var btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btns.Children.Add(pinBtn);
            btns.Children.Add(delBtn);
            DockPanel.SetDock(btns, Dock.Right);
            dock.Children.Add(btns);

            string preview = clip.Trim().Replace("\r", " ").Replace("\n", " ");
            if (preview.Length > 90) preview = preview.Substring(0, 90) + "…";
            var text = Theme.MakeText(preview, 12, Theme.Text);
            text.TextWrapping = TextWrapping.Wrap;
            text.MaxHeight = 36;
            text.VerticalAlignment = VerticalAlignment.Center;
            dock.Children.Add(text);
            row.Child = dock;

            row.MouseLeftButtonUp += (s, e) =>
            {
                CopyBack(clip);
                text.Foreground = Theme.Accent;
                text.Text = L.T("cb_copied");
            };
            return row;
        }

        static Border MakeMini(string glyph, Action onClick)
        {
            var b = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Child = Theme.MakeGlyph(glyph, 11, Theme.TextDim),
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0),
            };
            Theme.AttachHover(b, Brushes.Transparent, Theme.HoverBg);
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; onClick(); };
            return b;
        }
    }
}
