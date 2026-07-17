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

/// <summary>Opens every feature panel offscreen and renders each to a PNG for visual QA.
/// Compiled with /main:UiSmoke — not part of the shipped app.</summary>
static class UiSmoke
{
    static string _outDir;
    static Window _owner;
    static List<Tuple<string, Action, int>> _steps;
    static int _index = -1;
    static DispatcherTimer _timer;
    static Application _app;

    [STAThread]
    static int Main(string[] args)
    {
        _outDir = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        Directory.CreateDirectory(_outDir);
        Settings.Load();
        _app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        _owner = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            ShowInTaskbar = false,
            Width = 400,
            Height = 300,
            Left = 200,
            Top = 200,
            ShowActivated = false,
        };
        _owner.Show();

        _steps = new List<Tuple<string, Action, int>>
        {
            Tuple.Create("cleaner", (Action)(() => CleanerFeature.Open(_owner)), 900),
            Tuple.Create("calc", (Action)(() => CalculatorFeature.Open(_owner)), 900),
            Tuple.Create("wallpaper", (Action)(() => WallpaperFeature.Open(_owner)), 900),
            Tuple.Create("power", (Action)(() => PowerFeature.Open(_owner)), 900),
            Tuple.Create("clipboard", (Action)(() => ClipboardKeeper.Open(_owner)), 900),
            Tuple.Create("weather", (Action)(() => WeatherFeature.Open(_owner)), 8000),
            Tuple.Create("apps", (Action)(() => AppsFeature.Open(_owner)), 2000),
            Tuple.Create("prayer", (Action)(() => PrayerFeature.Open(_owner)), 8000),
            Tuple.Create("settings", (Action)(() => SettingsFeature.Open(_owner)), 1200),
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += NextStep;
        _timer.Start();
        _app.Run();
        return 0;
    }

    static void NextStep(object sender, EventArgs e)
    {
        _timer.Stop();
        // capture the panel opened by the previous step
        if (_index >= 0)
        {
            try { Capture(_steps[_index].Item1); }
            catch (Exception ex) { Console.WriteLine("capture " + _steps[_index].Item1 + ": " + ex.Message); }
        }
        _index++;
        if (_index >= _steps.Count)
        {
            PanelWindow.CloseCurrent();
            _app.Shutdown();
            return;
        }
        var step = _steps[_index];
        try
        {
            step.Item2();
            var p = PanelWindow.Current;
            if (p != null) { p.Left = -4000; p.Top = 100; } // keep it off the user's screen
        }
        catch (Exception ex) { Console.WriteLine("open " + step.Item1 + ": " + ex.Message); }
        _timer.Interval = TimeSpan.FromMilliseconds(step.Item3);
        _timer.Start();
    }

    static void Capture(string name)
    {
        var p = PanelWindow.Current;
        if (p == null) { Console.WriteLine("no panel for " + name); return; }
        var root = p.Content as FrameworkElement;
        if (root == null) return;
        p.UpdateLayout();
        int w = (int)Math.Ceiling(root.ActualWidth);
        int h = (int)Math.Ceiling(root.ActualHeight);
        if (w <= 0 || h <= 0) { Console.WriteLine("zero size for " + name); return; }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        // RenderTargetBitmap of an RTL-flow element comes out mirrored; flip it back
        BitmapSource output = rtb;
        if (root.FlowDirection == FlowDirection.RightToLeft)
            output = new TransformedBitmap(rtb, new ScaleTransform(-1, 1));
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(output));
        using (var fs = File.Create(Path.Combine(_outDir, "panel_" + name + ".png")))
            enc.Save(fs);
        Console.WriteLine("captured " + name + " " + w + "x" + h);
    }
}
