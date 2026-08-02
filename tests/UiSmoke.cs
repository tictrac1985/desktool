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
        Settings.Load();
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
            Tuple.Create("occasions", (Action)(() => OccasionsFeature.Open(_win)), 900),
            Tuple.Create("note", (Action)(() => StickyNoteFeature.Open(_win)), 900),
            Tuple.Create("sysmon", (Action)(() => SysMonFeature.Open(_win)), 1600),
            Tuple.Create("worldclock", (Action)(() => WorldClocksFeature.Open(_win)), 1200),
            Tuple.Create("weather", (Action)(() => WeatherFeature.Open(_win)), 8000),
            Tuple.Create("apps", (Action)(() => AppsFeature.Open(_win)), 2000),
            Tuple.Create("prayer", (Action)(() => PrayerFeature.Open(_win)), 8000),
            Tuple.Create("settings", (Action)(() => SettingsFeature.Open(_win)), 1200),
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += NextStep;
        _timer.Start();
        _app.Run();
        return _failures.Count == 0 ? 0 : 1;
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
                if (prev.StartsWith("layout")) CaptureWidget(prev);
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
