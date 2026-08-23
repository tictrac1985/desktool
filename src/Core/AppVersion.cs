namespace DeskMate.Core
{
    /// <summary>Single source of truth for the product version. This file is compiled into
    /// the app, the test harness AND the installer (see build.ps1), so the assembly version,
    /// the uninstall registry entry and the README can never drift apart again (they once
    /// shipped 1.4 binaries that registered themselves as 1.2.0).</summary>
    public static class AppVersion
    {
        public const string Text = "1.7.0";
        public const string FourPart = "1.7.0.0";
    }
}
