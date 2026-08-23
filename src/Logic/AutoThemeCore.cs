using System;

namespace DeskMate.Logic
{
    /// <summary>Auto day/night theme decision, pure and testable.</summary>
    public static class AutoThemeCore
    {
        public const int DayStartHour = 6, DayEndHour = 18;

        /// <summary>When auto is on, daylight hours resolve to the user's chosen light
        /// palette (default 4 = Light for backward compatibility); night uses the chosen
        /// palette. Out-of-range choices clamp to 0 so a corrupt setting can never make the
        /// effective index differ from the applied one forever (that mismatch used to spin
        /// Reload until the process died). Bounds mirror Settings.Normalize: palettes now
        /// run 0..11.</summary>
        public static int EffectiveIndex(int chosen, int light, bool auto, int hour)
        {
            if (chosen < 0 || chosen > 11) chosen = 0;
            if (light < 0 || light > 11) light = 4;
            if (!auto) return chosen;
            return hour >= DayStartHour && hour < DayEndHour ? light : chosen;
        }

        /// <summary>Backward-compatible overload that uses the hardcoded light index 4.
        /// Existing callers that pre-date the user-pickable light palette keep working.</summary>
        public static int EffectiveIndex(int chosen, bool auto, int hour)
        {
            return EffectiveIndex(chosen, 4, auto, hour);
        }
    }
}
