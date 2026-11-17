using System;
using System.Collections.Generic;
using System.Globalization;

namespace DeskMate.Logic
{
    /// <summary>One cell of the month grid, carrying both calendars.</summary>
    public class CalDay
    {
        public DateTime Date;
        public int HDay, HMonth, HYear;     // 0 when the date is outside the Umm al-Qura range
        /// <summary>False for the lead-in / lead-out cells borrowed from neighbouring months.</summary>
        public bool InMonth;
        public bool IsToday;
        public bool IsWeekend;
        /// <summary>Occasion falling on this day, already in the requested language; null = none.</summary>
        public string Occasion;
    }

    /// <summary>The month being shown: a fixed 6×7 grid plus its two titles.</summary>
    public class CalMonth
    {
        public CalDay[] Cells = new CalDay[42];
        /// <summary>Primary title ("نوفمبر 2026" or "جمادى الأولى 1448").</summary>
        public string Title = "";
        /// <summary>The other calendar's span over the same days ("جمادى الأولى – جمادى الآخرة 1448").</summary>
        public string SubTitle = "";
        /// <summary>The month's own coordinates, for stepping to the next / previous one.</summary>
        public int Year, Month;
        public bool Hijri;
    }

    /// <summary>Builds the Hijri/Gregorian month grid. Hijri dates come from the Umm al-Qura
    /// calendar — the civil calendar of Saudi Arabia — exactly like the widget's date line, so
    /// the two can never disagree. Pure: the panel only paints what this returns.</summary>
    public static class CalendarCore
    {
        public static readonly string[] HijriMonthsAr =
        {
            "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
            "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة"
        };
        public static readonly string[] HijriMonthsEn =
        {
            "Muharram", "Safar", "Rabi I", "Rabi II", "Jumada I", "Jumada II",
            "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qi'dah", "Dhu al-Hijjah"
        };
        static readonly string[] GregMonthsAr =
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };
        static readonly string[] GregMonthsEn =
        {
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December"
        };
        // indexed by DayOfWeek (Sunday = 0)
        static readonly string[] DayShortAr = { "أحد", "اثنين", "ثلاثاء", "أربعاء", "خميس", "جمعة", "سبت" };
        static readonly string[] DayShortEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
        static readonly string[] DayLongAr = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
        static readonly string[] DayLongEn = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        static readonly UmAlQuraCalendar Hc = new UmAlQuraCalendar();

        public static string GregMonthName(int month, bool arabic)
        {
            return month >= 1 && month <= 12 ? (arabic ? GregMonthsAr : GregMonthsEn)[month - 1] : "";
        }

        public static string HijriMonthName(int month, bool arabic)
        {
            return month >= 1 && month <= 12 ? (arabic ? HijriMonthsAr : HijriMonthsEn)[month - 1] : "";
        }

        public static string DayShort(DayOfWeek d, bool arabic) { return (arabic ? DayShortAr : DayShortEn)[(int)d]; }
        public static string DayLong(DayOfWeek d, bool arabic) { return (arabic ? DayLongAr : DayLongEn)[(int)d]; }

        /// <summary>Hijri date of a Gregorian day. False outside the range the Umm al-Qura
        /// tables cover (1900–2077), where the grid simply shows no Hijri number.</summary>
        public static bool TryHijri(DateTime date, out int year, out int month, out int day)
        {
            year = month = day = 0;
            try
            {
                if (date < Hc.MinSupportedDateTime || date > Hc.MaxSupportedDateTime) return false;
                year = Hc.GetYear(date); month = Hc.GetMonth(date); day = Hc.GetDayOfMonth(date);
                return true;
            }
            catch { return false; }
        }

        public static bool TryFromHijri(int year, int month, int day, out DateTime date)
        {
            date = DateTime.MinValue;
            try
            {
                if (month < 1 || month > 12 || day < 1 || day > 30) return false;
                if (day > Hc.GetDaysInMonth(year, month)) return false;
                date = Hc.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Weekend convention by country: Friday–Saturday across the Arab world,
        /// Saturday–Sunday elsewhere. No country set → follows the interface language.</summary>
        public static bool IsWeekend(DayOfWeek d, string countryIso2, bool arabicUi)
        {
            bool friSat;
            switch ((countryIso2 ?? "").ToUpperInvariant())
            {
                case "SA": case "EG": case "AE": case "KW": case "QA": case "BH": case "OM": case "JO":
                case "IQ": case "SY": case "YE": case "LY": case "DZ": case "SD": case "PS":
                    friSat = true; break;
                case "": friSat = arabicUi; break;
                default: friSat = false; break;
            }
            return friSat ? (d == DayOfWeek.Friday || d == DayOfWeek.Saturday)
                          : (d == DayOfWeek.Saturday || d == DayOfWeek.Sunday);
        }

        /// <summary>The seven column headers in display order, starting at <paramref name="weekStart"/>.</summary>
        public static DayOfWeek[] Columns(DayOfWeek weekStart)
        {
            var cols = new DayOfWeek[7];
            for (int i = 0; i < 7; i++) cols[i] = (DayOfWeek)(((int)weekStart + i) % 7);
            return cols;
        }

        // ---------------- occasions ----------------

        /// <summary>The occasions marked on the grid. Hijri ones are the fixed dates of the
        /// Islamic year; they follow the calculated calendar and may differ from the
        /// announced date by a day, which the panel says in its footnote.</summary>
        public static string OccasionFor(DateTime date, int hMonth, int hDay, string countryIso2, bool arabic)
        {
            if (hMonth == 1 && hDay == 1) return arabic ? "رأس السنة الهجرية" : "Islamic New Year";
            if (hMonth == 1 && hDay == 10) return arabic ? "عاشوراء" : "Ashura";
            if (hMonth == 9 && hDay == 1) return arabic ? "أول رمضان" : "Ramadan begins";
            if (hMonth == 10 && hDay == 1) return arabic ? "عيد الفطر" : "Eid al-Fitr";
            if (hMonth == 12 && hDay == 9) return arabic ? "يوم عرفة" : "Day of Arafah";
            if (hMonth == 12 && hDay == 10) return arabic ? "عيد الأضحى" : "Eid al-Adha";

            if (string.Equals(countryIso2, "SA", StringComparison.OrdinalIgnoreCase))
            {
                if (date.Month == 2 && date.Day == 22) return arabic ? "يوم التأسيس" : "Founding Day";
                if (date.Month == 9 && date.Day == 23) return arabic ? "اليوم الوطني" : "Saudi National Day";
            }
            return null;
        }

        // ---------------- grids ----------------

        /// <summary>A Gregorian month laid out on 6 weeks.</summary>
        public static CalMonth BuildGregorian(int year, int month, DayOfWeek weekStart, DateTime today,
                                              string countryIso2, bool arabic)
        {
            var first = new DateTime(year, month, 1);
            var cm = new CalMonth { Year = year, Month = month, Hijri = false };
            cm.Title = GregMonthName(month, arabic) + " " + year.ToString(CultureInfo.InvariantCulture);
            Fill(cm, first, d => d.Year == year && d.Month == month, weekStart, today, countryIso2, arabic);
            cm.SubTitle = HijriSpan(cm, arabic);
            return cm;
        }

        /// <summary>A Hijri month laid out on 6 weeks. Null when the month is outside the
        /// supported range.</summary>
        public static CalMonth BuildHijri(int hYear, int hMonth, DayOfWeek weekStart, DateTime today,
                                          string countryIso2, bool arabic)
        {
            DateTime first;
            if (!TryFromHijri(hYear, hMonth, 1, out first)) return null;
            var cm = new CalMonth { Year = hYear, Month = hMonth, Hijri = true };
            cm.Title = HijriMonthName(hMonth, arabic) + " " + hYear.ToString(CultureInfo.InvariantCulture)
                       + (arabic ? " هـ" : " AH");
            Fill(cm, first, d =>
            {
                int y, m, dd;
                return TryHijri(d, out y, out m, out dd) && y == hYear && m == hMonth;
            }, weekStart, today, countryIso2, arabic);
            cm.SubTitle = GregSpan(cm, arabic);
            return cm;
        }

        static void Fill(CalMonth cm, DateTime first, Func<DateTime, bool> inMonth, DayOfWeek weekStart,
                         DateTime today, string country, bool arabic)
        {
            int lead = ((int)first.DayOfWeek - (int)weekStart + 7) % 7;
            DateTime start = first.AddDays(-lead);
            for (int i = 0; i < 42; i++)
            {
                DateTime d = start.AddDays(i);
                var c = new CalDay { Date = d, InMonth = inMonth(d), IsToday = d.Date == today.Date };
                TryHijri(d, out c.HYear, out c.HMonth, out c.HDay);
                c.IsWeekend = IsWeekend(d.DayOfWeek, country, arabic);
                c.Occasion = OccasionFor(d, c.HMonth, c.HDay, country, arabic);
                cm.Cells[i] = c;
            }
        }

        /// <summary>"جمادى الأولى – جمادى الآخرة 1448 هـ": the Hijri months a Gregorian month touches.</summary>
        static string HijriSpan(CalMonth cm, bool arabic)
        {
            int m1 = 0, y1 = 0, m2 = 0, y2 = 0;
            foreach (var c in cm.Cells)
            {
                if (!c.InMonth || c.HMonth == 0) continue;
                if (m1 == 0) { m1 = c.HMonth; y1 = c.HYear; }
                m2 = c.HMonth; y2 = c.HYear;
            }
            if (m1 == 0) return "";
            string suffix = arabic ? " هـ" : " AH";
            if (m1 == m2 && y1 == y2) return HijriMonthName(m1, arabic) + " " + y1 + suffix;
            if (y1 == y2) return HijriMonthName(m1, arabic) + " – " + HijriMonthName(m2, arabic) + " " + y1 + suffix;
            return HijriMonthName(m1, arabic) + " " + y1 + " – " + HijriMonthName(m2, arabic) + " " + y2 + suffix;
        }

        static string GregSpan(CalMonth cm, bool arabic)
        {
            DateTime a = DateTime.MinValue, b = DateTime.MinValue;
            foreach (var c in cm.Cells)
            {
                if (!c.InMonth) continue;
                if (a == DateTime.MinValue) a = c.Date;
                b = c.Date;
            }
            if (a == DateTime.MinValue) return "";
            if (a.Month == b.Month && a.Year == b.Year) return GregMonthName(a.Month, arabic) + " " + a.Year;
            if (a.Year == b.Year) return GregMonthName(a.Month, arabic) + " – " + GregMonthName(b.Month, arabic) + " " + a.Year;
            return GregMonthName(a.Month, arabic) + " " + a.Year + " – " + GregMonthName(b.Month, arabic) + " " + b.Year;
        }

        /// <summary>Steps a (year, month) pair by ±n months in either calendar.</summary>
        public static void Step(ref int year, ref int month, int delta)
        {
            int idx = year * 12 + (month - 1) + delta;
            year = idx / 12;
            month = idx % 12 + 1;
        }

        /// <summary>Both dates of one day, spelled out — the line under the grid.</summary>
        public static string Describe(CalDay day, bool arabic)
        {
            string g = arabic
                ? DayLong(day.Date.DayOfWeek, true) + "، " + day.Date.Day + " " + GregMonthName(day.Date.Month, true) + " " + day.Date.Year
                : DayLong(day.Date.DayOfWeek, false) + ", " + GregMonthName(day.Date.Month, false) + " " + day.Date.Day + ", " + day.Date.Year;
            if (day.HMonth == 0) return g;
            string h = arabic
                ? day.HDay + " " + HijriMonthName(day.HMonth, true) + " " + day.HYear + " هـ"
                : HijriMonthName(day.HMonth, false) + " " + day.HDay + ", " + day.HYear + " AH";
            return g + "  ·  " + h;
        }

        /// <summary>"اليوم" / "بعد 12 يوماً" / "قبل 3 أيام" relative to today.</summary>
        public static string Relative(DateTime day, DateTime today, bool arabic)
        {
            int n = (int)(day.Date - today.Date).TotalDays;
            if (n == 0) return arabic ? "اليوم" : "Today";
            if (n == 1) return arabic ? "غداً" : "Tomorrow";
            if (n == -1) return arabic ? "أمس" : "Yesterday";
            int a = Math.Abs(n);
            if (arabic)
            {
                string unit = a == 2 ? "يومين" : a <= 10 ? a + " أيام" : a + " يوماً";
                return (n > 0 ? "بعد " : "قبل ") + unit;
            }
            return n > 0 ? "in " + a + " days" : a + " days ago";
        }
    }
}
