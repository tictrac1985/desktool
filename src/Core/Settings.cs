using System;
using System.Collections.Generic;
using System.IO;

namespace DeskMate.Core
{
    /// <summary>Application settings persisted as JSON at %AppData%\DeskMate\settings.json.</summary>
    public class Settings
    {
        public static Settings I = new Settings();
        /// <summary>Test hook: when set, Load/Save use this file instead of the default.</summary>
        public static string OverridePath;

        // general
        public string Lang = "ar";            // "ar" | "en"
        public int ClockStyle = 0;            // 0 digital, 1 analog, 2 minimal
        public bool Use24h = false;
        public bool ShowSeconds = false;
        public bool ShowHijri = true;
        public bool ShowNextPrayerLine = true;
        /// <summary>Compact "☀ 27° City" line on the widget itself. Independent of ShowWeather:
        /// the tool can stay in the icon row with the always-on line switched off.</summary>
        public bool ShowWeatherLine = true;
        public bool AutoStart = true;
        public double PosX = -1;
        public double PosY = -1;
        /// <summary>When true (default) feature panels stay open until the user closes them
        /// with the ✕ button, the pin button or Esc — clicking anywhere outside no longer
        /// dismisses them. When false, panels auto-close on deactivation (the pre-1.7
        /// behaviour). Read live by PanelWindow on every Deactivated, so flipping it in
        /// Settings affects even the panel that is already open.</summary>
        public bool PanelsPinned = true;
        public int ThemeIndex = 0;
        public bool AutoTheme = false;        // day (06–18) → light palette, night → chosen palette
        public int LayoutIndex = 0;      // 0 classic, 1 connected, 2 tri-panel, 3 orbit, 4 sidebar, 5 dock
        /// <summary>Index of the palette used for the "day" side of <see cref="AutoTheme"/>.
        /// Defaults to 4 (Light) for backward compatibility. The user can pick a different
        /// light-friendly palette without losing their night choice.</summary>
        public int LightThemeIndex = 4;
        /// <summary>When true, the dominant colour of the current desktop wallpaper is
        /// extracted once and used to build a custom palette. Overrides <see cref="ThemeIndex"/>
        /// (until turned off). Default off: most users want a stable theme.</summary>
        public bool MaterialYou = false;
        public double Scale = 1.0;
        /// <summary>Tool icon look: 0 = colour tiles (v1.11 default), 1 = glass tiles tinted by
        /// the theme, 2 = the v1.10 hand-drawn objects.</summary>
        public int IconStyle = 0;
        /// <summary>Interface font: 0 = the bundled Tajawal, 1 = the system font (Segoe UI).</summary>
        public int FontChoice = 0;
        public double BgOpacity = 1.0;   // 0.3 .. 1.0 — widget card background only

        /// <summary>Power saving. The widget is a layered window, so Windows composes it on
        /// the CPU rather than the GPU and every repaint costs real processor time. This mode
        /// stops the decorative rotations, shrinks the blur radii (a Gaussian blur is the most
        /// expensive thing the widget draws) and lets the face repaint once a minute instead of
        /// once a second. Nothing you can read changes — only how often the same picture is
        /// redrawn.</summary>
        public bool PowerSaver = false;

        /// <summary>Switch <see cref="PowerSaver"/> on by itself while the machine runs on
        /// battery, and off again when it is plugged back in. Ignored on desktops, which never
        /// report a battery.</summary>
        public bool PowerSaverOnBattery = true;

        // feature icon visibility
        public bool ShowCleaner = true;
        public bool ShowCalc = true;
        public bool ShowWallpaper = true;
        public bool ShowPower = true;
        public bool ShowClipboard = true;
        public bool ShowWeather = true;
        public bool ShowApps = true;
        public bool ShowPrayer = true;
        public bool ShowFocus = true;
        public bool ShowOccasions = true;
        public bool ShowNote = false;
        public bool ShowSysMon = false;
        public bool ShowWorldClock = false;
        public bool ShowCommand = true;
        // v1.12 tools
        public bool ShowCalendar = true;
        public bool ShowCapture = true;
        public bool ShowMedia = true;

        // tool grouping on the widget face: when on, the icon row splits into two
        // collapsible sections (daily / system) so eleven-plus tools stop crowding one row
        public bool GroupTools = true;
        public bool GroupDailyCollapsed = false;
        public bool GroupSystemCollapsed = false;

        // location
        public bool AutoLocation = true;
        public double Lat = 0;
        public double Lon = 0;
        public string City = "";
        public string Country = "";           // ISO code e.g. "EG"

        // prayer
        public int PrayerMethod = -1;         // -1 auto by country
        public bool PrayerNotify = true;
        public int PrayerPreAlertMin = 10;
        /// <summary>Per-prayer correction in minutes (fajr, sunrise, dhuhr, asr, maghrib, isha).
        /// Astronomical calculation and the local mosque routinely differ by a few minutes, and
        /// people follow their mosque — without this the times are "wrong" no matter how correct
        /// the maths is.</summary>
        public int[] PrayerAdjust = new int[6];

        // clipboard
        public int ClipboardMax = 25;
        public List<string> PinnedClips = new List<string>();

        // focus timer (pomodoro)
        public string FocusDoneDate = "";     // yyyyMMdd of the last counted session
        public int FocusDoneCount = 0;

        // prayer alert sound: 0 = system, 1 = soft chime, 2 = custom wav file
        public int AdhanSound = 0;
        public string AdhanPath = "";

        // sticky note — kept only to migrate the single pre-1.5.1 note into Notes on first load
        public string StickyNote = "";
        /// <summary>Notes, each a JSON object (see Logic.NoteItem) encrypted at rest.</summary>
        public List<string> Notes = new List<string>();

        // custom occasion date (yyyy-MM-dd, Gregorian); empty = none
        public string OccasionCustom = "";
        // custom occasion display name; empty = generic "custom date" label
        public string OccasionCustomName = "";
        // when true the custom occasion repeats every year (birthday/anniversary style)
        public bool OccasionCustomYearly;

        // world clocks: Windows timezone ids chosen by the user
        public List<string> WorldClocks = new List<string>();

        // ---- v1.12 ----

        /// <summary>Look for a newer build on the project's update channel shortly after
        /// start and every few hours. Only ever CHECKS — installing is always a click.</summary>
        public bool AutoUpdateCheck = true;
        /// <summary>UTC time of the last update check (ISO 8601), "" = never.</summary>
        public string LastUpdateCheck = "";

        /// <summary>How much the widget moves: 0 = still, 1 = calm (hover feedback only),
        /// 2 = cinematic (entrance choreography, the light that follows the pointer, the
        /// glint on the minute). Power saving always behaves as 0.</summary>
        public int Motion = 2;

        /// <summary>Global shortcut that summons the command bar: an index into
        /// Logic.HotkeyCore.Choices. 0 = automatic (the first combination that is free).</summary>
        public int Hotkey = 0;

        // currency converter (calculator): ISO codes; CurTo "" = the currency of Country
        public string CurFrom = "USD";
        public string CurTo = "";

        // month calendar
        /// <summary>When true the grid is laid out by Hijri months with the Gregorian day
        /// small in each cell; otherwise the other way round.</summary>
        public bool CalHijriFirst = false;
        /// <summary>First column of the month grid as a DayOfWeek number: 6 = Saturday,
        /// 0 = Sunday, 1 = Monday.</summary>
        public int WeekStart = 0;

        // screen capture & colour picker
        /// <summary>Also save every screenshot as a PNG under Pictures\DeskMate.</summary>
        public bool ShotSave = true;
        /// <summary>Recently picked colours, newest first, as #RRGGBB.</summary>
        public List<string> RecentColors = new List<string>();

        // ---------------- power saving ----------------

        static bool _saverCached;
        static DateTime _saverChecked = DateTime.MinValue;

        /// <summary>Power saving as it applies right now: the explicit switch, or the machine
        /// running on battery with the automatic option on. Asked on every tick, so the battery
        /// probe (a syscall, cheap but not free) is cached for twenty seconds — plugging a
        /// charger in is not something that needs a per-second answer.</summary>
        public static bool SaverActive
        {
            get
            {
                var s = I;
                if (s.PowerSaver) return true;
                if (!s.PowerSaverOnBattery) return false;
                var now = DateTime.UtcNow;
                if ((now - _saverChecked).TotalSeconds >= 20)
                {
                    _saverChecked = now;
                    _saverCached = Win32.OnBattery();
                }
                return _saverCached;
            }
        }

        // ---------------- persistence table ----------------

        /// <summary>One entry per persisted field. The read side, the write side and the
        /// default all come from this single declaration, so adding a setting is one line here
        /// plus the field itself — they can no longer drift apart. Order here is the order in
        /// the JSON file, so keeping it stable keeps settings.json diffs readable.</summary>
        abstract class Field
        {
            public string Name;
            public abstract void Read(Settings s, Dictionary<string, object> d);
            public abstract void Write(Settings s, Dictionary<string, object> d);
        }

        class StrField : Field
        {
            public Func<Settings, string> Get;
            public Action<Settings, string> Set;
            /// <summary>Optional at-rest transform, used to DPAPI-protect personal text.</summary>
            public Func<string, string> Decode, Encode;
            public override void Read(Settings s, Dictionary<string, object> d)
            {
                string v = Json.GetStr(d, Name, Get(s));
                Set(s, Decode == null ? v : Decode(v));
            }
            public override void Write(Settings s, Dictionary<string, object> d)
            {
                string v = Get(s);
                d[Name] = Encode == null ? v : Encode(v);
            }
        }

        class BoolField : Field
        {
            public Func<Settings, bool> Get;
            public Action<Settings, bool> Set;
            public override void Read(Settings s, Dictionary<string, object> d) { Set(s, Json.GetBool(d, Name, Get(s))); }
            public override void Write(Settings s, Dictionary<string, object> d) { d[Name] = Get(s); }
        }

        class IntField : Field
        {
            public Func<Settings, int> Get;
            public Action<Settings, int> Set;
            public override void Read(Settings s, Dictionary<string, object> d) { Set(s, Json.GetInt(d, Name, Get(s))); }
            public override void Write(Settings s, Dictionary<string, object> d) { d[Name] = (double)Get(s); }
        }

        class NumField : Field
        {
            public Func<Settings, double> Get;
            public Action<Settings, double> Set;
            public override void Read(Settings s, Dictionary<string, object> d) { Set(s, Json.GetNum(d, Name, Get(s))); }
            public override void Write(Settings s, Dictionary<string, object> d) { d[Name] = Get(s); }
        }

        class StrListField : Field
        {
            public Func<Settings, List<string>> Get;
            public Func<string, string> Decode, Encode;
            public override void Read(Settings s, Dictionary<string, object> d)
            {
                var list = Get(s);
                list.Clear();
                foreach (var o in Json.GetList(d, Name))
                {
                    var item = o as string;
                    if (item == null) continue;
                    if (Decode != null) item = Decode(item);
                    if (!string.IsNullOrEmpty(item)) list.Add(item);
                }
            }
            public override void Write(Settings s, Dictionary<string, object> d)
            {
                var outList = new List<string>();
                foreach (string item in Get(s)) outList.Add(Encode == null ? item : Encode(item));
                d[Name] = outList;
            }
        }

        /// <summary>Fixed-length int array, stored as a JSON number list. Length is preserved
        /// on read whatever the file says, so a hand-edited or older settings file cannot make
        /// the rest of the app index past the end.</summary>
        class IntArrayField : Field
        {
            public Func<Settings, int[]> Get;
            public override void Read(Settings s, Dictionary<string, object> d)
            {
                var target = Get(s);
                var list = Json.GetList(d, Name);
                for (int i = 0; i < target.Length; i++)
                    target[i] = i < list.Count && list[i] is double ? (int)Math.Round((double)list[i]) : 0;
            }
            public override void Write(Settings s, Dictionary<string, object> d)
            {
                var outList = new List<object>();
                foreach (int v in Get(s)) outList.Add((double)v);
                d[Name] = outList;
            }
        }

        static Field Ints(string n, Func<Settings, int[]> g)
        {
            return new IntArrayField { Name = n, Get = g };
        }

        static Field Str(string n, Func<Settings, string> g, Action<Settings, string> s)
        {
            return new StrField { Name = n, Get = g, Set = s };
        }
        /// <summary>A string field whose value is DPAPI-encrypted in settings.json.</summary>
        static Field Sec(string n, Func<Settings, string> g, Action<Settings, string> s)
        {
            return new StrField { Name = n, Get = g, Set = s, Decode = Secret.Unprotect, Encode = Secret.Protect };
        }
        static Field Bit(string n, Func<Settings, bool> g, Action<Settings, bool> s)
        {
            return new BoolField { Name = n, Get = g, Set = s };
        }
        static Field Int(string n, Func<Settings, int> g, Action<Settings, int> s)
        {
            return new IntField { Name = n, Get = g, Set = s };
        }
        static Field Num(string n, Func<Settings, double> g, Action<Settings, double> s)
        {
            return new NumField { Name = n, Get = g, Set = s };
        }
        static Field List(string n, Func<Settings, List<string>> g)
        {
            return new StrListField { Name = n, Get = g };
        }
        static Field SecList(string n, Func<Settings, List<string>> g)
        {
            return new StrListField { Name = n, Get = g, Decode = Secret.Unprotect, Encode = Secret.Protect };
        }

        static readonly Field[] Schema =
        {
            Str("Lang",               s => s.Lang,               (s, v) => s.Lang = v),
            Int("ClockStyle",         s => s.ClockStyle,         (s, v) => s.ClockStyle = v),
            Bit("Use24h",             s => s.Use24h,             (s, v) => s.Use24h = v),
            Bit("ShowSeconds",        s => s.ShowSeconds,        (s, v) => s.ShowSeconds = v),
            Bit("ShowHijri",          s => s.ShowHijri,          (s, v) => s.ShowHijri = v),
            Bit("ShowNextPrayerLine", s => s.ShowNextPrayerLine, (s, v) => s.ShowNextPrayerLine = v),
            Bit("ShowWeatherLine",    s => s.ShowWeatherLine,    (s, v) => s.ShowWeatherLine = v),
            Bit("AutoStart",          s => s.AutoStart,          (s, v) => s.AutoStart = v),
            Num("PosX",               s => s.PosX,               (s, v) => s.PosX = v),
            Num("PosY",               s => s.PosY,               (s, v) => s.PosY = v),
            Bit("PanelsPinned",       s => s.PanelsPinned,       (s, v) => s.PanelsPinned = v),
            Int("ThemeIndex",         s => s.ThemeIndex,         (s, v) => s.ThemeIndex = v),
            Bit("AutoTheme",          s => s.AutoTheme,          (s, v) => s.AutoTheme = v),
            Int("LightThemeIndex",    s => s.LightThemeIndex,    (s, v) => s.LightThemeIndex = v),
            Bit("MaterialYou",        s => s.MaterialYou,        (s, v) => s.MaterialYou = v),
            Int("LayoutIndex",        s => s.LayoutIndex,        (s, v) => s.LayoutIndex = v),
            Num("Scale",              s => s.Scale,              (s, v) => s.Scale = v),
            Int("IconStyle",          s => s.IconStyle,          (s, v) => s.IconStyle = v),
            Int("FontChoice",         s => s.FontChoice,         (s, v) => s.FontChoice = v),
            Num("BgOpacity",          s => s.BgOpacity,          (s, v) => s.BgOpacity = v),
            Bit("PowerSaver",         s => s.PowerSaver,         (s, v) => s.PowerSaver = v),
            Bit("PowerSaverOnBattery", s => s.PowerSaverOnBattery, (s, v) => s.PowerSaverOnBattery = v),

            Bit("ShowCleaner",        s => s.ShowCleaner,        (s, v) => s.ShowCleaner = v),
            Bit("ShowCalc",           s => s.ShowCalc,           (s, v) => s.ShowCalc = v),
            Bit("ShowWallpaper",      s => s.ShowWallpaper,      (s, v) => s.ShowWallpaper = v),
            Bit("ShowPower",          s => s.ShowPower,          (s, v) => s.ShowPower = v),
            Bit("ShowClipboard",      s => s.ShowClipboard,      (s, v) => s.ShowClipboard = v),
            Bit("ShowWeather",        s => s.ShowWeather,        (s, v) => s.ShowWeather = v),
            Bit("ShowApps",           s => s.ShowApps,           (s, v) => s.ShowApps = v),
            Bit("ShowPrayer",         s => s.ShowPrayer,         (s, v) => s.ShowPrayer = v),
            Bit("ShowFocus",          s => s.ShowFocus,          (s, v) => s.ShowFocus = v),
            Bit("ShowOccasions",      s => s.ShowOccasions,      (s, v) => s.ShowOccasions = v),
            Bit("ShowNote",           s => s.ShowNote,           (s, v) => s.ShowNote = v),
            Bit("ShowSysMon",         s => s.ShowSysMon,         (s, v) => s.ShowSysMon = v),
            Bit("ShowWorldClock",     s => s.ShowWorldClock,     (s, v) => s.ShowWorldClock = v),
            Bit("ShowCommand",        s => s.ShowCommand,        (s, v) => s.ShowCommand = v),
            Bit("ShowCalendar",       s => s.ShowCalendar,       (s, v) => s.ShowCalendar = v),
            Bit("ShowCapture",        s => s.ShowCapture,        (s, v) => s.ShowCapture = v),
            Bit("ShowMedia",          s => s.ShowMedia,          (s, v) => s.ShowMedia = v),
            Bit("GroupTools",         s => s.GroupTools,         (s, v) => s.GroupTools = v),
            Bit("GroupDailyCollapsed",    s => s.GroupDailyCollapsed,    (s, v) => s.GroupDailyCollapsed = v),
            Bit("GroupSystemCollapsed",   s => s.GroupSystemCollapsed,   (s, v) => s.GroupSystemCollapsed = v),

            Bit("AutoLocation",       s => s.AutoLocation,       (s, v) => s.AutoLocation = v),
            Num("Lat",                s => s.Lat,                (s, v) => s.Lat = v),
            Num("Lon",                s => s.Lon,                (s, v) => s.Lon = v),
            Str("City",               s => s.City,               (s, v) => s.City = v),
            Str("Country",            s => s.Country,            (s, v) => s.Country = v),

            Int("PrayerMethod",       s => s.PrayerMethod,       (s, v) => s.PrayerMethod = v),
            Bit("PrayerNotify",       s => s.PrayerNotify,       (s, v) => s.PrayerNotify = v),
            Int("PrayerPreAlertMin",  s => s.PrayerPreAlertMin,  (s, v) => s.PrayerPreAlertMin = v),
            Ints("PrayerAdjust",      s => s.PrayerAdjust),

            Int("ClipboardMax",       s => s.ClipboardMax,       (s, v) => s.ClipboardMax = v),
            SecList("PinnedClips",    s => s.PinnedClips),

            Str("FocusDoneDate",      s => s.FocusDoneDate,      (s, v) => s.FocusDoneDate = v),
            Int("FocusDoneCount",     s => s.FocusDoneCount,     (s, v) => s.FocusDoneCount = v),

            Int("AdhanSound",         s => s.AdhanSound,         (s, v) => s.AdhanSound = v),
            Str("AdhanPath",          s => s.AdhanPath,          (s, v) => s.AdhanPath = v),

            Sec("StickyNote",         s => s.StickyNote,         (s, v) => s.StickyNote = v),
            SecList("Notes",          s => s.Notes),
            Str("OccasionCustom",     s => s.OccasionCustom,     (s, v) => s.OccasionCustom = v),
            Str("OccasionCustomName", s => s.OccasionCustomName, (s, v) => s.OccasionCustomName = v),
            Bit("OccasionCustomYearly", s => s.OccasionCustomYearly, (s, v) => s.OccasionCustomYearly = v),
            List("WorldClocks",       s => s.WorldClocks),

            Bit("AutoUpdateCheck",    s => s.AutoUpdateCheck,    (s, v) => s.AutoUpdateCheck = v),
            Str("LastUpdateCheck",    s => s.LastUpdateCheck,    (s, v) => s.LastUpdateCheck = v),
            Int("Motion",             s => s.Motion,             (s, v) => s.Motion = v),
            Int("Hotkey",             s => s.Hotkey,             (s, v) => s.Hotkey = v),
            Str("CurFrom",            s => s.CurFrom,            (s, v) => s.CurFrom = v),
            Str("CurTo",              s => s.CurTo,              (s, v) => s.CurTo = v),
            Bit("CalHijriFirst",      s => s.CalHijriFirst,      (s, v) => s.CalHijriFirst = v),
            Int("WeekStart",          s => s.WeekStart,          (s, v) => s.WeekStart = v),
            Bit("ShotSave",           s => s.ShotSave,           (s, v) => s.ShotSave = v),
            List("RecentColors",      s => s.RecentColors),
        };

        /// <summary>Highest valid palette index. Keep equal to UI.Theme.Palettes.Length - 1
        /// (UiSmoke asserts it) — an out-of-range value used to loop Theme.Apply → Reload →
        /// tick → Apply until StackOverflow killed the process.</summary>
        public const int MaxThemeIndex = 14;
        public const int MaxRecentColors = 12;

        static bool IsCurrencyCode(string c)
        {
            if (c == null || c.Length != 3) return false;
            foreach (char ch in c) if (ch < 'A' || ch > 'Z') return false;
            return true;
        }

        static bool IsHexColor(string c)
        {
            if (c == null || c.Length != 7 || c[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char ch = c[i];
                bool hex = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        /// <summary>Forces values from a hand-edited or corrupted file back into usable ranges.
        /// Applied after every load; never trust the file to be sane.</summary>
        void Normalize()
        {
            if (LayoutIndex < 0 || LayoutIndex > 7) LayoutIndex = 0;   // 0..7: classic, connected, tri-panel, orbit, sidebar, dock, monolith, halo
            if (Scale < 0.6 || Scale > 1.8) Scale = 1.0;
            if (BgOpacity < 0.3 || BgOpacity > 1.0) BgOpacity = 1.0;
            if (AdhanSound < 0 || AdhanSound > 2) AdhanSound = 0;
            if (ThemeIndex < 0 || ThemeIndex > MaxThemeIndex) ThemeIndex = 0;
            if (LightThemeIndex < 0 || LightThemeIndex > MaxThemeIndex) LightThemeIndex = 4;
            if (Motion < 0 || Motion > 2) Motion = 2;
            if (Hotkey < 0 || Hotkey >= Logic.HotkeyCore.Choices.Length) Hotkey = 0;
            if (WeekStart != 6 && WeekStart != 0 && WeekStart != 1) WeekStart = 0;
            CurFrom = (CurFrom ?? "").Trim().ToUpperInvariant();
            CurTo = (CurTo ?? "").Trim().ToUpperInvariant();
            if (!IsCurrencyCode(CurFrom)) CurFrom = "USD";
            if (CurTo.Length > 0 && !IsCurrencyCode(CurTo)) CurTo = "";
            if (LastUpdateCheck == null || LastUpdateCheck.Length > 40) LastUpdateCheck = "";
            if (RecentColors == null) RecentColors = new List<string>();
            RecentColors.RemoveAll(c => !IsHexColor(c));
            while (RecentColors.Count > MaxRecentColors) RecentColors.RemoveAt(RecentColors.Count - 1);
            if (ClockStyle < 0 || ClockStyle > 10) ClockStyle = 0;
            if (IconStyle < 0 || IconStyle > 2) IconStyle = 0;
            if (FontChoice < 0 || FontChoice > 1) FontChoice = 0;   // 0..10: digital, analog, minimal, flip, word, neon, binary, day-ring, sun-path, day-stripes, moon-phase
            if (PrayerAdjust == null || PrayerAdjust.Length != 6) PrayerAdjust = new int[6];
            for (int i = 0; i < PrayerAdjust.Length; i++)
            {
                // a correction beyond an hour is a mis-set location, not a mosque difference
                if (PrayerAdjust[i] < -60) PrayerAdjust[i] = -60;
                if (PrayerAdjust[i] > 60) PrayerAdjust[i] = 60;
            }
        }

        // ---------------- load / save ----------------

        public static string DefaultPath
        {
            get { return Path.Combine(Log.Dir, "settings.json"); }
        }

        static string PathToUse { get { return string.IsNullOrEmpty(OverridePath) ? DefaultPath : OverridePath; } }

        public static void Load()
        {
            string p = PathToUse;
            // boot-time reads can hit a transient lock (AV scan / indexer) — retry, then
            // fall back to the .bak written by every successful Save
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    if (!File.Exists(p)) { I = new Settings(); return; }
                    var d = Json.Parse(File.ReadAllText(p)) as Dictionary<string, object>;
                    if (d == null) throw new FormatException("settings root is not an object");
                    I = FromDict(d);
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error("Settings.Load attempt " + attempt, ex);
                    System.Threading.Thread.Sleep(150);
                }
            }
            try
            {
                string bak = p + ".bak";
                if (File.Exists(bak))
                {
                    var d = Json.Parse(File.ReadAllText(bak)) as Dictionary<string, object>;
                    if (d != null)
                    {
                        I = FromDict(d);
                        Log.Write("Settings restored from backup");
                        I.Save(); // heal the primary file
                        return;
                    }
                }
            }
            catch (Exception ex) { Log.Error("Settings.Load backup", ex); }
            I = new Settings();
        }

        static Settings FromDict(Dictionary<string, object> d)
        {
            var s = new Settings();
            foreach (var f in Schema) f.Read(s, d);
            s.Normalize();
            return s;
        }

        /// <summary>Save runs from the UI thread and from background tasks (AutoLocate), so it
        /// must not interleave: two writers sharing one .tmp path corrupt each other.</summary>
        static readonly object SaveGate = new object();

        public void Save()
        {
            lock (SaveGate)
            {
                SaveLocked();
            }
        }

        void SaveLocked()
        {
            try
            {
                var d = new Dictionary<string, object>();
                foreach (var f in Schema) f.Write(this, d);

                string p = PathToUse;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                // atomic write: never leave a truncated settings file, keep a .bak of the
                // previous good version (used by Load as a fallback).
                // unique temp name: a leftover .tmp from a killed process must not be reused
                string tmp = p + "." + Guid.NewGuid().ToString("N") + ".tmp";
                string json = Json.Write(d);
                try
                {
                    for (int attempt = 0; attempt < 2; attempt++)
                    {
                        try
                        {
                            File.WriteAllText(tmp, json);
                            if (File.Exists(p)) File.Replace(tmp, p, p + ".bak");
                            else File.Move(tmp, p);
                            return;
                        }
                        catch (IOException) { System.Threading.Thread.Sleep(120); }
                    }
                    File.WriteAllText(p, json); // last resort
                }
                finally
                {
                    // the unique name means a failed attempt would otherwise litter the folder
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Settings.Save", ex);
            }
        }
    }
}
