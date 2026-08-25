using System.Collections.Generic;

namespace DeskMate.Logic
{
    /// <summary>Which collapsible section a tool belongs to when GroupTools is on.
    /// Pure and testable — the UI (MainWindow.Icons.cs) only renders what this decides.
    /// Group 0 = daily tools (things you touch many times a day: prayer, weather, tasks…),
    /// group 1 = system tools (cleaner, sleep timer, apps…). Settings is NEVER grouped:
    /// it is always pinned last so the user can reach it whatever is collapsed.</summary>
    public static class ToolGroups
    {
        public const int Daily = 0, System = 1, Ungrouped = -1;

        /// <summary>Maps a tool's label key (f_…) to its group. Unknown keys land in Daily:
        /// a tool added later without a mapping should show up front, not hide in the
        /// collapsible second section where nobody thinks to look.</summary>
        public static int GroupOf(string labelKey)
        {
            switch (labelKey)
            {
                case "f_cleaner":
                case "f_wallpaper":
                case "f_power":
                case "f_apps":
                case "f_sysmon":
                case "f_worldclock":
                    return System;
                case "f_settings":
                    return Ungrouped;
                default:
                    return Daily;   // f_prayer, f_weather, f_occasions, f_focus, f_note,
                                    // f_command, f_calc, f_clipboard, and anything new
            }
        }

        /// <summary>Splits an ordered list of label keys into (daily, system, pinned) keeping
        /// each tool's original position inside its group — the registry order is the user's
        /// muscle memory, grouping must not reshuffle it.</summary>
        public static void Split(IList<string> labelKeys,
            out List<int> daily, out List<int> system, out List<int> pinned)
        {
            daily = new List<int>();
            system = new List<int>();
            pinned = new List<int>();
            for (int i = 0; i < labelKeys.Count; i++)
            {
                switch (GroupOf(labelKeys[i]))
                {
                    case System: system.Add(i); break;
                    case Ungrouped: pinned.Add(i); break;
                    default: daily.Add(i); break;
                }
            }
        }
    }
}
