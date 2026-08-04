using System;

namespace DeskMate.Logic
{
    /// <summary>Local (offline) prayer time calculation using the standard PrayTimes.org
    /// astronomical formulas. Returns hours-of-day in the given timezone.</summary>
    public static class PrayerCalc
    {
        public enum Method { MWL = 0, Egyptian = 1, Karachi = 2, ISNA = 3, UmmAlQura = 4 }

        // fajr angle, isha angle (0 = fixed 90 min after maghrib for Umm al-Qura)
        static readonly double[][] Params =
        {
            new[] { 18.0, 17.0 },   // MWL
            new[] { 19.5, 17.5 },   // Egyptian
            new[] { 18.0, 18.0 },   // Karachi
            new[] { 15.0, 15.0 },   // ISNA
            new[] { 18.5, 0.0 },    // UmmAlQura (isha = maghrib + 90 min)
        };

        /// <summary>Applies the user's per-prayer correction in minutes to a computed set of
        /// times. Kept separate from the astronomy: the formulas are fixed and verified, while
        /// this is a preference — the local mosque's adhan is what people actually follow, and
        /// it differs from any calculation by a few minutes.
        /// NaN entries (polar days with no true sunrise) stay NaN, and results wrap inside the
        /// day so a correction near midnight cannot produce hour 24.5 or -0.3.</summary>
        public static double[] ApplyAdjust(double[] times, int[] minutes)
        {
            if (times == null) return null;
            var outT = new double[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                double t = times[i];
                if (minutes != null && i < minutes.Length && !double.IsNaN(t))
                {
                    t += minutes[i] / 60.0;
                    t %= 24.0;
                    if (t < 0) t += 24.0;
                }
                outT[i] = t;
            }
            return outT;
        }

        /// <summary>Pick a sensible default method from an ISO country code.</summary>
        public static Method AutoMethod(string country)
        {
            switch ((country ?? "").ToUpperInvariant())
            {
                case "EG": case "SD": case "LY": case "DZ": case "MA": case "TN": case "SY": case "IQ": case "LB": case "PS": case "JO":
                    return Method.Egyptian;
                case "SA":
                    return Method.UmmAlQura;
                case "US": case "CA": case "MX":
                    return Method.ISNA;
                case "PK": case "IN": case "BD": case "AF":
                    return Method.Karachi;
                default:
                    return Method.MWL;
            }
        }

        /// <summary>Times for a date at lat/lon with a fixed timezone offset (hours).
        /// Returns [Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha] as hours 0..24 (NaN when the sun
        /// never reaches the required angle, e.g. extreme latitudes).</summary>
        public static double[] Times(DateTime date, double lat, double lon, double tzOffsetHours, Method method)
        {
            double jdate = Julian(date.Year, date.Month, date.Day) - lon / (15.0 * 24.0);
            double fajrAngle = Params[(int)method][0];
            double ishaAngle = Params[(int)method][1];

            double dhuhr = MidDay(jdate, 12.0 / 24.0);
            double fajr = SunAngleTime(jdate, fajrAngle, 5.0 / 24.0, lat, true);
            double sunrise = SunAngleTime(jdate, 0.833, 6.0 / 24.0, lat, true);
            double asr = AsrTime(jdate, 1.0, 13.0 / 24.0, lat);
            double maghrib = SunAngleTime(jdate, 0.833, 18.0 / 24.0, lat, false);
            double isha = ishaAngle > 0
                ? SunAngleTime(jdate, ishaAngle, 18.0 / 24.0, lat, false)
                : maghrib + 1.5; // 90 minutes

            var raw = new[] { fajr, sunrise, dhuhr, asr, maghrib, isha };
            var result = new double[6];
            for (int i = 0; i < 6; i++)
            {
                double t = raw[i];
                if (double.IsNaN(t)) { result[i] = double.NaN; continue; }
                result[i] = FixHour(t + tzOffsetHours - lon / 15.0);
            }
            return result;
        }

        public static string FormatTime(double hours, bool use24h)
        {
            if (double.IsNaN(hours)) return "--:--";
            int totalMin = (int)Math.Round(hours * 60.0);
            totalMin = ((totalMin % 1440) + 1440) % 1440;
            int h = totalMin / 60, m = totalMin % 60;
            if (use24h) return string.Format("{0:00}:{1:00}", h, m);
            string suffix = h < 12 ? "AM" : "PM";
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            return string.Format("{0}:{1:00} {2}", h12, m, suffix);
        }

        // ---------------- astronomy ----------------

        static double Julian(int year, int month, int day)
        {
            if (month <= 2) { year -= 1; month += 12; }
            double a = Math.Floor(year / 100.0);
            double b = 2 - a + Math.Floor(a / 4.0);
            return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
        }

        // sun declination (deg) and equation of time (hours) at julian date jd
        static void SunPosition(double jd, out double decl, out double eqt)
        {
            double d = jd - 2451545.0;
            double g = FixAngle(357.529 + 0.98560028 * d);
            double q = FixAngle(280.459 + 0.98564736 * d);
            double l = FixAngle(q + 1.915 * DSin(g) + 0.020 * DSin(2 * g));
            double e = 23.439 - 0.00000036 * d;
            decl = DArcSin(DSin(e) * DSin(l));
            double ra = DArcTan2(DCos(e) * DSin(l), DCos(l)) / 15.0;
            ra = FixHour(ra);
            eqt = q / 15.0 - ra;
            // keep eqt in a small range around 0
            if (eqt > 12) eqt -= 24;
            if (eqt < -12) eqt += 24;
        }

        static double MidDay(double jdate, double time)
        {
            double decl, eqt;
            SunPosition(jdate + time, out decl, out eqt);
            return FixHour(12.0 - eqt);
        }

        /// <summary>Time when the sun is 'angle' degrees below the horizon; ccw = before noon.</summary>
        static double SunAngleTime(double jdate, double angle, double time, double lat, bool ccw)
        {
            double decl, eqt;
            SunPosition(jdate + time, out decl, out eqt);
            double noon = FixHour(12.0 - eqt);
            double cosArg = (-DSin(angle) - DSin(decl) * DSin(lat)) / (DCos(decl) * DCos(lat));
            if (cosArg < -1 || cosArg > 1) return double.NaN;
            double t = (1.0 / 15.0) * DArcCos(cosArg);
            return noon + (ccw ? -t : t);
        }

        static double AsrTime(double jdate, double factor, double time, double lat)
        {
            double decl, eqt;
            SunPosition(jdate + time, out decl, out eqt);
            double noon = FixHour(12.0 - eqt);
            double angle = -DArcCot(factor + DTan(Math.Abs(lat - decl)));
            double cosArg = (-DSin(angle) - DSin(decl) * DSin(lat)) / (DCos(decl) * DCos(lat));
            if (cosArg < -1 || cosArg > 1) return double.NaN;
            double t = (1.0 / 15.0) * DArcCos(cosArg);
            return noon + t;
        }

        // ---------------- degree-based trig ----------------
        static double Dtr(double d) { return d * Math.PI / 180.0; }
        static double Rtd(double r) { return r * 180.0 / Math.PI; }
        static double DSin(double d) { return Math.Sin(Dtr(d)); }
        static double DCos(double d) { return Math.Cos(Dtr(d)); }
        static double DTan(double d) { return Math.Tan(Dtr(d)); }
        static double DArcSin(double x) { return Rtd(Math.Asin(x)); }
        static double DArcCos(double x) { return Rtd(Math.Acos(x)); }
        static double DArcTan2(double y, double x) { return Rtd(Math.Atan2(y, x)); }
        static double DArcCot(double x) { return Rtd(Math.Atan(1.0 / x)); }
        static double FixAngle(double a) { a = a % 360.0; return a < 0 ? a + 360.0 : a; }
        static double FixHour(double h) { h = h % 24.0; return h < 0 ? h + 24.0 : h; }
    }
}
