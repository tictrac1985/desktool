namespace DeskMate.Core
{
    /// <summary>Single source of truth for the product version. This file is compiled into
    /// the app, the test harness AND the installer (see build.ps1), so the assembly version,
    /// the uninstall registry entry and the README can never drift apart again (they once
    /// shipped 1.4 binaries that registered themselves as 1.2.0).</summary>
    public static class AppVersion
    {
        /// <summary>The version people say out loud. Bump it for a release worth naming.</summary>
        public const string Text = "1.12.0";

        /// <summary>Text plus the build number — the number of commits on the branch the build
        /// was made from, stamped by build.ps1 into obj\BuildStamp.cs. The same commit
        /// therefore carries the same four-part version whether it was built on a developer's
        /// machine or by the GitHub workflow, and every later commit carries a higher one.
        /// That ordering is what the in-app updater compares (see Logic.UpdateCore).</summary>
        public const string FourPart = Text + "." + BuildStamp.Revision;
    }
}
