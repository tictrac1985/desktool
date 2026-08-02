using System;

namespace DeskMate.Logic
{
    /// <summary>Auto day/night theme decision, pure and testable.</summary>
    public static class AutoThemeCore
    {
        public const int DayStartHour = 6, DayEndHour = 18;

        /// <summary>When auto is on, daylight hours resolve to the light palette (index 4);
        /// otherwise the user's chosen palette wins. Out-of-range choices clamp to 0 so a
        /// corrupt setting can never make the effective index differ from the applied one
        /// forever (that mismatch used to spin Reload until the process died).</summary>
        public static int EffectiveIndex(int chosen, bool auto, int hour, int lightIndex = 4)
        {
            if (chosen < 0 || chosen > 4) chosen = 0;
            if (!auto) return chosen;
            return hour >= DayStartHour && hour < DayEndHour ? lightIndex : chosen;
        }
    }
}
