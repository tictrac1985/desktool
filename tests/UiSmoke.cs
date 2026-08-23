using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.UI;
using DeskMate.Features;

/// <summary>Renders the widget (every layout) and every feature panel to PNGs for
/// visual QA. Compiled with /main:UiSmoke — not part of the shipped app.</summary>
static class UiSmoke
{
    static string _outDir;
    static MainWindow _win;
    static List<Tuple<string, Action, int>> _steps;
    static int _index = -1;
    static DispatcherTimer _timer;
    static Application _app;
    /// <summary>Every swallowed problem lands here; a non-empty list fails the run.
    /// Silent passes once hid a crashing panel behind its stale screenshot.</summary>
    static readonly List<string> _failures = new List<string>();
    static string _retried;

    [STAThread]
    static int Main(string[] args)
    {
        _outDir = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        Directory.CreateDirectory(_outDir);
        // wipe stale screenshots first — an old PNG must never mask a step that died this run
        foreach (var old in Directory.GetFiles(_outDir, "*.png"))
        {
            try { File.Delete(old); } catch { }
        }
        IsolateSettings();
        Settings.Load();
        // never let a capture run repoint the user's autostart at this temporary exe
        Settings.I.AutoStart = false;
        // nor derive every palette from their wallpaper: Material You overrides Theme.Apply
        // on the first tick, which would paint every swatch and clock accent wallpaper-grey
        Settings.I.MaterialYou = false;
        Theme.Apply(DeskMate.Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.AutoTheme, DateTime.Now.Hour));
        _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        _win = new MainWindow();
        _win.Show();
        // keep everything off the user's screen while capturing
        _win.Left = -4000;
        _win.Top = 100;

        _steps = new List<Tuple<string, Action, int>>
        {
            Tuple.Create("layout0", (Action)(() => SetLayout(0)), 700),
            Tuple.Create("layout1", (Action)(() => SetLayout(1)), 700),
            Tuple.Create("layout2", (Action)(() => SetLayout(2)), 700),
            Tuple.Create("layout3", (Action)(() => SetLayout(3)), 700),
            Tuple.Create("layout4", (Action)(() => SetLayout(4)), 700),
            Tuple.Create("layout5", (Action)(() => SetLayout(5)), 700),
            Tuple.Create("cleaner", (Action)(() => CleanerFeature.Open(_win)), 900),
            Tuple.Create("calc", (Action)(() => CalculatorFeature.Open(_win)), 900),
            Tuple.Create("wallpaper", (Action)(() => WallpaperFeature.Open(_win)), 900),
            Tuple.Create("power", (Action)(() => PowerFeature.Open(_win)), 900),
            Tuple.Create("clipboard", (Action)(() => ClipboardKeeper.Open(_win)), 900),
            Tuple.Create("focus", (Action)(() => FocusFeature.Open(_win)), 900),
            Tuple.Create("command", (Action)(() => CommandBarFeature.Open(_win)), 900),
            Tuple.Create("occasions", (Action)(() => OccasionsFeature.Open(_win)), 900),
            // seeded first: an empty list only proves the placeholder renders, not the task row
            Tuple.Create("note", (Action)(() => { SeedTasks(); StickyNoteFeature.Open(_win); }), 900),
            Tuple.Create("note_edit", (Action)(() => StickyNoteFeature.OpenEditorForSmoke()), 900),
            Tuple.Create("sysmon", (Action)(() => SysMonFeature.Open(_win)), 1600),
            Tuple.Create("worldclock", (Action)(() => WorldClocksFeature.Open(_win)), 1200),
            Tuple.Create("weather", (Action)(() => WeatherFeature.Open(_win)), 8000),
            Tuple.Create("apps", (Action)(() => AppsFeature.Open(_win)), 2000),
            Tuple.Create("prayer", (Action)(() => PrayerFeature.Open(_win)), 8000),
            Tuple.Create("settings", (Action)(() => SettingsFeature.Open(_win)), 1200),
            // opened the way a user opens it — from an icon — so the caret and the anchored
            // placement are actually rendered instead of silently falling back
            Tuple.Create("anchored", (Action)(() =>
            {
                SetLayout(0);
                // on-screen for this one step: parked at -4000 the panel is clamped back into
                // the work area, thousands of pixels from its anchor, and the caret would only
                // ever prove the clamp rather than the placement
                _win.Left = 120;
                _win.Top = 120;
                _win.UpdateLayout();
                PanelWindow.PendingAnchor = FindToolCircle(_win);
                CalculatorFeature.Open(_win);
            }), 900),
            // v1.7: the two new clock styles rendered on the classic layout
            Tuple.Create("clock6_binary", (Action)(() => { Settings.I.ClockStyle = 6; SetLayout(0); }), 700),
            Tuple.Create("clock7_dayring", (Action)(() => { Settings.I.ClockStyle = 7; SetLayout(0); }), 700),
            // v1.8: the two newer clock styles + the two new layouts
            Tuple.Create("clock3_flip", (Action)(() => { Settings.I.ClockStyle = 3; SetLayout(0); }), 700),
            Tuple.Create("clock4_sidebar_flip", (Action)(() => { SetLayout(4); }), 800),
            Tuple.Create("clock8_sunpath", (Action)(() => { Settings.I.ClockStyle = 8; SetLayout(0); }), 700),
            Tuple.Create("clock9_stripes", (Action)(() => { Settings.I.ClockStyle = 9; SetLayout(0); }), 700),
            Tuple.Create("layout6_monolith", (Action)(() => { Settings.I.ClockStyle = 0; SetLayout(6); }), 800),
            Tuple.Create("layout7_halo", (Action)(() => { SetLayout(7); }), 900),
            // v1.7: a couple of the new palettes on the widget (Material You is already
            // forced off for the whole run — see Main — so Theme.Apply sticks)
            Tuple.Create("theme8_violet", (Action)(() =>
            {
                Settings.I.ThemeIndex = 8;
                Theme.Apply(8);
                _win.Reload();
            }), 700),
            Tuple.Create("theme9_copper", (Action)(() =>
            {
                Settings.I.ThemeIndex = 9;
                Theme.Apply(9);
                _win.Reload();
            }), 700),
            Tuple.Create("theme10_sand", (Action)(() =>
            {
                Settings.I.ThemeIndex = 10;
                Theme.Apply(10);
                _win.Reload();
            }), 700),
            Tuple.Create("theme11_ruby", (Action)(() =>
            {
                Settings.I.ThemeIndex = 11;
                Theme.Apply(11);
                _win.Reload();
            }), 700),
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += NextStep;
        _timer.Start();
        _app.Run();
        return _failures.Count == 0 ? 0 : 1;
    }

    /// <summary>Puts a couple of tasks in the isolated settings so the list screenshot shows
    /// a real row — completion circle, reminder line and actions — instead of the empty state.
    /// Safe because IsolateSettings has already redirected the file.</summary>
    static void SeedTasks()
    {
        var notes = new List<DeskMate.Logic.NoteItem>
        {
            new DeskMate.Logic.NoteItem
            {
                Text = "مراجعة تقرير الربع الأخير",
                RemindAt = DateTime.Now.Date.AddDays(1).AddHours(9),
            },
            new DeskMate.Logic.NoteItem { Text = "الاتصال بالصيانة", Pinned = true },
            new DeskMate.Logic.NoteItem { Text = "شراء هدية العيد" },
            // a task finished today, so the daily progress strip above the list has data to show
            new DeskMate.Logic.NoteItem
            {
                Text = "تسليم عرض التصميم",
                Done = true,
                DoneAt = DateTime.Now.Date.AddHours(11),
            },
        };
        StickyNoteFeature.Save(notes);
    }

    /// <summary>Points settings at a throwaway copy of the real file.
    /// This harness builds a live MainWindow, so without isolation a capture run writes to the
    /// user's %AppData% settings — panels save as they are exercised, and MainWindow.Loaded
    /// calls SetAutoStart, which repoints the HKCU Run key at this temporary exe. Copying the
    /// file keeps the screenshots representative of the real configuration while making the
    /// run read-only as far as the user's machine is concerned.</summary>
    static void IsolateSettings()
    {
        try
        {
            string temp = Path.Combine(Path.GetTempPath(),
                "deskmate_uismoke_" + Guid.NewGuid().ToString("N") + ".json");
            string live = Settings.DefaultPath;
            if (File.Exists(live)) File.Copy(live, temp, true);
            Settings.OverridePath = temp;
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                try { File.Delete(temp); } catch { }
                try { File.Delete(temp + ".bak"); } catch { }
            };
        }
        catch (Exception ex)
        {
            // better to render defaults than to write over the user's real settings
            Settings.OverridePath = Path.Combine(Path.GetTempPath(), "deskmate_uismoke_fallback.json");
            Console.WriteLine("WARN: settings isolation fell back to defaults: " + ex.Message);
        }
    }

    /// <summary>First tool-icon circle in the widget — the 40x40 Border built by
    /// Theme.MakeToolCell. Used as the anchor so the "opened from an icon" path is covered.</summary>
    static FrameworkElement FindToolCircle(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var b = child as System.Windows.Controls.Border;
            if (b != null && b.Width == 40 && b.Height == 40) return b;
            var found = FindToolCircle(child);
            if (found != null) return found;
        }
        return null;
    }

    static void SetLayout(int idx)
    {
        Settings.I.LayoutIndex = idx;
        _win.Reload();
        _win.Left = -4000;
        _win.Top = 100;
    }

    static void NextStep(object sender, EventArgs e)
    {
        _timer.Stop();
        // capture what the previous step produced
        if (_index >= 0)
        {
            var prev = _steps[_index].Item1;
            try
            {
                // a panel still opening under load gets ONE extra beat before it counts as failed
                bool widgetStep = prev.StartsWith("layout") || prev.StartsWith("clock") || prev.StartsWith("theme");
                bool panelMissing = !widgetStep && PanelWindow.Current == null;
                if (panelMissing && _retried != prev)
                {
                    _retried = prev;
                    _timer.Interval = TimeSpan.FromMilliseconds(900);
                    _timer.Start();
                    return;
                }
                if (widgetStep) CaptureWidget(prev);
                else CapturePanel(prev);
            }
            catch (Exception ex)
            {
                _failures.Add("capture " + prev + ": " + ex.Message);
                Console.WriteLine("FAIL capture " + prev + ": " + ex.Message);
            }
        }
        _index++;
        if (_index >= _steps.Count)
        {
            PanelWindow.CloseCurrent();
            if (_failures.Count > 0)
            {
                Console.WriteLine("UI SMOKE FAILED (" + _failures.Count + "):");
                foreach (var f in _failures) Console.WriteLine("  - " + f);
            }
            else Console.WriteLine("UI SMOKE OK");
            _app.Shutdown();
            return;
        }
        var step = _steps[_index];
        try
        {
            step.Item2();
            var p = PanelWindow.Current;
            if (p != null) { p.Left = -4000; p.Top = 100; }
        }
        catch (Exception ex)
        {
            _failures.Add("open " + step.Item1 + ": " + ex.Message);
            Console.WriteLine("FAIL open " + step.Item1 + ": " + ex.Message);
        }
        _timer.Interval = TimeSpan.FromMilliseconds(step.Item3);
        _timer.Start();
    }

    static void Save(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using (var fs = File.Create(path)) enc.Save(fs);
    }

    /// <summary>Renders a transparent-rooted element over a dark backdrop via VisualBrush
    /// (also sidesteps the RTL mirror artifact of rendering RTL roots directly).</summary>
    static void RenderWithBackdrop(FrameworkElement root, string path)
    {
        root.UpdateLayout();
        int w = (int)Math.Ceiling(root.ActualWidth);
        int h = (int)Math.Ceiling(root.ActualHeight);
        if (w <= 0 || h <= 0)
        {
            _failures.Add("zero size for " + Path.GetFileName(path));
            Console.WriteLine("FAIL zero size for " + path);
            return;
        }
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x12)), null, new Rect(0, 0, w, h));
            dc.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, w, h));
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        BitmapSource output = rtb;
        if (root.FlowDirection == FlowDirection.RightToLeft)
            output = new TransformedBitmap(rtb, new ScaleTransform(-1, 1));
        Save(output, path);
        Console.WriteLine("captured " + Path.GetFileName(path) + " " + w + "x" + h);
    }

    static void CaptureWidget(string name)
    {
        var root = _win.Content as FrameworkElement;
        if (root == null) return;
        RenderWithBackdrop(root, Path.Combine(_outDir, "widget_" + name + ".png"));
    }

    static void CapturePanel(string name)
    {
        var p = PanelWindow.Current;
        // a panel that failed to build (e.g. an internal cast crash) leaves nothing open —
        // that is a FAILURE of this run, not a skip
        if (p == null)
        {
            _failures.Add("no panel for " + name);
            Console.WriteLine("FAIL no panel for " + name);
            return;
        }
        var root = p.Content as FrameworkElement;
        if (root == null)
        {
            _failures.Add("empty panel for " + name);
            Console.WriteLine("FAIL empty panel for " + name);
            return;
        }
        RenderWithBackdrop(root, Path.Combine(_outDir, "panel_" + name + ".png"));
    }
}
