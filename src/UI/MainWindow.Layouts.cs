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
            _floatIcons.Clear();
            _clockCard = null;
            _connectors = null;
            _gregText = null;
            _hijriText = null;
            _dateKey = null;

            _prayerLine = Theme.MakeText("", 12.5, Theme.Accent);
            _prayerLine.HorizontalAlignment = HorizontalAlignment.Center;
            _prayerLine.Margin = new Thickness(0, 5, 0, 0);
            _prayerLine.Visibility = Visibility.Collapsed;

            _weatherGlyph = Theme.MakeText("", 13, Theme.Accent, Theme.SymbolFont);
            _weatherGlyph.VerticalAlignment = VerticalAlignment.Center;
            _weatherGlyph.Margin = new Thickness(0, 0, 5, 0);
            _weatherText = Theme.MakeText("", 12.5, Theme.TextDim);
            _weatherText.VerticalAlignment = VerticalAlignment.Center;
            _weatherLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Visibility = Visibility.Collapsed,
                // the temperature reads left-to-right in both languages
                FlowDirection = FlowDirection.LeftToRight,
            };
            _weatherLine.Children.Add(_weatherGlyph);
            _weatherLine.Children.Add(_weatherText);

            // seasonal occasion line (Ramadan/Eid/nearest occasion) — same pattern as weather
            _occasionGlyph = Theme.MakeText("", 13, Theme.Accent, Theme.SymbolFont);
            _occasionGlyph.VerticalAlignment = VerticalAlignment.Center;
            _occasionGlyph.Margin = new Thickness(0, 0, 5, 0);
            _occasionText = Theme.MakeText("", 12.5, Theme.TextDim);
            _occasionText.VerticalAlignment = VerticalAlignment.Center;
            _occasionLine = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
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
            stack.Children.Add(BuildBalancedRows(BuildIcons(false), 7, new Thickness(0, 8, 0, 0)));
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

            _connectors = new Canvas { Height = 28, IsHitTestVisible = false, ClipToBounds = false };

            double cw = CellWidthFor(IconDefs().Count, RowBudget());
            var iconsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(14, 0, 14, 12),
            };
            foreach (var ic in BuildIcons(true, cw)) { iconsRow.Children.Add(ic); _floatIcons.Add(ic); }

            var root = new StackPanel();
            root.Children.Add(_clockCard);
            root.Children.Add(_connectors);
            // the icon row is the bottom-most, widest thing here — its corner is the widget's
            root.Children.Add(WithGrip(iconsRow));
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

            var pillContent = BuildBalancedRows(BuildIcons(false), 7, new Thickness(0));
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
            text = Theme.MakeText("", 12, Theme.TextDim);
            text.TextWrapping = TextWrapping.Wrap;
            text.TextAlignment = TextAlignment.Center;
            text.MaxWidth = 96;
            stack.Children.Add(text);
            var card = MakeCardTracked(stack, 18);
            Theme.SetCardPadding(card, new Thickness(12, 10, 12, 10));
            card.Margin = new Thickness(4, 12, 4, 6);
            card.VerticalAlignment = VerticalAlignment.Center;
            return card;
        }

        /// <summary>Layout 3 — circular clock card with icons orbiting around it.</summary>
        FrameworkElement BuildOrbit()
        {
            // slightly smaller digits so time + seconds + AM/PM pill sit comfortably in the circle
            _clock = MakeClock(false, 46);
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());
            var card = MakeCardTracked(inner);
            card.Margin = new Thickness(0);
            Theme.SetCardPadding(card, new Thickness(26));

            // populate the clock text BEFORE measuring — an empty time string would
            // undersize the circle and clip the seconds at the edge
            _clock.Tick(DateTime.Now);
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double mw = card.DesiredSize.Width, mh = card.DesiredSize.Height;
            // diameter = diagonal of the measured content + breathing margin
            double size = Math.Sqrt(mw * mw + mh * mh) + 14;
            card.Width = size;
            card.Height = size;
            // through the helper: setting CornerRadius alone left the sheen, bevel and rim as
            // rounded rectangles, drawing a visible square edge inside this circular card
            Theme.SetCardRadius(card, size / 2);
            card.FlowDirection = L.IsAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            var icons = BuildIcons(true);
            double cellW = Theme.DefaultCellWidth;
            // ring wide enough that neighbouring cells do not touch: the arc between two icons
            // must be at least one cell plus a gap, otherwise wide labels collide on the circle
            double minR = icons.Count > 2
                ? (cellW + Theme.CellGap) / (2 * Math.Sin(Math.PI / icons.Count))
                : 0;
            double ringR = Math.Max(size / 2 + 48, minR);
            double pad = cellW / 2 + 34; // room for icon cells + their labels outside the ring
            double canvasSize = 2 * (ringR + pad);
            var canvas = new Canvas
            {
                Width = canvasSize,
                Height = canvasSize,
                Background = Brushes.Transparent,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(4),
            };
            double c = canvasSize / 2;

            var ringStroke = new SolidColorBrush(Color.FromArgb(0x34,
                Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B));
            ringStroke.Freeze();
            var ring = new Ellipse
            {
                Width = 2 * ringR,
                Height = 2 * ringR,
                Stroke = ringStroke,
                StrokeThickness = 1.2,
                StrokeDashArray = new DoubleCollection { 3, 4 },
            };
            Canvas.SetLeft(ring, c - ringR);
            Canvas.SetTop(ring, c - ringR);
            canvas.Children.Add(ring);

            Canvas.SetLeft(card, c - size / 2);
            Canvas.SetTop(card, c - size / 2);
            canvas.Children.Add(card);

            for (int i = 0; i < icons.Count; i++)
            {
                double a = (-90 + i * 360.0 / icons.Count) * Math.PI / 180.0;
                double ix = c + ringR * Math.Cos(a);
                double iy = c + ringR * Math.Sin(a);
                // tool cell is 58 wide; its 40px circle center sits at (29, 20)
                Canvas.SetLeft(icons[i], ix - cellW / 2);
                Canvas.SetTop(icons[i], iy - 20);
                canvas.Children.Add(icons[i]);
            }

            // This layout is a circle inside a square canvas, so the square's bottom-right
            // corner is empty sky — the grip would float there with nothing under it. Put it
            // ON the ring at 45°, the nearest thing to "the widget's own corner" here.
            var grip = BuildScaleGrip();
            grip.HorizontalAlignment = HorizontalAlignment.Left;
            grip.VerticalAlignment = VerticalAlignment.Top;
            grip.Margin = new Thickness(0);
            double d = ringR * Math.Sqrt(2) / 2;
            Canvas.SetLeft(grip, c + d - 11);
            Canvas.SetTop(grip, c + d - 11);
            canvas.Children.Add(grip);
            return canvas;
        }

        /// <summary>Layout 4 — vertical sidebar: compact clock on top, menu-style rows
        /// with always-visible names beneath. The clock honours the user's ClockStyle —
        /// Analog is rendered as a 104-px compact dial that fits the strip; Flip and Word
        /// are too tall for the column and fall back to Digital; Minimal and Neon are honoured.</summary>
        FrameworkElement BuildSidebar()
        {
            // Binary (6) and DayRing (7) are 168-px-class dials: too big for the narrow
            // strips, so Sidebar falls back to the compact analog dial; SunPath (8) is a
            // dial too, Stripes (9) is wide — both fall back like the others.
            int style = Settings.I.ClockStyle;
            _clock = style == 1 || style == 6 || style == 7 ? (IClockView)new AnalogClock(104)
                : style == 2 ? (IClockView)new MinimalClock()
                : style == 5 ? (IClockView)new NeonClock()
                : style == 8 ? (IClockView)new AnalogClock(104)
                : style == 9 ? (IClockView)new MinimalClock()
                : style == 3 || style == 4 ? MakeClock(false, 26)   // Flip/Word too tall → Digital fallback
                : MakeClock(false, 26);
            var stack = new StackPanel { MinWidth = 176 };
            stack.Children.Add(_clock.Root);
            stack.Children.Add(StatusLines());
            stack.Children.Add(Hairline(10, 6));
            foreach (var def in IconDefs())
                stack.Children.Add(MakeSidebarRow(def.Item1, def.Item2, def.Item3));

            // The strip must be at least as wide as the clock actually needs: a digital
            // time + AM/PM pill is wider than 176px, and squeezing it clipped the pill
            // instead of growing. Measure the real content and lift the floor to match.
            _clock.Tick(DateTime.Now);
            stack.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double need = stack.DesiredSize.Width + 8;    // card padding slack
            if (need > stack.MinWidth) stack.MinWidth = need;

            return WithGrip(MakeCardTracked(stack, 22));
        }

        FrameworkElement MakeSidebarRow(string glyph, string titleKey, Action onClick)
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
            var circle = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = Theme.ButtonBg,
                BorderBrush = Theme.ButtonBorder,
                BorderThickness = new Thickness(1),
                Child = Theme.MakeGlyph(glyph, 13),
            };
            content.Children.Add(circle);
            var name = Theme.MakeText(L.T(titleKey), 12.5, Theme.TextDim);
            name.VerticalAlignment = VerticalAlignment.Center;
            name.Margin = new Thickness(10, 0, 4, 0);
            content.Children.Add(name);
            row.Child = content;

            var slide = (TranslateTransform)row.RenderTransform;
            row.MouseEnter += (s, e) =>
            {
                row.Background = Theme.HoverBg;
                circle.BorderBrush = Theme.Accent;
                name.Foreground = Theme.Text;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(L.IsAr ? -4 : 4, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeave += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                circle.BorderBrush = Theme.ButtonBorder;
                name.Foreground = Theme.TextDim;
                slide.BeginAnimation(TranslateTransform.XProperty,
                    new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(130)));
            };
            row.MouseLeftButtonDown += (s, e) => { e.Handled = true; };
            row.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                PanelWindow.PendingAnchor = circle;   // open attached to this row's icon
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
            // Binary/DayRing/SunPath are dial-sized, Stripes is wide: Dock falls back like Analog does.
            int style = Settings.I.ClockStyle;
            _clock = style == 1 || style == 6 || style == 7 || style == 8 ? (IClockView)new AnalogClock(100)
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

            var pill = MakeCardTracked(rowStack, 40);
            Theme.SetCardPadding(pill, new Thickness(18, 10, 18, 10));
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
            double cw = Math.Min(CellWidthFor(IconDefs().Count, RowBudget()), 62);
            var shelfRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2),
            };
            foreach (var ic in BuildIcons(false, cw))
            {
                ic.Margin = new Thickness(2, 0, 2, 0);
                shelfRow.Children.Add(ic);
            }
            var shelf = new Border
            {
                Background = Theme.ButtonBg,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22, 5, 22, 4),
                Effect = Theme.Elevation(2),
                Child = shelfRow,
            };

            var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            root.Children.Add(MakeCardTracked(body, 22));
            root.Children.Add(WithGrip(shelf));
            return root;
        }

        /// <summary>Layout 7 — HALO: a glowing ring around a circular clock core with the
        /// tools in two side columns (right column in RTL). The halo is a dashed accent
        /// ellipse that slowly rotates via RenderTransform — alive without being loud.</summary>
        FrameworkElement BuildHalo()
        {
            _clock = MakeClock(false, 44);
            var inner = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            inner.Children.Add(_clock.Root);
            inner.Children.Add(StatusLines());
            var coreCard = MakeCardTracked(inner);
            coreCard.Margin = new Thickness(0);
            Theme.SetCardPadding(coreCard, new Thickness(20));

            // measure for a circular core like Orbit does
            _clock.Tick(DateTime.Now);
            coreCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double mw = coreCard.DesiredSize.Width, mh = coreCard.DesiredSize.Height;
            double size = Math.Sqrt(mw * mw + mh * mh) + 10;
            coreCard.Width = size;
            coreCard.Height = size;
            Theme.SetCardRadius(coreCard, size / 2);

            // rotating dashed halo just outside the core
            double haloR = size / 2 + 12;
            var halo = new Ellipse
            {
                Width = 2 * haloR, Height = 2 * haloR,
                Stroke = new SolidColorBrush(Color.FromArgb(0x66,
                    Theme.AccentColor.R, Theme.AccentColor.G, Theme.AccentColor.B)),
                StrokeThickness = 1.6,
                StrokeDashArray = new DoubleCollection { 4, 7 },
                IsHitTestVisible = false,
            };
            halo.RenderTransform = new RotateTransform(0);
            HaloBrushes.Halo = halo;

            var defs = IconDefs();
            int n = defs.Count;
            int rightCount = (n + 1) / 2;          // first half goes to the leading column
            int leftCount = n - rightCount;

            var colRight = BuildHaloColumn(defs, 0, rightCount);
            var colLeft = BuildHaloColumn(defs, rightCount, leftCount);

            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var canvas = new Grid { Width = size + 28, Height = size + 28 };
            Canvas.SetZIndex(halo, 0);
            canvas.Children.Add(halo);
            Canvas.SetLeft(coreCard, 14);
            Canvas.SetTop(coreCard, 14);
            var coreHost = new Canvas { Width = size + 28, Height = size + 28 };
            coreHost.Children.Add(canvas);
            Grid.SetColumn(coreHost, 1);
            grid.Children.Add(coreHost);

            colRight.VerticalAlignment = VerticalAlignment.Center;
            colLeft.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(colRight, L.IsAr ? 2 : 0);
            Grid.SetColumn(colLeft, L.IsAr ? 0 : 2);
            grid.Children.Add(colRight);
            grid.Children.Add(colLeft);

            // The layout is a circle floating between two columns — most of its bounding
            // box is transparent, and WPF only hit-tests painted pixels, so dragging the
            // "empty" middle did nothing and users could not move the widget to centre it.
            // A transparent full-bleed background turns the whole grid into one grabbable
            // surface (Background=null is NOT hit-testable; Transparent IS).
            var dragSurface = new Grid
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
            };
            dragSurface.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is FrameworkElement) { try { DragMove(); SavePosition(); } catch { } }
            };
            var root = new Grid();
            root.Children.Add(dragSurface);   // behind everything, catches all stray clicks
            root.Children.Add(grid);
            return WithGrip(root);
        }

        /// <summary>One vertical stack of compact tool rows for the Halo layout.</summary>
        StackPanel BuildHaloColumn(List<Tuple<string, string, Action>> defs, int start, int count)
        {
            var col = new StackPanel { MinWidth = 132 };
            for (int i = start; i < start + count && i < defs.Count; i++)
                col.Children.Add(MakeSidebarRow(defs[i].Item1, defs[i].Item2, defs[i].Item3));
            return col;
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
