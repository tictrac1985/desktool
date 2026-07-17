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

            var clockRow = new DockPanel();
            var clockCombo = Theme.MakeCombo();
            clockCombo.Items.Add(L.T("st_style_digital"));
            clockCombo.Items.Add(L.T("st_style_analog"));
            clockCombo.Items.Add(L.T("st_style_minimal"));
            clockCombo.SelectedIndex = Math.Max(0, Math.Min(2, Settings.I.ClockStyle));
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
            stack.Children.Add(Theme.MakeCheck(L.T("st_autostart"), Settings.I.AutoStart, v =>
            {
                Settings.I.AutoStart = v;
                Win32.SetAutoStart(v);
                Settings.I.Save();
            }));
            stack.Children.Add(Theme.MakeCheck(L.T("st_pin"), Settings.I.PinToDesktop, v => { Settings.I.PinToDesktop = v; Settings.I.Save(); }));

            // ---- visible tools ----
            stack.Children.Add(Theme.SectionTitle(L.T("st_features")));
            var grid = new UniformGrid { Columns = 2 };
            AddFeatureCheck(grid, "f_cleaner", Settings.I.ShowCleaner, v => Settings.I.ShowCleaner = v);
            AddFeatureCheck(grid, "f_calc", Settings.I.ShowCalc, v => Settings.I.ShowCalc = v);
            AddFeatureCheck(grid, "f_wallpaper", Settings.I.ShowWallpaper, v => Settings.I.ShowWallpaper = v);
            AddFeatureCheck(grid, "f_power", Settings.I.ShowPower, v => Settings.I.ShowPower = v);
            AddFeatureCheck(grid, "f_clipboard", Settings.I.ShowClipboard, v => Settings.I.ShowClipboard = v);
            AddFeatureCheck(grid, "f_weather", Settings.I.ShowWeather, v => Settings.I.ShowWeather = v);
            AddFeatureCheck(grid, "f_apps", Settings.I.ShowApps, v => Settings.I.ShowApps = v);
            AddFeatureCheck(grid, "f_prayer", Settings.I.ShowPrayer, v => Settings.I.ShowPrayer = v);
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
            var searchBtn = Theme.MakeButton(L.T("st_search"), () => { });
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
            // MakeButton was created with a null action; wire the real one now
            searchBtn.MouseLeftButtonUp += (s, e) => doSearch();
            cityBox.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) doSearch(); };

            return scroll;
        }

        static void AddFeatureCheck(Panel panel, string key, bool current, Action<bool> set)
        {
            panel.Children.Add(Theme.MakeCheck(L.T(key), current, v => { set(v); SaveAndReload(); }));
        }
    }
}
