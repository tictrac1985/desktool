using System;

namespace DeskMate.Logic
{
    /// <summary>Curated city list + timezone math for the world-clock tool.</summary>
    public static class WorldClockCore
    {
        public class City
        {
            public string TzId { get; set; }
            public string Ar { get; set; }
            public string En { get; set; }
            /// <summary>ComboBox display text in the active language.</summary>
            public override string ToString() { return DeskMate.Core.L.IsAr ? Ar : En; }
        }

        public static readonly City[] Cities =
        {
            new City { TzId = "Arab Standard Time",      Ar = "مكة المكرمة",   En = "Makkah" },
            new City { TzId = "Egypt Standard Time",     Ar = "القاهرة",       En = "Cairo" },
            new City { TzId = "Arabian Standard Time",   Ar = "دبي",           En = "Dubai" },
            new City { TzId = "Jordan Standard Time",    Ar = "عمّان",          En = "Amman" },
            new City { TzId = "Turkey Standard Time",    Ar = "إسطنبول",      En = "Istanbul" },
            new City { TzId = "GMT Standard Time",       Ar = "لندن",          En = "London" },
            new City { TzId = "Romance Standard Time",   Ar = "باريس",         En = "Paris" },
            new City { TzId = "W. Europe Standard Time", Ar = "برلين",         En = "Berlin" },
            new City { TzId = "Eastern Standard Time",   Ar = "نيويورك",       En = "New York" },
            new City { TzId = "Pacific Standard Time",   Ar = "لوس أنجلوس",    En = "Los Angeles" },
            new City { TzId = "Tokyo Standard Time",     Ar = "طوكيو",         En = "Tokyo" },
            new City { TzId = "Singapore Standard Time", Ar = "كوالالمبور",    En = "Kuala Lumpur" },
        };

        public static City Find(string tzId)
        {
            foreach (var c in Cities)
                if (c.TzId == tzId) return c;
            return null;
        }

        /// <summary>Current wall-clock time in a Windows timezone. Null when the id is unknown
        /// on this machine.</summary>
        public static DateTime? TimeIn(string tzId, DateTime utcNow)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), tz);
            }
            catch { return null; }
        }

        /// <summary>Calendar-day difference between the remote city and the local machine:
        /// -1 = still yesterday there, 0 = same day, +1 = already tomorrow.</summary>
        public static int DayDiff(string tzId, DateTime utcNow, DateTime localNow)
        {
            var remote = TimeIn(tzId, utcNow);
            if (remote == null) return 0;
            return (int)(remote.Value.Date - localNow.Date).TotalDays;
        }
    }
}
