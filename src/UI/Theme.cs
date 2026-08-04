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
                    MakeShadow(14, 3, IsLight ? 0.24 : 0.42),
                    MakeShadow(30, 7, IsLight ? 0.30 : 0.55),
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
        /// rectangle with a shadow" from a surface that looks like it has thickness.</summary>
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

        /// <summary>Small rounded-square accent-tinted badge holding a glyph (panel headers).</summary>
        public static Border MakeGlyphChip(string glyph, double size = 26)
        {
            return new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.32),
                Background = AccentSoft,
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

        /// <summary>The widget's signature button: a 40px circle with its name always
        /// visible underneath. Hover lifts the circle and lights glyph + label in the accent.
        /// cellWidth shrinks the cell for dense single-row layouts (label scales with it).</summary>
        public static FrameworkElement MakeToolCell(string glyph, string label, Action onClick, bool floating, double cellWidth = DefaultCellWidth)
        {
            var glyphText = MakeGlyph(glyph, 16);
            var circleLayers = new Grid();
            circleLayers.Children.Add(Bevel(19));
            circleLayers.Children.Add(RimLight(20));
            circleLayers.Children.Add(glyphText);
            var circle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = floating ? CardBg : ButtonBg,
                BorderBrush = ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = circleLayers,
                HorizontalAlignment = HorizontalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var lift = new TranslateTransform(0, 0);
            var zoom = new ScaleTransform(1, 1);
            var group = new TransformGroup();
            group.Children.Add(zoom);
            group.Children.Add(lift);
            circle.RenderTransform = group;
            // a floating icon sits straight on the desktop with no card behind it, so it needs
            // real separation; inside a card the card's own shadow already does that work
            if (floating) circle.Effect = Elevation(2);

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
            cell.Children.Add(circle);
            cell.Children.Add(name);

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var dur = TimeSpan.FromMilliseconds(130);
            cell.MouseEnter += (s, e) =>
            {
                circle.BorderBrush = Accent;
                glyphText.Foreground = Accent;
                name.Foreground = Accent;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.1, dur) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.1, dur) { EasingFunction = ease });
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-2, dur) { EasingFunction = ease });
            };
            cell.MouseLeave += (s, e) =>
            {
                circle.BorderBrush = ButtonBorder;
                glyphText.Foreground = Text;
                name.Foreground = TextDim;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, dur) { EasingFunction = ease });
                lift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, dur) { EasingFunction = ease });
            };
            cell.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, TimeSpan.FromMilliseconds(70)));
            };
            cell.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                zoom.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(90)));
                zoom.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.1, TimeSpan.FromMilliseconds(90)));
                // hand the panel the icon it came from, so it can open attached to it
                PanelWindow.PendingAnchor = circle;
                try { onClick(); } catch (Exception ex) { Log.Error("toolcell", ex); }
            };
            return cell;
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
                Content = new TextBlock { Text = text, Foreground = Text, FontSize = 13, FontFamily = UiFont, TextWrapping = TextWrapping.Wrap },
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
