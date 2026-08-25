using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DeskMate.UI
{
    /// <summary>The pieces of one drawn tool icon that the hover animation needs:
    /// Root is placed in the tool cell; Body is the object itself (lifted/scaled on hover);
    /// Shadow is the contact shadow beneath it (counter-animated: a lifting object's shadow
    /// shrinks and fades — that inversion is what makes the lift read as physical).</summary>
    public class IconVisual
    {
        public FrameworkElement Root;
        public FrameworkElement Body;
        public Ellipse Shadow;
    }

    /// <summary>Hand-drawn tangible tool icons — v1.10 replaces the MDL2 font glyphs with
    /// small physical objects rendered in code: layered material gradients (metal, glass,
    /// paper, plastic), a specular highlight from the top-left light, and a soft contact
    /// shadow grounding each object on the widget face. Everything is vector geometry built
    /// per Reload; nothing is per-tick, so the richer drawing costs nothing at runtime.
    ///
    /// Icon identity is fixed by TOOL ID (0..14), never by filtered position: hiding a tool
    /// must not recolour the others (it used to — the slot came from the visible list).
    ///
    /// Design canvas is 44×44 with the object roughly in the top 38 units; the remaining
    /// strip at the bottom is where the contact shadow lives.</summary>
    public static class ToolIcons
    {
        // Tool ids — the registry order in MainWindow.IconDefs and Theme.ToolHues.
        public const int Cleaner = 0, Calc = 1, Wallpaper = 2, Power = 3, Clipboard = 4,
                         Weather = 5, Apps = 6, Prayer = 7, Focus = 8, Occasions = 9,
                         Note = 10, SysMon = 11, WorldClock = 12, Command = 13, Settings = 14;

        /// <summary>Builds the icon for a tool id at the requested box size. Returns null for
        /// an unknown id so the caller can fall back to the font glyph (forward-compat).</summary>
        public static IconVisual Render(int id, double size)
        {
            Canvas body = Build(id);
            if (body == null) return null;

            var vb = new Viewbox
            {
                Width = size, Height = size,
                Stretch = Stretch.Uniform,
                Child = body,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };

            // contact shadow: an oval of soft dark light pooled under the object. On light
            // themes a desktop already reflects light upward, so the shadow is gentler.
            double sa = Theme.IsLight ? 0.26 : 0.42;
            var sh = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5) };
            sh.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * sa), 0, 0, 0), 0));
            sh.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(255 * sa * 0.45), 0, 0, 0), 0.55));
            sh.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            sh.Freeze();
            var shadow = new Ellipse
            {
                Width = size * 0.56, Height = size * 0.115,
                Fill = sh,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, size * 0.005),
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1),
            };

            var root = new Grid { Width = size, Height = size + size * 0.07 };
            root.Children.Add(shadow);
            root.Children.Add(vb);
            return new IconVisual { Root = root, Body = vb, Shadow = shadow };
        }

        static Canvas Build(int id)
        {
            switch (id)
            {
                case Cleaner: return DrawCleaner();
                case Calc: return DrawCalc();
                case Wallpaper: return DrawWallpaper();
                case Power: return DrawPower();
                case Clipboard: return DrawClipboard();
                case Weather: return DrawWeather();
                case Apps: return DrawApps();
                case Prayer: return DrawPrayer();
                case Focus: return DrawFocus();
                case Occasions: return DrawOccasions();
                case Note: return DrawNote();
                case SysMon: return DrawSysMon();
                case WorldClock: return DrawWorldClock();
                case Command: return DrawCommand();
                case Settings: return DrawSettings();
                default: return null;
            }
        }

        // ---------------- drawing helpers ----------------

        static Color C(string hex)
        {
            // #RRGGBB or #AARRGGBB
            uint v = uint.Parse(hex.TrimStart('#'), System.Globalization.NumberStyles.HexNumber);
            if (hex.Length == 7)
                return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        static Brush Fr(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }

        /// <summary>Vertical linear gradient, stops evenly spaced top → bottom.</summary>
        static Brush Lin(params Color[] stops) { return LinAt(0, 0, 0, 1, stops); }

        /// <summary>Horizontal linear gradient (left → right), for specular stripes.</summary>
        static Brush LinH(params Color[] stops) { return LinAt(0, 0, 1, 0, stops); }

        static Brush LinAt(double x1, double y1, double x2, double y2, params Color[] stops)
        {
            var b = new LinearGradientBrush { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2) };
            for (int i = 0; i < stops.Length; i++)
                b.GradientStops.Add(new GradientStop(stops[i], stops.Length == 1 ? 0 : (double)i / (stops.Length - 1)));
            return Fr(b);
        }

        /// <summary>Radial gradient, light origin (gx,gy) in relative coordinates.</summary>
        static Brush Rad(double gx, double gy, params Color[] stops)
        {
            var b = new RadialGradientBrush
            {
                GradientOrigin = new Point(gx, gy),
                Center = new Point(0.5, 0.5),
            };
            for (int i = 0; i < stops.Length; i++)
                b.GradientStops.Add(new GradientStop(stops[i], (double)i / (stops.Length - 1)));
            return Fr(b);
        }

        /// <summary>White gloss that fades downward — the top-left specular every object shares.</summary>
        static Brush Gloss(byte alpha)
        {
            return Fr(new LinearGradientBrush(
                Color.FromArgb(alpha, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90));
        }

        static Ellipse El(double x, double y, double w, double h, Brush fill, Brush stroke = null, double st = 0)
        {
            var e = new Ellipse { Width = w, Height = h, Fill = fill, IsHitTestVisible = false };
            if (stroke != null && st > 0) { e.Stroke = stroke; e.StrokeThickness = st; }
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            return e;
        }

        /// <summary>Rounded rectangle placed by canvas coordinates (Border carries the radius).</summary>
        static Border Bx(double x, double y, double w, double h, double r, Brush fill, Brush stroke = null, double st = 0)
        {
            var b = new Border
            {
                Width = w, Height = h, CornerRadius = new CornerRadius(r),
                Background = fill, IsHitTestVisible = false,
            };
            if (stroke != null && st > 0) { b.BorderBrush = stroke; b.BorderThickness = new Thickness(st); }
            Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
            return b;
        }

        static Path Pt(string figs, Brush fill, Brush stroke = null, double st = 0)
        {
            var p = new Path { Data = Geometry.Parse(figs), Fill = fill, IsHitTestVisible = false };
            if (stroke != null && st > 0) { p.Stroke = stroke; p.StrokeThickness = st; }
            return p;
        }

        static Path Pg(Geometry g, Brush fill, Brush stroke = null, double st = 0)
        {
            var p = new Path { Data = g, Fill = fill, IsHitTestVisible = false };
            if (stroke != null && st > 0) { p.Stroke = stroke; p.StrokeThickness = st; }
            return p;
        }

        static Path Line(Geometry g, Brush stroke, double st)
        {
            var p = new Path
            {
                Data = g, Stroke = stroke, StrokeThickness = st, Fill = null,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
            };
            return p;
        }

        static Canvas NewCanvas()
        {
            return new Canvas { Width = 44, Height = 44, FlowDirection = FlowDirection.LeftToRight };
        }

        /// <summary>N-pointed star geometry (alternating outer/inner radius).</summary>
        static Geometry StarGeo(double cx, double cy, double outer, double inner, int points, double startDeg)
        {
            var fig = new PathFigure();
            for (int i = 0; i < points * 2; i++)
            {
                double r = (i % 2 == 0) ? outer : inner;
                double a = (startDeg + i * 180.0 / points) * Math.PI / 180.0;
                var p = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
                if (i == 0) fig.StartPoint = p; else fig.Segments.Add(new LineSegment(p, true));
            }
            fig.IsClosed = true;
            var g = new PathGeometry(); g.Figures.Add(fig);
            return g;
        }

        static Brush Solid(Color c) { return Fr(new SolidColorBrush(c)); }

        // ---------------- the fifteen objects ----------------

        /// <summary>Brushed-metal trash can: tapered body with ridges, a lid with a handle,
        /// and a specular stripe running down the left of the body.</summary>
        static Canvas DrawCleaner()
        {
            var cv = NewCanvas();
            var metal = Solid(C("#4E7E84"));
            // handle + lid
            cv.Children.Add(Bx(18.4, 9.4, 7.2, 2.8, 1.4, Lin(C("#F2FBFB"), C("#A9CDD1")), metal, 0.5));
            cv.Children.Add(Bx(11.8, 12.2, 20.4, 3.8, 1.9, Lin(C("#F6FFFF"), C("#9FC6CB")), metal, 0.6));
            // body: trapezoid, slightly rounded base
            cv.Children.Add(Pt("M 13,17.6 L 31,17.6 L 29.8,35.4 Q 29.7,37 28.2,37 L 15.8,37 Q 14.3,37 14.2,35.4 Z",
                Lin(C("#EAF7F7"), C("#9CC4CA"), C("#5E939B")), metal, 0.7));
            // pressed ridges
            cv.Children.Add(Line(Geometry.Parse("M 19.3,20.4 L 19.9,33.6"),
                Fr(new SolidColorBrush(Color.FromArgb(0x66, 0x3E, 0x6E, 0x74))), 1.1));
            cv.Children.Add(Line(Geometry.Parse("M 24.7,20.4 L 24.1,33.6"),
                Fr(new SolidColorBrush(Color.FromArgb(0x66, 0x3E, 0x6E, 0x74))), 1.1));
            // specular stripe
            cv.Children.Add(Bx(15.4, 19.2, 2.6, 14.5, 1.3,
                LinH(Color.FromArgb(0x88, 255, 255, 255), Color.FromArgb(0, 255, 255, 255))));
            return cv;
        }

        /// <summary>Amber calculator slab: glass screen sunk into the top, nine keys with
        /// their own top-light, and the equals key in hot orange.</summary>
        static Canvas DrawCalc()
        {
            var cv = NewCanvas();
            cv.Children.Add(Bx(11.5, 7.2, 21, 29.6, 4.6,
                Lin(C("#FFD98E"), C("#F0B04A"), C("#CE831F")), Solid(C("#A86A12")), 0.7));
            // top sheen
            cv.Children.Add(Bx(13, 8.6, 18, 9, 4, Gloss(0x42)));
            // sunken screen + a pale result dash
            cv.Children.Add(Bx(14.6, 11.4, 14.8, 6.6, 1.8, Lin(C("#39424C"), C("#222931"))));
            cv.Children.Add(Bx(23.2, 13.6, 4.6, 1.5, 0.7, Solid(Color.FromArgb(0xCC, 0x9F, 0xE8, 0xB0))));
            // keys
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    bool eq = r == 2 && c == 2;
                    cv.Children.Add(Bx(14.6 + c * 5.9, 20.6 + r * 5.5, 4.8, 4.4, 1.4,
                        eq ? Lin(C("#FFB25C"), C("#E0690A")) : Lin(C("#FFF6DC"), C("#F0C87A")),
                        Solid(Color.FromArgb(0x88, 0xA8, 0x6A, 0x12)), 0.5));
                }
            return cv;
        }

        /// <summary>A framed photograph: white mat, sky gradient, a small sun and two
        /// mountain ridges inside the print.</summary>
        static Canvas DrawWallpaper()
        {
            var cv = NewCanvas();
            cv.Children.Add(Bx(9.5, 11.2, 25, 20.5, 2.4, Lin(C("#FFFFFF"), C("#D9DFE6")),
                Solid(C("#9AA6B2")), 0.8));
            // the print
            cv.Children.Add(Pt("M 11.9,13.6 L 32.1,13.6 L 32.1,29 L 11.9,29 Z", Lin(C("#7CC4F2"), C("#D2ECFF"))));
            cv.Children.Add(El(14.2, 15.6, 5, 5, Rad(0.4, 0.35, C("#FFF6C8"), C("#FFC94A"))));
            // far ridge then near ridge (near darker = depth)
            cv.Children.Add(Pt("M 11.9,29 L 17.4,21.2 L 22,26.4 L 26.2,21.8 L 32.1,29 Z", Solid(C("#4F7C52"))));
            cv.Children.Add(Pt("M 11.9,29 L 15.6,24.2 L 20.4,29 Z", Solid(C("#3A6146"))));
            // frame glass: diagonal light
            cv.Children.Add(Pt("M 11.9,13.6 L 20,13.6 L 14.5,29 L 11.9,29 Z",
                Solid(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF))));
            return cv;
        }

        /// <summary>A glossy red power push-button: domed disc, white power symbol,
        /// crescent glass reflection on the dome.</summary>
        static Canvas DrawPower()
        {
            var cv = NewCanvas();
            cv.Children.Add(El(10.8, 10.2, 22.4, 22.4, Rad(0.36, 0.3, C("#FF9A86"), C("#E23B2E"), C("#9E1408")),
                Solid(Color.FromArgb(0x99, 0x7E, 0x0F, 0x04)), 0.8));
            // power symbol: broken ring + stem
            var ring = Geometry.Parse("M 17.2,17.4 A 7,7 0 1 0 26.8,17.4");
            cv.Children.Add(Line(ring, Solid(C("#FFFFFF")), 2.5));
            cv.Children.Add(Line(Geometry.Parse("M 22,12.2 L 22,20.4"),
                Solid(C("#FFFFFF")), 2.5));
            // glass crescent on the dome
            cv.Children.Add(El(14.8, 12.6, 14.4, 7, Gloss(0x78)));
            return cv;
        }

        /// <summary>Wooden clipboard with a metal clip, a white sheet and ruled lines.</summary>
        static Canvas DrawClipboard()
        {
            var cv = NewCanvas();
            cv.Children.Add(Bx(12.4, 9.2, 19.2, 27.2, 2.8, Lin(C("#C89B66"), C("#96652F"), C("#7C4E20")),
                Solid(C("#5F3A14")), 0.7));
            cv.Children.Add(Bx(15, 12.8, 14, 20.4, 1.2, Lin(C("#FFFFFF"), C("#E9EDF1"))));
            // ruled lines
            for (int i = 0; i < 3; i++)
                cv.Children.Add(Bx(17.2, 16.6 + i * 4.2, 9.8, 1.35, 0.6, Solid(C("#A8B4C0"))));
            cv.Children.Add(Bx(17.2, 29.2, 6.2, 1.35, 0.6, Solid(C("#A8B4C0"))));
            // metal clip over the top edge
            cv.Children.Add(Bx(17.6, 6.6, 8.8, 5, 2.2, Lin(C("#F4F8FB"), C("#9FB2C2")), Solid(C("#6E8296")), 0.6));
            cv.Children.Add(El(20.7, 8, 2.6, 2.6, Solid(C("#6E8296"))));
            return cv;
        }

        /// <summary>The weather orb: a small burning sun peeking from behind a puffy
        /// three-lobe cloud with a shaded underside.</summary>
        static Canvas DrawWeather()
        {
            var cv = NewCanvas();
            var sunCore = Solid(C("#FFC23E"));
            // halo + sun
            cv.Children.Add(El(8.6, 7.6, 15, 15, Rad(0.5, 0.5,
                Color.FromArgb(0x66, 0xFF, 0xE2, 0x7A), Colors.Transparent)));
            // 8 short rays
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                double x1 = 16.1 + Math.Cos(a) * 7.4, y1 = 15.1 + Math.Sin(a) * 7.4;
                double x2 = 16.1 + Math.Cos(a) * 10, y2 = 15.1 + Math.Sin(a) * 10;
                var ray = Geometry.Parse(
                    string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "M {0:0.0},{1:0.0} L {2:0.0},{3:0.0}", x1, y1, x2, y2));
                cv.Children.Add(Line(ray, sunCore, 1.5));
            }
            cv.Children.Add(El(11.1, 10.1, 10, 10, Rad(0.4, 0.35, C("#FFF7C0"), C("#FFC23E"))));
            // cloud underside shade, then the three lobes and the base
            var cloudFill = Lin(C("#FFFFFF"), C("#FFFFFF"), C("#C7D4DF"));
            cv.Children.Add(El(15.2, 19, 12.4, 12.4, cloudFill));
            cv.Children.Add(El(23.6, 21.2, 10.4, 10.4, cloudFill));
            cv.Children.Add(El(12.4, 22.6, 8.6, 8.6, cloudFill));
            cv.Children.Add(Bx(13.8, 26.2, 21.4, 8.6, 4.3, cloudFill));
            // shade pooled under the cloud
            cv.Children.Add(El(15, 31.6, 19, 4.2, Rad(0.5, 0.5,
                Color.FromArgb(0x3C, 0x5A, 0x6E, 0x80), Colors.Transparent)));
            return cv;
        }

        /// <summary>Four app tiles in a 2×2 grid, each a squircle with its own colour and
        /// a top-light strip — a tiny launcher shelf.</summary>
        static Canvas DrawApps()
        {
            var cv = NewCanvas();
            Brush[] fills =
            {
                Lin(C("#8B96F2"), C("#4A5BD0")),   // indigo
                Lin(C("#5CD2C4"), C("#1F8A80")),   // teal
                Lin(C("#FFD98E"), C("#CE831F")),   // amber
                Lin(C("#F28FB4"), C("#B83A68")),   // rose
            };
            double[] xs = { 11.4, 23.2, 11.4, 23.2 };
            double[] ys = { 11.4, 11.4, 23.2, 23.2 };
            for (int i = 0; i < 4; i++)
            {
                cv.Children.Add(Bx(xs[i], ys[i], 9.6, 9.6, 2.6, fills[i],
                    Solid(Color.FromArgb(0x55, 0x1A, 0x20, 0x30)), 0.5));
                cv.Children.Add(Bx(xs[i] + 0.9, ys[i] + 0.9, 7.8, 2.6, 1.3, Gloss(0x4C)));
            }
            return cv;
        }

        /// <summary>A golden crescent — two discs punched out of each other — with a small
        /// four-point star and a warm halo.</summary>
        static Canvas DrawPrayer()
        {
            var cv = NewCanvas();
            cv.Children.Add(El(8.6, 8.4, 25, 25, Rad(0.5, 0.5,
                Color.FromArgb(0x59, 0xFF, 0xE9, 0xA8), Colors.Transparent)));
            var crescent = new GeometryGroup { FillRule = FillRule.EvenOdd };
            crescent.Children.Add(new EllipseGeometry(new Point(20.6, 21.4), 10.2, 10.2));
            crescent.Children.Add(new EllipseGeometry(new Point(25.6, 18.2), 8.4, 8.4));
            cv.Children.Add(Pg(crescent, Rad(0.38, 0.32, C("#FFF3C4"), C("#EFB93E"), C("#C08A20"))));
            // star
            cv.Children.Add(Pg(StarGeo(29.4, 13.2, 4.6, 1.5, 4, -90), Solid(C("#FFF6D8"))));
            return cv;
        }

        /// <summary>An archery target in rose: alternating rings down to the bullseye,
        /// with a soft top-left sheen.</summary>
        static Canvas DrawFocus()
        {
            var cv = NewCanvas();
            cv.Children.Add(El(10.2, 9.8, 23.6, 23.6, Rad(0.38, 0.3, C("#FBE9F1"), C("#F2C9D8")),
                Solid(C("#D6A0B4")), 0.7));
            cv.Children.Add(El(13.4, 13, 17.2, 17.2, Solid(C("#DE5B84"))));
            cv.Children.Add(El(16.6, 16.2, 10.8, 10.8, Solid(C("#F7E4EC"))));
            cv.Children.Add(El(19.4, 19, 5.2, 5.2, Rad(0.4, 0.35, C("#EF7BA0"), C("#C93366"))));
            cv.Children.Add(El(13.8, 11.6, 9.4, 4.6, Gloss(0x55)));
            return cv;
        }

        /// <summary>An eight-pointed gold star (najma) with a teal gem at its heart —
        /// the festival marker.</summary>
        static Canvas DrawOccasions()
        {
            var cv = NewCanvas();
            cv.Children.Add(El(8.8, 8.6, 26.4, 26.4, Rad(0.5, 0.5,
                Color.FromArgb(0x4D, 0xFF, 0xD7, 0x6E), Colors.Transparent)));
            cv.Children.Add(Pg(StarGeo(22, 21.6, 11.2, 4.9, 8, -90),
                Rad(0.4, 0.32, C("#FFF3C4"), C("#EFB93E"), C("#C98F1A")),
                Solid(Color.FromArgb(0x88, 0xA8, 0x76, 0x10)), 0.6));
            cv.Children.Add(El(19.3, 18.9, 5.4, 5.4, Rad(0.4, 0.35, C("#8FE8DC"), C("#1F8A80"))));
            cv.Children.Add(El(20.6, 19.9, 1.7, 1.7, Solid(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF))));
            return cv;
        }

        /// <summary>A yellow sticky note with a taped top edge, faint ruled lines and a
        /// folded bottom-right corner — the fold's underside is darker, as paper is.</summary>
        static Canvas DrawNote()
        {
            var cv = NewCanvas();
            // sheet with the corner cut away
            cv.Children.Add(Pt("M 13.2,10.4 L 30.8,10.4 L 30.8,26.8 L 24.2,33.4 L 13.2,33.4 Z",
                Lin(C("#FFF8B0"), C("#F3D54C"), C("#E3B62E")), Solid(C("#C79A1E")), 0.6));
            // fold
            cv.Children.Add(Pt("M 24.2,33.4 L 24.2,26.8 L 30.8,26.8 Z",
                Lin(C("#F0CE4A"), C("#C99E22")), Solid(C("#B3891B")), 0.4));
            // tape
            var tape = Bx(16.4, 7.8, 11.2, 4.8, 1.2, Solid(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));
            cv.Children.Add(tape);
            // ruled lines
            cv.Children.Add(Bx(15.6, 15.6, 12.6, 1.4, 0.7, Solid(Color.FromArgb(0xB3, 0xC7, 0xA4, 0x28))));
            cv.Children.Add(Bx(15.6, 19.8, 12.6, 1.4, 0.7, Solid(Color.FromArgb(0xB3, 0xC7, 0xA4, 0x28))));
            cv.Children.Add(Bx(15.6, 24, 8, 1.4, 0.7, Solid(Color.FromArgb(0xB3, 0xC7, 0xA4, 0x28))));
            return cv;
        }

        /// <summary>A little dark monitor with three rising green bars and a stand —
        /// the system pulse.</summary>
        static Canvas DrawSysMon()
        {
            var cv = NewCanvas();
            cv.Children.Add(Bx(10.4, 8.6, 23.2, 24.6, 3.2, Lin(C("#46525F"), C("#232B35")),
                Solid(C("#141A21")), 0.8));
            cv.Children.Add(Bx(12.6, 11, 18.8, 15.4, 1.6, Lin(C("#1B2430"), C("#10161E"))));
            // bars rising left → right
            double[] xs = { 15.4, 20.1, 24.8 };
            double[] hs = { 5.2, 9.2, 13 };
            Brush[] bf =
            {
                Lin(C("#B6E8A0"), C("#5DBA6E")),
                Lin(C("#8FD18A"), C("#3FA860")),
                Lin(C("#5DBA6E"), C("#2F8E4E")),
            };
            for (int i = 0; i < 3; i++)
                cv.Children.Add(Bx(xs[i], 24.6 - hs[i], 3.6, hs[i], 1, bf[i]));
            cv.Children.Add(Bx(14.6, 25.2, 14.8, 1, 0.5, Solid(C("#3A4A58"))));
            // stand
            cv.Children.Add(Bx(18.4, 33.2, 7.2, 2.6, 1, Lin(C("#46525F"), C("#232B35"))));
            return cv;
        }

        /// <summary>A glass globe: ocean-blue sphere, meridians and a latitude clipped to
        /// the disc, and a big window-light reflection.</summary>
        static Canvas DrawWorldClock()
        {
            var cv = NewCanvas();
            cv.Children.Add(El(10.8, 10.4, 22.4, 22.4, Rad(0.35, 0.3, C("#B9ECFF"), C("#3F8FD1"), C("#17457E")),
                Solid(Color.FromArgb(0x88, 0x0E, 0x2E, 0x52)), 0.8));
            // graticule, clipped to the sphere
            var grid = new Canvas { Width = 44, Height = 44, IsHitTestVisible = false };
            var lineA = Solid(Color.FromArgb(0xD9, 0xDF, 0xF4, 0xFF));
            var lineB = Solid(Color.FromArgb(0x80, 0xDF, 0xF4, 0xFF));
            grid.Children.Add(El(17.8, 10.4, 8.4, 22.4, null, lineA, 1));          // central meridian
            grid.Children.Add(El(13.6, 10.4, 16.8, 22.4, null, lineB, 0.9));       // outer meridian
            grid.Children.Add(El(10.8, 17.6, 22.4, 8.2, null, lineA, 1));          // equator band
            grid.Clip = new EllipseGeometry(new Point(22, 21.6), 11.2, 11.2);
            cv.Children.Add(grid);
            cv.Children.Add(El(14.4, 12.4, 8.6, 5, Gloss(0x66)));
            return cv;
        }

        /// <summary>A dark terminal tile with window dots and a glowing ›_ prompt —
        /// the quick-command console.</summary>
        static Canvas DrawCommand()
        {
            var cv = NewCanvas();
            cv.Children.Add(Bx(10.4, 12, 23.2, 19.6, 3.2, Lin(C("#333B5C"), C("#1A1F36")),
                Solid(C("#4A5588")), 0.8));
            // traffic-light dots
            cv.Children.Add(El(13.2, 14.2, 1.9, 1.9, Solid(C("#FF5F57"))));
            cv.Children.Add(El(16.2, 14.2, 1.9, 1.9, Solid(C("#FEBC2E"))));
            cv.Children.Add(El(19.2, 14.2, 1.9, 1.9, Solid(C("#28C840"))));
            cv.Children.Add(Bx(11.8, 17.4, 20.4, 0.9, 0, Solid(Color.FromArgb(0x66, 0x4A, 0x55, 0x88))));
            // prompt: chevron + cursor, in lit periwinkle
            var ch = Geometry.Parse("M 14.6,20.6 L 18,23.4 L 14.6,26.2");
            cv.Children.Add(Line(ch, Solid(C("#C7D2FF")), 2.1));
            cv.Children.Add(Bx(20, 25, 5.6, 2, 1, Solid(C("#C7D2FF"))));
            return cv;
        }

        /// <summary>A machined gear: eight teeth around a silver ring with a dark centre
        /// bore — drawn with real metal gradients, not a stroked outline.</summary>
        static Canvas DrawSettings()
        {
            var cv = NewCanvas();
            var toothFill = Lin(C("#E8EFF6"), C("#9DB0C0"));
            var toothEdge = Solid(Color.FromArgb(0x66, 0x66, 0x78, 0x8A));
            // 8 teeth, each rotated about the gear centre (22, 21.4)
            for (int k = 0; k < 8; k++)
            {
                double ang = k * 45;
                double a = ang * Math.PI / 180;
                double tx = 22 + Math.Sin(a) * 9.4 - 2.1;
                double ty = 21.4 - Math.Cos(a) * 9.4 - 2.5;
                var tooth = Bx(tx, ty, 4.2, 5, 1.3, toothFill, toothEdge, 0.5);
                // RenderTransform's centre is in the ELEMENT's own coordinate space, not the
                // canvas's — passing canvas coords here swung every tooth onto a wide orbit
                // (the "ring of dots" bug). Rotate about the tooth's own centre (2.1, 2.5).
                tooth.RenderTransform = new RotateTransform(ang, 2.1, 2.5);
                cv.Children.Add(tooth);
            }
            // ring + bore
            cv.Children.Add(El(13.6, 13, 16.8, 16.8, Rad(0.4, 0.32, C("#F2F6FA"), C("#A9BACA"), C("#8496A8")),
                Solid(C("#66788A")), 0.8));
            cv.Children.Add(El(18.7, 18.1, 6.6, 6.6, Rad(0.5, 0.5, C("#39434E"), C("#5A6B7C"))));
            // sheen across the upper-left face
            cv.Children.Add(El(15.4, 14.4, 8.4, 4.2, Gloss(0x59)));
            return cv;
        }
    }
}
