using System;
using System.Globalization;

namespace DeskMate.Logic
{
    /// <summary>Hijri & Gregorian occasion countdowns (Ramadan, the two Eids, custom dates).
    /// Hijri conversion uses UmAlQuraCalendar, matching the widget's Hijri date line.</summary>
    public static class OccasionCore
    {
        public class Occasion
        {
            public string KeyAr, KeyEn;
            public int Month, Day;
            public bool Hijri = true;
        }

        public static readonly Occasion[] Builtin =
        {
            new Occasion { KeyAr = "رمضان",        KeyEn = "Ramadan",     Month = 9,  Day = 1 },
            new Occasion { KeyAr = "عيد الفطر",    KeyEn = "Eid al-Fitr", Month = 10, Day = 1 },
            new Occasion { KeyAr = "عيد الأضحى",   KeyEn = "Eid al-Adha", Month = 12, Day = 10 },
        };

        /// <summary>Next Gregorian calendar date carrying the given Hijri month/day,
        /// today or in the future. Null when the conversion is unavailable.</summary>
        public static DateTime? NextHijri(int month, int day, DateTime today)
        {
            try
            {
                var cal = new UmAlQuraCalendar();
                int hy = cal.GetYear(today);
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        var d = cal.ToDateTime(hy + i, month, day, 0, 0, 0, 0);
                        if (d.Date >= today.Date) return d;
                    }
                    catch { /* invalid hijri date in that year (e.g. day 30 of a 29-day month) */ }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Next occurrence of a Gregorian month/day, rolling to next year when past.</summary>
        public static DateTime? NextGregorian(int month, int day, DateTime today)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var d = new DateTime(today.Year + i, month, day);
                    if (d.Date >= today.Date) return d;
                }
                catch { return null; }
            }
            return null;
        }

        public static int DaysLeft(DateTime today, DateTime target)
        {
            int n = (int)(target.Date - today.Date).TotalDays;
            return n < 0 ? 0 : n;
        }
    }
}
