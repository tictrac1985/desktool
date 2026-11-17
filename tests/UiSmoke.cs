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
        Theme.Apply(DeskMate.Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.LightThemeIndex, Settings.I.AutoTheme, DateTime.Now.Hour));
        _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        if (Settings.MaxThemeIndex != Theme.Palettes.Length - 1)
        {
            _failures.Add("Settings.MaxThemeIndex (" + Settings.MaxThemeIndex + ") != Palettes.Length - 1 (" + (Theme.Palettes.Length - 1) + ")");
            Console.WriteLine("FAIL " + _failures[_failures.Count - 1]);
        }
        Settings.I.Motion = 0;   // captures must be of the settled picture, never mid-entrance
        _win = new MainWindow();
        _win.Show();
        // keep everything off the user's screen while capturing
        _win.Left = -4000;
        _win.Top = 100;

        // v1.11: the matrix centres on what the redesign touched — every layout with the
        // sun-path face (the user's own setup), the orbit ring across light and dark themes
        // and all three icon looks, every clock face, then the panels.
        Settings.I.AutoTheme = false;   // a day/night flip mid-run would repaint the wrong theme
        int userLayout = Settings.I.LayoutIndex, userClock = Settings.I.ClockStyle, userTheme = Settings.I.ThemeIndex;
        _steps = new List<Tuple<string, Action, int>>();
        Action<string, Action, int> step = (n, a, w) => _steps.Add(Tuple.Create(n, a, w));
        step("mine", () => Look(userLayout, userClock, userTheme, 0, 0), 900);
        for (int ly = 0; ly <= 7; ly++)
        {
            int l = ly;
            step("layout" + l, () => Look(l, 8, 0, 0, 0), 800);
        }
        foreach (int th in new[] { 12, 10, 4, 8, 6, 1, 13, 14 })
        {
            int t = th;
            step("orbit_theme" + t, () => Look(3, 8, t, 0, 0), 800);
        }
        step("orbit_glass", () => Look(3, 8, 0, 1, 0), 800);
        step("orbit_objects", () => Look(3, 8, 0, 2, 0), 800);
        step("orbit_systemfont", () => Look(3, 8, 0, 0, 1), 800);
        step("orbit_digital", () => Look(3, 0, 0, 0, 0), 800);
        step("orbit_analog", () => Look(3, 1, 0, 0, 0), 800);
        step("halo_light", () => Look(7, 0, 12, 0, 0), 800);
        step("connected_light", () => Look(1, 8, 12, 0, 0), 800);
        step("classic_light", () => Look(0, 0, 10, 0, 0), 800);
        for (int cs = 0; cs <= 10; cs++)
        {
            int c = cs;
            step("clock" + c, () => Look(0, c, 0, 0, 0), 800);
        }
        step("sidebar_glass", () => Look(4, 2, 5, 1, 0), 800);
        step("dock_tiles", () => Look(5, 1, 0, 0, 0), 800);
        step("monolith_ruby", () => Look(6, 0, 11, 0, 0), 800);
        step("triple_digital", () => Look(2, 0, 0, 0, 0), 800);
        // panels in the default look
        step("p_reset", () => Look(3, 8, 0, 0, 0), 600);
        step("cleaner", () => CleanerFeature.Open(_win), 900);
        step("calc", () => CalculatorFeature.Open(_win), 900);
        step("clipboard", () => ClipboardKeeper.Open(_win), 900);
        step("focus", () => FocusFeature.Open(_win), 900);
        step("occasions", () => OccasionsFeature.Open(_win), 900);
        step("note", () => { SeedTasks(); StickyNoteFeature.Open(_win); }, 900);
        step("worldclock", () => WorldClocksFeature.Open(_win), 1200);
        step("weather", () => WeatherFeature.Open(_win), 6000);
        step("apps", () => AppsFeature.Open(_win), 2000);
        step("prayer", () => PrayerFeature.Open(_win), 6000);
        step("calendar", () => CalendarFeature.Open(_win), 900);
        step("calendar_hijri", () => { PanelWindow.CloseCurrent(); Settings.I.CalHijriFirst = true; CalendarFeature.Open(_win); }, 900);
        step("capture", () => { Settings.I.RecentColors.Clear(); foreach (var c in new[] { "#FF6BD6", "#5EEAD4", "#E6C06A", "#6EC1FF" }) Settings.I.RecentColors.Add(c); CaptureFeature.Open(_win); }, 900);
        step("media", () => MediaFeature.Open(_win), 900);
        step("update", () => UpdateFeature.Open(_win), 900);
        step("settings", () => SettingsFeature.Open(_win), 1200);
        step("settings_light", () => { Look(3, 8, 12, 0, 0); SettingsFeature.Open(_win); }, 1200);

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

    /// <summary>First tool-icon host in the widget. v1.10: tool cells hold a 46×48 Grid
    /// (iconSize 40 + padding) built by Theme.MakeToolCell — find the host by size so
    /// anchored panels still have an anchor.</summary>
    static FrameworkElement FindToolCircle(DependencyObject root)
    {
        int n = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            var g = child as System.Windows.Controls.Grid;
            if (g != null && g.Width == 46 && g.Height == 48) return g;
            var found = FindToolCircle(child);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>One complete look: layout, clock face, palette, icon style, font.</summary>
    static void Look(int layout, int clock, int theme, int icons, int font)
    {
        PanelWindow.CloseCurrent();
        Settings.I.LayoutIndex = layout;
        Settings.I.ClockStyle = clock;
        Settings.I.ThemeIndex = theme;
        Settings.I.IconStyle = icons;
        Settings.I.FontChoice = font;
        Theme.Apply(theme);
        _win.Reload();
        _win.Left = -4000;
        _win.Top = 100;
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
                bool widgetStep = !(prev == "cleaner" || prev == "calc" || prev == "clipboard" || prev == "focus"
                    || prev == "occasions" || prev == "note" || prev == "worldclock" || prev == "weather"
                    || prev == "apps" || prev == "prayer" || prev.StartsWith("calendar") || prev == "capture" || prev == "media"
                    || prev == "update" || prev.StartsWith("settings"));
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
