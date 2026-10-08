using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>The interface typeface. v1.11 ships Tajawal (SIL OFL 1.1, see
    /// assets\fonts\OFL.txt) inside the exe: the app always asked for it first, but on a
    /// machine without it installed WPF silently dropped to Segoe UI — so the font the widget
    /// was designed around never actually showed. The four weights are embedded as manifest
    /// resources, written once to %LocalAppData%\DeskMate\fonts and loaded from there as a
    /// private family: nothing is installed system-wide and no admin rights are needed.
    ///
    /// Any failure (locked profile, missing resource, a font WPF refuses) falls back to the
    /// system UI font, so the widget can never end up without text.</summary>
    public static class AppFonts
    {
        static readonly string[] Files =
        {
            "Tajawal-Light.ttf", "Tajawal-Regular.ttf", "Tajawal-Medium.ttf", "Tajawal-Bold.ttf",
        };

        static FontFamily _bundled, _bundledLight;
        static bool _tried;
        static readonly FontFamily SystemLight = new FontFamily("Segoe UI Light, Segoe UI, Tahoma");

        /// <summary>System fallback chain, also used for glyphs Tajawal lacks.</summary>
        public static readonly FontFamily SystemUi = new FontFamily("Segoe UI, Tahoma, Arial");

        /// <summary>True when the bundled family loaded and the user has not opted out.</summary>
        public static bool UsingBundled { get; private set; }

        /// <summary>The family every text element should use: bundled Tajawal when it loads
        /// (and FontChoice is 0), otherwise the system UI font.</summary>
        public static FontFamily Ui
        {
            get
            {
                var b = Settings.I.FontChoice == 0 ? Bundled() : null;
                UsingBundled = b != null;
                return b ?? SystemUi;
            }
        }

        /// <summary>The display family for big digits — the same typeface as <see cref="Ui"/>,
        /// a separate instance so callers can tell it apart and ask for the Light weight.</summary>
        public static FontFamily Light
        {
            get
            {
                var b = Settings.I.FontChoice == 0 ? Bundled() : null;
                return b != null ? _bundledLight : SystemLight;
            }
        }

        static FontFamily Bundled()
        {
            if (_tried) return _bundled;
            _tried = true;
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeskMate", "fonts", "v1");
                Directory.CreateDirectory(dir);
                var asm = Assembly.GetExecutingAssembly();
                int ready = 0;
                foreach (var f in Files)
                {
                    string target = Path.Combine(dir, f);
                    try
                    {
                        using (var s = asm.GetManifestResourceStream("DeskMate.Fonts." + f))
                        {
                            // rewrite only when absent or different — the folder is shared by
                            // every launch and a running instance may hold the files open
                            if (s != null && (!File.Exists(target) || new FileInfo(target).Length != s.Length))
                            {
                                using (var o = File.Create(target)) s.CopyTo(o);
                            }
                        }
                    }
                    catch (Exception ex) { Log.Error("font " + f, ex); }   // a held file is still usable
                    if (File.Exists(target)) ready++;
                }
                if (ready == 0) return null;
                var baseUri = new Uri(dir.TrimEnd('\\') + "\\");
                // the trailing system names are the per-glyph fallback (symbols, ☾, ★ …)
                var fam = new FontFamily(baseUri, "./#Tajawal, Segoe UI, Tahoma");
                // make sure WPF actually found the faces before committing to the family
                if (Fonts.GetTypefaces(baseUri).Count == 0) return null;
                _bundled = fam;
                _bundledLight = new FontFamily(baseUri, "./#Tajawal, Segoe UI Light, Segoe UI");
            }
            catch (Exception ex)
            {
                Log.Error("fonts", ex);
                _bundled = null;
            }
            return _bundled;
        }
    }
}
