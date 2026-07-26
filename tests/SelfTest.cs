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
        TestAutoTheme();
        TestFocus();
        TestOccasions();
        TestWorldClocks();
        TestChime();
        TestCpuSampler();
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

    // ---------------- v1.3 additions ----------------

    static void TestAutoTheme()
    {
        Check(AutoThemeCore.EffectiveIndex(2, false, 10) == 2, "auto theme off keeps chosen");
        Check(AutoThemeCore.EffectiveIndex(2, true, 10) == 4, "auto theme day → light");
        Check(AutoThemeCore.EffectiveIndex(2, true, 22) == 2, "auto theme night → chosen");
        Check(AutoThemeCore.EffectiveIndex(1, true, 6) == 4, "auto theme boundary 06:00");
        Check(AutoThemeCore.EffectiveIndex(1, true, 17) == 4, "auto theme boundary 17:59");
        Check(AutoThemeCore.EffectiveIndex(1, true, 18) == 1, "auto theme boundary 18:00");
    }

    static void TestFocus()
    {
        var f = new FocusCore { WorkMin = 25, BreakMin = 5 };
        var t0 = new DateTime(2026, 7, 26, 10, 0, 0);
        f.Start(FocusCore.Phase.Work, t0);
        Check(f.State == FocusCore.Phase.Work && !f.Paused, "focus starts running");
        CheckNear(f.Remaining(t0).TotalMinutes, 25, 0.01, "focus initial remaining");
        CheckNear(f.Remaining(t0.AddMinutes(10)).TotalMinutes, 15, 0.01, "focus remaining after 10m");
        CheckNear(f.Progress(t0.AddMinutes(10)), 0.4, 0.01, "focus progress 40%");

        f.TogglePause(t0.AddMinutes(10));
        Check(f.Paused, "focus pauses");
        CheckNear(f.Remaining(t0.AddMinutes(15)).TotalMinutes, 15, 0.01, "focus frozen while paused");
        f.TogglePause(t0.AddMinutes(20));
        Check(!f.Paused, "focus resumes");
        CheckNear(f.Remaining(t0.AddMinutes(22)).TotalMinutes, 13, 0.01, "focus remaining after resume");

        // paused 10 min between t0+10 and t0+20 → work ends at t0+35
        Check(!f.Tick(t0.AddMinutes(30)), "focus no early completion");
        Check(f.Tick(t0.AddMinutes(40)), "focus completes at end");
        Check(!f.Tick(t0.AddMinutes(41)), "focus completion fires once");

        f.Start(FocusCore.Phase.Break, t0.AddMinutes(50));
        Check(f.State == FocusCore.Phase.Break, "focus switches to break");
        CheckNear(f.Remaining(t0.AddMinutes(50)).TotalMinutes, 5, 0.01, "break length");
        f.Reset();
        Check(f.State == FocusCore.Phase.Idle && f.Remaining(t0) == TimeSpan.Zero, "focus reset");

        Check(FocusCore.Format(TimeSpan.FromSeconds(61)) == "01:01", "focus format 61s");
        Check(FocusCore.Format(TimeSpan.FromMinutes(25)) == "25:00", "focus format 25m");
        Check(FocusCore.Format(TimeSpan.Zero) == "00:00", "focus format zero");
    }

    static void TestOccasions()
    {
        var today = new DateTime(2026, 7, 26);
        // Ramadan 1447 ≈ Feb/Mar 2026 → next is Ramadan 1448 ≈ Feb 2027 (± a few days by calendar rules)
        var ram = OccasionCore.NextHijri(9, 1, today);
        Check(ram != null && ram.Value > today, "ramadan is in the future");
        Check(ram != null && ram.Value.Year >= 2027 && ram.Value.Year <= 2028, "ramadan rolls to next hijri year");
        // roundtrip: the returned gregorian date maps back to 1 Ramadan
        var cal = new System.Globalization.UmAlQuraCalendar();
        Check(cal.GetMonth(ram.Value) == 9 && cal.GetDayOfMonth(ram.Value) == 1, "ramadan roundtrip");
        // days-left math
        Check(OccasionCore.DaysLeft(today, today) == 0, "days left today");
        Check(OccasionCore.DaysLeft(today, today.AddDays(5)) == 5, "days left +5");
        Check(OccasionCore.DaysLeft(today, today.AddDays(-3)) == 0, "days left clamps past");
        // gregorian custom date rolls over
        var ny = OccasionCore.NextGregorian(1, 1, today);
        Check(ny != null && ny.Value == new DateTime(2027, 1, 1), "gregorian rolls to next year");
        var same = OccasionCore.NextGregorian(7, 26, today);
        Check(same != null && same.Value == today, "gregorian same day stays");
    }

    static void TestWorldClocks()
    {
        var utc = new DateTime(2026, 7, 26, 12, 0, 0);
        var makkah = WorldClockCore.TimeIn("Arab Standard Time", utc); // UTC+3, no DST
        Check(makkah != null && makkah.Value.Hour == 15, "makkah = utc+3");
        var tokyo = WorldClockCore.TimeIn("Tokyo Standard Time", utc); // UTC+9, no DST
        Check(tokyo != null && tokyo.Value.Hour == 21, "tokyo = utc+9");
        Check(WorldClockCore.TimeIn("No Such Zone", utc) == null, "unknown zone → null");
        Check(WorldClockCore.Find("Tokyo Standard Time") != null, "city lookup");
        Check(WorldClockCore.Find("nope") == null, "city lookup miss");
        // day offset: local machine = UTC+3 at 05:30 (02:30 UTC) → Tokyo 11:30 same day,
        // Pacific 19:30 the previous day
        var local = new DateTime(2026, 7, 26, 5, 30, 0);
        var utc2 = new DateTime(2026, 7, 26, 2, 30, 0);
        Check(WorldClockCore.DayDiff("Tokyo Standard Time", utc2, local) == 0, "tokyo same day");
        Check(WorldClockCore.DayDiff("Pacific Standard Time", utc2, local) == -1, "pacific still yesterday");
        Check(WorldClockCore.DayDiff("Arab Standard Time", utc2, local) == 0, "same-day zone");
    }

    static void TestChime()
    {
        var wav = ChimeWave.GenerateSoftChime();
        Check(wav.Length > 44 + 22050 * 2, "chime has >1s of audio");
        Check(wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F', "chime RIFF header");
        Check(wav[8] == 'W' && wav[9] == 'A' && wav[10] == 'V' && wav[11] == 'E', "chime WAVE header");
        bool audible = false;
        for (int i = 44; i + 1 < wav.Length; i += 2)
            if (wav[i] != 0 || wav[i + 1] != 0) { audible = true; break; }
        Check(audible, "chime is not silent");
    }

    static void TestCpuSampler()
    {
        CheckNear(CpuSampler.Fraction(100, 60, 40), 0, 1e-9, "cpu all idle");
        CheckNear(CpuSampler.Fraction(0, 60, 40), 1, 1e-9, "cpu fully busy");
        CheckNear(CpuSampler.Fraction(50, 60, 40), 0.5, 1e-9, "cpu half busy");
        CheckNear(CpuSampler.Fraction(0, 0, 0), 0, 1e-9, "cpu zero delta safe");
        CheckNear(CpuSampler.Percent(25, 60, 40), 75, 0.2, "cpu percent");
    }
}
