using System;
using System.Collections.Generic;
using System.Drawing;
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
        TestSecret();
        TestSettings();
        TestSettingsAllFields();
        TestEvaluator();
        TestPrayer();
        TestPrayerAdjust();
        TestNotes();
        TestCleaner();
        TestCleanerJunctionSafety();
        TestCleanerFileLinkSafety();
        TestAppScanner();
        TestWeather();
        TestAutoTheme();
        TestMaterialYou();
        TestV17Additions();
        TestFocus();
        TestOccasions();
        TestWorldClocks();
        TestChime();
        TestCpuSampler();
        TestCommands();
        TestUnitConvert();
        TestPercentPhrase();
        TestOccasionsSeasonal();
        TestMoonPhase();
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

        // Regression: unbounded recursion raised StackOverflowException, which .NET cannot
        // catch — the process died. Reachable from settings.json and from remote responses.
        threw = false;
        try { Json.Parse(new string('[', 50000) + new string(']', 50000)); }
        catch (FormatException) { threw = true; }
        Check(threw, "json deep nesting rejected, not fatal");

        // the limit must not reject legitimately shaped data
        int ok = Json.MaxDepth - 2;
        object shallow = null;
        bool parsed = true;
        try { shallow = Json.Parse(new string('[', ok) + new string(']', ok)); }
        catch { parsed = false; }
        Check(parsed && shallow != null, "json accepts depth below the limit");

        threw = false;
        try { Json.Parse("{"); } catch (FormatException) { threw = true; }
        Check(threw, "json truncated object throws FormatException");
    }

    /// <summary>DPAPI round-trip for the settings values that can hold personal text.</summary>
    static void TestSecret()
    {
        string plain = "كلمة سر: hunter2 \n line2";
        string sealed_ = Secret.Protect(plain);
        Check(sealed_ != plain, "secret is transformed");
        Check(!sealed_.Contains("hunter2"), "secret does not leak plaintext");
        Check(Secret.Unprotect(sealed_) == plain, "secret round-trips");
        // legacy plaintext (written by earlier versions) must survive untouched
        Check(Secret.Unprotect("just plain text") == "just plain text", "secret passes through legacy plaintext");
        Check(Secret.Protect("") == "", "secret ignores empty");
        Check(Secret.Unprotect(null) == null, "secret ignores null");

        // An undecryptable blob (foreign profile / restored backup / reset master key) must
        // survive verbatim. Returning "" here used to be written straight back by the next
        // Save, permanently destroying the note and every pinned clip.
        const string foreign = "enc:v1:not-base64!!";
        Check(Secret.Unprotect(foreign) == foreign, "secret keeps undecryptable blob");
        Check(Secret.IsSealed(foreign), "secret reports sealed");
        Check(Secret.Protect(Secret.Unprotect(foreign)) == foreign,
              "secret survives a full load/save cycle it cannot decrypt");
        Check(!Secret.IsSealed(plain) && !Secret.IsSealed("") && !Secret.IsSealed(null),
              "secret does not report plaintext as sealed");

        // and the whole point: readable values are still readable after the same cycle
        Check(Secret.Unprotect(Secret.Protect(Secret.Unprotect(sealed_))) == plain,
              "secret round-trips through load/save");
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
            Settings.I.StickyNote = "my-secret-note";
            Settings.I.Save();

            // sensitive fields must not be readable in the file itself
            string onDisk = File.ReadAllText(tmp);
            Check(!onDisk.Contains("my-secret-note"), "sticky note encrypted on disk");
            Check(!onDisk.Contains("نص مثبت"), "pinned clip encrypted on disk");
            Check(onDisk.Contains("\"Lang\":\"en\""), "non-sensitive fields stay readable");
            Check(!Directory.EnumerateFiles(Path.GetDirectoryName(tmp),
                      Path.GetFileName(tmp) + ".*.tmp").Any(), "no temp file left behind");

            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.Lang == "en", "settings lang persisted");
            Check(Settings.I.ClockStyle == 1, "settings clock persisted");
            Check(Math.Abs(Settings.I.Lat - 30.05) < 1e-9, "settings lat persisted");
            Check(Settings.I.PinnedClips.Count == 1 && Settings.I.PinnedClips[0] == "نص مثبت", "settings pinned persisted");
            Check(Settings.I.StickyNote == "my-secret-note", "settings note round-trips");
            Check(Settings.I.ShowCleaner && Settings.I.PrayerNotify, "settings defaults kept");

            // a settings file written by an older, unencrypted build must still load
            File.WriteAllText(tmp, "{\"Lang\":\"ar\",\"StickyNote\":\"legacy plain\",\"PinnedClips\":[\"old clip\"]}");
            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.StickyNote == "legacy plain", "legacy plaintext note loads");
            Check(Settings.I.PinnedClips.Count == 1 && Settings.I.PinnedClips[0] == "old clip", "legacy pinned clip loads");

            // A settings file carried over from another Windows profile: the app cannot read
            // the note or the pins, and must not erase them either. Before the fix, this very
            // sequence (load → save) wiped both permanently.
            const string foreignNote = "enc:v1:AAAAforeign-note-blob";
            const string foreignClip = "enc:v1:AAAAforeign-clip-blob";
            File.WriteAllText(tmp, "{\"Lang\":\"ar\",\"StickyNote\":\"" + foreignNote +
                                   "\",\"PinnedClips\":[\"" + foreignClip + "\"]}");
            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.StickyNote == foreignNote, "foreign note kept in memory");
            Check(Settings.I.PinnedClips.Count == 1 && Settings.I.PinnedClips[0] == foreignClip,
                  "foreign pinned clip kept in memory");
            Settings.I.Save();
            string after = File.ReadAllText(tmp);
            Check(after.Contains(foreignNote), "foreign note survives a save");
            Check(after.Contains(foreignClip), "foreign pinned clip survives a save");
        }
        finally
        {
            Settings.OverridePath = null;
            Settings.I = new Settings();
            try { File.Delete(tmp); } catch { }
        }
    }

    /// <summary>Every persisted field, set to a non-default value and read back. This is the
    /// safety net for any change to how settings are serialised: a field dropped from either
    /// the read or the write side shows up here instead of silently resetting for users.</summary>
    static void TestSettingsAllFields()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "deskmate_all_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Settings.OverridePath = tmp;
            var s = new Settings
            {
                Lang = "en", ClockStyle = 2, Use24h = true, ShowSeconds = true, ShowHijri = false,
                ShowNextPrayerLine = false, AutoStart = false, PosX = 123.5, PosY = 456.25,
                ThemeIndex = 3, AutoTheme = true, LayoutIndex = 5, Scale = 1.25, BgOpacity = 0.55,
                ShowCleaner = false, ShowCalc = false, ShowWallpaper = false, ShowPower = false,
                ShowClipboard = false, ShowWeather = false, ShowApps = false, ShowPrayer = false,
                ShowFocus = false, ShowOccasions = false, ShowNote = true, ShowSysMon = true,
                ShowWorldClock = true,
                AutoLocation = false, Lat = -33.8688, Lon = 151.2093, City = "سيدني", Country = "AU",
                PrayerMethod = 3, PrayerNotify = false, PrayerPreAlertMin = 30,
                ClipboardMax = 40,
                FocusDoneDate = "20260801", FocusDoneCount = 7,
                AdhanSound = 2, AdhanPath = @"C:\sounds\adhan.wav",
                StickyNote = "ملاحظة\nسطران", OccasionCustom = "2026-12-31",
                OccasionCustomName = "عيد ميلاد", OccasionCustomYearly = true,
            };
            s.PinnedClips.Add("clip one");
            s.PinnedClips.Add("مقطع اثنان");
            s.WorldClocks.Add("Egypt Standard Time");
            s.WorldClocks.Add("Tokyo Standard Time");
            Settings.I = s;
            Settings.I.Save();

            Settings.I = new Settings();
            Settings.Load();
            var r = Settings.I;

            Check(r.Lang == "en" && r.ClockStyle == 2 && r.Use24h && r.ShowSeconds, "all: general block");
            Check(!r.ShowHijri && !r.ShowNextPrayerLine && !r.AutoStart, "all: general toggles");
            Check(Math.Abs(r.PosX - 123.5) < 1e-9 && Math.Abs(r.PosY - 456.25) < 1e-9, "all: position");
            Check(r.ThemeIndex == 3 && r.AutoTheme && r.LayoutIndex == 5, "all: theme + layout");
            Check(Math.Abs(r.Scale - 1.25) < 1e-9 && Math.Abs(r.BgOpacity - 0.55) < 1e-9, "all: scale + opacity");
            Check(!r.ShowCleaner && !r.ShowCalc && !r.ShowWallpaper && !r.ShowPower &&
                  !r.ShowClipboard && !r.ShowWeather && !r.ShowApps && !r.ShowPrayer &&
                  !r.ShowFocus && !r.ShowOccasions, "all: tool visibility off");
            Check(r.ShowNote && r.ShowSysMon && r.ShowWorldClock, "all: tool visibility on");
            Check(!r.AutoLocation && Math.Abs(r.Lat + 33.8688) < 1e-9 &&
                  Math.Abs(r.Lon - 151.2093) < 1e-9 && r.City == "سيدني" && r.Country == "AU", "all: location");
            Check(r.PrayerMethod == 3 && !r.PrayerNotify && r.PrayerPreAlertMin == 30, "all: prayer");
            Check(r.ClipboardMax == 40 && r.PinnedClips.Count == 2 &&
                  r.PinnedClips[0] == "clip one" && r.PinnedClips[1] == "مقطع اثنان", "all: clipboard");
            Check(r.FocusDoneDate == "20260801" && r.FocusDoneCount == 7, "all: focus");
            Check(r.AdhanSound == 2 && r.AdhanPath == @"C:\sounds\adhan.wav", "all: sound");
            Check(r.StickyNote == "ملاحظة\nسطران" && r.OccasionCustom == "2026-12-31", "all: note + occasion");
            Check(r.OccasionCustomName == "عيد ميلاد" && r.OccasionCustomYearly, "all: occasion name + yearly");
            Check(r.WorldClocks.Count == 2 && r.WorldClocks[0] == "Egypt Standard Time" &&
                  r.WorldClocks[1] == "Tokyo Standard Time", "all: world clocks");

            // out-of-range values in a hand-edited or corrupted file fall back to defaults
            File.WriteAllText(tmp, "{\"LayoutIndex\":99,\"Scale\":50,\"BgOpacity\":-3,\"AdhanSound\":7}");
            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.LayoutIndex == 0, "clamp: layout index");
            Check(Math.Abs(Settings.I.Scale - 1.0) < 1e-9, "clamp: scale");
            Check(Math.Abs(Settings.I.BgOpacity - 1.0) < 1e-9, "clamp: opacity");
            Check(Settings.I.AdhanSound == 0, "clamp: adhan sound");

            // a corrupt file must not throw and must not lose the user's data silently
            File.WriteAllText(tmp, "{not json at all");
            Settings.I = new Settings();
            Settings.Load();
            Check(Settings.I.Lang == "ar", "corrupt file falls back to defaults");
        }
        finally
        {
            Settings.OverridePath = null;
            Settings.I = new Settings();
            try { File.Delete(tmp); } catch { }
            try { File.Delete(tmp + ".bak"); } catch { }
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

    static void TestPrayerAdjust()
    {
        var baseT = new double[] { 4.5, 6.0, 12.25, 15.5, 18.75, 20.0 };

        var none = PrayerCalc.ApplyAdjust(baseT, new int[6]);
        for (int i = 0; i < 6; i++) Check(Math.Abs(none[i] - baseT[i]) < 1e-9, "adjust 0 is identity " + i);

        var moved = PrayerCalc.ApplyAdjust(baseT, new[] { -5, 0, 3, 0, 0, 12 });
        Check(Math.Abs(moved[0] - (4.5 - 5 / 60.0)) < 1e-9, "fajr 5 min earlier");
        Check(Math.Abs(moved[2] - (12.25 + 3 / 60.0)) < 1e-9, "dhuhr 3 min later");
        Check(Math.Abs(moved[5] - (20.0 + 12 / 60.0)) < 1e-9, "isha 12 min later");
        Check(Math.Abs(moved[1] - 6.0) < 1e-9, "untouched prayer unchanged");
        Check(Math.Abs(baseT[0] - 4.5) < 1e-9, "source array not mutated");

        // wrap: a correction across midnight must stay a valid hour-of-day, never 24.1 or -0.2
        var late = PrayerCalc.ApplyAdjust(new[] { 23.9 }, new[] { 30 });
        Check(late[0] >= 0 && late[0] < 24, "wraps past midnight (got " + late[0] + ")");
        Check(Math.Abs(late[0] - 0.4) < 1e-9, "wrapped value correct");
        var early = PrayerCalc.ApplyAdjust(new[] { 0.1 }, new[] { -30 });
        Check(early[0] >= 0 && early[0] < 24, "wraps before midnight (got " + early[0] + ")");
        Check(Math.Abs(early[0] - 23.6) < 1e-9, "wrapped-back value correct");

        // polar days give NaN times; a correction must not turn that into a real-looking hour
        var polar = PrayerCalc.ApplyAdjust(new[] { double.NaN }, new[] { 10 });
        Check(double.IsNaN(polar[0]), "NaN stays NaN");

        // a shorter or missing correction array must not throw
        Check(PrayerCalc.ApplyAdjust(baseT, new[] { 5 })[0] > 0, "short adjust array tolerated");
        Check(PrayerCalc.ApplyAdjust(baseT, null)[3] == baseT[3], "null adjust tolerated");
        Check(PrayerCalc.ApplyAdjust(null, new int[6]) == null, "null times tolerated");
    }

    static void TestNotes()
    {
        var n = new NoteItem
        {
            Text = "اشتري خبزاً\nوحليب",
            Pinned = true,
            RemindAt = new DateTime(2026, 8, 5, 14, 30, 0),
        };
        var back = NoteItem.Parse(n.Write());
        Check(back != null, "note round-trips");
        Check(back.Text == n.Text, "note text survives newlines and Arabic");
        Check(back.Pinned, "note pin survives");
        Check(back.RemindAt == n.RemindAt, "note reminder survives");
        Check(!back.Done, "note defaults to not done");

        var doneNote = new NoteItem { Text = "finished", Done = true };
        Check(NoteItem.Parse(doneNote.Write()).Done, "note done flag round-trips");

        var doneStamped = new NoteItem { Text = "stamped", Done = true, DoneAt = new DateTime(2026, 8, 16, 7, 30, 0) };
        Check(NoteItem.Parse(doneStamped.Write()).DoneAt == doneStamped.DoneAt, "note done-at round-trips");
        Check(NoteItem.Parse(doneNote.Write()).DoneAt == DateTime.MinValue, "done without stamp stays unknown");

        // daily progress: done today counts, done yesterday falls off, open tasks count
        int dDone, dTotal;
        var day = new DateTime(2026, 8, 16);
        NoteItem.DailyProgress(new List<NoteItem>
        {
            new NoteItem { Text = "open" },
            new NoteItem { Text = "done today", Done = true, DoneAt = day.AddHours(9) },
            new NoteItem { Text = "done yesterday", Done = true, DoneAt = day.AddDays(-1) },
        }, day, out dDone, out dTotal);
        Check(dDone == 1 && dTotal == 2, "daily progress counts today only");

        var plain = NoteItem.Parse(new NoteItem { Text = "x" }.Write());
        Check(!plain.HasReminder, "note without reminder reports none");

        // garbage must vanish, not become a blank row the user cannot explain
        Check(NoteItem.Parse("not json") == null, "corrupt note dropped");
        Check(NoteItem.Parse("") == null, "empty note dropped");
        Check(NoteItem.Parse("{}") == null, "contentless note dropped");

        var a = new NoteItem { Text = "a" };
        var b = new NoteItem { Text = "b", Pinned = true };
        var c = new NoteItem { Text = "c" };
        var sorted = NoteItem.Sorted(new List<NoteItem> { a, b, c });
        Check(sorted.Count == 3 && sorted[0] == b, "pinned notes sort first");
        Check(sorted[1] == a && sorted[2] == c, "unpinned keep their order");
        Check(NoteItem.Sorted(new List<NoteItem> { null, a }).Count == 1, "null entries skipped");

        // completed tasks sink below everything, pinned or not — finishing must never hide
        // a task mid-list, and restoring it must bring it back where it was
        var d = new NoteItem { Text = "d", Done = true, Pinned = true };
        var sorted2 = NoteItem.Sorted(new List<NoteItem> { d, a, b, c });
        Check(sorted2[0] == b && sorted2[3] == d, "done tasks sink to the bottom");

        // a done task with a due reminder must not fire
        var t0 = new DateTime(2026, 8, 5, 15, 0, 0);
        var doneDue = new NoteItem { Text = "done but due", Done = true, RemindAt = t0.AddMinutes(-5) };
        Check(!NoteItem.DueIndices(new List<NoteItem> { doneDue }, t0).Contains(0),
              "done task reminders stay quiet");

        var now = new DateTime(2026, 8, 5, 15, 0, 0);
        var list = new List<NoteItem>
        {
            new NoteItem { Text = "future", RemindAt = now.AddMinutes(5) },
            new NoteItem { Text = "due",    RemindAt = now },
            new NoteItem { Text = "late",   RemindAt = now.AddMinutes(-90) },
            new NoteItem { Text = "none" },
        };
        var due = NoteItem.DueIndices(list, now);
        Check(due.Count == 2, "only due reminders fire (got " + due.Count + ")");
        Check(due.Contains(1) && due.Contains(2), "exact and overdue both fire");
        // an overdue reminder must still fire: the tick can be delayed under load, and
        // requiring an exact match would drop it silently forever
        Check(!due.Contains(0) && !due.Contains(3), "future and reminderless stay quiet");
        Check(NoteItem.DueIndices(null, now).Count == 0, "null list tolerated");

        Check(new NoteItem { Text = "one  two\r\nthree" }.Preview() == "one two three",
              "preview flattens whitespace");
        Check(new NoteItem { Text = new string('x', 200) }.Preview(10).Length == 11,
              "preview truncates with ellipsis");
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

    /// <summary>Regression: a junction planted inside a cleaned folder must never be followed.
    /// Windows\Temp is writable by every user, so this was a way to have the cleaner delete
    /// arbitrary data (Documents, a network share) on the victim's behalf.</summary>
    static void TestCleanerJunctionSafety()
    {
        string root = Path.Combine(Path.GetTempPath(), "deskmate_junc_" + Guid.NewGuid().ToString("N"));
        string cleanMe = Path.Combine(root, "temp");
        string victim = Path.Combine(root, "victim");
        Directory.CreateDirectory(cleanMe);
        Directory.CreateDirectory(victim);
        try
        {
            File.WriteAllBytes(Path.Combine(victim, "important.dat"), new byte[512]);
            File.WriteAllBytes(Path.Combine(cleanMe, "junk.tmp"), new byte[64]);

            string link = Path.Combine(cleanMe, "link");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = "/c mklink /J \"" + link + "\" \"" + victim + "\"",
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true,
            };
            using (var p = System.Diagnostics.Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(5000); }
            if (!Directory.Exists(link)) { Console.WriteLine("SKIP: junction test (mklink unavailable)"); return; }

            Check(CleanEngine.IsReparsePoint(link), "junction detected as reparse point");
            Check(!CleanEngine.IsReparsePoint(cleanMe), "plain directory is not a reparse point");

            long freed = CleanEngine.Clean(new[] { cleanMe });
            Check(File.Exists(Path.Combine(victim, "important.dat")), "junction target NOT deleted");
            Check(!Directory.Exists(link), "junction link itself removed");
            Check(freed == 64, "only real junk counted (got " + freed + ")");

            // and the whole tree is refused when the cleaned root is itself a link
            string rootLink = Path.Combine(root, "rootlink");
            psi.Arguments = "/c mklink /J \"" + rootLink + "\" \"" + victim + "\"";
            using (var p = System.Diagnostics.Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(5000); }
            if (Directory.Exists(rootLink))
            {
                CleanEngine.Clean(new[] { rootLink });
                Check(File.Exists(Path.Combine(victim, "important.dat")), "linked root refused");
            }
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    /// <summary>The junction defence covered directories only. GetFiles also returns file
    /// symlinks, and SetFileAttributes follows a reparse point — so clearing ReadOnly before
    /// deleting reached through the link and rewrote the attributes of the target, any file
    /// the user can write. The link must be unlinked without ever touching what it points at.
    /// </summary>
    static void TestCleanerFileLinkSafety()
    {
        string root = Path.Combine(Path.GetTempPath(), "deskmate_flink_" + Guid.NewGuid().ToString("N"));
        string cleanMe = Path.Combine(root, "temp");
        string victim = Path.Combine(root, "victim");
        Directory.CreateDirectory(cleanMe);
        Directory.CreateDirectory(victim);
        try
        {
            string target = Path.Combine(victim, "protected.dat");
            File.WriteAllBytes(target, new byte[256]);
            File.SetAttributes(target, FileAttributes.ReadOnly | FileAttributes.Hidden);
            File.WriteAllBytes(Path.Combine(cleanMe, "junk.tmp"), new byte[64]);

            string plain = Path.Combine(cleanMe, "junk.tmp");
            Check(!CleanEngine.IsFileReparsePoint(plain), "plain file is not a reparse point");
            Check(CleanEngine.IsFileReparsePoint(Path.Combine(cleanMe, "does-not-exist")),
                  "unreadable path treated as reparse point (fail closed)");

            string link = Path.Combine(cleanMe, "link.dat");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = "/c mklink \"" + link + "\" \"" + target + "\"",
                CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true,
            };
            using (var p = System.Diagnostics.Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(5000); }
            // file symlinks need admin or Developer Mode; the checks above still ran
            if (!File.Exists(link)) { Console.WriteLine("SKIP: file symlink test (needs Developer Mode)"); return; }

            Check(CleanEngine.IsFileReparsePoint(link), "file symlink detected as reparse point");

            CleanEngine.Clean(new[] { cleanMe });
            Check(File.Exists(target), "symlink target NOT deleted");
            var attrs = File.GetAttributes(target);
            Check((attrs & FileAttributes.ReadOnly) != 0, "symlink target keeps ReadOnly");
            Check((attrs & FileAttributes.Hidden) != 0, "symlink target keeps Hidden");
            Check(!File.Exists(link), "symlink itself removed");
        }
        finally
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                Directory.Delete(root, true);
            }
            catch { }
        }
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

        // Regression: a body without "current" (error page / truncation) rendered as
        // "0° and clear" from defaults instead of being refused
        bool threw = false;
        try { WeatherCore.ParseForecast("{}"); }
        catch (FormatException) { threw = true; }
        Check(threw, "forecast without current block rejected");

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
        // Regression: an out-of-range ThemeIndex made the effective index differ from the
        // applied one forever — Apply → Reload → tick looped until StackOverflow killed the app
        Check(AutoThemeCore.EffectiveIndex(99, false, 10) == 0, "out-of-range theme clamps to 0");
        Check(AutoThemeCore.EffectiveIndex(-3, true, 22) == 0, "negative theme clamps to 0");
        // Light index is user-pickable. The 4-arg overload takes the light index explicitly.
        Check(AutoThemeCore.EffectiveIndex(3, 4, true, 10) == 4, "auto theme day → chosen light (4)");
        Check(AutoThemeCore.EffectiveIndex(3, 7, true, 10) == 7, "auto theme day → chosen light (7)");
        Check(AutoThemeCore.EffectiveIndex(3, 99, true, 10) == 4, "out-of-range light falls back to 4");
        Check(AutoThemeCore.EffectiveIndex(3, 4, false, 10) == 3, "light ignored when auto off");
        // v1.7: the palette table grew to 12 (0..11) — the clamp must accept the new ones
        Check(AutoThemeCore.EffectiveIndex(8, false, 10) == 8, "violet (8) accepted");
        Check(AutoThemeCore.EffectiveIndex(11, 10, true, 22) == 11, "ruby (11) accepted at night");
        Check(AutoThemeCore.EffectiveIndex(11, 8, true, 10) == 8, "auto theme day → violet (8)");
        Check(AutoThemeCore.EffectiveIndex(12, false, 10) == 0, "index 12 clamps to 0");
        Check(AutoThemeCore.EffectiveIndex(-1, false, 10) == 0, "index -1 clamps to 0");
    }

    /// <summary>v1.7 additions: clock-style bounds and the panels-pinned setting
    /// round-trip. (Palette-table coverage lives in UiSmoke — SelfTest stays WPF-free.)</summary>
    static void TestV17Additions()
    {
        // settings bounds follow the 12-palette table and the 8 clock styles
        string tmp = Path.Combine(Path.GetTempPath(), "deskmate_v17_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Settings.OverridePath = tmp;
            var s = new Settings { ThemeIndex = 11, LightThemeIndex = 8, ClockStyle = 7, PanelsPinned = false };
            s.Save();
            Settings.Load();
            Check(Settings.I.ThemeIndex == 11, "theme 11 survives round-trip");
            Check(Settings.I.LightThemeIndex == 8, "light theme 8 survives round-trip");
            Check(Settings.I.ClockStyle == 7, "clock style 7 survives round-trip");
            Check(Settings.I.PanelsPinned == false, "panels-pinned=false survives round-trip");

            // out-of-range values fall back instead of looping Apply→Reload — tested through
            // the real pipeline: write a corrupt file, load it, read the clamped result
            var bad = new Settings { ThemeIndex = 99, LightThemeIndex = -5, ClockStyle = 42 };
            bad.Save();
            Settings.Load();
            Check(Settings.I.ThemeIndex == 0, "theme 99 clamps to 0 on load");
            Check(Settings.I.LightThemeIndex == 4, "light theme -5 clamps to 4 on load");
            Check(Settings.I.ClockStyle == 0, "clock 42 clamps to 0 on load");

            // default is pinned — the user asked for panels that stay open
            Check(new Settings().PanelsPinned, "panels pinned by default");
        }
        finally
        {
            Settings.OverridePath = null;
            try { File.Delete(tmp); } catch { }
        }
    }

    static void TestMaterialYou()
    {
        // 1) ExtractDominant on a uniformly-blue bitmap returns a near-blue colour
        using (var blue = new Bitmap(32, 32))
        {
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    blue.SetPixel(x, y, Color.FromArgb(0x10, 0x60, 0xE0));
            var dom = MaterialYouCore.ExtractDominant(blue);
            // blue channel should dominate; red should be well below it
            Check(dom.B > dom.R + 40, "blue wallpaper: blue channel dominates");
            Check(dom.B > 100, "blue wallpaper: blue channel reasonably high");
        }
        // 2) ExtractDominant on a near-black bitmap returns a safe value (no crash)
        using (var dark = new Bitmap(8, 8))
        {
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    dark.SetPixel(x, y, Color.FromArgb(2, 2, 2));
            var dom = MaterialYouCore.ExtractDominant(dark);
            Check(dom.R >= 0 && dom.G >= 0 && dom.B >= 0, "black wallpaper: returns non-negative");
        }
        // 3) BuildPalette for a dark accent lifts the accent and keeps the card dark
        var palDark = MaterialYouCore.BuildPalette(new MaterialYouCore.Rgb(0x10, 0x60, 0xE0));
        Check(palDark.Accent.R > 0x10 + 50, "BuildPalette: dark accent — red is lifted");
        Check(palDark.Accent.G > 0x60 + 50, "BuildPalette: dark accent — green is lifted");
        Check(palDark.CardTop.R < 0x40 && palDark.CardBottom.R < 0x40, "BuildPalette: dark card stays dark");
        Check(palDark.CardBottom.B > palDark.CardBottom.R, "BuildPalette: dark card keeps blue hue");
        // 4) BuildPalette for a light accent (white wallpaper) inverts: darkens the accent
        //    and produces a light card. This is the regression that produced white-on-white
        //    text on white wallpapers before v1.5.4.
        var palLight = MaterialYouCore.BuildPalette(new MaterialYouCore.Rgb(0xF8, 0xF2, 0xE8));
        Check(palLight.Accent.R < 0xF8, "BuildPalette: light wallpaper — accent is darkened");
        Check(palLight.CardBottom.R > 200, "BuildPalette: light wallpaper — card stays light");
        Check(palLight.CardTop.R >= palLight.CardBottom.R, "BuildPalette: light card gradient is top-light");
        // 5) Round-trip: extract from a green bitmap, build palette, green hue is preserved
        using (var green = new Bitmap(16, 16))
        {
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                    green.SetPixel(x, y, Color.FromArgb(0x30, 0xC0, 0x40));
            var dom = MaterialYouCore.ExtractDominant(green);
            var p = MaterialYouCore.BuildPalette(dom);
            Check(p.Accent.G > p.Accent.R, "green wallpaper: green survives lift");
        }
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

    static void TestCommands()
    {
        var now = new DateTime(2026, 8, 16, 10, 0, 0);   // Sunday 10:00

        var calc = CommandParse.Parse("= 2+3*4", now);
        Check(calc.Kind == CommandParse.Calc && calc.Text == "2+3*4", "command: calc prefix");

        var open = CommandParse.Parse("افتح كروم", now);
        Check(open.Kind == CommandParse.OpenApp && open.Text == "كروم", "command: open prefix");

        var task = CommandParse.Parse("ذكّرني بشراء الخبز غداً الساعة 9", now);
        Check(task.Kind == CommandParse.Task, "command: task prefix");
        Check(task.Text == "شراء الخبز", "command: time words stripped, got [" + task.Text + "]");
        Check(task.When == now.Date.AddDays(1).AddHours(9), "command: tomorrow 9 o'clock");

        var rel = CommandParse.Parse("ذكرني بالاتصال بعد 30 دقيقة", now);
        Check(rel.When == now.AddMinutes(30), "command: after 30 minutes");

        var tonight = CommandParse.Parse("مهمة مراجعة التقرير الساعة 21:30", now);
        Check(tonight.When == now.Date.AddHours(21).AddMinutes(30), "command: explicit clock time");

        // an hour that already passed today means tomorrow
        var past = CommandParse.Parse("مهمة سقي الزرع الساعة 8", now);
        Check(past.When == now.Date.AddDays(1).AddHours(8), "command: past hour rolls to tomorrow");

        // bare text is a reminderless task — the bar's default verb
        var bare = CommandParse.Parse("شراء حليب", now);
        Check(bare.Kind == CommandParse.Task && bare.Text == "شراء حليب" && bare.When == DateTime.MinValue,
              "command: bare text becomes a task");

        // Arabic-Indic digits parse the same as ASCII
        var indic = CommandParse.Parse("ذكرني بالدواء غداً الساعة ٩", now);
        Check(indic.When == now.Date.AddDays(1).AddHours(9), "command: Arabic-Indic digits");

        var empty = CommandParse.Parse("", now);
        Check(empty.Kind == CommandParse.Task && empty.Text == "", "command: empty input tolerated");
    }

    static void TestUnitConvert()
    {
        CheckNear(UnitConvert.Apply(0, 100), 62.1371, 1e-6, "km to mi");
        CheckNear(UnitConvert.Apply(1, 62.1371), 100, 1e-4, "mi to km round-trip");
        CheckNear(UnitConvert.Apply(6, 212), 100, 1e-9, "F to C");
        CheckNear(UnitConvert.Apply(7, 100), 212, 1e-9, "C to F");
        Check(UnitConvert.Apply(99, 42) == 42, "out-of-range conversion is identity");
        Check(UnitConvert.Labels.Length == 8, "8 unit chips");
    }

    static void TestPercentPhrase()
    {
        CheckNear(ExpressionEvaluator.Eval(ExpressionEvaluator.NormalizePercentPhrase("20% من 450")),
                  90, 1e-9, "Arabic percent-of phrase");
        CheckNear(ExpressionEvaluator.Eval(ExpressionEvaluator.NormalizePercentPhrase("15% of 200")),
                  30, 1e-9, "English percent-of phrase");
        // not a phrase: plain expressions and modulo stay untouched
        Check(ExpressionEvaluator.NormalizePercentPhrase("10%3") == "10%3", "modulo not rewritten");
        Check(ExpressionEvaluator.NormalizePercentPhrase("2+2") == "2+2", "plain expression untouched");
    }

    /// <summary>v1.8 seasonal occasion line: nearest-occasion ordering, Ramadan windows
    /// and Eid greetings. Uses fixed dates with known Hijri months via UmAlQura.</summary>
    static void TestOccasionsSeasonal()
    {
        bool ar = true;

        // NextLine: always names one of the builtins, never throws, format has the separator
        var today = DateTime.Today;
        string line = OccasionCore.NextLine(today, ar);
        Check(!string.IsNullOrEmpty(line) && line.Contains("·"), "NextLine returns a formatted line");

        // English variant differs
        string lineEn = OccasionCore.NextLine(today, false);
        Check(!string.IsNullOrEmpty(lineEn) && !lineEn.Contains("باقي"), "NextLine english variant");

        // SeasonalLine outside Ramadan/Eid falls back to the nearest-occasion countdown
        var info = OccasionCore.SeasonalLine(today, null, ar, true);
        Check(info != null && !string.IsNullOrEmpty(info.Text), "seasonal default = nearest occasion");

        // occasions off → nothing
        Check(OccasionCore.SeasonalLine(today, null, ar, false) == null, "seasonal respects ShowOccasions=false");

        // Eid al-Fitr greeting window (Shawwal 1..3): find a real Shawwal day this year
        DateTime? eidDay = OccasionCore.NextHijri(10, 1, today);
        if (eidDay != null)
        {
            var g = OccasionCore.SeasonalLine(eidDay.Value, null, ar, true);
            Check(g != null && g.Text.Contains("عيد"), "Eid greeting on Shawwal 1");
            // day+1 still inside the 3-day window
            if (OccasionCore.HijriMonth(eidDay.Value.AddDays(1)) == 10)
            {
                var g2 = OccasionCore.SeasonalLine(eidDay.Value.AddDays(1), null, ar, true);
                Check(g2 != null && g2.Text.Contains("عيد"), "Eid greeting on Shawwal 2");
            }
        }

        // Ramadan iftar countdown needs prayer times: craft synthetic ones (fajr 4.5h, maghrib 18.2h)
        var ramadanStart = OccasionCore.NextHijri(9, 1, today);
        if (ramadanStart != null)
        {
            double[] fake = { 4.5, 6.0, 12.0, 15.5, 18.2, 19.7 };
            var noon = ramadanStart.Value.Date.AddHours(12);   // fasting hours
            var r = OccasionCore.SeasonalLine(noon, fake, ar, true);
            Check(r != null && r.IsRamadan && r.Text.Contains("الإفطار"), "ramadan iftar countdown at noon");

            var night = ramadanStart.Value.Date.AddHours(2);   // before fajr
            var r2 = OccasionCore.SeasonalLine(night, fake, ar, true);
            Check(r2 != null && r2.IsRamadan && r2.Text.Contains("الإمساك"), "ramadan imsak countdown overnight");

            // no location → generic Ramadan greeting instead of a wrong countdown
            var r3 = OccasionCore.SeasonalLine(noon, null, ar, true);
            Check(r3 != null && r3.IsRamadan, "ramadan without location still greets");
        }

        // HijriMonth/HijriDay sane for today
        Check(OccasionCore.HijriMonth(today) >= 1 && OccasionCore.HijriMonth(today) <= 12, "hijri month in range");
        Check(OccasionCore.HijriDay(today) >= 1 && OccasionCore.HijriDay(today) <= 30, "hijri day in range");
    }

    static void TestMoonPhase()
    {
        // Known full moon: 2026-01-03 10:03 UTC (Wolf Moon) — must land in the Full bucket
        // with near-total illumination.
        var full = new DateTime(2026, 1, 3, 13, 3, 0, DateTimeKind.Utc);
        Check(MoonPhase.PhaseIndex(full) == MoonPhase.FullMoon, "known full moon buckets as full");
        Check(MoonPhase.Illumination(full) > 0.93, "full moon illumination ~1 (" + MoonPhase.Illumination(full).ToString("0.00") + ")");
        Check(MoonPhase.NameEn(full).Contains("Full"), "full moon name en");
        Check(!string.IsNullOrEmpty(MoonPhase.NameAr(full)), "phase has an arabic name");

        // Known new moon: 2026-01-18 19:52 UTC — dark disc, new bucket.
        var newm = new DateTime(2026, 1, 18, 22, 52, 0, DateTimeKind.Utc);
        Check(MoonPhase.PhaseIndex(newm) == MoonPhase.NewMoon, "known new moon buckets as new");
        Check(MoonPhase.Illumination(newm) < 0.07, "new moon illumination ~0");

        // A week after the new moon the moon is a waxing crescent ~half lit at first quarter.
        var firstQ = newm.AddDays(7.38);   // quarter cycle later
        Check(MoonPhase.IsWaxing(firstQ), "first-quarter moon is waxing");
        Check(MoonPhase.Illumination(firstQ) > 0.35 && MoonPhase.Illumination(firstQ) < 0.65,
              "quarter moon roughly half lit");

        // A week before the next new moon it is waning.
        var wane = newm.AddDays(-7.38);
        Check(!MoonPhase.IsWaxing(wane), "last-quarter moon is waning");

        // AgeFraction stays in [0,1) across a whole year of daily samples.
        bool inRange = true;
        for (int d = 0; d < 365; d++)
        {
            double f = MoonPhase.AgeFraction(new DateTime(2026, 6, 1).AddDays(d));
            if (f < 0 || f >= 1) { inRange = false; break; }
        }
        Check(inRange, "age fraction always within [0,1)");

        // Phase names differ between the known full and new moons in both languages.
        Check(MoonPhase.NameEn(newm) != MoonPhase.NameEn(full), "names distinguish phases en");
    }
}
