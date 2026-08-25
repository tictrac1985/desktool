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

        /// <summary>Formats password managers and browsers attach to say "do not record this".
        /// Honouring them is what keeps copied passwords out of the panel and off disk.</summary>
        static readonly string[] SecretMarkers =
        {
            "ExcludeClipboardContentFromMonitorProcessing",  // Windows / KeePass / 1Password
            "CanIncludeInClipboardHistory",                  // Windows 10+ clipboard history opt-out
            "CanUploadToCloudClipboard",
            "ConfidentialContent",                           // Edge / Chromium
            "org.nspasteboard.ConcealedType",
        };

        /// <summary>True when the current clipboard owner marked its content as secret.</summary>
        static bool IsSecret(IDataObject data)
        {
            if (data == null) return false;
            foreach (string marker in SecretMarkers)
            {
                try { if (data.GetDataPresent(marker)) return true; }
                catch { return true; }   // unreadable → assume secret rather than record it
            }
            return false;
        }

        /// <summary>Longest snippet worth keeping. Anything larger is a document, not a
        /// clipping, and would bloat both memory and settings.json.</summary>
        const int MaxClipLength = 8 * 1024;

        /// <summary>Called from MainWindow's WndProc on WM_CLIPBOARDUPDATE.</summary>
        public static void OnClipboardUpdate()
        {
            if (!Settings.I.ShowClipboard) return;
            try
            {
                if (!Clipboard.ContainsText()) return;
                if (IsSecret(Clipboard.GetDataObject())) return;
                string text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text) || text.Length > MaxClipLength || text == _lastSeen) return;
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
            // set _lastSeen first: the clipboard notification can arrive before SetText returns,
            // and the text comparison in OnClipboardUpdate is what suppresses the echo
            _lastSeen = text;
            try { Clipboard.SetText(text); }
            catch { /* another app holds the clipboard open; nothing to undo */ }
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
                // pins encrypted for another Windows profile stay in settings untouched, but
                // showing their ciphertext would be worse than useless — count them instead
                int sealedPins = 0;
                if (Settings.I.PinnedClips.Count > 0)
                {
                    listHost.Children.Add(Theme.SectionTitle(L.T("cb_pinned")));
                    foreach (string clip in Settings.I.PinnedClips.ToArray())
                    {
                        if (Secret.IsSealed(clip)) { sealedPins++; continue; }
                        listHost.Children.Add(MakeRow(clip, true, rebuild));
                        any = true;
                    }
                    if (sealedPins > 0)
                    {
                        var locked = Theme.MakeText(
                            string.Format(L.T("sec_locked_some"), sealedPins), 11.5, Theme.TextFaint);
                        locked.TextWrapping = TextWrapping.Wrap;
                        locked.Margin = new Thickness(4, 2, 4, 2);
                        listHost.Children.Add(locked);
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
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 6, 9, 6),
                Margin = new Thickness(0, 2.5, 0, 2.5),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
            };
            Theme.AttachHover(row, Theme.ButtonBg, Theme.HoverBg);
            var dock = new DockPanel();

            var pinBtn = MakeMini(pinned ? "\uE77A" : "\uE718", () =>
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
            var delBtn = MakeMini("\uE74D", () =>
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
