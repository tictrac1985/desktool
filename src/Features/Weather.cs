using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DeskMate.Core;
using DeskMate.Logic;
using DeskMate.UI;

namespace DeskMate.Features
{
    public static class WeatherFeature
    {
        static WeatherData _cache;

        static WeatherFeature()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public static string HttpGet(string url, int timeoutMs = 8000)
        {
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = timeoutMs;
            req.UserAgent = "DeskMate/1.0";
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream()))
                return reader.ReadToEnd();
        }

        /// <summary>Detect location by IP; fills Settings and returns it (null on failure).</summary>
        public static GeoLoc AutoLocate()
        {
            foreach (string url in new[] { "https://ipapi.co/json/", "http://ip-api.com/json/" })
            {
                try
                {
                    var g = WeatherCore.ParseIpLocation(HttpGet(url));
                    if (g != null)
                    {
                        Settings.I.Lat = g.Lat;
                        Settings.I.Lon = g.Lon;
                        Settings.I.City = g.City;
                        Settings.I.Country = g.Country;
                        Settings.I.Save();
                        return g;
                    }
                }
                catch (Exception ex) { Log.Error("AutoLocate " + url, ex); }
            }
            return null;
        }

        public static bool HasLocation
        {
            get { return Math.Abs(Settings.I.Lat) > 0.001 || Math.Abs(Settings.I.Lon) > 0.001; }
        }

        /// <summary>Ensures a location exists (auto-detecting if allowed), then returns fresh
        /// or cached weather. Runs on a background thread.</summary>
        static WeatherData GetWeather(bool forceRefresh)
        {
            if (!HasLocation && Settings.I.AutoLocation) AutoLocate();
            if (!HasLocation) return null;
            if (!forceRefresh && _cache != null && (DateTime.Now - _cache.FetchedAt).TotalMinutes < 10)
                return _cache;
            string json = HttpGet(WeatherCore.BuildForecastUrl(Settings.I.Lat, Settings.I.Lon));
            _cache = WeatherCore.ParseForecast(json);
            return _cache;
        }

        public static void Open(Window owner)
        {
            PanelWindow.Toggle(owner, "weather", L.T("f_weather"), Build);
        }

        static FrameworkElement Build(PanelWindow host)
        {
            var stack = new StackPanel();
            var status = Theme.MakeText(L.T("we_loading"), 12.5, Theme.TextDim);
            status.HorizontalAlignment = HorizontalAlignment.Center;
            status.Margin = new Thickness(0, 8, 0, 8);
            stack.Children.Add(status);

            var content = new StackPanel { Visibility = Visibility.Collapsed };
            stack.Children.Add(content);

            Action<bool> load = null;
            load = force =>
            {
                status.Visibility = Visibility.Visible;
                status.Text = HasLocation ? L.T("we_loading") : L.T("we_locating");
                Task.Run(() =>
                {
                    try { return GetWeather(force); }
                    catch (Exception ex) { Log.Error("weather", ex); return null; }
                }).ContinueWith(t =>
                {
                    host.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var w = t.IsFaulted ? null : t.Result;
                        if (w == null) { status.Text = L.T("we_err"); return; }
                        status.Visibility = Visibility.Collapsed;
                        Render(content, w);
                        content.Visibility = Visibility.Visible;
                    }));
                });
            };

            var refresh = Theme.MakeButton(L.T("we_refresh"), () => load(true));
            refresh.HorizontalAlignment = HorizontalAlignment.Center;
            refresh.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(refresh);

            load(false);
            return stack;
        }

        static void Render(StackPanel content, WeatherData w)
        {
            content.Children.Clear();

            var city = Theme.MakeText(Settings.I.City, 13, Theme.TextDim);
            city.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(city);

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            bool night = DateTime.Now.Hour < 6 || DateTime.Now.Hour >= 19;
            var glyph = Theme.MakeText(WeatherCore.WmoGlyph(w.Code, night), 42, Theme.Accent, Theme.SymbolFont);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(glyph);
            var temp = Theme.MakeText(double.IsNaN(w.Temp) ? "--" : Math.Round(w.Temp) + "°", 46, Theme.Text, Theme.LightFont);
            row.Children.Add(temp);
            content.Children.Add(row);

            string ar, en;
            WeatherCore.WmoText(w.Code, out ar, out en);
            var desc = Theme.MakeText(L.IsAr ? ar : en, 14, Theme.TextDim);
            desc.HorizontalAlignment = HorizontalAlignment.Center;
            content.Children.Add(desc);

            var meta = Theme.MakeText(
                L.T("we_humidity") + " " + (double.IsNaN(w.Humidity) ? "--" : Math.Round(w.Humidity) + "%") +
                "   •   " + L.T("we_wind") + " " + (double.IsNaN(w.WindKmh) ? "--" : Math.Round(w.WindKmh) + " km/h"),
                11.5, Theme.TextFaint);
            meta.HorizontalAlignment = HorizontalAlignment.Center;
            meta.Margin = new Thickness(0, 2, 0, 6);
            content.Children.Add(meta);

            if (w.Days.Count > 0)
            {
                var daysRow = new UniformGrid { Columns = Math.Min(3, w.Days.Count), Margin = new Thickness(0, 6, 0, 0) };
                var ci = L.IsAr ? new System.Globalization.CultureInfo("ar-EG") : new System.Globalization.CultureInfo("en-US");
                for (int i = 0; i < w.Days.Count && i < 3; i++)
                {
                    var d = w.Days[i];
                    var cell = new StackPanel { Margin = new Thickness(4, 0, 4, 0), HorizontalAlignment = HorizontalAlignment.Center };
                    DateTime date;
                    string dayName = DateTime.TryParse(d.Date, out date) ? date.ToString("ddd", ci) : d.Date;
                    var dn = Theme.MakeText(dayName, 11.5, Theme.TextDim);
                    dn.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.Children.Add(dn);
                    var dg = Theme.MakeText(WeatherCore.WmoGlyph(d.Code), 18, Theme.Accent, Theme.SymbolFont);
                    dg.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.Children.Add(dg);
                    var t = Theme.MakeText(Math.Round(d.TMax) + "° / " + Math.Round(d.TMin) + "°", 11.5, Theme.Text);
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.Children.Add(t);
                    daysRow.Children.Add(cell);
                }
                content.Children.Add(daysRow);
            }
        }
    }
}
