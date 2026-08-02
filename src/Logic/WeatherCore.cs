using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskMate.Logic
{
    public class GeoLoc
    {
        public double Lat;
        public double Lon;
        public string City = "";
        public string Country = "";   // ISO code
    }

    public class DayForecast
    {
        public string Date = "";
        public double TMax;
        public double TMin;
        public int Code;
    }

    public class WeatherData
    {
        public double Temp;
        public int Code;
        public double Humidity;
        public double WindKmh;
        public List<DayForecast> Days = new List<DayForecast>();
        public DateTime FetchedAt;
    }

    /// <summary>Pure parts of the weather feature: URL building, JSON→model, WMO code mapping.
    /// Network I/O lives in the UI layer (Features/Weather.cs).</summary>
    public static class WeatherCore
    {
        public static string BuildForecastUrl(double lat, double lon)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0:0.####}&longitude={1:0.####}" +
                "&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m" +
                "&daily=temperature_2m_max,temperature_2m_min,weather_code&timezone=auto&forecast_days=3",
                lat, lon);
        }

        public static string BuildGeocodeUrl(string city, string lang)
        {
            return "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(city) +
                   "&count=5&language=" + (lang == "ar" ? "ar" : "en") + "&format=json";
        }

        public static WeatherData ParseForecast(string json)
        {
            var root = DeskMate.Core.Json.Parse(json) as Dictionary<string, object>;
            if (root == null) throw new FormatException("bad weather json");
            var cur = DeskMate.Core.Json.GetObj(root, "current");
            // a response without the "current" block is an error page or a truncated body —
            // refuse it instead of rendering "0° and clear skies" from defaults
            if (cur == null) throw new FormatException("weather json missing current block");
            var w = new WeatherData { FetchedAt = DateTime.Now };
            w.Temp = DeskMate.Core.Json.GetNum(cur, "temperature_2m", double.NaN);
            w.Code = DeskMate.Core.Json.GetInt(cur, "weather_code", 0);
            w.Humidity = DeskMate.Core.Json.GetNum(cur, "relative_humidity_2m", double.NaN);
            w.WindKmh = DeskMate.Core.Json.GetNum(cur, "wind_speed_10m", double.NaN);
            var daily = DeskMate.Core.Json.GetObj(root, "daily");
            if (daily != null)
            {
                var dates = DeskMate.Core.Json.GetList(daily, "time");
                var tmax = DeskMate.Core.Json.GetList(daily, "temperature_2m_max");
                var tmin = DeskMate.Core.Json.GetList(daily, "temperature_2m_min");
                var codes = DeskMate.Core.Json.GetList(daily, "weather_code");
                for (int i = 0; i < dates.Count; i++)
                {
                    var df = new DayForecast { Date = dates[i] as string ?? "" };
                    if (i < tmax.Count && tmax[i] is double) df.TMax = (double)tmax[i];
                    if (i < tmin.Count && tmin[i] is double) df.TMin = (double)tmin[i];
                    if (i < codes.Count && codes[i] is double) df.Code = (int)(double)codes[i];
                    w.Days.Add(df);
                }
            }
            return w;
        }

        public static List<GeoLoc> ParseGeocode(string json)
        {
            var result = new List<GeoLoc>();
            var root = DeskMate.Core.Json.Parse(json) as Dictionary<string, object>;
            if (root == null) return result;
            foreach (var o in DeskMate.Core.Json.GetList(root, "results"))
            {
                var d = o as Dictionary<string, object>;
                if (d == null) continue;
                var g = new GeoLoc
                {
                    Lat = DeskMate.Core.Json.GetNum(d, "latitude", 0),
                    Lon = DeskMate.Core.Json.GetNum(d, "longitude", 0),
                    City = DeskMate.Core.Json.GetStr(d, "name", ""),
                    Country = DeskMate.Core.Json.GetStr(d, "country_code", ""),
                };
                string admin = DeskMate.Core.Json.GetStr(d, "admin1", "");
                if (!string.IsNullOrEmpty(admin) && admin != g.City) g.City = g.City + "، " + admin;
                result.Add(g);
            }
            return result;
        }

        /// <summary>Parses an IP-geolocation response. Both spellings are accepted so the
        /// provider can be swapped without touching this: latitude/longitude/country_code
        /// (ipapi.co, ipwho.is) and lat/lon/countryCode (ip-api.com style).</summary>
        public static GeoLoc ParseIpLocation(string json)
        {
            var d = DeskMate.Core.Json.Parse(json) as Dictionary<string, object>;
            if (d == null) return null;
            var g = new GeoLoc();
            // ipapi.co: latitude/longitude/city/country_code — ip-api.com: lat/lon/city/countryCode
            g.Lat = DeskMate.Core.Json.GetNum(d, "latitude", DeskMate.Core.Json.GetNum(d, "lat", double.NaN));
            g.Lon = DeskMate.Core.Json.GetNum(d, "longitude", DeskMate.Core.Json.GetNum(d, "lon", double.NaN));
            g.City = DeskMate.Core.Json.GetStr(d, "city", "");
            g.Country = DeskMate.Core.Json.GetStr(d, "country_code", DeskMate.Core.Json.GetStr(d, "countryCode", ""));
            if (double.IsNaN(g.Lat) || double.IsNaN(g.Lon)) return null;
            return g;
        }

        /// <summary>WMO weather code → (Arabic, English) description.</summary>
        public static void WmoText(int code, out string ar, out string en)
        {
            if (code == 0) { ar = "صافٍ"; en = "Clear"; return; }
            if (code == 1) { ar = "صافٍ غالباً"; en = "Mostly clear"; return; }
            if (code == 2) { ar = "غائم جزئياً"; en = "Partly cloudy"; return; }
            if (code == 3) { ar = "غائم"; en = "Overcast"; return; }
            if (code == 45 || code == 48) { ar = "ضباب"; en = "Fog"; return; }
            if (code >= 51 && code <= 57) { ar = "رذاذ"; en = "Drizzle"; return; }
            if ((code >= 61 && code <= 67) || code == 80 || code == 81 || code == 82) { ar = "أمطار"; en = "Rain"; return; }
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) { ar = "ثلوج"; en = "Snow"; return; }
            if (code >= 95) { ar = "عاصفة رعدية"; en = "Thunderstorm"; return; }
            ar = "غير معروف"; en = "Unknown";
        }

        /// <summary>WMO code → glyph char from Segoe UI Symbol (monochrome, always available).</summary>
        public static string WmoGlyph(int code, bool night = false)
        {
            if (code <= 1) return night ? "☽" : "☀";      // ☽ / ☀
            if (code == 2) return "⛅";                          // ⛅
            if (code == 3) return "☁";                          // ☁
            if (code == 45 || code == 48) return "▒";           // ▒ fog-ish
            if (code >= 51 && code <= 67) return "☂";           // ☂
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return "❄"; // ❄
            if (code >= 80 && code <= 82) return "☂";           // ☂
            if (code >= 95) return "⚡";                         // ⚡
            return "☀";
        }
    }
}
