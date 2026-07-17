using System.Collections.Generic;

namespace DeskMate.Core
{
    /// <summary>Localization: L.T("key") → Arabic or English per Settings.I.Lang.</summary>
    public static class L
    {
        public static bool IsAr { get { return Settings.I.Lang != "en"; } }

        public static string T(string key)
        {
            string[] v;
            if (Table.TryGetValue(key, out v)) return IsAr ? v[0] : v[1];
            return key;
        }

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            { "app_name",        new[] { "ديسك ميت", "DeskMate" } },
            { "tray_show",       new[] { "إظهار الأداة", "Show widget" } },
            { "tray_hide",       new[] { "إخفاء الأداة", "Hide widget" } },
            { "tray_settings",   new[] { "الإعدادات", "Settings" } },
            { "tray_exit",       new[] { "إغلاق", "Exit" } },

            { "f_cleaner",       new[] { "تنظيف سريع", "Quick Clean" } },
            { "f_calc",          new[] { "الآلة الحاسبة", "Calculator" } },
            { "f_wallpaper",     new[] { "خلفية الشاشة", "Wallpaper" } },
            { "f_power",         new[] { "وقت السكون", "Sleep Timer" } },
            { "f_clipboard",     new[] { "الحافظة", "Clipboard" } },
            { "f_weather",       new[] { "الطقس", "Weather" } },
            { "f_apps",          new[] { "البرامج", "Apps" } },
            { "f_prayer",        new[] { "مواقيت الصلاة", "Prayer Times" } },
            { "f_settings",      new[] { "الإعدادات", "Settings" } },

            { "cl_temp",         new[] { "الملفات المؤقتة للمستخدم", "User temp files" } },
            { "cl_wintemp",      new[] { "ملفات ويندوز المؤقتة", "Windows temp files" } },
            { "cl_inet",         new[] { "ذاكرة الإنترنت المؤقتة", "Internet cache" } },
            { "cl_recycle",      new[] { "إفراغ سلة المحذوفات", "Empty Recycle Bin" } },
            { "cl_run",          new[] { "ابدأ التنظيف", "Clean now" } },
            { "cl_running",      new[] { "جارٍ التنظيف…", "Cleaning…" } },
            { "cl_done",         new[] { "تم تحرير {0}", "Freed {0}" } },
            { "cl_note",         new[] { "يتم تجاهل الملفات المستخدمة حالياً تلقائياً", "Files in use are skipped automatically" } },

            { "calc_pro",        new[] { "علمية", "Pro" } },
            { "calc_simple",     new[] { "بسيطة", "Basic" } },
            { "calc_error",      new[] { "خطأ", "Error" } },

            { "wp_pick",         new[] { "اختر صورة…", "Choose image…" } },
            { "wp_style",        new[] { "طريقة العرض", "Fit style" } },
            { "wp_fill",         new[] { "ملء", "Fill" } },
            { "wp_fit",          new[] { "احتواء", "Fit" } },
            { "wp_stretch",      new[] { "تمديد", "Stretch" } },
            { "wp_tile",         new[] { "تجانب", "Tile" } },
            { "wp_center",       new[] { "توسيط", "Center" } },
            { "wp_apply",        new[] { "تعيين كخلفية", "Set as wallpaper" } },
            { "wp_done",         new[] { "تم تغيير الخلفية ✓", "Wallpaper changed ✓" } },
            { "wp_err",          new[] { "تعذر تغيير الخلفية", "Could not set wallpaper" } },

            { "pw_ac",           new[] { "أثناء الشحن", "Plugged in" } },
            { "pw_dc",           new[] { "على البطارية", "On battery" } },
            { "pw_never",        new[] { "أبداً", "Never" } },
            { "pw_min",          new[] { "{0} د", "{0} m" } },
            { "pw_applied",      new[] { "تم ضبط وقت السكون ✓", "Sleep timeout set ✓" } },
            { "pw_hint",         new[] { "بعد المدة المحددة من الخمول يدخل الجهاز في وضع السكون", "The PC sleeps after this idle period" } },

            { "cb_empty",        new[] { "انسخ أي نص وسيظهر هنا", "Copy any text and it appears here" } },
            { "cb_pinned",       new[] { "مثبَّت", "Pinned" } },
            { "cb_recent",       new[] { "الأحدث", "Recent" } },
            { "cb_copied",       new[] { "تم النسخ ✓", "Copied ✓" } },
            { "cb_clear",        new[] { "مسح الكل", "Clear all" } },

            { "we_loading",      new[] { "جارٍ التحميل…", "Loading…" } },
            { "we_locating",     new[] { "جارٍ تحديد الموقع…", "Locating…" } },
            { "we_err",          new[] { "تعذر جلب الطقس — تحقق من الاتصال", "Weather unavailable — check connection" } },
            { "we_humidity",     new[] { "الرطوبة", "Humidity" } },
            { "we_wind",         new[] { "الرياح", "Wind" } },
            { "we_refresh",      new[] { "تحديث", "Refresh" } },

            { "ap_search",       new[] { "ابحث عن برنامج…", "Search apps…" } },
            { "ap_open",         new[] { "فتح", "Open" } },
            { "ap_add",          new[] { "إضافة لسطح المكتب", "Add to desktop" } },
            { "ap_added",        new[] { "أُضيف لسطح المكتب ✓", "Added to desktop ✓" } },
            { "ap_none",         new[] { "لا نتائج", "No results" } },

            { "pr_fajr",         new[] { "الفجر", "Fajr" } },
            { "pr_sunrise",      new[] { "الشروق", "Sunrise" } },
            { "pr_dhuhr",        new[] { "الظهر", "Dhuhr" } },
            { "pr_asr",          new[] { "العصر", "Asr" } },
            { "pr_maghrib",      new[] { "المغرب", "Maghrib" } },
            { "pr_isha",         new[] { "العشاء", "Isha" } },
            { "pr_next",         new[] { "الصلاة القادمة", "Next prayer" } },
            { "pr_in",           new[] { "بعد", "in" } },
            { "pr_method",       new[] { "طريقة الحساب", "Calculation method" } },
            { "pr_m_auto",       new[] { "تلقائي حسب الدولة", "Auto (by country)" } },
            { "pr_m_mwl",        new[] { "رابطة العالم الإسلامي", "Muslim World League" } },
            { "pr_m_egypt",      new[] { "الهيئة المصرية العامة للمساحة", "Egyptian Authority" } },
            { "pr_m_karachi",    new[] { "جامعة العلوم — كراتشي", "Univ. of Karachi" } },
            { "pr_m_isna",       new[] { "الجمعية الإسلامية لأمريكا الشمالية", "ISNA" } },
            { "pr_m_umm",        new[] { "أم القرى — مكة", "Umm al-Qura (Makkah)" } },
            { "pr_notify",       new[] { "تنبيه عند دخول الوقت", "Notify at prayer time" } },
            { "pr_prealert",     new[] { "تنبيه مسبق (دقائق)", "Pre-alert (minutes)" } },
            { "pr_now",          new[] { "حان الآن وقت صلاة {0}", "It is now time for {0} prayer" } },
            { "pr_pre",          new[] { "بقي {1} دقيقة على صلاة {0}", "{1} minutes until {0} prayer" } },
            { "pr_need_loc",     new[] { "حدد موقعك أولاً لحساب المواقيت", "Set your location to compute times" } },
            { "pr_locate",       new[] { "تحديد الموقع تلقائياً", "Detect location" } },

            { "st_lang",         new[] { "اللغة", "Language" } },
            { "st_clock",        new[] { "شكل الساعة", "Clock style" } },
            { "st_style_digital",new[] { "رقمية", "Digital" } },
            { "st_style_analog", new[] { "عقارب", "Analog" } },
            { "st_style_minimal",new[] { "بسيطة", "Minimal" } },
            { "st_24h",          new[] { "نظام 24 ساعة", "24-hour format" } },
            { "st_seconds",      new[] { "إظهار الثواني", "Show seconds" } },
            { "st_hijri",        new[] { "التاريخ الهجري", "Hijri date" } },
            { "st_prayer_line",  new[] { "سطر الصلاة القادمة", "Next-prayer line" } },
            { "st_features",     new[] { "الأدوات الظاهرة", "Visible tools" } },
            { "st_autostart",    new[] { "التشغيل مع بدء ويندوز", "Start with Windows" } },
            { "st_pin",          new[] { "تثبيت خلف النوافذ (جزء من سطح المكتب)", "Pin behind windows (desktop layer)" } },
            { "st_location",     new[] { "الموقع (للطقس والصلاة)", "Location (weather & prayer)" } },
            { "st_auto_loc",     new[] { "تحديد تلقائي عبر الإنترنت", "Auto-detect via internet" } },
            { "st_city",         new[] { "المدينة", "City" } },
            { "st_search",       new[] { "بحث", "Search" } },
            { "st_general",      new[] { "عام", "General" } },

            { "g_close",         new[] { "إغلاق", "Close" } },
            { "g_ok",            new[] { "موافق", "OK" } },
        };
    }
}
