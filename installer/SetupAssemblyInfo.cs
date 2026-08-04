using System.Reflection;
using System.Runtime.InteropServices;
using DeskMate.Core;

// The installer had no assembly metadata at all, so it shipped as version 0.0.0.0 while the
// app it carried reported 1.4.2 — an unsigned executable with no version is exactly what a
// cautious user (and SmartScreen) treats as suspicious. Version comes from AppVersion, the
// same constant the app and the uninstall registry entry use.
[assembly: AssemblyTitle("DeskMate Setup")]
[assembly: AssemblyProduct("DeskMate")]
[assembly: AssemblyDescription("Installer for DeskMate desktop widget")]
[assembly: AssemblyCompany("DeskMate")]
[assembly: AssemblyCopyright("© 2026 DeskMate")]
[assembly: AssemblyVersion(AppVersion.FourPart)]
[assembly: AssemblyFileVersion(AppVersion.FourPart)]
[assembly: ComVisible(false)]
