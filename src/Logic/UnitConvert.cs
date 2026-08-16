using System;

namespace DeskMate.Logic
{
    /// <summary>Everyday unit conversions for the calculator's units ribbon. Each entry is
    /// a chip that converts the number currently on the display. Kept in Logic (not in the
    /// panel) so the conversion factors are testable without a UI.</summary>
    public static class UnitConvert
    {
        /// <summary>Chip labels, Arabic. Symbols (كم / °س …) stay readable at chip size where
        /// "كيلومتر إلى ميل" would not.</summary>
        public static readonly string[] Labels =
        {
            "كم ← ميل",   // 0: km → mi
            "ميل ← كم",   // 1: mi → km
            "كجم ← رطل",  // 2: kg → lb
            "رطل ← كجم",  // 3: lb → kg
            "م ← قدم",    // 4: m → ft
            "قدم ← م",    // 5: ft → m
            "°س ← °ف",    // 6: °F → °C  (value in Fahrenheit shown, converted to Celsius)
            "°ف ← °س",    // 7: °C → °F
        };

        /// <summary>Converts <paramref name="v"/> with the conversion at
        /// <paramref name="idx"/>. Out-of-range indices return the value unchanged — a
        /// mistuned ribbon must never eat the user's number.</summary>
        public static double Apply(int idx, double v)
        {
            switch (idx)
            {
                case 0: return v * 0.621371;            // km → mi
                case 1: return v / 0.621371;            // mi → km
                case 2: return v * 2.2046226218;        // kg → lb
                case 3: return v / 2.2046226218;        // lb → kg
                case 4: return v * 3.280839895;         // m → ft
                case 5: return v / 3.280839895;         // ft → m
                case 6: return (v - 32.0) * 5.0 / 9.0;  // °F → °C
                case 7: return v * 9.0 / 5.0 + 32.0;    // °C → °F
                default: return v;
            }
        }
    }
}
