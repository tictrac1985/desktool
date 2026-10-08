using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DeskMate.Core;

namespace DeskMate.UI
{
    /// <summary>MainWindow layout builders — the six widget arrangements.</summary>
    public partial class MainWindow
    {
        public void BuildUI()
        {
            FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            _cards.Clear();
            _plates.Clear();
            _floatIcons.Clear();
            _clockCard = null;
            _connectors = null;
            _gregText = null;
            _hijriText = null;
            _dateKey = null;
            // The previous visual tree is about to be thrown away. Its decorations live in
            // statics, so clear them here — otherwise a layout change leaves the tick nudging
            // transforms that are no longer on screen, and NeedsSecondCadence keeps paying for
            // a rotation nobody can see.
            HaloBrushes.Halo = null;
            SkyBodies.Reset();
            ClockDates.Hidden = false;   // only Tri-panel moves the dates out of the clock

            _prayerLine = Theme.MakeText("", 13.5, Theme.Accent);
            _prayerLine.FontWeight = FontWeights.Medium;
            _prayerLine.HorizontalAlignment = HorizontalAlignment.Center;
            _prayerLine.Margin = new Thickness(0, 6, 0, 0);
            _prayerLine.Visibility = Visibility.Collapsed;

            _weatherGlyph = Theme.MakeText("", 14, Theme.Accent, Theme.SymbolFont);
            _weatherGlyph.VerticalAlignment = VerticalAlignment.Center;
            _weatherGlyph.Margin = new Thickness(5, 0, 5, 0);
            _weatherText = Theme.MakeText("", 13, Theme.TextDim);
            _weatherText.VerticalAlignment = VerticalAlignment.Center;
            _weatherLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
                // the line follows the interface direction: in Arabic the weather glyph leads
                // on the right, then the temperature, then the city
                FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            };
            _weatherLine.Children.Add(_weatherGlyph);
            _weatherLine.Children.Add(_weatherText);

            // seasonal occasion line (Ramadan/Eid/nearest occasion) — same pattern as weather
            _occasionGlyph = Theme.MakeText("", 14, Theme.Accent, Theme.SymbolFont);
            _occasionGlyph.VerticalAlignment = VerticalAlignment.Center;
            _occasionGlyph.Margin = new Thickness(5, 0, 5, 0);
            _occasionText = Theme.MakeText("", 13, Theme.TextDim);
            _occasionText.VerticalAlignment = VerticalAlignment.Center;
            _occasionLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0),
                Visibility = Visibility.Collapsed,
                FlowDirection = FlowDirection.RightToLeft,   // Arabic-first reading for the text
            };
            _occasionLine.Children.Add(_occasionGlyph);
            _occasionLine.Children.Add(_occasionText);

            switch (Settings.I.LayoutIndex)
            {
                case 1: _rootEl = BuildConnected(); break;
                case 2: _rootEl = BuildTriple(); break;
                case 3: _rootEl = BuildOrbit(); break;
                case 4: _rootEl = BuildSidebar(); break;
                case 5: _rootEl = BuildDock(); break;
                case 6: _rootEl = BuildMonolith(); break;
                case 7: _rootEl = BuildHalo(); break;
                default: _rootEl = BuildClassic(); break;
            }
            _rootEl.LayoutTransform = new ScaleTransform(Settings.I.Scale, Settings.I.Scale);

            // the grip is attached by each layout to its own bottom card (see WithGrip), not
            // to this root — the root spans the union of all cards and its corner is empty
            var host = new Grid();
            host.Children.Add(_rootEl);
            host.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement) { try { DragMove(); SavePosition(); } catch { } }
            };
            Content = host;

            ApplyCardOpacity();
            if (_clock != null) _clock.Tick(DateTime.Now);
            ScheduleConnectorDraw();
        }

        /// <summary>The optional lines under the clock (next prayer, current weather, the
        /// seasonal occasion line), grouped so every layout has a single insertion point and
        /// all stay collapsed — taking no vertical space — until they actually have something
        /// to say. Density rule: below scale 0.85 only the prayer line survives, so the tiny
        /// widget stays clean; above it, weather and occasions join in order.</summary>
        StackPanel StatusLines()
        {
            var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            s.Children.Add(_prayerLine);
            if (Settings.I.Scale >= 0.85)
            {
                s.Children.Add(_weatherLine);
                s.Children.Add(_occasionLine);
            }
            return s;
        }

        IClockView MakeClock(bool timeOnly, double digitalSize = 58)
        {
            if (timeOnly) return new TimeOnlyClock();
            switch (Settings.I.ClockStyle)
            {
                case 1: return new AnalogClock();       // 168 px — large Classic / Connected / Orbit
                case 2: return new MinimalClock();
                case 3: return new FlipClock();
                case 4: return new WordClock();
                case 5: return new NeonClock();
                case 6: return new BinaryClock();
                case 7: return new DayRingClock();      // 168 px dial — matches Analog sizing
                case 8: return new SunPathClock();      // real sunrise→sunset arc from location
                case 9: return new DayStripesClock();
                case 10: return new MoonPhaseClock();   // night dome + real moon phase
                default: return new DigitalClock(digitalSize);
            }
        }

        Border MakeCardTracked(UIElement content, double radius = 24)
        {
            var c = Theme.MakeCard(content, radius);
            _cards.Add(c);
            return c;
        }

        Border Hairline(double top, double bottom = 0)
        {
            return new Border
            {
                Height = 1,
                Background = Theme.CardBorder,
                Margin = new Thickness(4, top, 4, bottom),
            };
        }

        /// <summary>How wide a single-row layout may grow. Scaled to the screen the widget
        /// lives on rather than a fixed number: on a wide desktop there is no reason to squeeze
        /// fourteen tools into 620px, and on a small one this still compresses. Capped so an
        /// ultrawide monitor does not stretch the widget from edge to edge.</summary>
        static double RowBudget()
        {
            double w = SystemParameters.WorkArea.Width;
            if (w <= 0) return 900;
            return Math.Min(w * 0.88, 1500);
        }

        /// <summary>Cell width that keeps n icons on one row within the given pixel budget.
        /// The gap between cells counts against the budget, otherwise a "fitting" row still
        /// overflowed by one gap per icon.</summary>
        static double CellWidthFor(int n, double budget)
        {
            if (n <= 0) return Theme.DefaultCellWidth;
            double each = budget / n - Theme.CellGap;
            if (each >= Theme.DefaultCellWidth) return Theme.DefaultCellWidth;
            return Math.Max(50, Math.Floor(each));
        }

        /// <summary>Splits tool cells into centered, evenly-sized rows — 6+5 instead of 8+3 —
        /// so multi-row layouts look balanced instead of leaving a sparse last row.</summary>
        static StackPanel BuildBalancedRows(List<FrameworkElement> cells, int maxPerRow, Thickness margin)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = margin };
            int n = cells.Count;
            if (n == 0) return stack;
            int rows = (int)Math.Ceiling(n / (double)maxPerRow);
            int perRow = (int)Math.Ceiling(n / (double)rows);
            int i = 0;
            while (i < n)
            {
                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    // breathing room between rows: two-line labels above would otherwise sit
                    // right against the next row's icons
                    Margin = new Thickness(0, i == 0 ? 0 : 8, 0, 0),
                };
                int take = Math.Min(perRow, n - i);
                for (int k = 0; k < take; k++) row.Children.Add(cells[i++]);
                stack.Children.Add(row);
            }
            return stack;
        }

        /// <summary>Layout 0 — everything in one card (the original look).</summary>
        FrameworkElement BuildClassic()
        {
            _clock = MakeClock(false);
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(StatusLines());
            stack.Children.Add(Hairline(12));
            stack.Children.Add(Settings.I.GroupTools
                ? BuildIconSections(false, 7)
                : BuildBalancedRows(BuildIcons(false), 7, new Thickness(0, 8, 0, 0)));
            return WithGrip(MakeCardTracked(stack));
        }

        /// <summary>Layout 1 — clock card with rays connecting to free-floating icons.
        /// Always a single row: cells shrink to fit so every icon gets a ray.</summary>
        FrameworkElement BuildConnected()
        {
            _clock = MakeClock(false);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());
            _clockCard = MakeCardTracked(inner);
            _clockCard.Margin = new Thickness(12, 12, 12, 2);
            // hug the clock content instead of stretching to the icon row's width —
            // a compact pill makes the ray fan look intentional
            _clockCard.HorizontalAlignment = HorizontalAlignment.Center;

            _connectors = new Canvas { Height = 26, IsHitTestVisible = false, ClipToBounds = false };

            double cw = CellWidthFor(IconDefs().Count, RowBudget() - 40);
            var iconsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            foreach (var ic in BuildIcons(true, cw)) { iconsRow.Children.Add(ic); _floatIcons.Add(ic); }
            // v1.11: the row rides on a glass shelf instead of the bare wallpaper, so the
            // names stay readable on any desktop and with light themes (they were dark text
            // painted straight onto a dark wallpaper)
            var shelf = MakeCardTracked(iconsRow, 26);
            Theme.SetCardPadding(shelf, new Thickness(12, 10, 12, 10));
            shelf.Margin = new Thickness(12, 0, 12, 12);
            shelf.HorizontalAlignment = HorizontalAlignment.Center;

            var root = new StackPanel();
            root.Children.Add(_clockCard);
            root.Children.Add(_connectors);
            // the shelf is the bottom-most, widest thing here — its corner is the widget's
            root.Children.Add(WithGrip(shelf));
            return root;
        }

        /// <summary>Layout 2 — big time in the middle, Hijri card on one side and
        /// Gregorian on the other, icon pill underneath. The clock honours the user's
        /// ClockStyle — Analog is rendered as a 120-px compact dial that fits between
        /// the date cards without overflowing.</summary>
        FrameworkElement BuildTriple()
        {
            // Tri-panel is wide enough for a 120-px analog dial; Flip/Word/Neon and Digital
            // are drawn at the size their builders default to. Minimal stays Minimal.
            _clock = Settings.I.ClockStyle == 1 ? (IClockView)new AnalogClock(120) : MakeClock(false);
            ClockDates.Hidden = true;   // the side cards carry both dates
            var centerInner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            centerInner.Children.Add(_clock.Root);
            centerInner.Children.Add(StatusLines());
            var centerCard = MakeCardTracked(centerInner, 26);
            centerCard.Margin = new Thickness(4, 12, 4, 6);

            var hijriCard = MakeDateCard(Glyphs.Prayer, out _hijriText);   // crescent
            var gregCard = MakeDateCard(Glyphs.Calendar, out _gregText);   // calendar
            if (!Settings.I.ShowHijri) hijriCard.Visibility = Visibility.Collapsed;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            // added first = starts at the visual right in RTL → Hijri right, Gregorian left
            row.Children.Add(hijriCard);
            row.Children.Add(centerCard);
            row.Children.Add(gregCard);

            var pillContent = Settings.I.GroupTools
                ? BuildIconSections(false, 7)
                : BuildBalancedRows(BuildIcons(false), 7, new Thickness(0));
            var pill = MakeCardTracked(pillContent, 30);
            Theme.SetCardPadding(pill, new Thickness(12, 6, 12, 8));
            pill.Margin = new Thickness(12, 0, 12, 12);
            pill.HorizontalAlignment = HorizontalAlignment.Center;

            var root = new StackPanel();
            root.Children.Add(row);
            root.Children.Add(WithGrip(pill));
            return root;
        }

        Border MakeDateCard(string glyph, out TextBlock text)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var chip = Theme.MakeGlyphChip(glyph, 30);
            chip.Margin = new Thickness(0, 0, 0, 6);
            stack.Children.Add(chip);
            text = Theme.MakeText("", 13.5, Theme.TextDim);
            text.FontWeight = FontWeights.Medium;
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.MaxWidth = 112;
            stack.Children.Add(text);
            var card = MakeCardTracked(stack, 18);
            Theme.SetCardPadding(card, new Thickness(12, 10, 12, 10));
            card.Margin = new Thickness(4, 12, 4, 6);
            card.VerticalAlignment = VerticalAlignment.Center;
            return card;
        }

        /// <summary>Layout 3 — ORBIT: a round clock core inside a bezel ring that carries the
        /// tools. v1.11 redesign: the tools used to float straight on the wallpaper (tiny
        /// labels, unreadable on a busy or bright desktop, invisible with a light theme).
        /// They now sit on a ring of the widget's own glass, so every label reads against
        /// the theme it was coloured for, and the whole widget is one round object.</summary>
        FrameworkElement BuildOrbit()
        {
            _clock = MakeClock(false, 48);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());

            double coreR;
            var coreCard = MakeRoundCore(inner, 16, out coreR);

            // ---- tool cells, measured so the ring can be cut to fit them ----
            var icons = BuildIcons(true);
            int n = icons.Count;
            double cellW = 0, cellH = 0;
            foreach (var ic in icons)
            {
                ic.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                cellW = Math.Max(cellW, ic.DesiredSize.Width);
                cellH = Math.Max(cellH, ic.DesiredSize.Height);
            }

            // ring radius: far enough out that the cells clear the core, and that the chord
            // between two neighbours fits a whole cell (wide labels never touch)
            const double gap = 5;          // seam between the core and the bezel
            const double bandPad = 9;      // breathing room inside the bezel, both edges
            double rOnCore = coreR + gap + bandPad + cellH / 2;
            double rChord = n > 1 ? (Math.Max(cellW, cellH) + 2) / (2 * Math.Sin(Math.PI / n)) : 0;
            double ringR = Math.Max(rOnCore, rChord);
            double rIn = coreR + gap;
            double rOut = ringR + cellH / 2 + bandPad;

            const double shadowPad = 22;   // the bezel's shadow needs room inside the window
            double canvasSize = 2 * (rOut + shadowPad);
            double c = canvasSize / 2;
            var canvas = new Canvas
            {
                Width = canvasSize,
                Height = canvasSize,
                FlowDirection = FlowDirection.LeftToRight,
            };

            // ---- the bezel: a ring of card glass with a lit outer rim ----
            var ringGeo = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(c, c), rOut, rOut),
                new EllipseGeometry(new Point(c, c), rIn, rIn));
            ringGeo.Freeze();
            var bezel = new Path
            {
                Data = ringGeo,
                Fill = Theme.CardBg,
                Stroke = Theme.CardBorder,
                StrokeThickness = 1,
                Effect = Theme.Elevation(3),
            };
            _plates.Add(bezel);
            canvas.Children.Add(bezel);
            var rim = new Ellipse
            {
                Width = 2 * rOut - 2, Height = 2 * rOut - 2,
                Stroke = Theme.RimBrush, StrokeThickness = 1, IsHitTestVisible = false,
            };
            Canvas.SetLeft(rim, c - rOut + 1);
            Canvas.SetTop(rim, c - rOut + 1);
            canvas.Children.Add(rim);
            // a faint accent track through the middle of the ring, behind the cells
            var trackBrush = new SolidColorBrush(Color.FromArgb(Theme.IsLight ? (byte)0x30 : (byte)0x26,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            trackBrush.Freeze();
            var track = new Ellipse
            {
                Width = 2 * ringR, Height = 2 * ringR,
                Stroke = trackBrush, StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 5 },
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(track, c - ringR);
            Canvas.SetTop(track, c - ringR);
            canvas.Children.Add(track);

            // ---- the core ----
            Canvas.SetLeft(coreCard, c - coreCard.Width / 2);
            Canvas.SetTop(coreCard, c - coreCard.Height / 2);
            canvas.Children.Add(coreCard);

            // ---- tools around the ring, first one at 12 o'clock, clockwise ----
            for (int i = 0; i < n; i++)
            {
                double a = (-90 + i * 360.0 / n) * Math.PI / 180.0;
                double ix = c + ringR * Math.Cos(a);
                double iy = c + ringR * Math.Sin(a);
                var sz = icons[i].DesiredSize;
                Canvas.SetLeft(icons[i], ix - sz.Width / 2);
                Canvas.SetTop(icons[i], iy - sz.Height / 2);
                canvas.Children.Add(icons[i]);
            }

            // the scale grip sits on the bezel at 45°, the nearest thing to a corner here
            var grip = BuildScaleGrip();
            grip.HorizontalAlignment = HorizontalAlignment.Left;
            grip.VerticalAlignment = VerticalAlignment.Top;
            grip.Margin = new Thickness(0);
            double d = (rOut - 13) * Math.Sqrt(2) / 2;
            Canvas.SetLeft(grip, c + d - 11);
            Canvas.SetTop(grip, c + d - 11);
            canvas.Children.Add(grip);
            return canvas;
        }

        /// <summary>A circular clock card cut to its content. The old rule made the circle the
        /// content's diagonal, which wastes a third of the disc on empty corners; here every
        /// line is measured where it actually sits, round dials and sky domes count as the
        /// shapes they are, and the content is slid up or down to the height where the circle
        /// it needs is smallest. Status lines are primed with their real text first (they
        /// normally fill on the first tick) so the circle is cut for what will be shown.</summary>
        Border MakeRoundCore(StackPanel inner, double pad, out double radius)
        {
            _clock.Tick(DateTime.Now);
            PrimeStatusLines(DateTime.Now);
            var restore = ReserveStatusLines();

            inner.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            inner.Arrange(new Rect(inner.DesiredSize));
            double cy;
            double r = FitCircle(inner, out cy);
            restore();

            double R = Math.Ceiling(r + pad);
            var host = new Grid { Width = 2 * R, Height = 2 * R };
            inner.Margin = new Thickness(0, Math.Max(0, R - cy), 0, 0);
            inner.HorizontalAlignment = HorizontalAlignment.Center;
            inner.VerticalAlignment = VerticalAlignment.Top;
            host.Children.Add(inner);

            var card = MakeCardTracked(host);
            card.Margin = new Thickness(0);
            Theme.SetCardPadding(card, new Thickness(0));
            card.Width = 2 * R + 2;     // + the card's 1px border on each side
            card.Height = 2 * R + 2;
            Theme.SetCardRadius(card, R + 1);
            card.FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            radius = R + 1;
            return card;
        }

        /// <summary>Lines that are switched on but have nothing to say yet (weather still
        /// fetching, no occasion near) get a representative placeholder while the core is
        /// measured, so the circle does not overflow the moment they appear. Returns the
        /// action that puts them back.</summary>
        Action ReserveStatusLines()
        {
            var undo = new List<Action>();
            Action<FrameworkElement> reserve = el =>
            {
                if (el == null || el.Visibility == Visibility.Visible) return;
                var prev = el.Visibility;
                el.Visibility = Visibility.Visible;
                undo.Add(() => el.Visibility = prev);
            };
            var s = Settings.I;
            if (s.ShowPrayer && s.ShowNextPrayerLine && string.IsNullOrEmpty(_prayerLine.Text))
            {
                _prayerLine.Text = L.IsAr ? "الفجر بعد ٠:٠٠" : "Fajr in 0:00";
                undo.Add(() => _prayerLine.Text = "");
                reserve(_prayerLine);
            }
            if (s.ShowWeather && s.ShowWeatherLine && _weatherLine.Visibility != Visibility.Visible
                && Settings.I.Scale >= 0.85)
            {
                string oldG = _weatherGlyph.Text, oldT = _weatherText.Text;
                _weatherGlyph.Text = "☀";
                _weatherText.Text = "00°  ·  " + (string.IsNullOrEmpty(s.City) ? "City" : s.City);
                undo.Add(() => { _weatherGlyph.Text = oldG; _weatherText.Text = oldT; });
                reserve(_weatherLine);
            }
            return () => { for (int i = undo.Count - 1; i >= 0; i--) undo[i](); };
        }

        /// <summary>Smallest circle (radius, and the content y it is centred on) that holds
        /// every line of an arranged content stack. Each leaf row contributes its outline:
        /// a rectangle by default, an inscribed ellipse for a round dial, an upper half
        /// ellipse for a sky dome (see <see cref="Footprint"/>).</summary>
        static double FitCircle(FrameworkElement content, out double bestCy)
        {
            var pts = new List<Point>();
            CollectFootprint(content, content, pts);
            double w = content.ActualWidth > 0 ? content.ActualWidth : content.DesiredSize.Width;
            double h = content.ActualHeight > 0 ? content.ActualHeight : content.DesiredSize.Height;
            double cx = w / 2;
            double best = double.MaxValue;
            bestCy = h / 2;
            if (pts.Count == 0) { best = Math.Sqrt(w * w + h * h) / 2; return best; }
            for (double cy = h * 0.25; cy <= h * 0.75; cy += 1)
            {
                double m = 0;
                foreach (var p in pts)
                {
                    double dx = p.X - cx, dy = p.Y - cy;
                    double dd = dx * dx + dy * dy;
                    if (dd > m) m = dd;
                }
                m = Math.Sqrt(m);
                if (m < best) { best = m; bestCy = cy; }
            }
            return best;
        }

        static void CollectFootprint(FrameworkElement root, FrameworkElement el, List<Point> pts)
        {
            if (el.Visibility != Visibility.Visible) return;
            var tag = el.Tag as string;
            var sp = el as StackPanel;
            if (tag == null && sp != null && sp.Orientation == Orientation.Vertical)
            {
                foreach (var child in sp.Children)
                {
                    var fe = child as FrameworkElement;
                    if (fe != null) CollectFootprint(root, fe, pts);
                }
                return;
            }
            if (el.ActualWidth <= 0 || el.ActualHeight <= 0) return;
            // TransformBounds, not TranslatePoint(0,0): nearly every text line here is an RTL
            // element inside an LTR stack, and an RTL element's local origin is its RIGHT edge —
            // adding the width to it pushed every line's outline a full width off to one side
            // and cut the circle a third too big
            Rect b;
            try { b = el.TransformToAncestor(root).TransformBounds(new Rect(0, 0, el.ActualWidth, el.ActualHeight)); }
            catch { return; }
            double w = b.Width, h = b.Height;
            var o = new Point(b.X, b.Y);
            if (tag == Footprint.Round || tag == Footprint.Dome)
            {
                double rx = w / 2, ry = tag == Footprint.Dome ? h : h / 2;
                double ox = o.X + rx, oy = tag == Footprint.Dome ? o.Y + h : o.Y + h / 2;
                int steps = 36;
                for (int i = 0; i <= steps; i++)
                {
                    double t = (tag == Footprint.Dome ? Math.PI : 2 * Math.PI) * i / steps;
                    pts.Add(new Point(ox + rx * Math.Cos(t), oy - ry * Math.Sin(t)));
                }
                if (tag == Footprint.Dome)
                {
                    pts.Add(new Point(o.X, o.Y + h));
                    pts.Add(new Point(o.X + w, o.Y + h));
                }
                return;
            }
            pts.Add(o);
            pts.Add(new Point(o.X + w, o.Y));
            pts.Add(new Point(o.X, o.Y + h));
            pts.Add(new Point(o.X + w, o.Y + h));
        }

        /// <summary>Layout 4 — vertical sidebar: compact clock on top, menu-style rows
        /// with always-visible names beneath. The clock honours the user's ClockStyle —
        /// Analog is rendered as a 104-px compact dial that fits the strip; Flip and Word
        /// are too tall for the column and fall back to Digital; Minimal and Neon are honoured.</summary>
        FrameworkElement BuildSidebar()
        {
            // Binary (6), DayRing (7), SunPath (8) are 168-px-class dials: too big for the
            // narrow strips, so Sidebar falls back to the compact analog dial; Stripes (9)
            // is wide and MoonPhase (10) is a wide dome — both fall back like the others.
            int style = Settings.I.ClockStyle;
            _clock = style == 1 || style == 6 || style == 7 ? (IClockView)new AnalogClock(104)
                : style == 2 ? (IClockView)new MinimalClock()
                : style == 5 ? (IClockView)new NeonClock()
                : style == 8 || style == 10 ? (IClockView)new AnalogClock(104)
                : style == 9 ? (IClockView)new MinimalClock()
                : style == 3 || style == 4 ? MakeClock(false, 26)   // Flip/Word too tall → Digital fallback
                : MakeClock(false, 26);
            var stack = new StackPanel { MinWidth = 196 };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(StatusLines());
            stack.Children.Add(Hairline(10, 6));
            var allDefs = IconDefs();
            for (int i = 0; i < allDefs.Count; i++)
                stack.Children.Add(MakeSidebarRow(allDefs[i].Glyph, allDefs[i].Key, allDefs[i].Open, allDefs[i].Id));

            // The strip must be at least as wide as the clock actually needs: a digital
            // time + AM/PM pill is wider than 176px, and squeezing it clipped the pill
            // instead of growing. Measure the real content and lift the floor to match.
            _clock.Tick(DateTime.Now);
            stack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double need = stack.DesiredSize.Width + 8;    // card padding slack
            if (need > stack.MinWidth) stack.MinWidth = need;

            return WithGrip(MakeCardTracked(stack, 22));
        }

        FrameworkElement MakeSidebarRow(string glyph, string titleKey, Action onClick, int toolSlot = 0)
        {
            var row = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(7, 4, 7, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                RenderTransform = new TranslateTransform(0, 0),
            };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            // v1.10: the drawn tangible icon; the MDL2 glyph only survives as a fallback
            // for a slot ToolIcons does not know yet
            FrameworkElement iconEl;
            var icon = ToolIcons.Render(toolSlot, 32);
            if (icon != null)
            {
                iconEl = icon.Root;
                iconEl.VerticalAlignment = VerticalAlignment.Center;
                iconEl.Margin = new Thickness(1.5, 0, 1.5, 0);
            }
            else
            {
                iconEl = new TextBlock
                {
                    Text = glyph,
                    FontSize = 17,
                    FontFamily = Theme.Mdl2,
                    Foreground = Theme.ToolTint(toolSlot),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FlowDirection = FlowDirection.LeftToRight,   // glyph codepoint, not text
                };
            }
            var glyphHost = new Border { Width = 36, Height = 38, Child = iconEl };
            content.Children.Add(glyphHost);
            var name = Theme.MakeText(L.T(titleKey), 13.5, Theme.TextDim);
            name.FontWeight = FontWeights.Medium;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(10, 0, 4, 0);
            content.Children.Add(name);
            row.Child = content;

            var slide = (TranslateTransform)row.RenderTransform;
            row.MouseEnter += (s, e) =>
            {
                row.Background = Theme.HoverBg;
                name.Foreground = Theme.Text;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(L.IsAr ? -4 : 4, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeave += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                name.Foreground = Theme.TextDim;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                PanelWindow.PendingAnchor = glyphHost;   // open attached to this row's icon
                try { onClick(); } catch (Exception ex) { Log.Error("sidebar", ex); }
            };
            return row;
        }

        /// <summary>Layout 5 — slim horizontal dock pill: time + date on one side,
        /// labeled tool cells inline. The clock honours the user's ClockStyle — Analog
        /// is rendered as a 100-px compact dial, Flip/Word/Neon are honoured as long as
        /// they fit the strip height, Minimal still fits comfortably.</summary>
        FrameworkElement BuildDock()
        {
            // Binary/DayRing/SunPath/MoonPhase are dial-sized or domed, Stripes is wide:
            // Dock falls back like Analog does.
            int style = Settings.I.ClockStyle;
            _clock = style == 1 || style == 6 || style == 7 || style == 8 || style == 10 ? (IClockView)new AnalogClock(100)
                : style == 2 || style == 9 ? (IClockView)new MinimalClock()
                : MakeClock(false, 22);
            var clockCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            clockCol.Children.Add(_clock.Root);
            clockCol.Children.Add(StatusLines());
            clockCol.Margin = new Thickness(6, 0, 6, 0);

            var rowStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            rowStack.Children.Add(clockCol);
            rowStack.Children.Add(new Border
            {
                Width = 1,
                Height = 46,
                Background = Theme.CardBorder,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
            double cw = CellWidthFor(IconDefs().Count, RowBudget());
            foreach (var ic in BuildIcons(false, cw))
            {
                ((FrameworkElement)ic).VerticalAlignment = VerticalAlignment.Center;
                rowStack.Children.Add(ic);
            }

            var pill = MakeCardTracked(rowStack, 30);
            Theme.SetCardPadding(pill, new Thickness(18, 16, 18, 16));
            return WithGrip(pill);
        }

        /// <summary>Layout 6 — MONOLITH: a standing glass slab with real 3D depth —
        /// a thick extruded base, an inner glow well behind the clock, and the tool icons
        /// as floating chips on a pedestal row. The whole widget reads as one moulded
        /// object rather than flat cards.</summary>
        FrameworkElement BuildMonolith()
        {
            _clock = MakeClock(false, 52);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());

            var face = new Border
            {
                Background = Theme.MakeInnerGlow(),
                CornerRadius = new CornerRadius(20, 20, 8, 8),
                Padding = new Thickness(24, 18, 24, 14),
                Child = inner,
            };

            // pedestal: darker band under the face, giving the slab its thickness
            var pedestal = new Border
            {
                Height = 16,
                CornerRadius = new CornerRadius(0, 0, 18, 18),
                Background = Theme.PedestalBrush(),
                Margin = new Thickness(6, 0, 6, 0),
                IsHitTestVisible = false,
            };

            var body = new StackPanel();
            body.Children.Add(face);
            body.Children.Add(pedestal);

            // icon chips on a floating shelf beneath the monolith. Cells are capped well
            // below the normal width AND the shelf gets generous side padding: eleven
            // two-line Arabic labels need real room or the end ones kiss the rounded edge.
            double cw = Math.Min(CellWidthFor(IconDefs().Count, RowBudget()), Theme.DefaultCellWidth);
            FrameworkElement shelfContent;
            if (Settings.I.GroupTools)
            {
                // grouped: sections stack vertically — the shelf becomes a short column
                // of labelled groups instead of one very wide pill
                shelfContent = BuildIconSections(false, 7, cw);
            }
            else
            {
                var shelfRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                foreach (var ic in BuildIcons(false, cw))
                {
                    ic.Margin = new Thickness(2, 0, 2, 0);
                    shelfRow.Children.Add(ic);
                }
                shelfContent = shelfRow;
            }
            // the shelf is the same glass as the slab (it used to be a flat neutral grey
            // that matched no theme), sitting just under the pedestal
            var shelf = MakeCardTracked(shelfContent, 22);
            Theme.SetCardPadding(shelf, new Thickness(16, 6, 16, 8));
            shelf.Margin = new Thickness(18, 0, 18, 12);

            var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var slab = MakeCardTracked(body, 22);
            slab.Margin = new Thickness(12, 12, 12, 6);
            root.Children.Add(slab);
            root.Children.Add(WithGrip(shelf));
            return root;
        }

        /// <summary>Layout 7 — HALO: a round clock core wrapped in a slowly revolving dashed
        /// halo, with the tools on a glass card beneath it. v1.11 rebuild: the old version
        /// sized the core with a Grid that ignored its Canvas offsets, so the disc sat high and
        /// was cut off at the top while the halo hung off-centre; the tool list floated on
        /// the bare desktop, where light themes painted dark names on a dark wallpaper.</summary>
        FrameworkElement BuildHalo()
        {
            _clock = MakeClock(false, 46);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());

            double coreR;
            var coreCard = MakeRoundCore(inner, 14, out coreR);

            // rotating dashed halo just outside the core, with a soft accent glow under it
            double haloR = coreR + 11;
            double box = 2 * (haloR + 4);
            var stage = new Grid { Width = box, Height = box, Margin = new Thickness(10, 10, 10, 0) };
            var glow = new RadialGradientBrush { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5) };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B), 0.80));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(Theme.IsLight ? (byte)0x30 : (byte)0x40, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B), 0.93));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B), 1.0));
            glow.Freeze();
            stage.Children.Add(new Ellipse { Width = box, Height = box, Fill = glow, IsHitTestVisible = false });
            var halo = new Ellipse
            {
                Width = 2 * haloR, Height = 2 * haloR,
                Stroke = new SolidColorBrush(Color.FromArgb(0x8C,
                    Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 3, 6 },
                StrokeDashCap = PenLineCap.Round,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(0),
            };
            HaloBrushes.Halo = halo;
            stage.Children.Add(halo);
            coreCard.HorizontalAlignment = HorizontalAlignment.Center;
            coreCard.VerticalAlignment = VerticalAlignment.Center;
            stage.Children.Add(coreCard);

            // tools: a compact glass card of tiles, four to a row
            var cells = Settings.I.GroupTools ? null : BuildIcons(false);
            FrameworkElement toolContent = cells != null
                ? (FrameworkElement)BuildBalancedRows(cells, 4, new Thickness(0))
                : BuildIconSections(false, 4);
            var toolsCard = MakeCardTracked(toolContent, 26);
            Theme.SetCardPadding(toolsCard, new Thickness(10, 10, 10, 12));
            toolsCard.HorizontalAlignment = HorizontalAlignment.Center;
            toolsCard.Margin = new Thickness(12, 2, 12, 12);

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(stage);
            stack.Children.Add(WithGrip(toolsCard));
            // mostly transparent around the circle, and WPF only hit-tests painted pixels —
            // a transparent background makes the whole widget one grabbable surface
            stack.Background = Brushes.Transparent;
            return stack;
        }

        /// <summary>Build one crystal shard holding 1 or 2 tool glyphs. The shard is a 56px
        /// rounded square; each glyph is a clickable region that opens its feature panel.
        /// Tilt is randomised per shard so the cluster feels organic, not regimented.
        /// (Crystal layout was removed in v1.5.4 after the user reported it was too busy;
        /// these helpers remain compiled but unused so older call sites — and the WPF
        /// designer — keep building.)</summary>
        FrameworkElement MakeShard(Tuple<string, string, Action> first, Tuple<string, string, Action> secondTuple)
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var b1 = MakeShardButton(first.Item1, first.Item3);
            stack.Children.Add(b1);
            if (secondTuple != null)
            {
                var b2 = MakeShardButton(secondTuple.Item1, secondTuple.Item3);
                stack.Children.Add(b2);
            }
            var shard = new Border
            {
                Width = secondTuple == null ? 44 : 60,
                Height = 44,
                CornerRadius = new CornerRadius(14),
                Background = Theme.ButtonBg,
                BorderBrush = Theme.CardBorder,
                BorderThickness = new Thickness(1),
                Child = stack,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = Theme.Elevation(2),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            // small per-shard tilt. The angle is derived from the shard's hash so it's stable
            // across rebuilds (no jitter on every settings change) but differs between shards
            var angle = (shard.GetHashCode() % 7) - 3;   // -3..+3 degrees
            shard.RenderTransform = new RotateTransform(angle);
            return shard;
        }

        FrameworkElement MakeShardButton(string glyph, Action onClick)
        {
            var tb = Theme.MakeGlyph(glyph, 14);
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            var b = new Border
            {
                Width = 28, Height = 28,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                Child = tb,
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            b.MouseEnter += (s, e) => { tb.Foreground = Theme.Accent; b.Background = Theme.HoverBg; };
            b.MouseLeave += (s, e) => { tb.Foreground = Theme.Text; b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                PanelWindow.PendingAnchor = b;
                try { onClick(); } catch (Exception ex) { Log.Error("shard", ex); }
            };
            b.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            return b;
        }
    }
}
