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

        /// <summary>Hijri month of "today", or 0 when the conversion fails.</summary>
        public static int HijriMonth(DateTime today)
        {
            try { return new UmAlQuraCalendar().GetMonth(today); }
            catch { return 0; }
        }

        /// <summary>Hijri day-of-month of "today", or 0 when the conversion fails.</summary>
        public static int HijriDay(DateTime today)
        {
            try { return new UmAlQuraCalendar().GetDayOfMonth(today); }
            catch { return 0; }
        }

        /// <summary>Nearest upcoming builtin occasion as a display-ready line.
        /// Null when nothing computes. Pure so SelfTest can pin the ordering.</summary>
        public static string NextLine(DateTime today, bool arabic)
        {
            string bestName = null;
            int bestDays = int.MaxValue;
            foreach (var oc in Builtin)
            {
                var next = NextHijri(oc.Month, oc.Day, today);
                if (next == null) continue;
                int left = DaysLeft(today, next.Value);
                if (left < bestDays)
                {
                    bestDays = left;
                    bestName = arabic ? oc.KeyAr : oc.KeyEn;
                }
            }
            if (bestName == null) return null;

            string days = bestDays <= 0
                ? (arabic ? "اليوم" : "today")
                : bestDays == 1 ? (arabic ? "غداً" : "tomorrow")
                : arabic ? string.Format("باقي {0} يوم", bestDays)
                         : string.Format("{0} days left", bestDays);
            return bestName + " · " + days;
        }

        /// <summary>The seasonal line shown on the widget face: Ramadan iftar/imsak
        /// countdown during the month, Eid greeting on the day itself, else the nearest
        /// occasion countdown. Returns null when there is nothing worth saying (no location
        /// for prayer-based times, or the user turned occasions off).</summary>
        public static SeasonInfo SeasonalLine(DateTime now, double[] prayerTimes, bool arabic, bool showOccasions)
        {
            int hm = HijriMonth(now), hd = HijriDay(now);

            // Eid al-Fitr day (Shawwal 1..3): a pure greeting, no countdown needed
            if (hm == 10 && hd <= 3 && showOccasions)
                return new SeasonInfo { Text = arabic ? "🌙 عيد فطر مبارك" : "🌙 Blessed Eid al-Fitr" };

            // Eid al-Adha day (Dhu al-Hijjah 10..12)
            if (hm == 12 && hd >= 10 && hd <= 12 && showOccasions)
                return new SeasonInfo { Text = arabic ? "🕋 عيد أضحى مبارك" : "🕋 Blessed Eid al-Adha" };

            // Ramadan: needs real prayer times for a live countdown; without them
            // (no location yet) still show a warm greeting rather than nothing.
            if (hm == 9 && showOccasions)
            {
                if (prayerTimes != null)
                {
                    double h = now.TimeOfDay.TotalHours;
                    double fajr = prayerTimes[0], maghrib = prayerTimes[4];
                    if (!double.IsNaN(maghrib) && h < maghrib && !double.IsNaN(fajr) && h >= fajr)
                    {
                        var left = now.Date.AddHours(maghrib) - now;
                        return new SeasonInfo
                        {
                            Text = (arabic ? "☪ الإفطار بعد " : "☪ Iftar in ")
                                   + string.Format("{0}:{1:00}", Math.Floor(left.TotalHours), left.Minutes),
                            IsRamadan = true,
                        };
                    }
                    if (!double.IsNaN(fajr) && h < fajr)
                    {
                        var left = now.Date.AddHours(fajr) - now;
                        return new SeasonInfo
                        {
                            Text = (arabic ? "☪ الإمساك بعد " : "☪ Imsak in ")
                                   + string.Format("{0}:{1:00}", Math.Floor(left.TotalHours), left.Minutes),
                            IsRamadan = true,
                        };
                    }
                }
                // after Maghrib / before Fajr window, or no location: greet
                return new SeasonInfo
                {
                    Text = arabic ? "☪ تقبّل الله طاعتكم" : "☪ May Allah accept your fast",
                    IsRamadan = true,
                };
            }

            // default: nearest occasion countdown (only when the tool is enabled)
            return showOccasions ? new SeasonInfo { Text = NextLine(now, arabic) } : null;
        }

        public class SeasonInfo
        {
            public string Text;
            public bool IsRamadan;
        }
    }
}
