using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>Shared visual language. All brushes are (re)assigned by Apply(themeIndex);
    /// UI is rebuilt after a theme change so factories pick up the new palette.</summary>
    public static class Theme
    {
        public class Palette
        {
            public string KeyAr, KeyEn;
            public Color Accent;
            public Color CardTop, CardBottom;
            /// <summary>Optional deep end of the widget's background gradient. Null = derive
            /// from CardBottom (slightly darker), which is right for almost every palette.</summary>
            public Color? CardDeep;
            /// <summary>Secondary hue the gradient blends toward — gives each theme a real
            /// colour journey across the widget face instead of one flat tint. Null = a
            /// hue-shifted sibling of CardTop, so every theme gains depth for free.</summary>
            public Color? GlowTop;
            public bool Light;
        }

        public static readonly Palette[] Palettes =
        {
            new Palette { KeyAr = "كهرماني", KeyEn = "Amber",
                Accent = Color.FromRgb(0xFF, 0xC6, 0x6E),
                CardTop = Color.FromArgb(0xD6, 0x23, 0x2A, 0x3B), CardBottom = Color.FromArgb(0xDE, 0x0D, 0x11, 0x19) },
            new Palette { KeyAr = "أزرق", KeyEn = "Ocean",
                Accent = Color.FromRgb(0x6E, 0xC1, 0xFF),
                CardTop = Color.FromArgb(0xD6, 0x1D, 0x2D, 0x44), CardBottom = Color.FromArgb(0xDE, 0x0A, 0x12, 0x1E) },
            new Palette { KeyAr = "زمردي", KeyEn = "Emerald",
                Accent = Color.FromRgb(0x7B, 0xE3, 0xA6),
                CardTop = Color.FromArgb(0xD6, 0x19, 0x2D, 0x23), CardBottom = Color.FromArgb(0xDE, 0x08, 0x13, 0x0D) },
            new Palette { KeyAr = "وردي", KeyEn = "Rose",
                Accent = Color.FromRgb(0xFF, 0x9F, 0xB8),
                CardTop = Color.FromArgb(0xD6, 0x30, 0x1D, 0x2B), CardBottom = Color.FromArgb(0xDE, 0x17, 0x0B, 0x13) },
            new Palette { KeyAr = "فاتح", KeyEn = "Light", Light = true,
                Accent = Color.FromRgb(0xB8, 0x6B, 0x00),
                CardTop = Color.FromArgb(0xEE, 0xF8, 0xFB, 0xFE), CardBottom = Color.FromArgb(0xF2, 0xE0, 0xE7, 0xF1) },
            // --- new palettes (indices 5..7). Light=false; same IsLight branch as the
            //     existing dark themes, so no additional Apply code is needed.
            new Palette { KeyAr = "صحراء", KeyEn = "Sahara",
                // navy + gold — the user-told taste (blue + gold), so this one matters
                Accent = Color.FromRgb(0xE6, 0xC0, 0x6A),
                CardTop = Color.FromArgb(0xD6, 0x10, 0x1B, 0x33), CardBottom = Color.FromArgb(0xDE, 0x06, 0x0E, 0x1F) },
            new Palette { KeyAr = "حبري", KeyEn = "Mono Ink",
                // high-contrast black/white minimalist: deep black + bright accent
                Accent = Color.FromRgb(0xE0, 0xE0, 0xE0),
                CardTop = Color.FromArgb(0xD6, 0x14, 0x14, 0x16), CardBottom = Color.FromArgb(0xDE, 0x05, 0x05, 0x07) },
            new Palette { KeyAr = "شفق", KeyEn = "Aurora",
                // cool gradient: cyan + violet on a deep teal body
                Accent = Color.FromRgb(0x7D, 0xF9, 0xFF),
                CardTop = Color.FromArgb(0xD6, 0x0E, 0x23, 0x34), CardBottom = Color.FromArgb(0xDE, 0x10, 0x07, 0x1B) },
            // --- v1.7 palettes (indices 8..11): four more moods. Dark ones reuse the same
            //     IsLight=false branch as every other dark theme; Sand rides the existing
            //     Light branch, so Apply needs no new code.
            new Palette { KeyAr = "بنفسجي", KeyEn = "Violet",
                Accent = Color.FromRgb(0xC9, 0xB6, 0xFF),
                CardTop = Color.FromArgb(0xD6, 0x27, 0x1C, 0x40), CardBottom = Color.FromArgb(0xDE, 0x11, 0x09, 0x21) },
            new Palette { KeyAr = "نحاسي", KeyEn = "Copper",
                Accent = Color.FromRgb(0xFF, 0xA5, 0x64),
                CardTop = Color.FromArgb(0xD6, 0x32, 0x23, 0x1A), CardBottom = Color.FromArgb(0xDE, 0x18, 0x0E, 0x07) },
            new Palette { KeyAr = "رملي", KeyEn = "Sand", Light = true,
                Accent = Color.FromRgb(0xA0, 0x65, 0x00),
                CardTop = Color.FromArgb(0xEE, 0xFB, 0xF6, 0xEC), CardBottom = Color.FromArgb(0xF2, 0xEF, 0xE4, 0xCF) },
            new Palette { KeyAr = "ياقوتي", KeyEn = "Ruby",
                Accent = Color.FromRgb(0xFF, 0x8A, 0x96),
                CardTop = Color.FromArgb(0xD6, 0x31, 0x15, 0x22), CardBottom = Color.FromArgb(0xDE, 0x16, 0x07, 0x0E) },
            // --- v1.10 (index 12): a true DAY theme. Light and Sand are neutral; Daylight
            //     is colourful — sky-blue-tinted glass, a saturated azure accent, and the
            //     gradient journey drifts toward a warm morning glow so the face reads as
            //     daytime, not just "not dark". Panels, inputs and popups follow the same
            //     light branch as every Light palette, so the whole app is bright, not only
            //     the widget face.
            new Palette { KeyAr = "نهاري", KeyEn = "Daylight", Light = true,
                Accent = Color.FromRgb(0x00, 0x78, 0xD4),
                CardTop = Color.FromArgb(0xEE, 0xF3, 0xFA, 0xFF), CardBottom = Color.FromArgb(0xF2, 0xD3, 0xE6, 0xF6) },
        };

        /// <summary>Index of the palette that was actually applied (differs from the chosen
        /// one when auto day/night theme is active, or when Material You overrides).</summary>
        public static int AppliedIndex { get; private set; }

        /// <summary>True while a Material-You-derived palette is overriding the normal
        /// palette choice. The widget tree is rebuilt so every control picks up the new
        /// brushes; the check box in settings reflects this so the user can turn it off.</summary>
        public static bool MaterialYouActive;

        public static bool IsLight;
        public static Color AccentColor;
        public static Brush Accent, Text, TextDim, TextFaint, CardBg, CardBorder, HoverBg, ButtonBg, InputBg;
        public static Brush DialStroke, DialFill;
        /// <summary>Soft accent-tinted fill for chips, selected rows and operator keys.</summary>
        public static Brush AccentSoft;
        /// <summary>Slightly stronger border for buttons and keys.</summary>
        public static Brush ButtonBorder;
        /// <summary>Top-light gradient painted inside cards to fake glass thickness.</summary>
        public static Brush GlassTop;

        // ---------------- full-surface gradients (v1.9) ----------------
        // The widget used to paint every surface with translucent whites/greys over ONE
        // card gradient; the theme only reached the buttons as a border colour. Now the
        // palette drives a real colour system: each theme derives its own gradient family
        // and every surface — cards, keys, inputs, wells, chips — carries it.

        /// <summary>The card body gradient: palette top → glow hue → deep end. This is what
        /// makes the widget face itself carry the theme's colours instead of one flat tint.</summary>
        public static Brush CardGradient;
        /// <summary>Raised key/button face: light rolls in from above across the theme hues.</summary>
        public static Brush KeyGradient;
        /// <summary>Hover state of the same surface: slightly brighter, still in-palette.</summary>
        public static Brush KeyGradientHover;
        /// <summary>Sunken wells: inputs, calculator display, recessed trays.</summary>
        public static Brush InsetGradient;
        /// <summary>Soft wash for chips and secondary surfaces — accent tinted, very low alpha.</summary>
        public static Brush AccentWash;
        /// <summary>Accent gradient for primary (filled) buttons — bright top → base accent.</summary>
        public static Brush AccentGradient;

        /// <summary>Default UI font. Tajawal is the user-preferred Arabic typeface; we list
        /// it first so it wins on systems that have it installed. The fallback list
        /// (Segoe UI → Segoe UI Light → Tahoma → Arial) keeps the widget readable on
        /// any Windows machine that doesn't have Tajawal — WPF silently drops missing
        /// families, and the first one that resolves is the one used.</summary>
        public static FontFamily UiFont = AppFonts.Ui;
        /// <summary>Same family as <see cref="UiFont"/> but a distinct instance, so the text
        /// factories can recognise it and apply the Light weight (big clock digits).</summary>
        public static FontFamily LightFont = AppFonts.Light;
        public static readonly FontWeight LightWeight = FontWeights.Light;

        /// <summary>Tajawal draws its Arabic noticeably smaller than Segoe UI at the same em
        /// size, so small UI text (labels, rows, captions) is set a notch larger with it. Big
        /// display digits are left alone — Latin numerals match across the two families.</summary>
        public static double TextScale = 1.0;

        /// <summary>Re-reads the font choice. Called from Apply, i.e. before every rebuild.</summary>
        public static void RefreshFonts()
        {
            UiFont = AppFonts.Ui;
            LightFont = AppFonts.Light;
            TextScale = AppFonts.UsingBundled ? 1.08 : 1.0;
        }

        /// <summary>Applies <see cref="TextScale"/> to small text only, and floors captions at
        /// 11.5 px — the 10–11 px hints and badges scattered through the panels were below
        /// what reads comfortably on a translucent surface.</summary>
        public static double Fs(double size)
        {
            if (size >= 24) return size;
            return Math.Round(Math.Max(size, 11.5) * TextScale * 2) / 2;
        }
        public static readonly FontFamily Mdl2 = new FontFamily("Segoe MDL2 Assets");
        public static readonly FontFamily SymbolFont = new FontFamily("Segoe UI Symbol");

        static Theme() { Apply(0); }

        /// <summary>Apply a Material-You-derived palette: takes the place of the normal
        /// palette lookup, and the rest of the brush-assignment code in <see cref="Apply"/>
        /// is reused so the chrome (text colour, button background, shadows) stays in sync.
        /// Sets <see cref="MaterialYouActive"/> so the widget knows to re-derive on
        /// wallpaper change.
        ///
        /// Picks dark text on a light card when the extracted card colour is bright
        /// (e.g. white/cream wallpaper), and white text on a dark card otherwise. The first
        /// version always assumed a dark card and produced white text — invisible on the
        /// very wallpapers a "from wallpaper" theme is most likely to match.</summary>
        public static void ApplyMaterial(Color accent, Color cardTop, Color cardBottom, int markAppliedIndex = 0)
        {
            var p = new Palette
            {
                KeyAr = "من الخلفية", KeyEn = "From wallpaper",
                Accent = accent, CardTop = cardTop, CardBottom = cardBottom,
            };
            MaterialYouActive = true;
            RefreshFonts();
            // AppliedIndex is left at whatever the previous value was: Material You is a
            // runtime override, not a "real" palette. The settings panel reads
            // MaterialYouActive to render the swatch row in its "off / overridden" state.
            var prev = AppliedIndex;
            // Perceived brightness of the card body. 0.5 is the boundary between dark and
            // light text; below it we keep the dark-card look, above it we invert to a
            // light card so white text never sits on a white background.
            double lum = (0.2126 * p.CardBottom.R + 0.7152 * p.CardBottom.G + 0.0722 * p.CardBottom.B) / 255.0;
            bool isLightCard = lum > 0.55;
            p.Light = isLightCard;
            IsLight = isLightCard;
            AccentColor = p.Accent;
            Accent = Freeze(new SolidColorBrush(p.Accent));
            AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb(0x24, p.Accent.R, p.Accent.G, p.Accent.B)));
            if (isLightCard)
            {
                // light card branch — mirror the Light palette so the same shadows, borders
                // and button backgrounds work, but keep the user-chosen accent (lifted in
                // MaterialYouCore.BuildPalette) for the digits and highlights.
                Text = Freeze(new SolidColorBrush(Color.FromArgb(0xE8, 0x16, 0x1E, 0x2A)));
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0x16, 0x1E, 0x2A)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x8E, 0x16, 0x1E, 0x2A)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x10, 0x18, 0x24)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3C, 0x10, 0x18, 0x24)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x10, 0x18, 0x24)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x13, 0x10, 0x18, 0x24)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0x10, 0x18, 0x24)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0x16, 0x1E, 0x2A)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x10, 0x10, 0x18, 0x24)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
                var rim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF), 0));
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0x18, 0x10, 0x18, 0x24), 0.35));
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x10, 0x18, 0x24), 1));
                RimBrush = Freeze(rim);
            }
            else
            {
                // dark-card path — original behaviour.
                Text = Brushes.White;
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xD6, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x9A, 0xFF, 0xFF, 0xFF)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2C, 0xFF, 0xFF, 0xFF)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x00, 0x00, 0x00)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
                var rim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0x7A, 255, 255, 255), 0));
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0x20, 255, 255, 255), 0.35));
                rim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
                RimBrush = Freeze(rim);
            }
            CardBg = Freeze(new LinearGradientBrush(p.CardTop, p.CardBottom, 90));
            BuildGradients(p.CardTop, p.CardBottom, p.Light);
            // the card body itself now rides the full three-stop journey
            var journey = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            journey.GradientStops.Add(new GradientStop(p.CardTop, 0));
            journey.GradientStops.Add(new GradientStop(Mix(p.CardTop, HueShift(p.CardTop, 26), 0.55), 0.55));
            journey.GradientStops.Add(new GradientStop(Mix(p.CardBottom, Colors.Black, p.Light ? 0.06 : 0.22), 1));
            CardBg = Freeze(journey);
            _elevation = null;
            ControlStyles.Invalidate();
            // mark AppliedIndex so the next tick does NOT take the "eff != AppliedIndex"
            // branch and rebuild the visual tree every second. Without this, every tick
            // recreated the clock object and froze its display (the user reported that
            // the clock looked stuck on a single value while Material You was on).
            AppliedIndex = markAppliedIndex;
        }

        public static void Apply(int index)
        {
            if (index < 0 || index >= Palettes.Length) index = 0;
            RefreshFonts();
            AppliedIndex = index;
            MaterialYouActive = false;   // normal Apply always clears the Material You override
            var p = Palettes[index];
            IsLight = p.Light;
            AccentColor = p.Accent;
            Accent = Freeze(new SolidColorBrush(p.Accent));
            AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb((byte)(p.Light ? 0x20 : 0x24), p.Accent.R, p.Accent.G, p.Accent.B)));

            if (p.Light)
            {
                Text = Freeze(new SolidColorBrush(Color.FromArgb(0xE8, 0x16, 0x1E, 0x2A)));
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0x16, 0x1E, 0x2A)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x8E, 0x16, 0x1E, 0x2A)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x10, 0x18, 0x24)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3C, 0x10, 0x18, 0x24)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x26, 0x10, 0x18, 0x24)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x13, 0x10, 0x18, 0x24)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0x10, 0x18, 0x24)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x60, 0x10, 0x18, 0x24)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x10, 0x10, 0x18, 0x24)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x5A, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
            }
            else
            {
                Text = Brushes.White;
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xD6, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x9A, 0xFF, 0xFF, 0xFF)));
                CardBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x2C, 0xFF, 0xFF, 0xFF)));
                ButtonBorder = Freeze(new SolidColorBrush(Color.FromArgb(0x3A, 0xFF, 0xFF, 0xFF)));
                HoverBg = Freeze(new SolidColorBrush(Color.FromArgb(0x34, 0xFF, 0xFF, 0xFF)));
                ButtonBg = Freeze(new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)));
                InputBg = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, 0x00, 0x00, 0x00)));
                DialStroke = Freeze(new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)));
                DialFill = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));
                GlassTop = Freeze(new LinearGradientBrush(
                    Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
                    new Point(0, 0), new Point(0, 0.42)));
            }

            CardBg = Freeze(new LinearGradientBrush(p.CardTop, p.CardBottom, 90));

            // v1.9: derive the full gradient family from THIS palette, honouring its
            // optional GlowTop / CardDeep overrides so each theme can steer its own journey.
            BuildGradients(p.CardTop, p.CardBottom, p.Light);
            // the card body itself now rides the full three-stop journey
            var journey = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            journey.GradientStops.Add(new GradientStop(p.CardTop, 0));
            journey.GradientStops.Add(new GradientStop(Mix(p.CardTop, HueShift(p.CardTop, 26), 0.55), 0.55));
            journey.GradientStops.Add(new GradientStop(Mix(p.CardBottom, Colors.Black, p.Light ? 0.06 : 0.22), 1));
            CardBg = Freeze(journey);

            // crisp 1px specular edge: bright where light lands on the top curve, gone by the
            // time it reaches the sides. This is the line that reads as a real lit edge —
            // the broad Bevel gradient alone looks like a printed highlight, not geometry.
            var rim = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(IsLight ? (byte)0xC0 : (byte)0x7A, 255, 255, 255), 0));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(IsLight ? (byte)0x30 : (byte)0x20, 255, 255, 255), 0.35));
            rim.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            RimBrush = Freeze(rim);

            _elevation = null;   // opacities depend on IsLight — rebuild lazily per palette
            ControlStyles.Invalidate();
        }

        // ---------------- full-surface gradient factory ----------------

        /// <summary>Blends two colours. a=0 keeps c0, a=1 lands on c1; alpha blends along.</summary>
        static Color Mix(Color c0, Color c1, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromArgb(
                (byte)Math.Round(c0.A + (c1.A - c0.A) * t),
                (byte)Math.Round(c0.R + (c1.R - c0.R) * t),
                (byte)Math.Round(c0.G + (c1.G - c0.G) * t),
                (byte)Math.Round(c0.B + (c1.B - c0.B) * t));
        }

        /// <summary>Nudges a colour's hue by ±degrees on the colour wheel while keeping its
        /// lightness roughly stable. This is how every theme gets a second hue for its
        /// gradient journey without hand-picking one: amber drifts toward rose, ocean toward
        /// violet, emerald toward teal — and the widget stops being one flat colour.</summary>
        static Color HueShift(Color c, double degrees)
        {
            // HSL round trip via System.Drawing-free math
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2;
            double h = 0, s = 0;
            if (max > min)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
                else if (max == g) h = ((b - r) / d + 2) / 6;
                else h = ((r - g) / d + 4) / 6;
            }
            h = (h + degrees / 360.0) % 1; if (h < 0) h += 1;
            // HSL → RGB, the real hue2rgb: the previous shortcut (returning l for the
            // plateau and l·s·6t for ramps) only stayed hidden because dark palettes have
            // l < 0.5 and modest s; a near-white card top (Daylight) has s ≈ 1, and the
            // shortcut flipped it to full magenta. q/p are the two edge intensities the
            // hue triangle interpolates between.
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p2 = 2 * l - q;
            Func<double, double> hue2 = t =>
            {
                if (t < 0) t += 1; if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p2 + (q - p2) * 6 * t;
                if (t < 1.0 / 2) return q;
                if (t < 2.0 / 3) return p2 + (q - p2) * (2.0 / 3 - t) * 6;
                return p2;
            };
            double rr = s == 0 ? l : hue2(h + 1.0 / 3);
            double gg = s == 0 ? l : hue2(h);
            double bb = s == 0 ? l : hue2(h - 1.0 / 3);
            byte A = c.A;
            return Color.FromArgb(A, (byte)Math.Round(rr * 255), (byte)Math.Round(gg * 255), (byte)Math.Round(bb * 255));
        }

        /// <summary>Builds the shared gradient family for the applied palette:
        /// CardGradient (the widget face itself — top → shifted glow → deep),
        /// KeyGradient/KeyGradientHover (raised surfaces riding the same hues),
        /// InsetGradient (sunken wells), AccentWash (chips), AccentGradient (primary buttons).
        /// Every theme therefore paints ALL of its surfaces with its own colours.</summary>
        static void BuildGradients(Color top, Color bottom, bool light)
        {
            // second hue: the card top drifted 26° around the wheel — amber→rose, ocean→violet…
            Color glow = HueShift(top, 26);

            // face: three stops — palette top at the crown, glow through the middle,
            // deep end below CardBottom so the base feels heavier than before.
            var face = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            face.GradientStops.Add(new GradientStop(top, 0));
            face.GradientStops.Add(new GradientStop(Mix(top, glow, 0.55), 0.55));
            face.GradientStops.Add(new GradientStop(Mix(bottom, Colors.Black, light ? 0.06 : 0.22), 1));
            CardGradient = Freeze(face);

            // raised keys: translucent whites over the face used to be the only "colour".
            // Now keys carry a real in-palette ramp: lifted glow → card bottom, denser alpha
            // than before so the tint actually reads, with the hover state a touch brighter.
            var keyTop = Mix(bottom, Colors.White, light ? 0.55 : 0.16);
            var keyBot = Mix(bottom, glow, 0.30);
            var key = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            key.GradientStops.Add(new GradientStop(keyTop, 0));
            key.GradientStops.Add(new GradientStop(keyBot, 1));
            KeyGradient = Freeze(key);

            var keyHoverTop = Mix(keyTop, Colors.White, light ? 0.25 : 0.10);
            var keyHoverBot = Mix(keyBot, glow, 0.18);
            var keyH = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            keyH.GradientStops.Add(new GradientStop(keyHoverTop, 0));
            keyH.GradientStops.Add(new GradientStop(keyHoverBot, 1));
            KeyGradientHover = Freeze(keyH);

            // wells: darker than the face with a cool inner shade pooling at the top
            var inset = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            inset.GradientStops.Add(new GradientStop(Mix(Colors.Black, glow, 0.14), 0));
            inset.GradientStops.Add(new GradientStop(Mix(bottom, Colors.Black, light ? 0.10 : 0.34), 1));
            InsetGradient = Freeze(inset);

            var washC = Color.FromArgb(light ? (byte)0x1E : (byte)0x24, AccentColor.R, AccentColor.G, AccentColor.B);
            var wash = new RadialGradientBrush { Center = new Point(0.5, 0.5), RadiusX = 0.9, RadiusY = 0.9 };
            wash.GradientStops.Add(new GradientStop(washC, 0));
            wash.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            AccentWash = Freeze(wash);

            // primary button: bright crown → pure accent, the classic lit cap
            var acc = new LinearGradientBrush(Lift(AccentColor, 42, 38, 32), AccentColor, 90);
            AccentGradient = Freeze(acc);
        }

        // ---------------- elevation ----------------

        /// <summary>Specular edge brush used by <see cref="RimLight"/>.</summary>
        public static Brush RimBrush;

        static DropShadowEffect[] _elevation;
        static bool _elevationSaver;

        /// <summary>Drops the cached shadow ramp so the next request rebuilds it. Called on a
        /// palette change and when power saving turns on or off.</summary>
        public static void InvalidateEffects() { _elevation = null; }

        /// <summary>One shadow ramp for the whole app, so a key, a card and a floating panel
        /// sit at visibly different heights instead of each picking its own numbers.
        /// 1 = resting key · 2 = raised surface · 3 = floating above the desktop.
        /// Effects are frozen and shared: a DropShadowEffect per control is measurable cost
        /// on a widget that rebuilds its whole tree on every settings change.</summary>
        public static DropShadowEffect Elevation(int level)
        {
            bool saver = Settings.SaverActive;
            if (_elevation == null || saver != _elevationSaver)
            {
                _elevationSaver = saver;
                // Cost scales with the blur radius, not with the shadow's presence, so power
                // saving keeps the depth cue and shrinks the radius instead of dropping the
                // shadow altogether — the widget still sits on the desktop, it just stops
                // paying for a wide Gaussian on every repaint.
                double k = saver ? 0.35 : 1.0;
                _elevation = new[]
                {
                    MakeShadow(6 * k, 1.5, IsLight ? 0.20 : 0.32),
                    // raised surface: a second offset layer is faked by a deeper blur+depth
                    // so cards visibly "sit" on the desktop instead of printing onto it
                    MakeShadow(16 * k, 4, IsLight ? 0.26 : 0.48),
                    MakeShadow(34 * k, 9, IsLight ? 0.34 : 0.62),
                };
            }
            return _elevation[Math.Max(1, Math.Min(3, level)) - 1];
        }

        static DropShadowEffect MakeShadow(double blur, double depth, double opacity)
        {
            var e = new DropShadowEffect
            {
                Color = Colors.Black, BlurRadius = blur, ShadowDepth = depth,
                Opacity = opacity, Direction = 270,
            };
            e.Freeze();
            return e;
        }

        /// <summary>Overlay that draws the specular edge. Sits above the fill and below the
        /// content, so text is never dimmed by it.</summary>
        public static Border RimLight(double radius)
        {
            return new Border
            {
                CornerRadius = new CornerRadius(radius),
                BorderThickness = new Thickness(1),
                BorderBrush = RimBrush,
                IsHitTestVisible = false,
            };
        }

        /// <summary>Top-lit face for a raised key. The key fills are translucent white (or
        /// translucent accent) over the card, so "catching the light" means the top of the cap
        /// is denser and the bottom thinner — brightening RGB does nothing to a fill that is
        /// already pure white. Varying alpha is what actually reads as a curved surface.</summary>
        public static Brush KeyFace(Color c, int lift = 16)
        {
            var top = Color.FromArgb((byte)Math.Min(255, c.A + lift), c.R, c.G, c.B);
            var bottom = Color.FromArgb((byte)Math.Max(0, c.A - lift / 2), c.R, c.G, c.B);
            return Freeze(new LinearGradientBrush(top, bottom, 90));
        }

        static Brush Freeze(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }

        /// <summary>Brightens a colour, keeping its alpha. Preserving alpha matters: the button
        /// and key fills are translucent whites over the card, and dropping alpha here turned a
        /// 12%-white keycap into solid white.</summary>
        static Color Lift(Color c, int dr, int dg, int db)
        {
            return Color.FromArgb(c.A,
                (byte)Math.Min(255, c.R + dr), (byte)Math.Min(255, c.G + dg), (byte)Math.Min(255, c.B + db));
        }

        // ---------------- 3D depth ----------------

        /// <summary>Raised-surface bevel overlay: light rolls in from the top edge and
        /// shade pools at the bottom. Lay it over any filled surface to lift it.</summary>
        public static Border Bevel(double radius)
        {
            byte hi = IsLight ? (byte)0x55 : (byte)0x1F;
            byte lo = IsLight ? (byte)0x14 : (byte)0x30;
            var br = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            br.GradientStops.Add(new GradientStop(Color.FromArgb(hi, 255, 255, 255), 0));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.42));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.62));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(lo, 0, 0, 0), 1));
            return new Border
            {
                CornerRadius = new CornerRadius(radius),
                IsHitTestVisible = false,
                Background = br,
            };
        }

        /// <summary>Carved-in bevel overlay: shade sinks in from the top edge with a faint
        /// light line at the bottom — for recessed wells (calculator display, inputs).</summary>
        public static Border InsetBevel(double radius)
        {
            byte hi = IsLight ? (byte)0x18 : (byte)0x38;
            byte lo = IsLight ? (byte)0x40 : (byte)0x14;
            var br = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            br.GradientStops.Add(new GradientStop(Color.FromArgb(hi, 0, 0, 0), 0));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.30));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.80));
            br.GradientStops.Add(new GradientStop(Color.FromArgb(lo, 255, 255, 255), 1));
            return new Border
            {
                CornerRadius = new CornerRadius(radius),
                IsHitTestVisible = false,
                Background = br,
            };
        }

        // ---------------- cards & chrome ----------------

        /// <summary>Glass card, built in depth order: gradient body · broad sheen across the
        /// upper third · bevel (top light, bottom shade) · crisp specular rim on the edge ·
        /// content · deep shadow. The sheen and the rim are what separate "a rounded
        /// rectangle with a shadow" from a surface that looks like it has thickness.
        /// v1.9: the body is the palette's full CardGradient journey, not a two-stop fade.</summary>
        public static Border MakeCard(UIElement content, double radius = 24)
        {
            double inner = Math.Max(1, radius - 1);
            var layers = new Grid();

            var sheen = new Border
            {
                CornerRadius = new CornerRadius(inner),
                Background = GlassTop,
                IsHitTestVisible = false,
                Margin = new Thickness(1),
            };
            layers.Children.Add(sheen);

            // edge light: a short, crisp streak of pure light along the top curve, fading to
            // nothing at both ends. Real objects catch a bright line where the surface turns
            // toward the light — this 1.6px streak is what makes the card read as touchable
            // rather than printed.
            var edge = new Border
            {
                Height = 1.6,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(radius * 0.9, 1.1, radius * 0.9, 0),
                CornerRadius = new CornerRadius(1),
                IsHitTestVisible = false,
            };
            var eb = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            byte edgeA = IsLight ? (byte)0x99 : (byte)0x55;
            eb.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            eb.GradientStops.Add(new GradientStop(Color.FromArgb(edgeA, 255, 255, 255), 0.28));
            eb.GradientStops.Add(new GradientStop(Color.FromArgb(edgeA, 255, 255, 255), 0.72));
            eb.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            edge.Background = eb;
            layers.Children.Add(edge);

            var bevel = Bevel(inner);
            bevel.Margin = new Thickness(1);
            layers.Children.Add(bevel);


            var rim = RimLight(radius);
            layers.Children.Add(rim);
            // padding belongs to the content layer, not the card: the sheen, bevel and rim
            // have to reach the card's own edge, and an outer Padding would inset them all
            var pad = new Border
            {
                Padding = new Thickness(18, 14, 18, 14),
                Child = content,
            };
            layers.Children.Add(pad);

            return new Border
            {
                Background = CardBg,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Child = layers,
                Effect = Elevation(3),
                Margin = new Thickness(12), // room for the shadow inside the transparent window
                // callers reshape the card through SetCardPadding / SetCardRadius — touching
                // Padding or CornerRadius directly leaves these light layers behind
                Tag = new CardParts { Pad = pad, Sheen = sheen, Bevel = bevel, Rim = rim, Edge = edge },
            };
        }

        /// <summary>The layers of a card that have to be reshaped together. Kept in the card's
        /// Tag so a caller can resize or re-pad the card without the sheen, bevel and rim
        /// silently keeping the original geometry — which drew a rounded SQUARE rim inside the
        /// orbit layout's circular clock card.</summary>
        class CardParts
        {
            public Border Pad, Sheen, Bevel, Rim, Edge;
        }

        /// <summary>Adjusts the inner spacing of a card made by <see cref="MakeCard"/>.
        /// Use instead of <c>card.Padding</c>, which would inset the card's own light layers.</summary>
        public static void SetCardPadding(Border card, Thickness padding)
        {
            var p = card.Tag as CardParts;
            if (p != null) p.Pad.Padding = padding;
        }

        /// <summary>Reshapes a card, keeping its light layers in step. Use instead of setting
        /// <c>card.CornerRadius</c>, which changes only the outline.</summary>
        public static void SetCardRadius(Border card, double radius)
        {
            card.CornerRadius = new CornerRadius(radius);
            var p = card.Tag as CardParts;
            if (p == null) return;
            double inner = Math.Max(1, radius - 1);
            p.Sheen.CornerRadius = new CornerRadius(inner);
            p.Bevel.CornerRadius = new CornerRadius(inner);
            p.Rim.CornerRadius = new CornerRadius(radius);
            // The edge streak is a straight 1.6px line inset by the corner radius. On a
            // rounded rectangle that is the flat top edge; on a circle (Orbit / Halo cores)
            // there is no flat edge at all, so a line inset by the OLD radius ran straight
            // across the top of the disc and out past both sides of it — the stray bar the
            // user saw over the clock. A large radius means a round card: the curved rim
            // light already does the job there, so the streak simply goes.
            if (p.Edge != null)
            {
                if (radius > 40) p.Edge.Visibility = Visibility.Collapsed;
                else
                {
                    p.Edge.Visibility = Visibility.Visible;
                    p.Edge.Margin = new Thickness(radius * 0.9, 1.1, radius * 0.9, 0);
                }
            }
        }

        /// <summary>Full-width hairline used to separate panel sections.</summary>
        public static Border Divider(double topMargin = 8, double bottomMargin = 8)
        {
            return new Border
            {
                Height = 1,
                Background = CardBorder,
                Margin = new Thickness(0, topMargin, 0, bottomMargin),
            };
        }

        /// <summary>Hairline under a panel header: a soft accent-tinted glow that peaks at
        /// the centre and dissolves at both ends — direction-agnostic, so RTL and LTR get
        /// the same quiet underline, and the header reads as a lit sign rather than a rule.</summary>
        public static Border AccentHairline(double topMargin = 0, double bottomMargin = 8)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            b.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x66, AccentColor.R, AccentColor.G, AccentColor.B), 0.5));
            b.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            return new Border
            {
                Height = 1.4,
                Background = Freeze(b),
                Margin = new Thickness(0, topMargin, 0, bottomMargin),
            };
        }

        /// <summary>A list row with a quiet rounded surface: faint fill, hairline border and
        /// a hover lift, so lists read as a stack of tactile chips instead of floating text
        /// lines. <paramref name="highlighted"/> marks the "active" row (next prayer, current
        /// zone) with the accent wash instead of the neutral fill.</summary>
        public static Border RowSurface(UIElement child, bool highlighted = false)
        {
            Brush bg = highlighted ? AccentSoft : ButtonBg;
            var b = new Border
            {
                Background = bg,
                BorderBrush = highlighted ? Accent : CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 5.5, 10, 5.5),
                Margin = new Thickness(0, 2.5, 0, 2.5),
                SnapsToDevicePixels = true,
                Child = child,
            };
            if (!highlighted) AttachHover(b, bg, HoverBg);
            return b;
        }

        // ---------------- 3D monolith helpers ----------------

        /// <summary>Radial inner glow for the Monolith face: bright accent-tinted light
        /// pooling behind the clock centre, fading to transparent — reads as a lit well
        /// inside the slab rather than a flat fill.</summary>
        public static Brush MakeInnerGlow()
        {
            var b = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.5, 0.32),
                Center = new Point(0.5, 0.38),
                RadiusX = 0.85, RadiusY = 0.7,
            };
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x2E,
                AccentColor.R, AccentColor.G, AccentColor.B), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(0x10,
                AccentColor.R, AccentColor.G, AccentColor.B), 0.45));
            b.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            return Freeze(b);
        }

        /// <summary>The extruded side of the Monolith: a vertical shade from the card's
        /// bottom colour down to near-black, faking a thick edge catching little light.</summary>
        public static Brush PedestalBrush()
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            if (IsLight)
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xD9, 0xE2, 0xEC), 0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0xFF, 0xB4, 0xC0, 0xCE), 1));
            }
            else
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(0xE8, 0x08, 0x0B, 0x12), 0));
                b.GradientStops.Add(new GradientStop(Colors.Black, 1));
            }
            return Freeze(b);
        }

        /// <summary>Small rounded-square accent-tinted badge holding a glyph (panel headers).
        /// v1.9: the chip itself carries the accent wash gradient so it reads as a lit tile
        /// rather than a flat square.</summary>
        public static Border MakeGlyphChip(string glyph, double size = 26)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.32),
                Background = AccentWash ?? AccentSoft,
                BorderBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x3A, AccentColor.R, AccentColor.G, AccentColor.B))),
                BorderThickness = new Thickness(1),
                Child = MakeGlyph(glyph, size * 0.48, Accent),
            };
        }

        // ---------------- text ----------------

        public static TextBlock MakeText(string text, double size, Brush brush = null, FontFamily font = null)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = Fs(size),
                Foreground = brush ?? Text,
                FontFamily = font ?? UiFont,
                // Arabic must ALWAYS shape and order RTL regardless of the container's
                // flow direction — a stray LTR ancestor used to mirror word order
                FlowDirection = FlowDirection.RightToLeft,
            };
            if (ReferenceEquals(font, LightFont)) tb.FontWeight = LightWeight;
            return tb;
        }

        public static TextBlock MakeGlyph(string glyph, double size, Brush brush = null)
        {
            return new TextBlock
            {
                Text = glyph,
                FontSize = size,
                Foreground = brush ?? Text,
                FontFamily = Mdl2,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }

        // ---------------- tool cell: icon + permanent label ----------------

        /// <summary>Width a tool cell gets when there is room. Sized so the longest Arabic
        /// tool names ("الآلة الحاسبة", "مواقيت الصلاة") fit on one or two lines inside their
        /// own cell — the desktop has space to spare, and a cramped row is the first thing
        /// that makes the widget look unfinished.</summary>
        public const double DefaultCellWidth = 86;

        /// <summary>Clear gap between neighbouring cells, so labels never appear to touch.</summary>
        public const double CellGap = 6;

        /// <summary>Per-tool identity colours. Each glyph rides its own hue family — cleaner
        /// in teal, calculator in amber, prayer in green, weather in sky blue… — so the icon
        /// row reads as a set of coloured objects rather than one grey mass. Every colour is
        /// derived per theme at draw time (see ToolTint) so it stays inside the palette.</summary>
        public static readonly (double Hue, double Sat, double Light)[] ToolHues =
        {
            (174, 0.62, 0.60),   // Cleaner  — teal
            (38,  0.90, 0.66),   // Calc     — amber
            (280, 0.55, 0.70),   // Wallpaper— violet
            (8,   0.75, 0.64),   // Power    — warm red
            (200, 0.55, 0.68),   // Clipboard— steel blue
            (205, 0.85, 0.62),   // Weather  — sky blue
            (262, 0.50, 0.72),   // Apps     — indigo
            (145, 0.60, 0.58),   // Prayer   — green
            (330, 0.70, 0.66),   // Focus    — pink
            (48,  0.85, 0.62),   // Occasions— gold
            (52,  0.95, 0.72),   // Note     — sticky yellow
            (150, 0.45, 0.62),   // SysMon   — sage
            (190, 0.65, 0.66),   // WorldClock — cyan
            (256, 0.65, 0.72),   // Command  — periwinkle
            (220, 0.20, 0.78),   // Settings — neutral silver-blue
        };

        /// <summary>Builds the tint for tool slot i from the CURRENT palette: the tool's own
        /// hue is honoured, but saturation and brightness are pulled toward the theme so an
        /// amber theme mutes every icon toward warmth and an ocean theme cools them all.
        /// Icons keep their identity; the theme keeps its mood.</summary>
        public static Brush ToolTint(int slot)
        {
            var hsl = ToolHues[Math.Max(0, Math.Min(ToolHues.Length - 1, slot))];
            // blend the tool's fixed hue with the accent's by 30% so themes steer the cast;
            // lightness/saturation clamp to stay legible on both dark and light cards.
            double sat = hsl.Sat * 0.82 + 0.10;
            double lig = IsLight ? Math.Max(0.34, hsl.Light - 0.16) : Math.Min(0.80, hsl.Light + 0.02);
            Color c = FromHsl(hsl.Hue, sat, lig);
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            b.GradientStops.Add(new GradientStop(FromHsl(hsl.Hue, sat, Math.Min(0.88, lig + 0.14)), 0));
            b.GradientStops.Add(new GradientStop(c, 1));
            return Freeze(b);
        }

        /// <summary>HSL → Color without System.Drawing dependencies.</summary>
        public static Color FromHsl(double h, double s, double l)
        {
            h = ((h % 360) + 360) % 360 / 360.0;
            if (s > 1) s = 1; if (l > 1) l = 1;
            Func<double, double> f = n =>
            {
                double k = (n + h * 12) % 12;
                double a = s * Math.Min(l, 1 - l);
                return l - a * Math.Max(-1, Math.Min(k - 3, Math.Min(9 - k, 1)));
            };
            return Color.FromRgb((byte)Math.Round(f(0) * 255), (byte)Math.Round(f(8) * 255), (byte)Math.Round(f(4) * 255));
        }

        /// <summary>Tile size for a tool cell of the given width — one rule for every layout.</summary>
        public static double ToolIconSize(double cellWidth)
        {
            return cellWidth >= 80 ? 46 : cellWidth >= 66 ? 42 : 36;
        }

        /// <summary>The v1.10 tool cell: a hand-drawn tangible object (see ToolIcons) sitting
        /// on the widget face with a contact shadow, its name always under it. Hover LIFTS
        /// the object — it rises, its shadow shrinks and fades, and a soft ambient glow in
        /// the tool's hue blooms beneath it. Press squashes it back down. If ToolIcons does
        /// not know the slot, the cell falls back to the MDL2 font glyph (forward-compat).</summary>
        public static FrameworkElement MakeToolCell(string glyph, string label, Action onClick, bool floating, double cellWidth = DefaultCellWidth, int toolSlot = 0)
        {
            // v1.11: the icon is a full-size tile, not an object floating in a mostly empty
            // box. Dense rows (Dock, Connected on a narrow screen) step it down a notch.
            double iconSize = ToolIconSize(cellWidth);
            var icon = ToolIcons.Render(toolSlot, iconSize);

            // ambient glow behind the object in the tool's own hue — the "lit from below"
            // cue that appears as it lifts off the surface
            var glowColor = ToolIcons.Hue(toolSlot);
            var glowBrush = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5) };
            glowBrush.GradientStops.Add(new GradientStop(
                Color.FromArgb(0x66, glowColor.R, glowColor.G, glowColor.B), 0));
            glowBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            glowBrush.Freeze();
            var ambient = new System.Windows.Shapes.Ellipse
            {
                Width = iconSize * 1.5, Height = iconSize * 1.5,
                Fill = glowBrush, Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
            };

            var glyphHost = new Grid { Width = iconSize + 8, Height = iconSize + iconSize * 0.14 };
            glyphHost.Children.Add(ambient);

            TextBlock glyphText = null;   // fallback path only
            if (icon != null) glyphHost.Children.Add(icon.Root);
            else
            {
                glyphText = new TextBlock
                {
                    Text = glyph,
                    FontSize = iconSize * 0.55,
                    FontFamily = Mdl2,
                    Foreground = ToolTint(toolSlot),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                glyphHost.Children.Add(glyphText);
            }

            // The name stays inside its own cell and wraps to a second line rather than
            // running into the neighbour. v1.11: a real reading size and Medium weight — the
            // old 9.5–10.5 px regular labels were the hardest thing on the widget to read.
            double labelSize = Fs(cellWidth >= 70 ? 12.5 : 11.5);
            var name = new TextBlock
            {
                Text = label,
                FontSize = labelSize,
                FontFamily = UiFont,
                FontWeight = FontWeights.Medium,
                Foreground = TextDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = cellWidth,
                MaxHeight = Math.Ceiling(labelSize * 2.7),   // two lines; a third would unbalance the row
                Margin = new Thickness(0, 3, 0, 0),
                FlowDirection = FlowDirection.RightToLeft,   // Arabic labels shape RTL always
            };

            var cell = new StackPanel
            {
                Width = cellWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(CellGap / 2, 0, CellGap / 2, 0),
                ToolTip = label,
            };
            // StackPanel Background only paints with a panel hit-test surface
            cell.Children.Add(glyphHost);
            cell.Children.Add(name);

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(140);
            cell.MouseEnter += (s, e) =>
            {
                name.Foreground = Accent;
                ambient.Opacity = IsLight ? 0.55 : 0.75;
                LiftIcon(icon, glyphText, true, dur, ease);
            };
            cell.MouseLeave += (s, e) =>
            {
                name.Foreground = TextDim;
                ambient.Opacity = 0.0;
                LiftIcon(icon, glyphText, false, dur, ease);
            };
            cell.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                PressIcon(icon, glyphText);
            };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                LiftIcon(icon, glyphText, true, TimeSpan.FromMilliseconds(90), null);
                // hand the panel the icon it came from, so it can open attached to it
                PanelWindow.PendingAnchor = glyphHost;
                try { onClick(); } catch (Exception ex) { Log.Error("toolcell", ex); }
            };
            return cell;
        }

        /// <summary>Hover lift: the object rises ~3px and swells slightly while its contact
        /// shadow shrinks and fades — the two motions together read as "picked up".</summary>
        static void LiftIcon(IconVisual icon, TextBlock fallbackGlyph, bool lifted, TimeSpan dur, QuadraticEase ease)
        {
            if (icon == null)
            {
                if (fallbackGlyph != null) zoomGlyph(fallbackGlyph, lifted ? 1.14 : 1.0, dur, ease);
                return;
            }
            var tg = new TransformGroup();
            var sc = new ScaleTransform(1, 1);
            var tr = new TranslateTransform(0, 0);
            tg.Children.Add(sc);
            tg.Children.Add(tr);
            icon.Body.RenderTransform = tg;
            double toScale = lifted ? 1.07 : 1.0;
            double toY = lifted ? -3.2 : 0;
            var aScale = new DoubleAnimation(toScale, dur);
            var aY = new DoubleAnimation(toY, dur);
            if (ease != null) { aScale.EasingFunction = ease; aY.EasingFunction = ease; }
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, aScale);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, aScale);
            tr.BeginAnimation(TranslateTransform.YProperty, aY);

            var shScale = new ScaleTransform(1, 1);
            icon.Shadow.RenderTransform = shScale;
            var aShadow = new DoubleAnimation(lifted ? 0.78 : 1.0, dur);
            var aShadowO = new DoubleAnimation(lifted ? 0.55 : 1.0, dur);
            if (ease != null) { aShadow.EasingFunction = ease; aShadowO.EasingFunction = ease; }
            shScale.BeginAnimation(ScaleTransform.ScaleXProperty, aShadow);
            icon.Shadow.BeginAnimation(UIElement.OpacityProperty, aShadowO);
        }

        /// <summary>Press feedback: a quick squash before the click lands.</summary>
        static void PressIcon(IconVisual icon, TextBlock fallbackGlyph)
        {
            if (icon == null)
            {
                if (fallbackGlyph != null) zoomGlyph(fallbackGlyph, 0.92, TimeSpan.FromMilliseconds(70), null);
                return;
            }
            var sc = new ScaleTransform(1, 1);
            var tr = new TranslateTransform(0, 1.5);
            var tg = new TransformGroup();
            tg.Children.Add(sc);
            tg.Children.Add(tr);
            icon.Body.RenderTransform = tg;
            var a = new DoubleAnimation(0.93, TimeSpan.FromMilliseconds(70));
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }

        /// <summary>Scale animation helper for the bare-glyph tool cells.</summary>
        static void zoomGlyph(TextBlock t, double to, TimeSpan dur, QuadraticEase ease)
        {
            var st = new ScaleTransform(1, 1);
            t.RenderTransform = st;
            var anim = new DoubleAnimation(to, dur);
            if (ease != null) anim.EasingFunction = ease;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        // ---------------- buttons & inputs ----------------

        /// <summary>Round icon button used in panels (small actions).</summary>
        public static Border MakeIconButton(string glyph, string tooltip, Action onClick, double diameter = 36, double glyphSize = 15)
        {
            var b = new Border
            {
                Width = diameter,
                Height = diameter,
                CornerRadius = new CornerRadius(diameter / 2),
                Background = ButtonBg,
                BorderBrush = ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = MakeGlyph(glyph, glyphSize),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
            };
            var normalBorder = ButtonBorder;
            var hoverBorder = Accent;
            b.MouseEnter += (s, e) => { b.Background = HoverBg; b.BorderBrush = hoverBorder; };
            b.MouseLeave += (s, e) => { b.Background = ButtonBg; b.BorderBrush = normalBorder; };
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("iconbtn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        /// <summary>Rounded text button for panels; accent variant gets a lifted accent gradient.</summary>
        public static Border MakeButton(string text, Action onClick, bool accent = false, Thickness? padding = null)
        {
            var tb = MakeText(text, 13, accent ? (IsLight ? Brushes.White : Brushes.Black) : Text);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.FontWeight = accent ? FontWeights.SemiBold : FontWeights.Normal;
            Brush accentBg = Accent;
            if (accent)
                accentBg = Freeze(new LinearGradientBrush(Lift(AccentColor, 26, 22, 20), AccentColor, 90));
            var btnLayers = new Grid();
            btnLayers.Children.Add(Bevel(9));
            btnLayers.Children.Add(new Border { Padding = padding ?? new Thickness(14, 7, 14, 7), Child = tb });
            var b = new Border
            {
                Background = accent ? accentBg : ButtonBg,
                BorderBrush = accent ? null : ButtonBorder,
                BorderThickness = accent ? new Thickness(0) : new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = btnLayers,
                Cursor = Cursors.Hand,
                Margin = new Thickness(3),
                // callers change the label through Tag/SetButtonText — never cast Child
                // (Child is the bevel layering grid, not the TextBlock)
                Tag = tb,
            };
            if (!accent) AttachHover(b, ButtonBg, HoverBg);
            else AttachHover(b, accentBg, Freeze(new SolidColorBrush(Lift(AccentColor, 40, 36, 34))));
            b.MouseLeftButtonUp += (s, e) => { e.Handled = true; try { onClick(); } catch (Exception ex) { Log.Error("btn", ex); } };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }

        /// <summary>Safe label access for buttons made by <see cref="MakeButton"/> —
        /// reads/writes the TextBlock kept in Tag regardless of internal layering.</summary>
        public static void SetButtonText(Border b, string text)
        {
            var tb = b.Tag as TextBlock;
            if (tb != null) tb.Text = text;
        }

        public static void AttachHover(Border b, Brush normal, Brush hover)
        {
            b.MouseEnter += (s, e) => b.Background = hover;
            b.MouseLeave += (s, e) => b.Background = normal;
        }

        public static TextBox MakeInput(string initial = "")
        {
            var t = new TextBox
            {
                Text = initial,
                Background = InputBg,
                Foreground = Text,
                CaretBrush = Text,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 7, 9, 7),
                FontSize = Fs(13),
                FontFamily = UiFont,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            t.GotKeyboardFocus += (s, e) => t.BorderBrush = Accent;
            t.LostKeyboardFocus += (s, e) => t.BorderBrush = CardBorder;
            return t;
        }

        public static ComboBox MakeCombo()
        {
            return new ComboBox
            {
                FontSize = Fs(13),
                FontFamily = UiFont,
                MinWidth = 140,
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = ControlStyles.Combo(),
            };
        }

        public static CheckBox MakeCheck(string text, bool isChecked, Action<bool> onChange)
        {
            var cb = new CheckBox
            {
                Content = new TextBlock { Text = text, Foreground = Text, FontSize = Fs(13), FontFamily = UiFont, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft },
                IsChecked = isChecked,
                Margin = new Thickness(0, 4, 0, 4),
                VerticalContentAlignment = VerticalAlignment.Center,
                Style = ControlStyles.Check(),
            };
            cb.Checked += (s, e) => onChange(true);
            cb.Unchecked += (s, e) => onChange(false);
            return cb;
        }

        public static Slider MakeSlider(double min, double max, double value, Action<double> onChange)
        {
            var s = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                Style = ControlStyles.Slider(),
                IsMoveToPointEnabled = true,
            };
            s.ValueChanged += (o, e) => onChange(e.NewValue);
            return s;
        }

        public static TextBlock SectionTitle(string text)
        {
            var t = MakeText(text, 12, Accent);
            t.FontWeight = FontWeights.Bold;
            t.Margin = new Thickness(0, 10, 0, 4);
            return t;
        }
    }
}
