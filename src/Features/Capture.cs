using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;
using WF = System.Windows.Forms;
using DPoint = System.Drawing.Point;
using DPen = System.Drawing.Pen;
using DPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DeskMate.Features
{
    /// <summary>Screenshots and a colour picker. A snip freezes the screen, dims it, and lets
    /// the user drag a rectangle; the result goes to the clipboard and (optionally) to
    /// Pictures\DeskMate. The picker uses the same frozen frame with a pixel magnifier, so
    /// what it reads is exactly what was on screen — not a window that moved meanwhile.</summary>
    public static class CaptureFeature
    {
        static string _lastColor = "";

        static string ShotDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "DeskMate"); }
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "capture", L.T("f_capture"), Build, 330);
        }

        // ---------------- panel ----------------

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var hint = Theme.MakeText(L.T("cap_hint"), 11.5, Theme.TextFaint);
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(0, 0, 0, 6);
            stack.Children.Add(hint);

            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(Theme.MakeButton(L.T("cap_region"), () => Begin(Mode.Region), true, new Thickness(14, 8, 14, 8)));
            row.Children.Add(Theme.MakeButton(L.T("cap_full"), () => Begin(Mode.Full), false, new Thickness(14, 8, 14, 8)));
            row.Children.Add(Theme.MakeButton(L.T("cap_pick"), () => Begin(Mode.Picker), false, new Thickness(14, 8, 14, 8)));
            stack.Children.Add(row);

            stack.Children.Add(Theme.MakeCheck(L.T("cap_save"), Settings.I.ShotSave,
                v => { Settings.I.ShotSave = v; Settings.I.Save(); }));
            var folderBtn = Theme.MakeButton(L.T("cap_folder"), OpenFolder, false, new Thickness(12, 5, 12, 5));
            folderBtn.HorizontalAlignment = HorizontalAlignment.Left;
            stack.Children.Add(folderBtn);

            // last picked colour, spelled in the three notations people paste
            string hex = !string.IsNullOrEmpty(_lastColor) ? _lastColor
                       : Settings.I.RecentColors.Count > 0 ? Settings.I.RecentColors[0] : "";
            byte r, g, b;
            if (hex.Length > 0 && ColorCore.TryParseHex(hex, out r, out g, out b))
            {
                stack.Children.Add(Theme.SectionTitle(L.T("cap_color")));
                var swatch = new Border
                {
                    Height = 54,
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b)),
                    BorderBrush = Theme.CardBorder,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 6),
                };
                var big = Theme.MakeText(ColorCore.ToHex(r, g, b), 16, ColorCore.PrefersDarkText(r, g, b) ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White);
                big.FontWeight = FontWeights.Bold;
                big.HorizontalAlignment = HorizontalAlignment.Center;
                big.VerticalAlignment = VerticalAlignment.Center;
                big.FlowDirection = FlowDirection.LeftToRight;
                swatch.Child = big;
                stack.Children.Add(swatch);
                stack.Children.Add(CopyRow(ColorCore.ToHex(r, g, b)));
                stack.Children.Add(CopyRow(ColorCore.ToRgbText(r, g, b)));
                stack.Children.Add(CopyRow(ColorCore.ToHslText(r, g, b)));
            }

            if (Settings.I.RecentColors.Count > 0)
            {
                stack.Children.Add(Theme.SectionTitle(L.T("cap_recent")));
                var wrap = new WrapPanel { FlowDirection = FlowDirection.LeftToRight, HorizontalAlignment = HorizontalAlignment.Left };
                foreach (string c in Settings.I.RecentColors)
                {
                    byte cr, cg, cb;
                    if (!ColorCore.TryParseHex(c, out cr, out cg, out cb)) continue;
                    string hx = ColorCore.ToHex(cr, cg, cb);
                    var chip = new Border
                    {
                        Width = 30, Height = 30, Margin = new Thickness(2.5),
                        CornerRadius = new CornerRadius(9),
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(cr, cg, cb)),
                        BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1),
                        Cursor = System.Windows.Input.Cursors.Hand, ToolTip = hx,
                    };
                    chip.MouseLeftButtonUp += (s, e) => { e.Handled = true; _lastColor = hx; CopyText(hx); PanelWindow.CloseCurrent(); Open(MainWindow.Instance); };
                    chip.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                    wrap.Children.Add(chip);
                }
                stack.Children.Add(wrap);
            }
            return stack;
        }

        static FrameworkElement CopyRow(string text)
        {
            var t = Theme.MakeText(text, 12.5, Theme.Text);
            t.FlowDirection = FlowDirection.LeftToRight;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            var hint = Theme.MakeGlyph("\uE8C8", 11, Theme.TextFaint);   // copy
            hint.Margin = new Thickness(8, 0, 0, 0);
            var inner = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, FlowDirection = FlowDirection.LeftToRight };
            inner.Children.Add(t);
            inner.Children.Add(hint);
            var b = Theme.RowSurface(inner);
            b.Cursor = System.Windows.Input.Cursors.Hand;
            b.ToolTip = L.T("cap_copy");
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; CopyText(text); hint.Text = "\uE73E"; };   // check mark
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        static void CopyText(string text)
        {
            try { System.Windows.Clipboard.SetText(text); }
            catch (Exception ex) { Log.Error("capture copy", ex); }
        }

        static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(ShotDir);
                Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "\"" + ShotDir + "\"") { UseShellExecute = false });
            }
            catch (Exception ex) { Log.Error("open shots folder", ex); }
        }

        // ---------------- capture ----------------

        enum Mode { Region, Full, Picker }

        static bool _busy;

        /// <summary>Hides the widget and the panel, waits a beat for the screen to repaint
        /// without them, then takes the frame.</summary>
        static void Begin(Mode mode)
        {
            if (_busy) return;
            _busy = true;
            var main = MainWindow.Instance;
            PanelWindow.CloseCurrent();
            if (main != null) main.Opacity = 0;
            var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            t.Tick += (s, e) =>
            {
                t.Stop();
                Bitmap frame = null;
                try { frame = Grab(); }
                catch (Exception ex) { Log.Error("screen grab", ex); }
                finally { if (main != null) main.Opacity = 1; }
                try
                {
                    if (frame == null) { Toast.Show(L.T("cap_fail"), ""); return; }
                    if (mode == Mode.Full) Deliver(frame, true);
                    else if (mode == Mode.Region) Deliver(Snip(frame), false);
                    else
                    {
                        string hex = Pick(frame);
                        if (hex != null)
                        {
                            _lastColor = hex;
                            ColorCore.PushRecent(Settings.I.RecentColors, hex, Settings.MaxRecentColors);
                            Settings.I.Save();
                            CopyText(hex);
                            Toast.Show(hex + "  " + L.T("cap_copied"), Glyphs.Capture);
                            Open(main);
                        }
                    }
                }
                catch (Exception ex) { Log.Error("capture", ex); Toast.Show(L.T("cap_fail"), ""); }
                finally { if (frame != null) frame.Dispose(); _busy = false; }
            };
            t.Start();
        }

        static Rectangle Area { get { return WF.SystemInformation.VirtualScreen; } }

        static Bitmap Grab()
        {
            var a = Area;
            var bmp = new Bitmap(a.Width, a.Height, DPixelFormat.Format32bppRgb);
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(a.X, a.Y, 0, 0, a.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>Takes ownership of the cropped bitmap's lifetime: disposes it after use.</summary>
        static void Deliver(Bitmap shot, bool disposeShot)
        {
            if (shot == null) return;
            try
            {
                try { WF.Clipboard.SetImage(shot); }
                catch (Exception ex) { Log.Error("clipboard image", ex); }
                string path = null;
                if (Settings.I.ShotSave)
                {
                    try
                    {
                        Directory.CreateDirectory(ShotDir);
                        path = Path.Combine(ShotDir, "DeskMate " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss") + ".png");
                        shot.Save(path, ImageFormat.Png);
                    }
                    catch (Exception ex) { Log.Error("save screenshot", ex); path = null; }
                }
                string saved = path;
                if (saved != null)
                    Toast.Show(L.T("cap_done_saved"), Glyphs.Capture, L.T("cap_show"), () =>
                    {
                        try { Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), "/select,\"" + saved + "\"") { UseShellExecute = false }); }
                        catch (Exception ex) { Log.Error("reveal screenshot", ex); }
                    });
                else Toast.Show(L.T("cap_done"), Glyphs.Capture);
            }
            finally { shot.Dispose(); }
        }

        static Bitmap Snip(Bitmap frame)
        {
            using (var ov = new Overlay(frame, false))
            {
                ov.ShowDialog();
                Rectangle sel = ov.Selection;
                if (sel.Width < 3 || sel.Height < 3) return null;
                return frame.Clone(sel, frame.PixelFormat);
            }
        }

        static string Pick(Bitmap frame)
        {
            using (var ov = new Overlay(frame, true))
            {
                ov.ShowDialog();
                return ov.Picked;
            }
        }

        // ---------------- the frozen-screen overlay ----------------

        sealed class Overlay : WF.Form
        {
            readonly Bitmap _frame;
            readonly bool _picker;
            DPoint _start, _now;
            bool _drag;
            public Rectangle Selection = Rectangle.Empty;
            public string Picked;

            public Overlay(Bitmap frame, bool picker)
            {
                _frame = frame; _picker = picker;
                FormBorderStyle = WF.FormBorderStyle.None;
                StartPosition = WF.FormStartPosition.Manual;
                Bounds = Area;
                TopMost = true;
                ShowInTaskbar = false;
                Cursor = WF.Cursors.Cross;
                DoubleBuffered = true;
                KeyPreview = true;
            }

            protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); }

            protected override void OnKeyDown(WF.KeyEventArgs e)
            {
                if (e.KeyCode == WF.Keys.Escape) { Selection = Rectangle.Empty; Picked = null; Close(); }
                base.OnKeyDown(e);
            }

            protected override void OnMouseDown(WF.MouseEventArgs e)
            {
                if (e.Button == WF.MouseButtons.Right) { Selection = Rectangle.Empty; Picked = null; Close(); return; }
                if (_picker) { Picked = PixelHex(e.Location); Close(); return; }
                _drag = true; _start = _now = e.Location;
            }

            protected override void OnMouseMove(WF.MouseEventArgs e)
            {
                _now = e.Location;
                Invalidate();
            }

            protected override void OnMouseUp(WF.MouseEventArgs e)
            {
                if (_picker || !_drag) return;
                _drag = false;
                Selection = Normalize(_start, e.Location);
                Close();
            }

            Rectangle Normalize(DPoint a, DPoint b)
            {
                int x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
                var r = new Rectangle(x, y, Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
                r.Intersect(new Rectangle(0, 0, _frame.Width, _frame.Height));
                return r;
            }

            string PixelHex(DPoint p)
            {
                if (p.X < 0 || p.Y < 0 || p.X >= _frame.Width || p.Y >= _frame.Height) return null;
                var c = _frame.GetPixel(p.X, p.Y);
                return ColorCore.ToHex(c.R, c.G, c.B);
            }

            protected override void OnPaint(WF.PaintEventArgs e)
            {
                var g = e.Graphics;
                g.DrawImageUnscaled(_frame, 0, 0);
                using (var dim = new SolidBrush(System.Drawing.Color.FromArgb(_picker ? 0 : 110, 0, 0, 0)))
                    g.FillRectangle(dim, ClientRectangle);

                if (_picker) { PaintMagnifier(g); return; }

                if (_drag)
                {
                    var r = Normalize(_start, _now);
                    if (r.Width > 0 && r.Height > 0)
                    {
                        g.DrawImage(_frame, r, r, GraphicsUnit.Pixel);   // the selection shows undimmed
                        using (var pen = new DPen(System.Drawing.Color.FromArgb(255, 80, 200, 255), 2f)) g.DrawRectangle(pen, r);
                        string label = r.Width + " × " + r.Height;
                        using (var f = new Font("Segoe UI", 10f, System.Drawing.FontStyle.Bold))
                        {
                            var sz = g.MeasureString(label, f);
                            float ty = r.Y > sz.Height + 8 ? r.Y - sz.Height - 4 : r.Y + 4;
                            using (var bg = new SolidBrush(System.Drawing.Color.FromArgb(200, 10, 14, 24)))
                                g.FillRectangle(bg, r.X, ty, sz.Width + 8, sz.Height + 2);
                            g.DrawString(label, f, System.Drawing.Brushes.White, r.X + 4, ty + 1);
                        }
                    }
                }
                else
                {
                    using (var f = new Font("Segoe UI", 12f, System.Drawing.FontStyle.Bold))
                        g.DrawString(L.T("cap_drag"), f, System.Drawing.Brushes.White, 24, 20);
                }
            }

            /// <summary>An 11 × 11 pixel loupe beside the pointer, with the centre pixel's hex.</summary>
            void PaintMagnifier(Graphics g)
            {
                const int cells = 11, zoom = 12;
                int size = cells * zoom;
                int lx = _now.X + 22, ly = _now.Y + 22;
                if (lx + size + 8 > ClientSize.Width) lx = _now.X - size - 30;
                if (ly + size + 40 > ClientSize.Height) ly = _now.Y - size - 60;
                var dest = new Rectangle(lx, ly, size, size);
                var src = new Rectangle(_now.X - cells / 2, _now.Y - cells / 2, cells, cells);

                var old = g.InterpolationMode; var oldPx = g.PixelOffsetMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(_frame, dest, src, GraphicsUnit.Pixel);
                g.InterpolationMode = old; g.PixelOffsetMode = oldPx;

                using (var pen = new DPen(System.Drawing.Color.White, 2f)) g.DrawRectangle(pen, dest);
                var mid = new Rectangle(lx + (cells / 2) * zoom, ly + (cells / 2) * zoom, zoom, zoom);
                using (var pen = new DPen(System.Drawing.Color.Black, 1f)) g.DrawRectangle(pen, mid);
                string hex = PixelHex(_now) ?? "";
                using (var f = new Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold))
                {
                    var sz = g.MeasureString(hex, f);
                    using (var bg = new SolidBrush(System.Drawing.Color.FromArgb(220, 10, 14, 24)))
                        g.FillRectangle(bg, lx, ly + size + 4, Math.Max(size, sz.Width + 10), sz.Height + 4);
                    g.DrawString(hex, f, System.Drawing.Brushes.White, lx + 5, ly + size + 6);
                }
            }
        }
    }
}
