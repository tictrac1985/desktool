using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Features;

namespace DeskMate.UI
{
    /// <summary>MainWindow tool icons: the ordered tool registry, labeled cell factory
    /// and the connector rays of the "linked" layout.</summary>
    public partial class MainWindow
    {
        /// <summary>Single source of truth: glyph, label key and open action per tool,
        /// gated by its visibility setting.</summary>
        List<Tuple<string, string, Action>> IconDefs()
        {
            var s = Settings.I;
            var list = new List<Tuple<string, string, Action>>();
            if (s.ShowCleaner) list.Add(Tuple.Create(Glyphs.Cleaner, "f_cleaner", (Action)(() => CleanerFeature.Open(this))));
            if (s.ShowCalc) list.Add(Tuple.Create(Glyphs.Calc, "f_calc", (Action)(() => CalculatorFeature.Open(this))));
            if (s.ShowWallpaper) list.Add(Tuple.Create(Glyphs.Wallpaper, "f_wallpaper", (Action)(() => WallpaperFeature.Open(this))));
            if (s.ShowPower) list.Add(Tuple.Create(Glyphs.Power, "f_power", (Action)(() => PowerFeature.Open(this))));
            if (s.ShowClipboard) list.Add(Tuple.Create(Glyphs.Clipboard, "f_clipboard", (Action)(() => ClipboardKeeper.Open(this))));
            if (s.ShowWeather) list.Add(Tuple.Create(Glyphs.Weather, "f_weather", (Action)(() => WeatherFeature.Open(this))));
            if (s.ShowApps) list.Add(Tuple.Create(Glyphs.Apps, "f_apps", (Action)(() => AppsFeature.Open(this))));
            if (s.ShowPrayer) list.Add(Tuple.Create(Glyphs.Prayer, "f_prayer", (Action)(() => PrayerFeature.Open(this))));
            if (s.ShowFocus) list.Add(Tuple.Create(Glyphs.Focus, "f_focus", (Action)(() => FocusFeature.Open(this))));
            if (s.ShowOccasions) list.Add(Tuple.Create(Glyphs.Occasions, "f_occasions", (Action)(() => OccasionsFeature.Open(this))));
            if (s.ShowNote) list.Add(Tuple.Create(Glyphs.Note, "f_note", (Action)(() => StickyNoteFeature.Open(this))));
            if (s.ShowSysMon) list.Add(Tuple.Create(Glyphs.SysMon, "f_sysmon", (Action)(() => SysMonFeature.Open(this))));
            if (s.ShowWorldClock) list.Add(Tuple.Create(Glyphs.WorldClock, "f_worldclock", (Action)(() => WorldClocksFeature.Open(this))));
            list.Add(Tuple.Create(Glyphs.SettingsGear, "f_settings", (Action)(() => SettingsFeature.Open(this))));
            return list;
        }

        /// <summary>Tool cells with permanently visible names under each icon.</summary>
        List<FrameworkElement> BuildIcons(bool floating)
        {
            var list = new List<FrameworkElement>();
            foreach (var def in IconDefs())
                list.Add(Theme.MakeToolCell(def.Item1, L.T(def.Item2), def.Item3, floating));
            return list;
        }

        // ---------------- connector rays (layout 1) ----------------

        void ScheduleConnectorDraw()
        {
            if (_connectors == null) return;
            Dispatcher.BeginInvoke(new Action(DrawConnectors), DispatcherPriority.Loaded);
        }

        void DrawConnectors()
        {
            if (_connectors == null || _clockCard == null) return;
            try
            {
                _connectors.Children.Clear();
                double h = _connectors.Height;
                Point from = _clockCard.TranslatePoint(
                    new Point(_clockCard.ActualWidth / 2, _clockCard.ActualHeight - 12), _connectors);
                var stroke = new SolidColorBrush(Color.FromArgb(0x58,
                    Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
                stroke.Freeze();
                foreach (var ic in _floatIcons)
                {
                    Point to = ic.TranslatePoint(new Point(ic.ActualWidth / 2, 4), _connectors);
                    _connectors.Children.Add(new Line
                    {
                        X1 = from.X, Y1 = Math.Max(0, from.Y),
                        X2 = to.X, Y2 = Math.Min(h + 6, to.Y),
                        Stroke = stroke,
                        StrokeThickness = 1.2,
                    });
                    var dot = new Ellipse { Width = 4, Height = 4, Fill = Theme.Accent };
                    Canvas.SetLeft(dot, to.X - 2);
                    Canvas.SetTop(dot, Math.Min(h + 4, to.Y) - 2);
                    _connectors.Children.Add(dot);
                }
            }
            catch (Exception ex) { Log.Error("connectors", ex); }
        }
    }
}
