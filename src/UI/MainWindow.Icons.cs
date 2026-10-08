using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskMate.Core;
using DeskMate.Features;
using DeskMate.Logic;

namespace DeskMate.UI
{
    /// <summary>MainWindow tool icons: the ordered tool registry, labeled cell factory,
    /// collapsible icon sections, and the connector rays of the "linked" layout.</summary>
    public partial class MainWindow
    {
        /// <summary>One registered tool: its fallback glyph, label key, open action and a
        /// FIXED identity id. The id never moves when other tools are hidden — the drawn
        /// icon (ToolIcons) and the identity hue (Theme.ToolHues) both key off it, so a
        /// visibility change must not recolour or redraw a neighbour.</summary>
        class ToolDef
        {
            public string Glyph;
            public string Key;
            public Action Open;
            public int Id;
            public ToolDef(string glyph, string key, int id, Action open)
            {
                Glyph = glyph; Key = key; Id = id; Open = open;
            }
        }

        /// <summary>Single source of truth: glyph, label key, fixed id and open action per
        /// tool, gated by its visibility setting.</summary>
        List<ToolDef> IconDefs()
        {
            var s = Settings.I;
            var list = new List<ToolDef>();
            if (s.ShowCleaner) list.Add(new ToolDef(Glyphs.Cleaner, "f_cleaner", ToolIcons.Cleaner, () => CleanerFeature.Open(this)));
            if (s.ShowCalc) list.Add(new ToolDef(Glyphs.Calc, "f_calc", ToolIcons.Calc, () => CalculatorFeature.Open(this)));
            if (s.ShowWallpaper) list.Add(new ToolDef(Glyphs.Wallpaper, "f_wallpaper", ToolIcons.Wallpaper, () => WallpaperFeature.Open(this)));
            if (s.ShowPower) list.Add(new ToolDef(Glyphs.Power, "f_power", ToolIcons.Power, () => PowerFeature.Open(this)));
            if (s.ShowClipboard) list.Add(new ToolDef(Glyphs.Clipboard, "f_clipboard", ToolIcons.Clipboard, () => ClipboardKeeper.Open(this)));
            if (s.ShowWeather) list.Add(new ToolDef(Glyphs.Weather, "f_weather", ToolIcons.Weather, () => WeatherFeature.Open(this)));
            if (s.ShowApps) list.Add(new ToolDef(Glyphs.Apps, "f_apps", ToolIcons.Apps, () => AppsFeature.Open(this)));
            if (s.ShowPrayer) list.Add(new ToolDef(Glyphs.Prayer, "f_prayer", ToolIcons.Prayer, () => PrayerFeature.Open(this)));
            if (s.ShowFocus) list.Add(new ToolDef(Glyphs.Focus, "f_focus", ToolIcons.Focus, () => FocusFeature.Open(this)));
            if (s.ShowOccasions) list.Add(new ToolDef(Glyphs.Occasions, "f_occasions", ToolIcons.Occasions, () => OccasionsFeature.Open(this)));
            if (s.ShowNote) list.Add(new ToolDef(Glyphs.Note, "f_note", ToolIcons.Note, () => StickyNoteFeature.Open(this)));
            if (s.ShowSysMon) list.Add(new ToolDef(Glyphs.SysMon, "f_sysmon", ToolIcons.SysMon, () => SysMonFeature.Open(this)));
            if (s.ShowWorldClock) list.Add(new ToolDef(Glyphs.WorldClock, "f_worldclock", ToolIcons.WorldClock, () => WorldClocksFeature.Open(this)));
            if (s.ShowCommand) list.Add(new ToolDef(Glyphs.Command, "f_command", ToolIcons.Command, () => CommandBarFeature.Open(this)));
            list.Add(new ToolDef(Glyphs.SettingsGear, "f_settings", ToolIcons.Settings, () => SettingsFeature.Open(this)));
            return list;
        }

        /// <summary>Tool cells with permanently visible names under each icon. The drawn icon
        /// and its ambient glow key off the tool's fixed identity id.</summary>
        List<FrameworkElement> BuildIcons(bool floating, double cellWidth = Theme.DefaultCellWidth)
        {
            var list = new List<FrameworkElement>();
            foreach (var d in IconDefs())
            {
                var def = d;
                list.Add(Theme.MakeToolCell(def.Glyph, L.T(def.Key), () => def.Open(), floating, cellWidth, def.Id));
            }
            return list;
        }

        // ---------------- collapsible sections (v1.10) ----------------

        /// <summary>Icon cells grouped into two collapsible sections — «Daily» and «System» —
        /// with Settings always pinned ungrouped at the end. Used by the multi-row layouts
        /// (Classic, Tri-panel, Monolith) where fourteen tools would otherwise crowd one
        /// undifferentiated mass. Single-row strips (Sidebar/Dock/…) stay flat.</summary>
        FrameworkElement BuildIconSections(bool floating, int maxPerRow, double cellWidth = Theme.DefaultCellWidth)
        {
            var defs = IconDefs();
            var keys = defs.Select(d => d.Key).ToList();
            List<int> daily, system, pinned;
            ToolGroups.Split(keys, out daily, out system, out pinned);

            var host = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            if (daily.Count > 0)
                host.Children.Add(IconSection("grp_daily", daily, defs, floating, cellWidth, maxPerRow,
                    Settings.I.GroupDailyCollapsed, v => Settings.I.GroupDailyCollapsed = v));
            if (system.Count > 0)
                host.Children.Add(IconSection("grp_system", system, defs, floating, cellWidth, maxPerRow,
                    Settings.I.GroupSystemCollapsed, v => Settings.I.GroupSystemCollapsed = v));
            if (pinned.Count > 0)
            {
                var cells = pinned.Select(i => CellFor(defs[i], floating, cellWidth)).ToList();
                host.Children.Add(BuildBalancedRows(cells, maxPerRow, new Thickness(0, 6, 0, 0)));
            }
            return host;
        }

        FrameworkElement CellFor(ToolDef def, bool floating, double cellWidth)
        {
            return Theme.MakeToolCell(def.Glyph, L.T(def.Key), () => def.Open(), floating, cellWidth, def.Id);
        }

        /// <summary>One section: a quiet clickable header (name + hairline + chevron) that
        /// collapses its rows. Collapse state is persisted, so a tidied widget stays tidy.</summary>
        FrameworkElement IconSection(string titleKey, List<int> indices, List<ToolDef> defs,
            bool floating, double cellWidth, int maxPerRow, bool collapsed, Action<bool> setCollapsed)
        {
            var section = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            var title = Theme.MakeText(L.T(titleKey), 12, Theme.TextFaint);
            title.FontWeight = FontWeights.Medium;
            title.VerticalAlignment = VerticalAlignment.Center;
            // the title keeps a clear gap from the rule on its inner side (it used to run
            // straight into the line)
            title.Margin = new Thickness(0, 0, 10, 0);
            var chevron = Theme.MakeGlyph(collapsed ? "\uE70D" : "\uE70E", 9, Theme.TextFaint); // E70D/E70E chevrons
            chevron.VerticalAlignment = VerticalAlignment.Center;
            chevron.Margin = new Thickness(10, 0, 2, 0);
            var rule = new Border
            {
                Height = 1, Background = Theme.CardBorder,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // logical sides: the panel mirrors under RTL, so Dock.Left is the reading start —
            // the title leads (right in Arabic), the chevron trails (left in Arabic). The old
            // docking had them swapped, which put the Arabic title at the far left
            DockPanel.SetDock(title, Dock.Left);
            DockPanel.SetDock(chevron, Dock.Right);
            var headRow = new DockPanel();
            headRow.Children.Add(chevron);
            headRow.Children.Add(title);
            headRow.Children.Add(rule);                 // fills the middle
            var head = new Border
            {
                Child = headRow,
                Margin = new Thickness(14, 9, 14, 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent,        // full-width click target
            };
            head.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            head.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                setCollapsed(!collapsed);
                Settings.I.Save();
                Reload();
            };
            section.Children.Add(head);

            if (!collapsed)
            {
                var cells = indices.Select(i => CellFor(defs[i], floating, cellWidth)).ToList();
                section.Children.Add(BuildBalancedRows(cells, maxPerRow, new Thickness(0, 4, 0, 0)));
            }
            return section;
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
