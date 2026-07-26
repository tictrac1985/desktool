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
        public bool AutoStart = true;
        public double PosX = -1;
        public double PosY = -1;
        public int ThemeIndex = 0;
        public bool AutoTheme = false;        // day (06–18) → light palette, night → chosen palette
        public int LayoutIndex = 0;      // 0 classic, 1 connected, 2 tri-panel, 3 orbit, 4 sidebar, 5 dock
        public double Scale = 1.0;
        public double BgOpacity = 1.0;   // 0.3 .. 1.0 — widget card background only

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

        // clipboard
        public int ClipboardMax = 25;
        public List<string> PinnedClips = new List<string>();

        // focus timer (pomodoro)
        public string FocusDoneDate = "";     // yyyyMMdd of the last counted session
        public int FocusDoneCount = 0;

        // prayer alert sound: 0 = system, 1 = soft chime, 2 = custom wav file
        public int AdhanSound = 0;
        public string AdhanPath = "";

        // sticky note
        public string StickyNote = "";

        // custom occasion date (yyyy-MM-dd, Gregorian); empty = none
        public string OccasionCustom = "";

        // world clocks: Windows timezone ids chosen by the user
        public List<string> WorldClocks = new List<string>();

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
            {
                var s = new Settings();
                s.Lang = Json.GetStr(d, "Lang", s.Lang);
                s.ClockStyle = Json.GetInt(d, "ClockStyle", s.ClockStyle);
                s.Use24h = Json.GetBool(d, "Use24h", s.Use24h);
                s.ShowSeconds = Json.GetBool(d, "ShowSeconds", s.ShowSeconds);
                s.ShowHijri = Json.GetBool(d, "ShowHijri", s.ShowHijri);
                s.ShowNextPrayerLine = Json.GetBool(d, "ShowNextPrayerLine", s.ShowNextPrayerLine);
                s.AutoStart = Json.GetBool(d, "AutoStart", s.AutoStart);
                s.PosX = Json.GetNum(d, "PosX", s.PosX);
                s.PosY = Json.GetNum(d, "PosY", s.PosY);
                s.ThemeIndex = Json.GetInt(d, "ThemeIndex", s.ThemeIndex);
                s.AutoTheme = Json.GetBool(d, "AutoTheme", s.AutoTheme);
                s.LayoutIndex = Json.GetInt(d, "LayoutIndex", s.LayoutIndex);
                if (s.LayoutIndex < 0 || s.LayoutIndex > 5) s.LayoutIndex = 0;
                s.Scale = Json.GetNum(d, "Scale", s.Scale);
                if (s.Scale < 0.6 || s.Scale > 1.8) s.Scale = 1.0;
                s.BgOpacity = Json.GetNum(d, "BgOpacity", s.BgOpacity);
                if (s.BgOpacity < 0.3 || s.BgOpacity > 1.0) s.BgOpacity = 1.0;
                s.ShowCleaner = Json.GetBool(d, "ShowCleaner", s.ShowCleaner);
                s.ShowCalc = Json.GetBool(d, "ShowCalc", s.ShowCalc);
                s.ShowWallpaper = Json.GetBool(d, "ShowWallpaper", s.ShowWallpaper);
                s.ShowPower = Json.GetBool(d, "ShowPower", s.ShowPower);
                s.ShowClipboard = Json.GetBool(d, "ShowClipboard", s.ShowClipboard);
                s.ShowWeather = Json.GetBool(d, "ShowWeather", s.ShowWeather);
                s.ShowApps = Json.GetBool(d, "ShowApps", s.ShowApps);
                s.ShowPrayer = Json.GetBool(d, "ShowPrayer", s.ShowPrayer);
                s.ShowFocus = Json.GetBool(d, "ShowFocus", s.ShowFocus);
                s.ShowOccasions = Json.GetBool(d, "ShowOccasions", s.ShowOccasions);
                s.ShowNote = Json.GetBool(d, "ShowNote", s.ShowNote);
                s.ShowSysMon = Json.GetBool(d, "ShowSysMon", s.ShowSysMon);
                s.ShowWorldClock = Json.GetBool(d, "ShowWorldClock", s.ShowWorldClock);
                s.AutoLocation = Json.GetBool(d, "AutoLocation", s.AutoLocation);
                s.Lat = Json.GetNum(d, "Lat", s.Lat);
                s.Lon = Json.GetNum(d, "Lon", s.Lon);
                s.City = Json.GetStr(d, "City", s.City);
                s.Country = Json.GetStr(d, "Country", s.Country);
                s.PrayerMethod = Json.GetInt(d, "PrayerMethod", s.PrayerMethod);
                s.PrayerNotify = Json.GetBool(d, "PrayerNotify", s.PrayerNotify);
                s.PrayerPreAlertMin = Json.GetInt(d, "PrayerPreAlertMin", s.PrayerPreAlertMin);
                s.ClipboardMax = Json.GetInt(d, "ClipboardMax", s.ClipboardMax);
                s.PinnedClips = new List<string>();
                foreach (var o in Json.GetList(d, "PinnedClips"))
                    if (o is string) s.PinnedClips.Add((string)o);
                s.FocusDoneDate = Json.GetStr(d, "FocusDoneDate", s.FocusDoneDate);
                s.FocusDoneCount = Json.GetInt(d, "FocusDoneCount", s.FocusDoneCount);
                s.AdhanSound = Json.GetInt(d, "AdhanSound", s.AdhanSound);
                if (s.AdhanSound < 0 || s.AdhanSound > 2) s.AdhanSound = 0;
                s.AdhanPath = Json.GetStr(d, "AdhanPath", s.AdhanPath);
                s.StickyNote = Json.GetStr(d, "StickyNote", s.StickyNote);
                s.OccasionCustom = Json.GetStr(d, "OccasionCustom", s.OccasionCustom);
                s.WorldClocks = new List<string>();
                foreach (var o in Json.GetList(d, "WorldClocks"))
                    if (o is string) s.WorldClocks.Add((string)o);
                return s;
            }
        }

        public void Save()
        {
            try
            {
                var d = new Dictionary<string, object>();
                d["Lang"] = Lang;
                d["ClockStyle"] = (double)ClockStyle;
                d["Use24h"] = Use24h;
                d["ShowSeconds"] = ShowSeconds;
                d["ShowHijri"] = ShowHijri;
                d["ShowNextPrayerLine"] = ShowNextPrayerLine;
                d["AutoStart"] = AutoStart;
                d["PosX"] = PosX;
                d["PosY"] = PosY;
                d["ThemeIndex"] = (double)ThemeIndex;
                d["AutoTheme"] = AutoTheme;
                d["LayoutIndex"] = (double)LayoutIndex;
                d["Scale"] = Scale;
                d["BgOpacity"] = BgOpacity;
                d["ShowCleaner"] = ShowCleaner;
                d["ShowCalc"] = ShowCalc;
                d["ShowWallpaper"] = ShowWallpaper;
                d["ShowPower"] = ShowPower;
                d["ShowClipboard"] = ShowClipboard;
                d["ShowWeather"] = ShowWeather;
                d["ShowApps"] = ShowApps;
                d["ShowPrayer"] = ShowPrayer;
                d["ShowFocus"] = ShowFocus;
                d["ShowOccasions"] = ShowOccasions;
                d["ShowNote"] = ShowNote;
                d["ShowSysMon"] = ShowSysMon;
                d["ShowWorldClock"] = ShowWorldClock;
                d["AutoLocation"] = AutoLocation;
                d["Lat"] = Lat;
                d["Lon"] = Lon;
                d["City"] = City;
                d["Country"] = Country;
                d["PrayerMethod"] = (double)PrayerMethod;
                d["PrayerNotify"] = PrayerNotify;
                d["PrayerPreAlertMin"] = (double)PrayerPreAlertMin;
                d["ClipboardMax"] = (double)ClipboardMax;
                d["PinnedClips"] = PinnedClips;
                d["FocusDoneDate"] = FocusDoneDate;
                d["FocusDoneCount"] = (double)FocusDoneCount;
                d["AdhanSound"] = (double)AdhanSound;
                d["AdhanPath"] = AdhanPath;
                d["StickyNote"] = StickyNote;
                d["OccasionCustom"] = OccasionCustom;
                d["WorldClocks"] = WorldClocks;
                string p = PathToUse;
                Directory.CreateDirectory(Path.GetDirectoryName(p));
                // atomic write: never leave a truncated settings file, keep a .bak of the
                // previous good version (used by Load as a fallback)
                string tmp = p + ".tmp";
                string json = Json.Write(d);
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
            catch (Exception ex)
            {
                Log.Error("Settings.Save", ex);
            }
        }
    }
}
