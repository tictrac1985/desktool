using System;

namespace DeskMate.Logic
{
    /// <summary>Moon phase computation. The synodic month is 29.530588853 days on average;
    /// a known new moon (2000-01-06 18:14 UTC, the most widely used epoch) anchors the cycle.
    /// Pure arithmetic — no I/O — so SelfTest can exercise it without WPF.</summary>
    public static class MoonPhase
    {
        /// <summary>Reference new moon: 2000-01-06 18:14 UTC ≈ 2000-01-06 21:14 Arabia.</summary>
        static readonly DateTime Epoch = new DateTime(2000, 1, 6, 21, 14, 0, DateTimeKind.Utc);

        /// <summary>Mean synodic month length in days (mean of the Saros-scale average).</summary>
        public const double SynodicDays = 29.530588853;

        /// <summary>Fractional position through the current lunation at the given date:
        /// 0 = new moon, 0.5 = full moon, back to ~1.0 at the next new. Wraps into [0,1).</summary>
        public static double AgeFraction(DateTime now)
        {
            var utc = now.ToUniversalTime();
            double days = (utc - Epoch).TotalDays % SynodicDays;
            if (days < 0) days += SynodicDays;
            return days / SynodicDays;
        }

        /// <summary>Illuminated share of the disc (0..1), peaking at full moon.
        /// The real curve is a sine over the phase angle; that is close enough for a widget.</summary>
        public static double Illumination(DateTime now)
        {
            return (1 - Math.Cos(2 * Math.PI * AgeFraction(now))) / 2;
        }

        /// <summary>Phase name buckets. Eight classical phases with the four principal ones
        /// getting narrow windows and the intermediates taking the rest:</summary>
        public const int NewMoon = 0, WaxingCrescent = 1, FirstQuarter = 2, WaxingGibbous = 3,
                        FullMoon = 4, WaningGibbous = 5, LastQuarter = 6, WaningCrescent = 7;

        /// <summary>The eight-bucket phase index for the given moment. Principal phases get
        /// ±~0.98 day windows (fraction ±0.0166); crescents/gibbous split what remains,
        /// waxing before the full-moon half, waning after.</summary>
        public static int PhaseIndex(DateTime now)
        {
            // offset so bucket boundaries land symmetrically around each principal phase
            double f = AgeFraction(now);
            if (f < 1.855 / SynodicDays || f > (SynodicDays - 1.855) / SynodicDays) return NewMoon;
            if (Math.Abs(f - 0.25) * SynodicDays <= 1.855) return FirstQuarter;
            if (Math.Abs(f - 0.50) * SynodicDays <= 1.855) return FullMoon;
            if (Math.Abs(f - 0.75) * SynodicDays <= 1.855) return LastQuarter;
            return f < 0.25 ? WaxingCrescent
                 : f < 0.50 ? WaxingGibbous
                 : f < 0.75 ? WaningGibbous
                 : WaningCrescent;
        }

        /// <summary>Waxing = growing toward full (phase between new and full).</summary>
        public static bool IsWaxing(DateTime now) { return AgeFraction(now) < 0.5; }

        /// <summary>Localised Arabic name of today's phase.</summary>
        public static string NameAr(DateTime now)
        {
            switch (PhaseIndex(now))
            {
                case NewMoon: return "محاق";
                case FirstQuarter: return "التربيع الأول";
                case FullMoon: return "بدر";
                case LastQuarter: return "التربيع الأخير";
                default: return IsWaxing(now) ? "هلال متزايد" : "هلال متضائل";
            }
        }

        /// <summary>English name of today's phase.</summary>
        public static string NameEn(DateTime now)
        {
            switch (PhaseIndex(now))
            {
                case NewMoon: return "New Moon";
                case FirstQuarter: return "First Quarter";
                case FullMoon: return "Full Moon";
                case LastQuarter: return "Last Quarter";
                default: return IsWaxing(now) ? "Waxing Crescent" : "Waning Crescent";
            }
        }
    }
}
