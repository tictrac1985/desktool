using System;

namespace DeskMate.Logic
{
    /// <summary>One global shortcut the command bar can answer to.</summary>
    public class HotkeyChoice
    {
        public uint Modifiers, Key;
        public string Label;
        public HotkeyChoice(uint modifiers, uint key, string label) { Modifiers = modifiers; Key = key; Label = label; }
    }

    /// <summary>The shortcuts offered for summoning the command bar.
    ///
    /// Up to v1.11 the shortcut was fixed at Win+Alt+D — a combination Windows 10 and 11
    /// reserve for the taskbar clock flyout. Registration therefore failed on every start
    /// (one user's log recorded it 115 times in a row) and the feature never worked. The
    /// shortcut is now a setting, and its default — "automatic" — walks a list of
    /// combinations Windows does not use and takes the first one that is free.</summary>
    public static class HotkeyCore
    {
        public const uint ModAlt = 0x0001, ModCtrl = 0x0002, ModShift = 0x0004, ModWin = 0x0008;
        const uint VkSpace = 0x20, VkD = 0x44, VkK = 0x4B, VkBacktick = 0xC0;

        /// <summary>Index 0 is "automatic"; the last entry (no key) turns the shortcut off.
        /// Indices are persisted in Settings.Hotkey — append, never reorder.</summary>
        public static readonly HotkeyChoice[] Choices =
        {
            new HotkeyChoice(0, 0, "auto"),
            new HotkeyChoice(ModCtrl | ModAlt, VkSpace, "Ctrl + Alt + Space"),
            new HotkeyChoice(ModCtrl | ModAlt, VkD, "Ctrl + Alt + D"),
            new HotkeyChoice(ModCtrl | ModShift, VkSpace, "Ctrl + Shift + Space"),
            new HotkeyChoice(ModAlt, VkBacktick, "Alt + `"),
            new HotkeyChoice(ModCtrl | ModAlt, VkK, "Ctrl + Alt + K"),
            new HotkeyChoice(ModWin | ModAlt, VkD, "Win + Alt + D"),
            new HotkeyChoice(0, 0, "off"),
        };

        public const int Auto = 0;
        public static int Off { get { return Choices.Length - 1; } }

        /// <summary>The order "automatic" tries. Win+Alt+D is deliberately absent.</summary>
        public static readonly int[] AutoOrder = { 1, 2, 3, 5, 4 };

        /// <summary>The candidates to try, in order, for a stored setting: the user's explicit
        /// pick alone, the automatic list, or nothing at all when switched off or invalid.</summary>
        public static int[] Candidates(int setting)
        {
            if (setting == Auto) return AutoOrder;
            if (setting <= 0 || setting >= Off) return new int[0];
            return new[] { setting };
        }
    }
}
