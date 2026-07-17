using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeskMate.Core;
using DeskMate.Logic;

static class SelfTest
{
    static int _fail;
    static int _pass;

    static void Check(bool cond, string name)
    {
        if (cond) { _pass++; }
        else { _fail++; Console.WriteLine("FAIL: " + name); }
    }

    static void CheckNear(double actual, double expected, double tol, string name)
    {
        Check(!double.IsNaN(actual) && Math.Abs(actual - expected) <= tol,
              name + " (got " + actual + ", want " + expected + " ±" + tol + ")");
    }

    static void CheckRange(double actual, double lo, double hi, string name)
    {
        Check(!double.IsNaN(actual) && actual >= lo && actual <= hi,
              name + " (got " + actual + ", want [" + lo + "," + hi + "])");
    }

    static int Main()
    {
        TestJson();
        TestSettings();
        TestEvaluator();
        TestPrayer();
        TestCleaner();
        TestAppScanner();
        TestWeather();
        Console.WriteLine();
        Console.WriteLine("Passed: " + _pass + "   Failed: " + _fail);
        return _fail == 0 ? 0 : 1;
    }

    static void TestJson()
    {
        var obj = Json.Parse("{\"a\":1.5,\"b\":\"نص عربي\",\"c\":[true,null,\"x\\n\\\"y\"],\"d\":{\"e\":-2e3}}")
                  as Dictionary<string, object>;
        Check(obj != null, "json parse object");
        Check(Json.GetNum(obj, "a", 0) == 1.5, "json num");
        Check(Json.GetStr(obj, "b", "") == "نص عربي", "json arabic string");
        var c = Json.GetList(obj, "c");
        Check(c.Count == 3 && (bool)c[0] && c[1] == null && (string)c[2] == "x\n\"y", "json array + escapes");
        Check(Json.GetNum(Json.GetObj(obj, "d"), "e", 0) == -2000, "json nested + exp");
        // roundtrip
        string written = Json.Write(obj);
        var re = Json.Parse(written) as Dictionary<string, object>;
        Check(Json.GetStr(re, "b", "") == "نص عربي", "json roundtrip");
        Check((string)Json.GetList(re, "c")[2] == "x\n\"y", "json roundtrip escapes");
        // unicode escape
        Check((string)Json.Parse("\"\\u0641\"") == "ف", "json \\u escape");
        bool threw = false;
        try { Json.Parse("{\"a\":}"); } catch { threw = true; }
        Check(threw, "json invalid throws");
    }

    static void TestSettings()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "deskmate_test_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Settings.OverridePath = tmp;
            Settings.I = new Settings();
            Settings.I.Lang = "en";
            Settings.I.ClockStyle = 1;
            Settings.I.Lat = 30.05;
            Settings.I.PinnedClips.Add("نص مثبت");
            Settings.I.Save();
            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.Lang == "en", "settings lang persisted");
            Check(Settings.I.ClockStyle == 1, "settings clock persisted");
            Check(Math.Abs(Settings.I.Lat - 30.05) < 1e-9, "settings lat persisted");
            Check(Settings.I.PinnedClips.Count == 1 && Settings.I.PinnedClips[0] == "نص مثبت", "settings pinned persisted");
            Check(Settings.I.ShowCleaner && Settings.I.PrayerNotify, "settings defaults kept");
        }
        finally
        {
            Settings.OverridePath = null;
            Settings.I = new Settings();
            try { File.Delete(tmp); } catch { }
        }
    }

    static void TestEvaluator()
    {
        CheckNear(ExpressionEvaluator.Eval("2+3*4"), 14, 1e-12, "precedence");
        CheckNear(ExpressionEvaluator.Eval("(2+3)*4"), 20, 1e-12, "parens");
        CheckNear(ExpressionEvaluator.Eval("10/4"), 2.5, 1e-12, "division");
        CheckNear(ExpressionEvaluator.Eval("2^3^2"), 512, 1e-9, "power right-assoc");
        CheckNear(ExpressionEvaluator.Eval("-3+5"), 2, 1e-12, "unary minus");
        CheckNear(ExpressionEvaluator.Eval("-(2^2)"), -4, 1e-12, "unary before paren");
        CheckNear(ExpressionEvaluator.Eval("10%3"), 1, 1e-12, "modulo");
        CheckNear(ExpressionEvaluator.Eval("sin(30)"), 0.5, 1e-9, "sin degrees");
        CheckNear(ExpressionEvaluator.Eval("cos(60)"), 0.5, 1e-9, "cos degrees");
        CheckNear(ExpressionEvaluator.Eval("sqrt(16)"), 4, 1e-12, "sqrt");
        CheckNear(ExpressionEvaluator.Eval("log(1000)"), 3, 1e-9, "log10");
        CheckNear(ExpressionEvaluator.Eval("ln(e)"), 1, 1e-9, "ln e");
        CheckNear(ExpressionEvaluator.Eval("2*pi"), 2 * Math.PI, 1e-9, "pi const");
        CheckNear(ExpressionEvaluator.Eval("abs(-7.5)"), 7.5, 1e-12, "abs");
        CheckNear(ExpressionEvaluator.Eval("3×4÷2"), 6, 1e-12, "unicode operators");
        CheckNear(ExpressionEvaluator.Eval("atan(1)"), 45, 1e-9, "atan degrees");
        foreach (string bad in new[] { "2+", "(2", "2)*3", "foo(2)", "1/0", "", "2**3" })
        {
            bool threw = false;
            try { ExpressionEvaluator.Eval(bad); } catch { threw = true; }
            Check(threw, "evaluator rejects '" + bad + "'");
        }
    }

    static void TestPrayer()
    {
        // Cairo, 2026-07-17, UTC+3 (Egypt summer time), Egyptian method.
        var t = PrayerCalc.Times(new DateTime(2026, 7, 17), 30.05, 31.25, 3.0, PrayerCalc.Method.Egyptian);
        CheckRange(t[0], 4.0, 4.7, "Cairo Fajr");
        CheckRange(t[1], 5.8, 6.3, "Cairo Sunrise");
        CheckRange(t[2], 12.9, 13.15, "Cairo Dhuhr");
        CheckRange(t[3], 16.2, 17.1, "Cairo Asr");
        CheckRange(t[4], 19.5, 20.2, "Cairo Maghrib");
        CheckRange(t[5], 21.0, 21.9, "Cairo Isha");
        // ordering
        for (int i = 1; i < 6; i++) Check(t[i] > t[i - 1], "Cairo order " + i);

        // Makkah, Umm al-Qura: Isha is exactly Maghrib + 1.5h
        var m = PrayerCalc.Times(new DateTime(2026, 1, 10), 21.4225, 39.8262, 3.0, PrayerCalc.Method.UmmAlQura);
        CheckNear(m[5] - m[4], 1.5, 1e-9, "UmmAlQura fixed isha");
        CheckRange(m[2], 12.2, 12.6, "Makkah Dhuhr Jan");

        Check(PrayerCalc.AutoMethod("EG") == PrayerCalc.Method.Egyptian, "auto method EG");
        Check(PrayerCalc.AutoMethod("SA") == PrayerCalc.Method.UmmAlQura, "auto method SA");
        Check(PrayerCalc.AutoMethod("US") == PrayerCalc.Method.ISNA, "auto method US");
        Check(PrayerCalc.AutoMethod("") == PrayerCalc.Method.MWL, "auto method default");

        Check(PrayerCalc.FormatTime(13.5083, false).StartsWith("1:3"), "format 12h");
        Check(PrayerCalc.FormatTime(13.5, true) == "13:30", "format 24h");
        Check(PrayerCalc.FormatTime(double.NaN, true) == "--:--", "format NaN");
        Check(PrayerCalc.FormatTime(0.0, false) == "12:00 AM", "format midnight");
    }

    static void TestCleaner()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "deskmate_clean_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);
        try
        {
            File.WriteAllBytes(Path.Combine(sandbox, "a.tmp"), new byte[100]);
            Directory.CreateDirectory(Path.Combine(sandbox, "sub"));
            File.WriteAllBytes(Path.Combine(sandbox, "sub", "b.tmp"), new byte[250]);
            long freed = CleanEngine.Clean(new[] { sandbox });
            Check(freed == 350, "cleaner freed bytes (got " + freed + ")");
            Check(Directory.Exists(sandbox), "cleaner keeps root");
            Check(Directory.GetFileSystemEntries(sandbox).Length == 0, "cleaner empties root");
            Check(CleanEngine.FormatBytes(1536) == "1.5 KB", "format KB");
            Check(CleanEngine.FormatBytes(3 * 1024 * 1024) == "3 MB", "format MB");
            Check(CleanEngine.DefaultDirs().Any(), "default dirs nonempty");
        }
        finally { try { Directory.Delete(sandbox, true); } catch { } }
    }

    static void TestAppScanner()
    {
        string sandbox = Path.Combine(Path.GetTempPath(), "deskmate_apps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(sandbox, "sub"));
        try
        {
            File.WriteAllText(Path.Combine(sandbox, "Zeta App.lnk"), "x");
            File.WriteAllText(Path.Combine(sandbox, "Uninstall Zeta.lnk"), "x");
            File.WriteAllText(Path.Combine(sandbox, "sub", "Alpha Tool.lnk"), "x");
            File.WriteAllText(Path.Combine(sandbox, "notes.txt"), "x");
            var apps = AppScanner.Scan(sandbox);
            Check(apps.Count == 2, "scanner count (got " + apps.Count + ")");
            Check(apps[0].Name == "Alpha Tool" && apps[1].Name == "Zeta App", "scanner sorted + filtered");
        }
        finally { try { Directory.Delete(sandbox, true); } catch { } }
    }

    static void TestWeather()
    {
        string url = WeatherCore.BuildForecastUrl(30.05, 31.25);
        Check(url.Contains("latitude=30.05") && url.Contains("longitude=31.25") && url.Contains("forecast_days=3"), "forecast url");
        Check(WeatherCore.BuildGeocodeUrl("القاهرة", "ar").Contains("language=ar"), "geocode url");

        string canned = "{\"current\":{\"temperature_2m\":35.5,\"relative_humidity_2m\":40,\"weather_code\":1,\"wind_speed_10m\":12.5}," +
            "\"daily\":{\"time\":[\"2026-07-17\",\"2026-07-18\",\"2026-07-19\"]," +
            "\"temperature_2m_max\":[36,37,38],\"temperature_2m_min\":[24,25,26],\"weather_code\":[0,2,3]}}";
        var w = WeatherCore.ParseForecast(canned);
        Check(Math.Abs(w.Temp - 35.5) < 1e-9 && w.Code == 1 && Math.Abs(w.Humidity - 40) < 1e-9, "parse current");
        Check(w.Days.Count == 3 && w.Days[1].TMax == 37 && w.Days[2].Code == 3 && w.Days[0].Date == "2026-07-17", "parse daily");

        var g1 = WeatherCore.ParseIpLocation("{\"latitude\":30.1,\"longitude\":31.2,\"city\":\"Cairo\",\"country_code\":\"EG\"}");
        Check(g1 != null && g1.Country == "EG" && Math.Abs(g1.Lat - 30.1) < 1e-9, "ipapi.co format");
        var g2 = WeatherCore.ParseIpLocation("{\"lat\":21.4,\"lon\":39.8,\"city\":\"Mecca\",\"countryCode\":\"SA\"}");
        Check(g2 != null && g2.Country == "SA" && Math.Abs(g2.Lon - 39.8) < 1e-9, "ip-api.com format");
        Check(WeatherCore.ParseIpLocation("{\"city\":\"x\"}") == null, "ip location missing coords");

        string ar, en;
        WeatherCore.WmoText(0, out ar, out en);
        Check(ar == "صافٍ" && en == "Clear", "wmo clear");
        WeatherCore.WmoText(95, out ar, out en);
        Check(en == "Thunderstorm", "wmo storm");
        Check(WeatherCore.WmoGlyph(0) == "☀" && WeatherCore.WmoGlyph(3) == "☁" && WeatherCore.WmoGlyph(63) == "☂", "wmo glyphs");

        var geo = WeatherCore.ParseGeocode("{\"results\":[{\"latitude\":30,\"longitude\":31,\"name\":\"القاهرة\",\"country_code\":\"EG\",\"admin1\":\"القاهرة\"}]}");
        Check(geo.Count == 1 && geo[0].Country == "EG" && geo[0].City == "القاهرة", "geocode parse");
    }
}
