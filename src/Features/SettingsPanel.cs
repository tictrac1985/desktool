using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class SettingsFeature
    {
        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "settings", L.T("f_settings"), Build, 340);
        }

        static void SaveAndReload()
        {
            Settings.I.Save();
            if (MainWindow.Instance != null) MainWindow.Instance.Reload();
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var scroll = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            // ---- general ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_general")));

            var langRow = new DockPanel();
            var langCombo = Theme.MakeCombo();
            langCombo.Items.Add("العربية");
            langCombo.Items.Add("English");
            langCombo.SelectedIndex = L.IsAr ? 0 : 1;
            langCombo.SelectionChanged += (s, e) =>
            {
                Settings.I.Lang = langCombo.SelectedIndex == 1 ? "en" : "ar";
                SaveAndReload();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            };
            DockPanel.SetDock(langCombo, Dock.Right);
            langRow.Children.Add(langCombo);
            var langLbl = Theme.MakeText(L.T("st_lang"), 13, Theme.TextDim);
            langLbl.VerticalAlignment = VerticalAlignment.Center;
            langRow.Children.Add(langLbl);
            langRow.Margin = new Thickness(0, 2, 0, 6);
            stack.Children.Add(langRow);

            // ---- layout picker ----
            var layoutRow = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var layoutCombo = Theme.MakeCombo();
            layoutCombo.Items.Add(L.T("ly_classic"));
            layoutCombo.Items.Add(L.T("ly_connected"));
            layoutCombo.Items.Add(L.T("ly_triple"));
            layoutCombo.Items.Add(L.T("ly_orbit"));
            layoutCombo.Items.Add(L.T("ly_sidebar"));
            layoutCombo.Items.Add(L.T("ly_dock"));
            layoutCombo.Items.Add(L.T("ly_monolith"));
            layoutCombo.Items.Add(L.T("ly_halo"));
            layoutCombo.SelectedIndex = Math.Max(0, Math.Min(7, Settings.I.LayoutIndex));
            layoutCombo.SelectionChanged += (s, e) =>
            {
                Settings.I.LayoutIndex = layoutCombo.SelectedIndex;
                SaveAndReload();
            };
            DockPanel.SetDock(layoutCombo, Dock.Right);
            layoutRow.Children.Add(layoutCombo);
            var layoutLbl = Theme.MakeText(L.T("st_layout"), 13, Theme.TextDim);
            layoutLbl.VerticalAlignment = VerticalAlignment.Center;
            layoutRow.Children.Add(layoutLbl);
            stack.Children.Add(layoutRow);

            // ---- theme picker ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_theme")));
            var themeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
            for (int i = 0; i < Theme.Palettes.Length; i++)
            {
                int idx = i;
                var pal = Theme.Palettes[i];
                bool selected = Settings.I.ThemeIndex == i;
                var swatch = new Border
                {
                    Width = 30, Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = new System.Windows.Media.LinearGradientBrush(pal.CardTop, pal.CardBottom, 90),
                    BorderBrush = selected ? Theme.Accent : Theme.CardBorder,
                    BorderThickness = new Thickness(selected ? 2.5 : 1),
                    Margin = new Thickness(3, 0, 3, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = L.IsAr ? pal.KeyAr : pal.KeyEn,
                    Child = new System.Windows.Shapes.Ellipse
                    {
                        Width = 12, Height = 12,
                        Fill = new System.Windows.Media.SolidColorBrush(pal.Accent),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                swatch.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                swatch.MouseLeftButtonUp += (s, e) =>
                {
                    e.Handled = true;
                    Settings.I.ThemeIndex = idx;
                    Theme.Apply(Logic.AutoThemeCore.EffectiveIndex(idx, Settings.I.AutoTheme, DateTime.Now.Hour));
                    SaveAndReload();
                    PanelWindow.CloseCurrent();
                    Open(MainWindow.Instance);
                };
                themeRow.Children.Add(swatch);
            }
            stack.Children.Add(themeRow);
            stack.Children.Add(Theme.MakeCheck(L.T("st_material_you"), Settings.I.MaterialYou, v =>
            {
                Settings.I.MaterialYou = v;
                if (v) Logic.MaterialYouCore.Invalidate();   // force re-derive on next tick
                SaveAndReload();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            }));

            stack.Children.Add(Theme.MakeCheck(L.T("st_auto_theme"), Settings.I.AutoTheme, v =>
            {
                Settings.I.AutoTheme = v;
                Theme.Apply(Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, Settings.I.LightThemeIndex, v, DateTime.Now.Hour));
                SaveAndReload();
                PanelWindow.CloseCurrent();
                Open(MainWindow.Instance);
            }));

            // "Day palette" — which palette to flip to during daylight when AutoTheme is on.
            // Hidden when AutoTheme is off: the chosen theme is always the active one in that case.
            if (Settings.I.AutoTheme)
            {
                stack.Children.Add(Theme.SectionTitle(L.T("st_day_palette")));
                var dayRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
                for (int i = 0; i < Theme.Palettes.Length; i++)
                {
                    int idx = i;
                    var pal = Theme.Palettes[i];
                    bool selected = Settings.I.LightThemeIndex == i;
                    var swatch = new Border
                    {
                        Width = 26, Height = 26,
                        CornerRadius = new CornerRadius(13),
                        Background = new System.Windows.Media.LinearGradientBrush(pal.CardTop, pal.CardBottom, 90),
                        BorderBrush = selected ? Theme.Accent : Theme.CardBorder,
                        BorderThickness = new Thickness(selected ? 2.5 : 1),
                        Margin = new Thickness(2, 0, 2, 0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        ToolTip = L.IsAr ? pal.KeyAr : pal.KeyEn,
                        Child = new System.Windows.Shapes.Ellipse
                        {
                            Width = 10, Height = 10,
                            Fill = new System.Windows.Media.SolidColorBrush(pal.Accent),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    };
                    swatch.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
                    swatch.MouseLeftButtonUp += (s, e) =>
                    {
                        e.Handled = true;
                        Settings.I.LightThemeIndex = idx;
                        Theme.Apply(Logic.AutoThemeCore.EffectiveIndex(Settings.I.ThemeIndex, idx, true, DateTime.Now.Hour));
                        SaveAndReload();
                        PanelWindow.CloseCurrent();
                        Open(MainWindow.Instance);
                    };
                    dayRow.Children.Add(swatch);
                }
                stack.Children.Add(dayRow);
            }

            // ---- background transparency ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_opacity")));
            var opSlider = Theme.MakeSlider(30, 100, Settings.I.BgOpacity * 100, v =>
            {
                Settings.I.BgOpacity = Math.Max(0.3, Math.Min(1.0, v / 100.0));
                if (MainWindow.Instance != null) MainWindow.Instance.ApplyCardOpacity();
            });
            opSlider.Margin = new Thickness(2, 0, 2, 6);
            opSlider.LostMouseCapture += (s, e) => Settings.I.Save();
            stack.Children.Add(opSlider);

            var clockRow = new DockPanel();
            var clockCombo = Theme.MakeCombo();
            clockCombo.Items.Add(L.T("st_style_digital"));
            clockCombo.Items.Add(L.T("st_style_analog"));
            clockCombo.Items.Add(L.T("st_style_minimal"));
            clockCombo.Items.Add(L.T("st_style_flip"));
            clockCombo.Items.Add(L.T("st_style_word"));
            clockCombo.Items.Add(L.T("st_style_neon"));
            clockCombo.Items.Add(L.T("st_style_binary"));
            clockCombo.Items.Add(L.T("st_style_dayring"));
            clockCombo.Items.Add(L.T("st_style_sunpath"));
            clockCombo.Items.Add(L.T("st_style_stripes"));
            clockCombo.Items.Add(L.T("st_style_moonphase"));
            clockCombo.SelectedIndex = Math.Max(0, Math.Min(10, Settings.I.ClockStyle));
            clockCombo.SelectionChanged += (s, e) =>
            {
                Settings.I.ClockStyle = clockCombo.SelectedIndex;
                SaveAndReload();
            };
            DockPanel.SetDock(clockCombo, Dock.Right);
            clockRow.Children.Add(clockCombo);
            var clockLbl = Theme.MakeText(L.T("st_clock"), 13, Theme.TextDim);
            clockLbl.VerticalAlignment = VerticalAlignment.Center;
            clockRow.Children.Add(clockLbl);
            stack.Children.Add(clockRow);

            stack.Children.Add(Theme.MakeCheck(L.T("st_24h"), Settings.I.Use24h, v => { Settings.I.Use24h = v; SaveAndReload(); }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_seconds"), Settings.I.ShowSeconds, v => { Settings.I.ShowSeconds = v; SaveAndReload(); }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_hijri"), Settings.I.ShowHijri, v => { Settings.I.ShowHijri = v; SaveAndReload(); }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_prayer_line"), Settings.I.ShowNextPrayerLine, v => { Settings.I.ShowNextPrayerLine = v; SaveAndReload(); }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_weather_line"), Settings.I.ShowWeatherLine, v => { Settings.I.ShowWeatherLine = v; SaveAndReload(); }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_panels_pinned"), Settings.I.PanelsPinned, v =>
            {
                // live: the panel that is already open follows immediately — its Deactivated
                // handler reads this flag, and the header pin reflects the new state too
                Settings.I.PanelsPinned = v;
                if (PanelWindow.Current is PanelWindow pw && pw.Pinned != v) pw.TogglePin();
                Settings.I.Save();
            }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_autostart"), Settings.I.AutoStart, v =>
            {
                Settings.I.AutoStart = v;
                Win32.SetAutoStart(v);
                Settings.I.Save();
            }));

            // ---- visible tools ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_features")));
            stack.Children.Add(Theme.MakeCheck(L.T("st_group_tools"), Settings.I.GroupTools,
                v => { Settings.I.GroupTools = v; SaveAndReload(); }));
            var grid = new UniformGrid { Columns = 2 };
            AddFeatureCheck(grid, "f_cleaner", Settings.I.ShowCleaner, v => Settings.I.ShowCleaner = v);
            AddFeatureCheck(grid, "f_calc", Settings.I.ShowCalc, v => Settings.I.ShowCalc = v);
            AddFeatureCheck(grid, "f_wallpaper", Settings.I.ShowWallpaper, v => Settings.I.ShowWallpaper = v);
            AddFeatureCheck(grid, "f_power", Settings.I.ShowPower, v => Settings.I.ShowPower = v);
            AddFeatureCheck(grid, "f_clipboard", Settings.I.ShowClipboard, v => Settings.I.ShowClipboard = v);
            AddFeatureCheck(grid, "f_weather", Settings.I.ShowWeather, v => Settings.I.ShowWeather = v);
            AddFeatureCheck(grid, "f_apps", Settings.I.ShowApps, v => Settings.I.ShowApps = v);
            AddFeatureCheck(grid, "f_prayer", Settings.I.ShowPrayer, v => Settings.I.ShowPrayer = v);
            AddFeatureCheck(grid, "f_focus", Settings.I.ShowFocus, v => Settings.I.ShowFocus = v);
            AddFeatureCheck(grid, "f_occasions", Settings.I.ShowOccasions, v => Settings.I.ShowOccasions = v);
            AddFeatureCheck(grid, "f_note", Settings.I.ShowNote, v => Settings.I.ShowNote = v);
            AddFeatureCheck(grid, "f_sysmon", Settings.I.ShowSysMon, v => Settings.I.ShowSysMon = v);
            AddFeatureCheck(grid, "f_worldclock", Settings.I.ShowWorldClock, v => Settings.I.ShowWorldClock = v);
            AddFeatureCheck(grid, "f_command", Settings.I.ShowCommand, v => Settings.I.ShowCommand = v);
            stack.Children.Add(grid);

            // ---- location ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_location")));
            var locStatus = Theme.MakeText(
                WeatherFeature.HasLocation ? Settings.I.City + " · " + Settings.I.Country : "—",
                12, Theme.TextDim);
            locStatus.FlowDirection = FlowDirection.LeftToRight;
            locStatus.HorizontalAlignment = HorizontalAlignment.Center;
            locStatus.Margin = new Thickness(0, 0, 0, 4);
            stack.Children.Add(locStatus);

            var autoBtn = Theme.MakeButton(L.T("pr_locate"), () =>
            {
                locStatus.Text = L.T("we_locating");
                Task.Run(() => WeatherFeature.AutoLocate()).ContinueWith(t =>
                {
                    host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!t.IsFaulted && t.Result != null)
                        {
                            PrayerFeature.Invalidate();
                            locStatus.Text = Settings.I.City + " · " + Settings.I.Country;
                        }
                        else locStatus.Text = L.T("we_err");
                    }));
                });
            });
            autoBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(autoBtn);

            var cityRow = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            // the real action is assigned below (doSearch); routed through MakeButton because a
            // second MouseLeftButtonUp handler would be blocked by the internal Handled flag
            Action doSearchHolder = null;
            var searchBtn = Theme.MakeButton(L.T("st_search"), () => { if (doSearchHolder != null) doSearchHolder(); });
            DockPanel.SetDock(searchBtn, Dock.Right);
            var cityBox = Theme.MakeInput(Settings.I.City);
            cityRow.Children.Add(searchBtn);
            cityRow.Children.Add(cityBox);
            stack.Children.Add(cityRow);
            var resultsHost = new StackPanel();
            stack.Children.Add(resultsHost);

            Action doSearch = () =>
            {
                string q = cityBox.Text.Trim();
                if (q.Length < 2) return;
                locStatus.Text = L.T("we_loading");
                Task.Run(() =>
                {
                    string json = WeatherFeature.HttpGet(WeatherCore.BuildGeocodeUrl(q, Settings.I.Lang));
                    return WeatherCore.ParseGeocode(json);
                }).ContinueWith(t =>
                {
                    host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        resultsHost.Children.Clear();
                        if (t.IsFaulted || t.Result == null || t.Result.Count == 0)
                        {
                            locStatus.Text = L.T("ap_none");
                            return;
                        }
                        locStatus.Text = "";
                        foreach (var g in t.Result)
                        {
                            var loc = g;
                            var b = Theme.MakeButton(loc.City + " (" + loc.Country + ")", () =>
                            {
                                Settings.I.Lat = loc.Lat;
                                Settings.I.Lon = loc.Lon;
                                Settings.I.City = loc.City;
                                Settings.I.Country = loc.Country;
                                Settings.I.AutoLocation = false;
                                Settings.I.Save();
                                PrayerFeature.Invalidate();
                                resultsHost.Children.Clear();
                                locStatus.Text = loc.City + " · " + loc.Country;
                            });
                            b.HorizontalAlignment = HorizontalAlignment.Stretch;
                            resultsHost.Children.Add(b);
                        }
                    }));
                });
            };
            doSearchHolder = doSearch;
            cityBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) doSearch(); };

            // ---- backup: export / import the whole settings file ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_backup")));
            // the file carries tasks/notes/pinned clips too (DPAPI-encrypted) — say so,
            // otherwise users assume "settings" means theme choices only
            var note = Theme.MakeText(L.T("st_backup_note"), 11, Theme.TextFaint);
            note.TextWrapping = TextWrapping.Wrap;
            note.Margin = new Thickness(0, 0, 0, 6);
            stack.Children.Add(note);
            var backupStatus = Theme.MakeText("", 11.5, Theme.Accent);
            backupStatus.HorizontalAlignment = HorizontalAlignment.Center;
            backupStatus.Margin = new Thickness(0, 4, 0, 0);

            var exportBtn = Theme.MakeButton(L.T("st_export"), () =>
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "JSON|*.json",
                    FileName = "DeskMate-settings.json",
                };
                host.SuppressAutoClose = true;
                try
                {
                    if (dlg.ShowDialog(host) == true)
                    {
                        Settings.I.Save();   // flush first: the file must hold the latest state
                        System.IO.File.Copy(Settings.DefaultPath, dlg.FileName, true);
                        backupStatus.Text = L.T("st_exported");
                    }
                }
                catch (Exception ex) { Log.Error("settings export", ex); }
                finally { host.SuppressAutoClose = false; try { host.Activate(); } catch { } }
            });
            exportBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            stack.Children.Add(exportBtn);

            var importBtn = Theme.MakeButton(L.T("st_import"), () =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON|*.json",
                    CheckFileExists = true,
                };
                host.SuppressAutoClose = true;
                try
                {
                    if (dlg.ShowDialog(host) == true)
                    {
                        // validate before touching the live file: a corrupt or foreign JSON
                        // must not wipe the user's settings
                        var parsed = Json.Parse(System.IO.File.ReadAllText(dlg.FileName))
                            as System.Collections.Generic.Dictionary<string, object>;
                        if (parsed == null || !parsed.ContainsKey("Lang"))
                        {
                            backupStatus.Text = L.T("st_import_bad");
                            return;
                        }
                        System.IO.File.Copy(dlg.FileName, Settings.DefaultPath, true);
                        Settings.Load();
                        Theme.Apply(Settings.I.ThemeIndex);
                        Win32.SetAutoStart(Settings.I.AutoStart);
                        PrayerFeature.Invalidate();
                        PanelWindow.CloseCurrent();
                        if (MainWindow.Instance != null) MainWindow.Instance.Reload();
                        Toast.Show(L.T("st_imported"), Glyphs.SettingsGear);
                    }
                }
                catch (Exception ex) { Log.Error("settings import", ex); backupStatus.Text = L.T("st_import_bad"); }
                finally { host.SuppressAutoClose = false; }
            });
            importBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            importBtn.Margin = new Thickness(3, 4, 3, 0);
            stack.Children.Add(importBtn);
            stack.Children.Add(backupStatus);

            return scroll;
        }

        static void AddFeatureCheck(Panel panel, string key, bool current, Action<bool> set)
        {
            panel.Children.Add(Theme.MakeCheck(L.T(key), current, v => { set(v); SaveAndReload(); }));
        }
    }
}
