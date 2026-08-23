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
        public static readonly FontFamily UiFont = new FontFamily("Tajawal, Segoe UI, Tahoma, Arial");
        public static readonly FontFamily LightFont = new FontFamily("Tajawal, Segoe UI Light, Segoe UI, Tahoma, Arial");
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
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xAC, 0x16, 0x1E, 0x2A)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x68, 0x16, 0x1E, 0x2A)));
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
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
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
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xAC, 0x16, 0x1E, 0x2A)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x68, 0x16, 0x1E, 0x2A)));
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
                TextDim = Freeze(new SolidColorBrush(Color.FromArgb(0xB8, 0xFF, 0xFF, 0xFF)));
                TextFaint = Freeze(new SolidColorBrush(Color.FromArgb(0x70, 0xFF, 0xFF, 0xFF)));
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
            // HSL → RGB
            Func<double, double> hue2 = p =>
            {
                if (p < 0) p += 1; if (p > 1) p -= 1;
                if (p < 1.0 / 6) return l * s * 6 * p;
                if (p < 1.0 / 2) return l;
                if (p < 2.0 / 3) return l * s * (2.0 / 3 - p) * 6;
                return l;
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

        /// <summary>One shadow ramp for the whole app, so a key, a card and a floating panel
        /// sit at visibly different heights instead of each picking its own numbers.
        /// 1 = resting key · 2 = raised surface · 3 = floating above the desktop.
        /// Effects are frozen and shared: a DropShadowEffect per control is measurable cost
        /// on a widget that rebuilds its whole tree on every settings change.</summary>
        public static DropShadowEffect Elevation(int level)
        {
            if (_elevation == null)
            {
                _elevation = new[]
                {
                    MakeShadow(6, 1.5, IsLight ? 0.20 : 0.32),
                    // raised surface: a second offset layer is faked by a deeper blur+depth
                    // so cards visibly "sit" on the desktop instead of printing onto it
                    MakeShadow(16, 4, IsLight ? 0.26 : 0.48),
                    MakeShadow(34, 9, IsLight ? 0.34 : 0.62),
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
                Tag = new CardParts { Pad = pad, Sheen = sheen, Bevel = bevel, Rim = rim },
            };
        }

        /// <summary>The layers of a card that have to be reshaped together. Kept in the card's
        /// Tag so a caller can resize or re-pad the card without the sheen, bevel and rim
        /// silently keeping the original geometry — which drew a rounded SQUARE rim inside the
        /// orbit layout's circular clock card.</summary>
        class CardParts
        {
            public Border Pad, Sheen, Bevel, Rim;
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
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = brush ?? Text,
                FontFamily = font ?? UiFont,
                // Arabic must ALWAYS shape and order RTL regardless of the container's
                // flow direction — a stray LTR ancestor used to mirror word order
                FlowDirection = FlowDirection.RightToLeft,
            };
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
        public const double DefaultCellWidth = 76;

        /// <summary>Clear gap between neighbouring cells, so labels never appear to touch.</summary>
        public const double CellGap = 8;

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

        /// <summary>The v1.9 tool cell: the glyph itself becomes the object — drawn large,
        /// filled with its own colour gradient, given a lit top edge, a soft drop shadow and
        /// a coloured under-glow that reads as a reflection beneath it. NO circle, NO plate:
        /// nothing between the eye and the shape of the tool. Hover lifts the glyph, brightens
        /// its glow and warms the label.</summary>
        public static FrameworkElement MakeToolCell(string glyph, string label, Action onClick, bool floating, double cellWidth = DefaultCellWidth, int toolSlot = 0)
        {
            // dense single-row layouts (Orbit/Dock) shrink the cell but keep the glyph
            // readable — colour needs size to be seen at all
            double glyphSize = cellWidth >= 60 ? 24 : 20;

            // coloured glyph: gradient fill via the Foreground brush
            var glyphText = new TextBlock
            {
                Text = glyph,
                FontSize = glyphSize,
                FontFamily = Mdl2,
                Foreground = ToolTint(toolSlot),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            // soft coloured glow behind the glyph — the "object sitting on the desktop" cue
            var halo = new DropShadowEffect
            {
                Color = FromHsl(ToolHues[Math.Max(0, Math.Min(ToolHues.Length - 1, toolSlot))].Hue,
                                0.9, IsLight ? 0.45 : 0.6),
                BlurRadius = 14, ShadowDepth = 0, Opacity = 0.0,
            };
            glyphText.Effect = halo;

            // reflection pad under the glyph: a thin bright line that fades down — grounds
            // the floating glyph like a floor sheen, replacing the old circle entirely
            var reflect = new System.Windows.Shapes.Rectangle
            {
                Width = cellWidth < 60 ? 22 : 26,
                Height = 2.5,
                RadiusX = 1.25, RadiusY = 1.25,
                Fill = Freeze(new LinearGradientBrush(
                    Color.FromArgb(IsLight ? (byte)0x66 : (byte)0x54,
                        AccentColor.R, AccentColor.G, AccentColor.B),
                    Colors.Transparent,
                    new Point(0, 0), new Point(1, 0))),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 1),
                IsHitTestVisible = false,
            };
            if (floating) reflect.Opacity = 0.9;

            var glyphHost = new Grid { Width = 40, Height = 40 };
            glyphHost.Children.Add(glyphText);
            if (!floating) glyphHost.Children.Add(reflect);

            // The label used to be allowed to overflow its cell (MaxWidth = cellWidth + 20 with
            // -10 side margins), so neighbouring Arabic names — "تنظيف سريع" beside
            // "الآلة الحاسبة" — ran straight into each other with no gap. A label now stays
            // inside its own cell and wraps to a second line instead of invading the next one.
            var name = new TextBlock
            {
                Text = label,
                FontSize = cellWidth < 60 ? 9.5 : 10.5,
                FontFamily = UiFont,
                Foreground = TextDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = cellWidth,
                MaxHeight = 30,          // two lines at this size; a third would unbalance the row
                Margin = new Thickness(0, 5, 0, 0),
                FlowDirection = FlowDirection.RightToLeft,   // Arabic labels shape RTL always
            };

            var cell = new StackPanel
            {
                Width = cellWidth,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(CellGap / 2, 0, CellGap / 2, 0),
            };
            // StackPanel Background only paints with a panel hit-test surface
            cell.Children.Add(glyphHost);
            cell.Children.Add(name);

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(130);
            cell.MouseEnter += (s, e) =>
            {
                name.Foreground = Accent;
                halo.Opacity = 0.55;
                zoomGlyph(glyphText, 1.14, dur, ease);
            };
            cell.MouseLeave += (s, e) =>
            {
                name.Foreground = TextDim;
                halo.Opacity = 0.0;
                zoomGlyph(glyphText, 1.0, dur, ease);
            };
            cell.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                zoomGlyph(glyphText, 0.92, TimeSpan.FromMilliseconds(70), null);
            };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                zoomGlyph(glyphText, 1.14, TimeSpan.FromMilliseconds(90), null);
                // hand the panel the icon it came from, so it can open attached to it
                PanelWindow.PendingAnchor = glyphHost;
                try { onClick(); } catch (Exception ex) { Log.Error("toolcell", ex); }
            };
            return cell;
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
                FontSize = 13,
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
                FontSize = 13,
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
                Content = new TextBlock { Text = text, Foreground = Text, FontSize = 13, FontFamily = UiFont, TextWrapping = TextWrapping.Wrap, FlowDirection = FlowDirection.RightToLeft },
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
            var t = MakeText(text, 11.5, Accent);
            t.FontWeight = FontWeights.SemiBold;
            t.Margin = new Thickness(0, 10, 0, 4);
            return t;
        }
    }
}
